using Kiyote.Buffers.ChunkedGrids;
using Kiyote.Geometry.Topology;

namespace Kiyote.Simulations.LowFidelity.Atmospherics;

internal sealed class AtmosphereFrames : IAtmosphereFrames {

	private const int Published = 4;
	private const int IndexMask = 3;

	private readonly AtmosphereFrameBuffer?[] _buffers = new AtmosphereFrameBuffer?[3];
	private int _writable;
	private int _reader = 1;
	private int _middle = 2;
	private long _leaseState;
	private long _leaseGeneration;
	private AtmosphereFrameBuffer? _lastPublished;
	private bool _validateTopology = true;
	private long _stepCount;
	private float _vented;

	public AtmosphereFrames(
		IGasRegistry gases
	) {
		Gases = gases;
	}

	public IGasRegistry Gases { get; }

	long IAtmosphereFrames.StepCount => Volatile.Read( ref _stepCount );

	float IAtmosphereFrames.Vented => Volatile.Read( ref _vented );

	AtmosphereFrameLease IAtmosphereFrames.AcquireLatest() {
		long token = Interlocked.Increment( ref _leaseGeneration );
		if( token <= 0 ) {
			throw new InvalidOperationException( "The frame lease generation is exhausted." );
		}
		if( Interlocked.CompareExchange( ref _leaseState, token, 0 ) != 0 ) {
			throw new InvalidOperationException( "A frame lease is already outstanding." );
		}

		// The consumer owns _reader, the producer owns _writable, and only _middle is exchanged.
		// The published flag prevents a reader from exchanging its latest frame for an older one.
		if( ( Volatile.Read( ref _middle ) & Published ) != 0 ) {
			_reader = Interlocked.Exchange( ref _middle, _reader ) & IndexMask;
		}
		AtmosphereFrameBuffer? buffer = _buffers[_reader];
		if( buffer is null ) {
			Release( token );
			throw new InvalidOperationException( "No simulation frame has been published." );
		}
		return new AtmosphereFrameLease( this, buffer, token );
	}

	internal void Release(
		long token
	) {
		_ = Interlocked.CompareExchange( ref _leaseState, 0, token );
	}

	public void InvalidateTopology() {
		_validateTopology = true;
	}

	public void Publish(
		long stepCount,
		float vented,
		IGridLayer<float> pressure,
		IGridLayer<float> temperature,
		IGridLayer<float> windX,
		IGridLayer<float> windY,
		IGridLayer<float>[] gas,
		IGridLayer<float>?[] condensate,
		IGridLayer<float>? structuralTemperature = null
	) {
		GridLayout sourceLayout = pressure.Layout;
		AtmosphereFrameBuffer? previous = _lastPublished;
		ChunkedGridLayout layout;
		int[] sourceChunks;
		if( previous is null || ( _validateTopology && !Matches( previous.Layout, sourceLayout ) ) ) {
			layout = CreateLayout( sourceLayout );
			sourceChunks = CreateMapping( layout, sourceLayout );
		} else {
			layout = previous.Layout;
			sourceChunks = previous.SourceChunks;
		}

		AtmosphereFrameBuffer? buffer = _buffers[_writable];
		if( buffer is null || !ReferenceEquals( buffer.Layout, layout )
			|| ( buffer.StructuralTemperature is not null ) != ( structuralTemperature is not null )
		) {
			buffer = new AtmosphereFrameBuffer( layout, sourceChunks, condensate, structuralTemperature is not null );
			_buffers[_writable] = buffer;
		}

		Copy( pressure, buffer.Pressure, sourceChunks );
		Copy( temperature, buffer.Temperature, sourceChunks );
		Copy( windX, buffer.WindX, sourceChunks );
		Copy( windY, buffer.WindY, sourceChunks );
		for( int g = 0; g < gas.Length; g++ ) {
			Copy( gas[g], buffer.Gas[g], sourceChunks );
			if( condensate[g] is IGridLayer<float> layer ) {
				Copy( layer, buffer.Condensate[g]!, sourceChunks );
			}
		}
		if( structuralTemperature is not null ) {
			Copy( structuralTemperature, buffer.StructuralTemperature!, sourceChunks );
		}
		buffer.StepCount = stepCount;
		buffer.Vented = vented;

		// A single exchange publishes every layer, its immutable topology and its diagnostics.
		_writable = Interlocked.Exchange( ref _middle, _writable | Published ) & IndexMask;
		_lastPublished = buffer;
		_validateTopology = false;
		Volatile.Write( ref _stepCount, stepCount );
		Volatile.Write( ref _vented, vented );
	}

	private static ChunkedGridLayout CreateLayout(
		GridLayout source
	) {
		ChunkedGridLayout layout = new ChunkedGridLayout( source.ChunkSize );
		foreach( int chunk in source.EnumerateChunks() ) {
			ReadOnlySpan<ulong> presence = source.GetPresence( chunk );
			if( presence.ContainsAnyExcept( 0UL ) ) {
				int target = layout.GetOrCreateChunk( source.GetChunkColumn( chunk ), source.GetChunkRow( chunk ) );
				presence.CopyTo( layout.GetPresence( target ) );
			}
		}
		return layout;
	}

	private static bool Matches(
		ChunkedGridLayout published,
		GridLayout source
	) {
		if( published.ChunkSize != source.ChunkSize ) {
			return false;
		}
		int populated = 0;
		foreach( int chunk in source.EnumerateChunks() ) {
			if( source.GetPresence( chunk ).ContainsAnyExcept( 0UL ) ) {
				populated++;
			}
		}
		if( populated != published.ChunkCount ) {
			return false;
		}
		foreach( int chunk in published.EnumerateChunks() ) {
			if( !source.TryGetChunk( published.GetChunkColumn( chunk ), published.GetChunkRow( chunk ), out int target )
				|| !published.GetPresence( chunk ).SequenceEqual( source.GetPresence( target ) )
			) {
				return false;
			}
		}
		return true;
	}

	private static void Copy(
		IGridLayer<float> source,
		ChunkedGridLayer<float> target,
		ReadOnlySpan<int> sourceChunks
	) {
		foreach( int chunk in target.Layout.EnumerateChunks() ) {
			source.GetValues( sourceChunks[chunk] ).CopyTo( target.GetValues( chunk ) );
		}
	}

	private static int[] CreateMapping(
		ChunkedGridLayout target,
		GridLayout source
	) {
		int[] mapping = new int[target.ChunkCapacity];
		foreach( int chunk in target.EnumerateChunks() ) {
			if( !source.TryGetChunk( target.GetChunkColumn( chunk ), target.GetChunkRow( chunk ), out int sourceChunk ) ) {
				throw new InvalidOperationException( "Published chunk has no corresponding source chunk." );
			}
			mapping[chunk] = sourceChunk;
		}
		return mapping;
	}

}
