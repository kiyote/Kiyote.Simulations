using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using Kiyote.Buffers.ChunkedGrids;
using Kiyote.Geometry.Topology;

namespace Kiyote.Simulations.Topology.Projection;

// Pressure projection over a chunked assembly, with Topology boundary semantics:
//   - Passable direction: the neighbour cell takes part normally.
//   - Wall (unflagged direction to an existing cell): velocity reflects, pressure is zero-gradient.
//   - Vacuum (missing neighbour cell): zero (Dirichlet) pressure, zero-gradient outflow velocity.
// Missing and unoccupied cells always hold 0, so vacuum pressure reads as 0 straight from the
// layer. Layers read at neighbour offsets are gathered per chunk, with a one-cell border from
// the neighbouring chunks, into padded scratch; missing chunks pad with 0. Every pass is
// vectorized in blocks of 16 cells, with a scalar tail.
public sealed class GridProjection : IGridProjection {

	// Scales the eight-direction divergence for the Jacobi pressure solve.
	private const float PoissonScale = 8.235294f;
	private const int DirectionCount = 8;
	private const float Diagonal = 0.7071068f;
	private const int Block = 16;
	private const float MinNormal = 1.17549435E-38f;
	private const int Halo = 1;

	private static readonly Direction[] _directions = [
		Direction.North,
		Direction.NorthEast,
		Direction.East,
		Direction.SouthEast,
		Direction.South,
		Direction.SouthWest,
		Direction.West,
		Direction.NorthWest
	];
	private static readonly int[] _deltaColumns = [ 0, 1, 1, 1, 0, -1, -1, -1 ];
	private static readonly int[] _deltaRows = [ -1, -1, 0, 1, 1, 1, 0, -1 ];
	private static readonly float[] _unitX = [ 0f, Diagonal, 1f, Diagonal, 0f, -Diagonal, -1f, -Diagonal ];
	private static readonly float[] _unitY = [ -1f, -Diagonal, 0f, Diagonal, 1f, Diagonal, 0f, -Diagonal ];

	private readonly int _iterations;

	public GridProjection(
		IGridProjectionSettings settings
	) {
		_iterations = settings.Iterations;
		ArgumentOutOfRangeException.ThrowIfNegative( _iterations, nameof( settings ) );
	}

	ProjectionNeighbourhood IGridProjection.CreateNeighbourhood<TCell>(
		ICompiledGridAssembly<TCell> compiled,
		IGridLayer<Direction> connectivity
	) {
		IGridLayer<Direction> vacuum = compiled.CreateVacuumLayer();
		IGridLayer<Direction> combined = compiled.CreateLayer<Direction>();
		GridLayout layout = compiled.ChunkLayout;
		foreach( int chunk in layout.EnumerateChunks() ) {
			ReadOnlySpan<Direction> passable = connectivity.GetValues( chunk );
			ReadOnlySpan<Direction> missing = vacuum.GetValues( chunk );
			Span<Direction> output = combined.GetValues( chunk );
			for( int i = 0; i < output.Length; i++ ) {
				output[i] = passable[i] | missing[i];
			}
		}
		compiled.RemoveLayer( vacuum );
		return new ProjectionNeighbourhood( connectivity, combined );
	}

	void IGridProjection.Update<TCell>(
		ICompiledGridAssembly<TCell> compiled,
		ProjectionNeighbourhood neighbourhood,
		IGridLayer<float> sourceVelocityX,
		IGridLayer<float> sourceVelocityY,
		IGridLayer<float> destinationVelocityX,
		IGridLayer<float> destinationVelocityY,
		IGridLayer<float> pressure,
		IGridLayer<float> pressureScratch,
		IGridLayer<float> divergence
	) {
		int paddedLength = ChunkPadding.GetPaddedLength( pressure.Layout.ChunkSize, Halo );
		float[] scratchA = ArrayPool<float>.Shared.Rent( paddedLength );
		float[] scratchB = ArrayPool<float>.Shared.Rent( paddedLength );
		try {
			CalculateDivergence( neighbourhood, sourceVelocityX, sourceVelocityY, divergence, scratchA, scratchB );

			IGridLayer<float> relaxationSource = pressure;
			IGridLayer<float> relaxationDestination = pressureScratch;
			for( int i = 0; i < _iterations; i++ ) {
				Relax( neighbourhood.Combined, relaxationSource, relaxationDestination, divergence, scratchA );
				(relaxationSource, relaxationDestination) = (relaxationDestination, relaxationSource);
			}
			if( ( _iterations & 1 ) == 1 ) {
				compiled.Swap( pressure, pressureScratch );
			}

			SubtractGradient( neighbourhood.Combined, pressure, sourceVelocityX, sourceVelocityY, destinationVelocityX, destinationVelocityY, scratchA );
		} finally {
			ArrayPool<float>.Shared.Return( scratchB );
			ArrayPool<float>.Shared.Return( scratchA );
		}
	}

	private static void CalculateDivergence(
		ProjectionNeighbourhood neighbourhood,
		IGridLayer<float> sourceX,
		IGridLayer<float> sourceY,
		IGridLayer<float> divergence,
		float[] scratchX,
		float[] scratchY
	) {
		IGridLayer<Direction> passableValues = neighbourhood.Passable;
		IGridLayer<Direction> combinedValues = neighbourhood.Combined;
		IGridLayer<float> sourceXValues = sourceX;
		IGridLayer<float> sourceYValues = sourceY;
		IGridLayer<float> divergenceValues = divergence;
		GridLayout layout = sourceX.Layout;
		int size = layout.ChunkSize;
		int stride = size + ( 2 * Halo );
		Span<int> offsets = stackalloc int[DirectionCount];
		Offsets( stride, offsets );
		float scale = PoissonScale / DirectionCount;
		Vector256<float> minusTwo = Vector256.Create( -2f );

		foreach( int chunk in layout.EnumerateChunks() ) {
			if( layout.GetFill( chunk ) == ChunkFill.Empty ) {
				continue;
			}
			TopologyChunk.GatherPadded( sourceXValues, chunk, scratchX );
			TopologyChunk.GatherPadded( sourceYValues, chunk, scratchY );
			ReadOnlySpan<ulong> presence = layout.GetPresence( chunk );
			int words = layout.PresenceWordsPerRow;
			ReadOnlySpan<Direction> passable = passableValues.GetValues( chunk );
			ReadOnlySpan<Direction> combined = combinedValues.GetValues( chunk );
			ReadOnlySpan<float> vx = scratchX;
			ReadOnlySpan<float> vy = scratchY;
			Span<float> rhs = divergenceValues.GetValues( chunk );
			ref byte passableRef = ref Unsafe.As<Direction, byte>( ref MemoryMarshal.GetReference( passable ) );
			ref byte combinedRef = ref Unsafe.As<Direction, byte>( ref MemoryMarshal.GetReference( combined ) );
			ref float vxRef = ref MemoryMarshal.GetReference( vx );
			ref float vyRef = ref MemoryMarshal.GetReference( vy );
			ref float rhsRef = ref MemoryMarshal.GetReference( rhs );

			for( int row = 0; row < size; row++ ) {
				if( !TopologyChunk.AnyInRow( presence, words, row ) ) {
					continue;
				}
				int passableStart = row * size;
				int combinedStart = row * size;
				int xStart = ( ( row + Halo ) * stride ) + Halo;
				int yStart = xStart;
				int rhsStart = row * size;

				int column = 0;
				for( ; column + Block <= size; column += Block ) {
					if( TopologyChunk.Bits( presence, words, row, column, Block ) == 0 ) {
						continue;
					}
					(Vector256<int> passable0, Vector256<int> passable1) = LoadFlags( ref passableRef, passableStart + column );
					(Vector256<int> combined0, Vector256<int> combined1) = LoadFlags( ref combinedRef, combinedStart + column );
					int ix = xStart + column;
					int iy = yStart + column;
					Vector256<float> x0 = Vector256.LoadUnsafe( ref vxRef, (nuint)ix );
					Vector256<float> x1 = Vector256.LoadUnsafe( ref vxRef, (nuint)( ix + 8 ) );
					Vector256<float> y0 = Vector256.LoadUnsafe( ref vyRef, (nuint)iy );
					Vector256<float> y1 = Vector256.LoadUnsafe( ref vyRef, (nuint)( iy + 8 ) );
					Vector256<float> sum0 = Vector256<float>.Zero;
					Vector256<float> sum1 = Vector256<float>.Zero;
					for( int d = 0; d < DirectionCount; d++ ) {
						Vector256<int> bit = Vector256.Create( (int)_directions[d] );
						Vector256<float> ux = Vector256.Create( _unitX[d] );
						Vector256<float> uy = Vector256.Create( _unitY[d] );
						Vector256<float> self0 = ( x0 * ux ) + ( y0 * uy );
						Vector256<float> self1 = ( x1 * ux ) + ( y1 * uy );
						Vector256<float> open0 = ( ~Vector256.Equals( passable0 & bit, Vector256<int>.Zero ) ).AsSingle();
						Vector256<float> open1 = ( ~Vector256.Equals( passable1 & bit, Vector256<int>.Zero ) ).AsSingle();
						Vector256<float> wall0 = Vector256.Equals( combined0 & bit, Vector256<int>.Zero ).AsSingle();
						Vector256<float> wall1 = Vector256.Equals( combined1 & bit, Vector256<int>.Zero ).AsSingle();
						int nx = ix + offsets[d];
						int ny = iy + offsets[d];
						Vector256<float> neighbour0 = ( Vector256.LoadUnsafe( ref vxRef, (nuint)nx ) * ux ) + ( Vector256.LoadUnsafe( ref vyRef, (nuint)ny ) * uy );
						Vector256<float> neighbour1 = ( Vector256.LoadUnsafe( ref vxRef, (nuint)( nx + 8 ) ) * ux ) + ( Vector256.LoadUnsafe( ref vyRef, (nuint)( ny + 8 ) ) * uy );
						// Wall: reflected neighbour velocity, (reflected - self) . u = -2 (self . u). Vacuum contributes nothing.
						sum0 += ( ( neighbour0 - self0 ) & open0 ) + ( ( self0 * minusTwo ) & wall0 );
						sum1 += ( ( neighbour1 - self1 ) & open1 ) + ( ( self1 * minusTwo ) & wall1 );
					}
					( sum0 * scale ).StoreUnsafe( ref rhsRef, (nuint)( rhsStart + column ) );
					( sum1 * scale ).StoreUnsafe( ref rhsRef, (nuint)( rhsStart + column + 8 ) );
				}
				for( ; column < size; column++ ) {
					Direction open = passable[passableStart + column];
					Direction any = combined[combinedStart + column];
					int ix = xStart + column;
					int iy = yStart + column;
					float x = vx[ix];
					float y = vy[iy];
					float sum = 0f;
					for( int d = 0; d < DirectionCount; d++ ) {
						float self = ( x * _unitX[d] ) + ( y * _unitY[d] );
						if( ( open & _directions[d] ) != 0 ) {
							sum += ( vx[ix + offsets[d]] * _unitX[d] ) + ( vy[iy + offsets[d]] * _unitY[d] ) - self;
						} else if( ( any & _directions[d] ) == 0 ) {
							sum -= 2f * self;
						}
					}
					rhs[rhsStart + column] = sum * scale;
				}
			}
			divergence.MarkDirty( chunk );
		}
	}

	private static void Relax(
		IGridLayer<Direction> neighbourhood,
		IGridLayer<float> source,
		IGridLayer<float> destination,
		IGridLayer<float> divergence,
		float[] scratch
	) {
		IGridLayer<Direction> flagValues = neighbourhood;
		IGridLayer<float> sourceValues = source;
		IGridLayer<float> divergenceValues = divergence;
		IGridLayer<float> destinationValues = destination;
		GridLayout layout = source.Layout;
		int size = layout.ChunkSize;
		int stride = size + ( 2 * Halo );
		Span<int> offsets = stackalloc int[DirectionCount];
		Offsets( stride, offsets );
		Vector256<float> minNormal = Vector256.Create( MinNormal );

		foreach( int chunk in layout.EnumerateChunks() ) {
			if( layout.GetFill( chunk ) == ChunkFill.Empty ) {
				continue;
			}
			TopologyChunk.GatherPadded( sourceValues, chunk, scratch );
			ReadOnlySpan<ulong> presence = layout.GetPresence( chunk );
			int words = layout.PresenceWordsPerRow;
			ReadOnlySpan<Direction> flags = flagValues.GetValues( chunk );
			ReadOnlySpan<float> input = scratch;
			ReadOnlySpan<float> rhs = divergenceValues.GetValues( chunk );
			Span<float> output = destinationValues.GetValues( chunk );
			ref byte flagsRef = ref Unsafe.As<Direction, byte>( ref MemoryMarshal.GetReference( flags ) );
			ref float inputRef = ref MemoryMarshal.GetReference( input );
			ref float rhsRef = ref MemoryMarshal.GetReference( rhs );
			ref float outputRef = ref MemoryMarshal.GetReference( output );

			for( int row = 0; row < size; row++ ) {
				if( !TopologyChunk.AnyInRow( presence, words, row ) ) {
					continue;
				}
				int flagStart = row * size;
				int inputStart = ( ( row + Halo ) * stride ) + Halo;
				int rhsStart = row * size;
				int outputStart = row * size;

				int column = 0;
				for( ; column + Block <= size; column += Block ) {
					if( TopologyChunk.Bits( presence, words, row, column, Block ) == 0 ) {
						continue;
					}
					(Vector256<int> include0, Vector256<int> include1) = LoadFlags( ref flagsRef, flagStart + column );
					int i = inputStart + column;
					Vector256<float> sum0 = Vector256<float>.Zero;
					Vector256<float> sum1 = Vector256<float>.Zero;
					Vector256<float> count0 = Vector256<float>.Zero;
					Vector256<float> count1 = Vector256<float>.Zero;
					for( int d = 0; d < DirectionCount; d++ ) {
						Vector256<int> bit = Vector256.Create( (int)_directions[d] );
						Vector256<float> mask0 = ( ~Vector256.Equals( include0 & bit, Vector256<int>.Zero ) ).AsSingle();
						Vector256<float> mask1 = ( ~Vector256.Equals( include1 & bit, Vector256<int>.Zero ) ).AsSingle();
						int n = i + offsets[d];
						sum0 += Vector256.LoadUnsafe( ref inputRef, (nuint)n ) & mask0;
						sum1 += Vector256.LoadUnsafe( ref inputRef, (nuint)( n + 8 ) ) & mask1;
						count0 += Vector256<float>.One & mask0;
						count1 += Vector256<float>.One & mask1;
					}
					Vector256<float> current0 = Vector256.LoadUnsafe( ref inputRef, (nuint)i );
					Vector256<float> current1 = Vector256.LoadUnsafe( ref inputRef, (nuint)( i + 8 ) );
					Vector256<float> rhs0 = Vector256.LoadUnsafe( ref rhsRef, (nuint)( rhsStart + column ) );
					Vector256<float> rhs1 = Vector256.LoadUnsafe( ref rhsRef, (nuint)( rhsStart + column + 8 ) );
					Vector256<float> updated0 = ( sum0 - rhs0 ) / Vector256.Max( count0, Vector256<float>.One );
					Vector256<float> updated1 = ( sum1 - rhs1 ) / Vector256.Max( count1, Vector256<float>.One );
					Vector256<float> result0 = Vector256.ConditionalSelect( Vector256.GreaterThan( count0, Vector256<float>.Zero ), updated0, current0 );
					Vector256<float> result1 = Vector256.ConditionalSelect( Vector256.GreaterThan( count1, Vector256<float>.Zero ), updated1, current1 );
					// Flush denormals; pressure decaying towards vacuum is otherwise very slow on x86.
					( result0 & Vector256.GreaterThanOrEqual( Vector256.Abs( result0 ), minNormal ) ).StoreUnsafe( ref outputRef, (nuint)( outputStart + column ) );
					( result1 & Vector256.GreaterThanOrEqual( Vector256.Abs( result1 ), minNormal ) ).StoreUnsafe( ref outputRef, (nuint)( outputStart + column + 8 ) );
				}
				for( ; column < size; column++ ) {
					int i = inputStart + column;
					Direction cell = flags[flagStart + column];
					float sum = 0f;
					int count = 0;
					for( int d = 0; d < DirectionCount; d++ ) {
						if( ( cell & _directions[d] ) != 0 ) {
							sum += input[i + offsets[d]];
							count++;
						}
					}
					float result = count == 0 ? input[i] : ( sum - rhs[rhsStart + column] ) / count;
					output[outputStart + column] = MathF.Abs( result ) >= MinNormal ? result : 0f;
				}
			}
			destination.MarkDirty( chunk );
		}
	}

	private static void SubtractGradient(
		IGridLayer<Direction> neighbourhood,
		IGridLayer<float> pressure,
		IGridLayer<float> sourceX,
		IGridLayer<float> sourceY,
		IGridLayer<float> destinationX,
		IGridLayer<float> destinationY,
		float[] scratch
	) {
		IGridLayer<Direction> flagValues = neighbourhood;
		IGridLayer<float> pressureValues = pressure;
		IGridLayer<float> sourceXValues = sourceX;
		IGridLayer<float> sourceYValues = sourceY;
		IGridLayer<float> destinationXValues = destinationX;
		IGridLayer<float> destinationYValues = destinationY;
		GridLayout layout = pressure.Layout;
		int size = layout.ChunkSize;
		int stride = size + ( 2 * Halo );
		Span<int> offsets = stackalloc int[DirectionCount];
		Offsets( stride, offsets );
		Vector256<float> inverseCount = Vector256.Create( 1f / DirectionCount );

		foreach( int chunk in layout.EnumerateChunks() ) {
			if( layout.GetFill( chunk ) == ChunkFill.Empty ) {
				continue;
			}
			TopologyChunk.GatherPadded( pressureValues, chunk, scratch );
			ReadOnlySpan<ulong> presence = layout.GetPresence( chunk );
			int words = layout.PresenceWordsPerRow;
			ReadOnlySpan<Direction> flags = flagValues.GetValues( chunk );
			ReadOnlySpan<float> p = scratch;
			ReadOnlySpan<float> vx = sourceXValues.GetValues( chunk );
			ReadOnlySpan<float> vy = sourceYValues.GetValues( chunk );
			Span<float> outX = destinationXValues.GetValues( chunk );
			Span<float> outY = destinationYValues.GetValues( chunk );
			ref byte flagsRef = ref Unsafe.As<Direction, byte>( ref MemoryMarshal.GetReference( flags ) );
			ref float pRef = ref MemoryMarshal.GetReference( p );
			ref float vxRef = ref MemoryMarshal.GetReference( vx );
			ref float vyRef = ref MemoryMarshal.GetReference( vy );
			ref float outXRef = ref MemoryMarshal.GetReference( outX );
			ref float outYRef = ref MemoryMarshal.GetReference( outY );

			for( int row = 0; row < size; row++ ) {
				if( !TopologyChunk.AnyInRow( presence, words, row ) ) {
					continue;
				}
				int flagStart = row * size;
				int pStart = ( ( row + Halo ) * stride ) + Halo;
				int xStart = row * size;
				int yStart = row * size;
				int outXStart = row * size;
				int outYStart = row * size;

				int column = 0;
				for( ; column + Block <= size; column += Block ) {
					if( TopologyChunk.Bits( presence, words, row, column, Block ) == 0 ) {
						continue;
					}
					(Vector256<int> include0, Vector256<int> include1) = LoadFlags( ref flagsRef, flagStart + column );
					int i = pStart + column;
					Vector256<float> self0 = Vector256.LoadUnsafe( ref pRef, (nuint)i );
					Vector256<float> self1 = Vector256.LoadUnsafe( ref pRef, (nuint)( i + 8 ) );
					Vector256<float> gx0 = Vector256<float>.Zero;
					Vector256<float> gx1 = Vector256<float>.Zero;
					Vector256<float> gy0 = Vector256<float>.Zero;
					Vector256<float> gy1 = Vector256<float>.Zero;
					for( int d = 0; d < DirectionCount; d++ ) {
						Vector256<int> bit = Vector256.Create( (int)_directions[d] );
						Vector256<float> mask0 = ( ~Vector256.Equals( include0 & bit, Vector256<int>.Zero ) ).AsSingle();
						Vector256<float> mask1 = ( ~Vector256.Equals( include1 & bit, Vector256<int>.Zero ) ).AsSingle();
						Vector256<float> ux = Vector256.Create( _unitX[d] );
						Vector256<float> uy = Vector256.Create( _unitY[d] );
						int n = i + offsets[d];
						// Vacuum neighbours hold 0, giving delta = -self; walls are masked out (zero gradient).
						Vector256<float> delta0 = ( Vector256.LoadUnsafe( ref pRef, (nuint)n ) - self0 ) & mask0;
						Vector256<float> delta1 = ( Vector256.LoadUnsafe( ref pRef, (nuint)( n + 8 ) ) - self1 ) & mask1;
						gx0 += delta0 * ux;
						gx1 += delta1 * ux;
						gy0 += delta0 * uy;
						gy1 += delta1 * uy;
					}
					int ix = xStart + column;
					int iy = yStart + column;
					( Vector256.LoadUnsafe( ref vxRef, (nuint)ix ) - ( gx0 * inverseCount ) ).StoreUnsafe( ref outXRef, (nuint)( outXStart + column ) );
					( Vector256.LoadUnsafe( ref vxRef, (nuint)( ix + 8 ) ) - ( gx1 * inverseCount ) ).StoreUnsafe( ref outXRef, (nuint)( outXStart + column + 8 ) );
					( Vector256.LoadUnsafe( ref vyRef, (nuint)iy ) - ( gy0 * inverseCount ) ).StoreUnsafe( ref outYRef, (nuint)( outYStart + column ) );
					( Vector256.LoadUnsafe( ref vyRef, (nuint)( iy + 8 ) ) - ( gy1 * inverseCount ) ).StoreUnsafe( ref outYRef, (nuint)( outYStart + column + 8 ) );
				}
				for( ; column < size; column++ ) {
					int i = pStart + column;
					Direction cell = flags[flagStart + column];
					float self = p[i];
					float gx = 0f;
					float gy = 0f;
					for( int d = 0; d < DirectionCount; d++ ) {
						if( ( cell & _directions[d] ) != 0 ) {
							float delta = p[i + offsets[d]] - self;
							gx += delta * _unitX[d];
							gy += delta * _unitY[d];
						}
					}
					outX[outXStart + column] = vx[xStart + column] - ( gx / DirectionCount );
					outY[outYStart + column] = vy[yStart + column] - ( gy / DirectionCount );
				}
			}
			destinationX.MarkDirty( chunk );
			destinationY.MarkDirty( chunk );
		}
	}

	private static void Offsets(
		int stride,
		Span<int> offsets
	) {
		for( int d = 0; d < DirectionCount; d++ ) {
			offsets[d] = ( _deltaRows[d] * stride ) + _deltaColumns[d];
		}
	}

	[MethodImpl( MethodImplOptions.AggressiveInlining )]
	private static (Vector256<int>, Vector256<int>) LoadFlags(
		ref byte flags,
		int index
	) {
		Vector128<byte> include = Vector128.LoadUnsafe( ref flags, (nuint)index );
		(Vector128<ushort> low, Vector128<ushort> high) = Vector128.Widen( include );
		(Vector128<uint> a, Vector128<uint> b) = Vector128.Widen( low );
		(Vector128<uint> c, Vector128<uint> e) = Vector128.Widen( high );
		return (Vector256.Create( a, b ).AsInt32(), Vector256.Create( c, e ).AsInt32());
	}

}
