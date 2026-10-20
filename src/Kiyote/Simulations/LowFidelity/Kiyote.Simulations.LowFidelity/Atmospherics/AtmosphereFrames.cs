using Kiyote.Buffers.ChunkedGrids;
using Kiyote.Geometry.Topology;

namespace Kiyote.Simulations.LowFidelity.Atmospherics;

// Holds one TripleBufferedChunkedGrid<float> per published quantity, laid out as
// [pressure, temperature, windX, windY, gas..., condensate...] with null for gases that do not
// condense. The simulation thread replaces the whole array when the topology changes, so the
// reader never sees a grid whose topology is being edited.
internal sealed class AtmosphereFrames : IAtmosphereFrames {

	private const int PressureIndex = 0;
	private const int TemperatureIndex = 1;
	private const int WindXIndex = 2;
	private const int WindYIndex = 3;
	private const int FirstGasIndex = 4;

	private readonly IGasRegistry _gases;
	private readonly int _gasCount;
	private TripleBufferedChunkedGrid<float>?[] _grids;
	private long _stepCount;
	private float _vented;

	public AtmosphereFrames(
		IGasRegistry gases
	) {
		_gases = gases;
		_gasCount = gases.Count;
		_grids = [];
	}

	IGasRegistry IAtmosphereFrames.Gases => _gases;

	long IAtmosphereFrames.StepCount => Volatile.Read( ref _stepCount );

	float IAtmosphereFrames.Vented => Volatile.Read( ref _vented );

	TripleBufferedLease<float> IAtmosphereFrames.AcquirePressure() {
		return Acquire( PressureIndex );
	}

	TripleBufferedLease<float> IAtmosphereFrames.AcquireTemperature() {
		return Acquire( TemperatureIndex );
	}

	TripleBufferedLease<float> IAtmosphereFrames.AcquireWindX() {
		return Acquire( WindXIndex );
	}

	TripleBufferedLease<float> IAtmosphereFrames.AcquireWindY() {
		return Acquire( WindYIndex );
	}

	TripleBufferedLease<float> IAtmosphereFrames.AcquireGas(
		GasIndex gas
	) {
		return Acquire( FirstGasIndex + gas.Value );
	}

	TripleBufferedLease<float> IAtmosphereFrames.AcquireCondensate(
		GasIndex gas
	) {
		TripleBufferedChunkedGrid<float>? grid = Volatile.Read( ref _grids )[FirstGasIndex + _gasCount + gas.Value];
		return grid?.Acquire()
			?? throw new ArgumentException( $"Gas '{_gases.GetDefinition( gas ).Id}' does not condense.", nameof( gas ) );
	}

	// Called only by the simulation thread. Copies every layer into the buffer the reader cannot
	// see, then publishes it.
	public void Publish(
		long stepCount,
		float vented,
		IGridLayer<float> pressure,
		IGridLayer<float> temperature,
		IGridLayer<float> windX,
		IGridLayer<float> windY,
		IGridLayer<float>[] gas,
		IGridLayer<float>?[] condensate
	) {
		GridLayout layout = pressure.Layout;
		TripleBufferedChunkedGrid<float>?[] grids = _grids;
		if( !Matches( grids, layout ) ) {
			grids = Create( layout, condensate );
		}

		Copy( pressure, grids[PressureIndex]! );
		Copy( temperature, grids[TemperatureIndex]! );
		Copy( windX, grids[WindXIndex]! );
		Copy( windY, grids[WindYIndex]! );
		for( int g = 0; g < _gasCount; g++ ) {
			Copy( gas[g], grids[FirstGasIndex + g]! );
			IGridLayer<float>? layer = condensate[g];
			if( layer is not null ) {
				Copy( layer, grids[FirstGasIndex + _gasCount + g]! );
			}
		}
		foreach( TripleBufferedChunkedGrid<float>? grid in grids ) {
			grid?.Swap();
		}

		Volatile.Write( ref _grids, grids );
		Volatile.Write( ref _stepCount, stepCount );
		Volatile.Write( ref _vented, vented );
	}

	private TripleBufferedLease<float> Acquire(
		int index
	) {
		return Volatile.Read( ref _grids )[index]!.Acquire();
	}

	private TripleBufferedChunkedGrid<float>?[] Create(
		GridLayout layout,
		IGridLayer<float>?[] condensate
	) {
		ChunkedGrid<float> template = new ChunkedGrid<float>( layout.ChunkSize );
		foreach( int chunk in layout.EnumerateChunks() ) {
			ReadOnlySpan<ulong> presence = layout.GetPresence( chunk );
			if( presence.ContainsAnyExcept( 0UL ) ) {
				presence.CopyTo( template.GetOrCreateChunk( layout.GetChunkColumn( chunk ), layout.GetChunkRow( chunk ) ).Presence );
			}
		}

		TripleBufferedChunkedGrid<float>?[] grids = new TripleBufferedChunkedGrid<float>?[FirstGasIndex + ( 2 * _gasCount )];
		for( int i = 0; i < FirstGasIndex + _gasCount; i++ ) {
			grids[i] = CreateGrid( template );
		}
		for( int g = 0; g < _gasCount; g++ ) {
			if( condensate[g] is not null ) {
				grids[FirstGasIndex + _gasCount + g] = CreateGrid( template );
			}
		}
		return grids;
	}

	private static TripleBufferedChunkedGrid<float> CreateGrid(
		ChunkedGrid<float> template
	) {
		TripleBufferedChunkedGrid<float> grid = new TripleBufferedChunkedGrid<float>( template.ChunkSize );
		grid.Attach( template, 0, 0 );
		return grid;
	}

	// Every grid in a set shares one topology, so checking the first is enough.
	private static bool Matches(
		TripleBufferedChunkedGrid<float>?[] grids,
		GridLayout layout
	) {
		if( grids.Length == 0 ) {
			return false;
		}
		TripleBufferedChunkedGrid<float> published = grids[PressureIndex]!;
		if( published.ChunkSize != layout.ChunkSize ) {
			return false;
		}
		int matched = 0;
		foreach( int chunk in layout.EnumerateChunks() ) {
			ReadOnlySpan<ulong> presence = layout.GetPresence( chunk );
			if( published.TryGetChunk( layout.GetChunkColumn( chunk ), layout.GetChunkRow( chunk ), out BufferedChunk<float> target ) ) {
				if( !presence.SequenceEqual( target.Current.Presence ) ) {
					return false;
				}
				matched++;
			} else if( presence.ContainsAnyExcept( 0UL ) ) {
				return false;
			}
		}
		return matched == published.ChunkCount;
	}

	private static void Copy(
		IGridLayer<float> source,
		TripleBufferedChunkedGrid<float> target
	) {
		GridLayout layout = source.Layout;
		IGridLayer<float> values = source;
		foreach( int chunk in layout.EnumerateChunks() ) {
			if( target.TryGetChunk( layout.GetChunkColumn( chunk ), layout.GetChunkRow( chunk ), out BufferedChunk<float> published ) ) {
				values.GetValues( chunk ).CopyTo( published.Current.Values );
			}
		}
	}

}
