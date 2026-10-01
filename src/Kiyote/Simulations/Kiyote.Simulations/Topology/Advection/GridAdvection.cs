using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using Kiyote.Geometry.Topology;

namespace Kiyote.Simulations.Topology.Advection;

// Backtraces are data-dependent gathers, so the general walk and sample are scalar per cell.
// Blocks of 8 cells whose displacements are all within [-1, 1) take a vectorized fast path:
// the backtrace can then only land in the cell itself or its W/N/NW neighbour, so every read
// is a contiguous load at a fixed offset and the walk becomes per-lane selects.
public sealed class GridAdvection : IGridAdvection {

	private const int Lanes = 8;

	private readonly float _timeStep;

	public GridAdvection(
		ISimulationClock clock
	) {
		_timeStep = clock.FixedTimeStep;
	}

	AdvectionNeighbourhood IGridAdvection.CreateNeighbourhood<TCell>(
		ICompiledGridAssembly<TCell> compiled,
		IGridLayer<Direction> connectivity
	) {
		// The fast path reads W/N/NW flags across chunk edges, so both flag layers need a halo.
		const int halo = 1;
		IGridLayer<Direction> passable = connectivity;
		if( connectivity.Halo < halo ) {
			passable = compiled.CreateLayer<Direction>( halo );
			IGridChunkLayout layout = connectivity.Space;
			int size = layout.ChunkSize;
			ReadOnlySpan<Direction> from = connectivity.Cells;
			Span<Direction> to = passable.Cells;
			for( int slot = 0; slot < layout.SlotCount; slot++ ) {
				for( int row = 0; row < size; row++ ) {
					from.Slice( connectivity.IndexOf( slot, 0, row ), size )
						.CopyTo( to.Slice( passable.IndexOf( slot, 0, row ), size ) );
				}
			}
			passable.ExchangeHalos();
		}
		return new AdvectionNeighbourhood( passable, compiled.CreateVacuumLayer( passable.Halo ) );
	}

	void IGridAdvection.Update(
		AdvectionNeighbourhood neighbourhood,
		IGridLayer<float> velocityX,
		IGridLayer<float> velocityY,
		IGridLayer<float> source,
		IGridLayer<float> destination
	) {
		ArgumentOutOfRangeException.ThrowIfLessThan( source.Halo, 1 );

		source.ExchangeHalos();

		IGridLayer<Direction> passableLayer = neighbourhood.Passable;
		IGridLayer<Direction> vacuumLayer = neighbourhood.Vacuum;
		IGridChunkLayout layout = source.Space;
		int size = layout.ChunkSize;
		float timeStep = _timeStep;

		ReadOnlySpan<Direction> passable = passableLayer.Cells;
		ReadOnlySpan<Direction> vacuum = vacuumLayer.Cells;
		ReadOnlySpan<float> vx = velocityX.Cells;
		ReadOnlySpan<float> vy = velocityY.Cells;
		ReadOnlySpan<float> input = source.Cells;
		Span<float> output = destination.Cells;
		LayerIndexer flags = new LayerIndexer( passableLayer );
		LayerIndexer values = new LayerIndexer( source );
		int stride = source.Stride;
		int flagStride = passableLayer.Stride;
		// With flag halos, the W/N/NW neighbour flags at a chunk edge come from the neighbouring
		// chunk (the source halo is always exchanged), so edge blocks can stay vectorized.
		bool crossEdges = passableLayer.Halo >= 1 && vacuumLayer.Halo >= 1;
		bool vectorize = Vector256.IsHardwareAccelerated;
		ref float vxRef = ref MemoryMarshal.GetReference( vx );
		ref float vyRef = ref MemoryMarshal.GetReference( vy );
		ref float inputRef = ref MemoryMarshal.GetReference( input );
		ref float outputRef = ref MemoryMarshal.GetReference( output );
		ref byte passableRef = ref Unsafe.As<Direction, byte>( ref MemoryMarshal.GetReference( passable ) );
		ref byte vacuumRef = ref Unsafe.As<Direction, byte>( ref MemoryMarshal.GetReference( vacuum ) );

		for( int slot = 0; slot < layout.SlotCount; slot++ ) {
			if( layout.GetState( slot ) == ChunkState.Empty ) {
				continue;
			}
			for( int row = 0; row < size; row++ ) {
				int xStart = velocityX.IndexOf( slot, 0, row );
				int yStart = velocityY.IndexOf( slot, 0, row );
				int inputStart = source.IndexOf( slot, 0, row );
				int outputStart = destination.IndexOf( slot, 0, row );
				int flagStart = flags.Index( slot, 0, row );
				int column = 0;
				if( vectorize ) {
					for( ; column + Lanes <= size; column += Lanes ) {
						if( !TryAdvectBlock(
							ref vxRef, ref vyRef, ref inputRef, ref outputRef, ref passableRef, ref vacuumRef,
							xStart + column, yStart + column, inputStart + column, outputStart + column, flagStart + column,
							stride, flagStride, !crossEdges && column == 0, !crossEdges && row == 0, timeStep
						) ) {
							for( int lane = column; lane < column + Lanes; lane++ ) {
								output[outputStart + lane] = AdvectCell( layout, size, passable, vacuum, vx, vy, input, flags, values, stride, timeStep, slot, lane, row, xStart, yStart, inputStart );
							}
						}
					}
				}
				for( ; column < size; column++ ) {
					output[outputStart + column] = AdvectCell( layout, size, passable, vacuum, vx, vy, input, flags, values, stride, timeStep, slot, column, row, xStart, yStart, inputStart );
				}
			}
			destination.MarkDirty( slot );
		}
	}

	// Returns false (writing nothing) when any lane needs the general walk.
	[MethodImpl( MethodImplOptions.AggressiveInlining )]
	private static bool TryAdvectBlock(
		ref float vxRef,
		ref float vyRef,
		ref float inputRef,
		ref float outputRef,
		ref byte passableRef,
		ref byte vacuumRef,
		int x,
		int y,
		int i,
		int o,
		int f,
		int s,
		int fs,
		bool firstColumn,
		bool firstRow,
		float timeStep
	) {
		Vector256<float> dt = Vector256.Create( -timeStep );
		Vector256<float> dx = Vector256.LoadUnsafe( ref vxRef, (nuint)x ) * dt;
		Vector256<float> dy = Vector256.LoadUnsafe( ref vyRef, (nuint)y ) * dt;
		Vector256<float> one = Vector256<float>.One;
		Vector256<float> minusOne = -one;
		// NaN fails both comparisons, so non-finite displacements fall back too.
		Vector256<float> inRange = Vector256.GreaterThanOrEqual( dx, minusOne ) & Vector256.LessThan( dx, one )
			& Vector256.GreaterThanOrEqual( dy, minusOne ) & Vector256.LessThan( dy, one );
		if( !Vector256.EqualsAll( inRange.AsInt32(), Vector256<int>.AllBitsSet ) ) {
			return false;
		}

		Vector256<float> zero = Vector256<float>.Zero;
		Vector256<int> wx = Vector256.LessThan( dx, zero ).AsInt32();
		Vector256<int> wy = Vector256.LessThan( dy, zero ).AsInt32();
		if( ( firstColumn && wx.GetElement( 0 ) != 0 ) || ( firstRow && wy != Vector256<int>.Zero ) ) {
			return false;
		}
		if( f - fs - 1 < 0 ) {
			return false;
		}

		Vector256<int> p0 = LoadFlags( ref passableRef, f );
		Vector256<int> pW = LoadFlags( ref passableRef, f - 1 );
		Vector256<int> pN = LoadFlags( ref passableRef, f - fs );
		Vector256<int> pNW = LoadFlags( ref passableRef, f - fs - 1 );
		Vector256<int> v0 = LoadFlags( ref vacuumRef, f );
		Vector256<int> vW = LoadFlags( ref vacuumRef, f - 1 );
		Vector256<int> vN = LoadFlags( ref vacuumRef, f - fs );
		Vector256<int> vNW = LoadFlags( ref vacuumRef, f - fs - 1 );

		Vector256<int> west = Vector256.Create( (int)Direction.West );
		Vector256<int> north = Vector256.Create( (int)Direction.North );

		// Step west first (matching the scalar walk's tie-break), then north from wherever that left us.
		Vector256<int> passX = wx & Has( p0, west );
		Vector256<int> vacuumX = wx & ~passX & Has( v0, west );
		Vector256<int> wallX = wx & ~passX & ~vacuumX;
		Vector256<int> currentP = Vector256.ConditionalSelect( passX, pW, p0 );
		Vector256<int> currentV = Vector256.ConditionalSelect( passX, vW, v0 );
		Vector256<int> passY = wy & Has( currentP, north );
		Vector256<int> vacuumY = wy & ~passY & Has( currentV, north );
		Vector256<int> wallY = wy & ~passY & ~vacuumY;
		Vector256<int> inVacuum = vacuumX | ( ~vacuumX & vacuumY );

		Vector256<float> fx = Vector256.ConditionalSelect( wx.AsSingle(), dx + one, dx );
		Vector256<float> fy = Vector256.ConditionalSelect( wy.AsSingle(), dy + one, dy );
		fx = Vector256.ConditionalSelect( wallX.AsSingle(), zero, fx );
		fy = Vector256.ConditionalSelect( wallY.AsSingle(), zero, fy );

		Vector256<int> open = Pick( passX, passY, p0, pW, pN, pNW );
		Vector256<int> missing = Pick( passX, passY, v0, vW, vN, vNW );

		Vector256<float> nw = Vector256.LoadUnsafe( ref inputRef, (nuint)( i - s - 1 ) );
		Vector256<float> n = Vector256.LoadUnsafe( ref inputRef, (nuint)( i - s ) );
		Vector256<float> ne = Vector256.LoadUnsafe( ref inputRef, (nuint)( i - s + 1 ) );
		Vector256<float> w = Vector256.LoadUnsafe( ref inputRef, (nuint)( i - 1 ) );
		Vector256<float> c = Vector256.LoadUnsafe( ref inputRef, (nuint)i );
		Vector256<float> e = Vector256.LoadUnsafe( ref inputRef, (nuint)( i + 1 ) );
		Vector256<float> sw = Vector256.LoadUnsafe( ref inputRef, (nuint)( i + s - 1 ) );
		Vector256<float> so = Vector256.LoadUnsafe( ref inputRef, (nuint)( i + s ) );
		Vector256<float> se = Vector256.LoadUnsafe( ref inputRef, (nuint)( i + s + 1 ) );

		Vector256<float> originValue = Pick( passX, passY, c, w, n, nw );
		Vector256<float> eastValue = Pick( passX, passY, e, c, ne, n );
		Vector256<float> southValue = Pick( passX, passY, so, sw, c, w );
		Vector256<float> southEastValue = Pick( passX, passY, se, so, e, c );

		Vector256<int> eastBit = Vector256.Create( (int)Direction.East );
		Vector256<int> southBit = Vector256.Create( (int)Direction.South );
		Vector256<int> southEastBit = Vector256.Create( (int)Direction.SouthEast );
		Vector256<int> either = open | missing;
		Vector256<float> eastWall = ( ~Has( either, eastBit ) ).AsSingle();
		Vector256<float> southWall = ( ~Has( either, southBit ) ).AsSingle();

		Vector256<float> east = Vector256.ConditionalSelect(
			Has( open, eastBit ).AsSingle(),
			eastValue,
			Vector256.ConditionalSelect( eastWall, originValue, zero )
		);
		Vector256<float> south = Vector256.ConditionalSelect(
			Has( open, southBit ).AsSingle(),
			southValue,
			Vector256.ConditionalSelect( southWall, originValue, zero )
		);
		Vector256<float> fallback = Vector256.ConditionalSelect(
			southWall,
			east,
			Vector256.ConditionalSelect( eastWall, south, originValue )
		);
		Vector256<float> southEast = Vector256.ConditionalSelect(
			Has( open, southEastBit ).AsSingle(),
			southEastValue,
			Vector256.ConditionalSelect( Has( missing, southEastBit ).AsSingle(), zero, fallback )
		);

		Vector256<float> near = originValue + ( ( east - originValue ) * fx );
		Vector256<float> far = south + ( ( southEast - south ) * fx );
		Vector256<float> result = near + ( ( far - near ) * fy );
		result = Vector256.ConditionalSelect( inVacuum.AsSingle(), zero, result );
		Vector256<float> still = Vector256.Equals( dx, zero ) & Vector256.Equals( dy, zero );
		result = Vector256.ConditionalSelect( still, c, result );
		result.StoreUnsafe( ref outputRef, (nuint)o );
		return true;
	}

	[MethodImpl( MethodImplOptions.AggressiveInlining )]
	private static Vector256<int> Has(
		Vector256<int> flags,
		Vector256<int> bit
	) => ~Vector256.Equals( flags & bit, Vector256<int>.Zero );

	[MethodImpl( MethodImplOptions.AggressiveInlining )]
	private static Vector256<T> Pick<T>(
		Vector256<int> movedX,
		Vector256<int> movedY,
		Vector256<T> self,
		Vector256<T> westCell,
		Vector256<T> northCell,
		Vector256<T> northWestCell
	) {
		Vector256<T> stayY = Vector256.ConditionalSelect( movedX.As<int, T>(), westCell, self );
		Vector256<T> movedNorth = Vector256.ConditionalSelect( movedX.As<int, T>(), northWestCell, northCell );
		return Vector256.ConditionalSelect( movedY.As<int, T>(), movedNorth, stayY );
	}

	[MethodImpl( MethodImplOptions.AggressiveInlining )]
	private static Vector256<int> LoadFlags(
		ref byte flags,
		int index
	) {
		ulong bits = Unsafe.ReadUnaligned<ulong>( ref Unsafe.Add( ref flags, index ) );
		Vector128<ushort> widened = Vector128.WidenLower( Vector128.CreateScalar( bits ).AsByte() );
		return Vector256.Create( Vector128.WidenLower( widened ), Vector128.WidenUpper( widened ) ).AsInt32();
	}

	private static float AdvectCell(
		IGridChunkLayout layout,
		int size,
		ReadOnlySpan<Direction> passable,
		ReadOnlySpan<Direction> vacuum,
		ReadOnlySpan<float> vx,
		ReadOnlySpan<float> vy,
		ReadOnlySpan<float> input,
		LayerIndexer flags,
		LayerIndexer values,
		int stride,
		float timeStep,
		int slot,
		int column,
		int row,
		int xStart,
		int yStart,
		int inputStart
	) {
					float self = input[inputStart + column];
					float dx = -vx[xStart + column] * timeStep;
					float dy = -vy[yStart + column] * timeStep;
					if( ( dx == 0f && dy == 0f ) || !float.IsFinite( dx ) || !float.IsFinite( dy ) ) {
						return self;
					}

					// Walk to the cell containing the backtraced position, so displacements of
					// more than one cell are interpolated rather than truncated.
					float floorX = MathF.Floor( dx );
					float floorY = MathF.Floor( dy );
					float fx = dx - floorX;
					float fy = dy - floorY;
					int remainingX = Math.Abs( (int)floorX );
					int remainingY = Math.Abs( (int)floorY );
					Direction horizontal = floorX < 0f ? Direction.West : Direction.East;
					Direction vertical = floorY < 0f ? Direction.North : Direction.South;
					int s = slot;
					int c = column;
					int r = row;
					bool inVacuum = false;
					while( remainingX > 0 || remainingY > 0 ) {
						bool moveX = remainingX >= remainingY;
						Direction step = moveX ? horizontal : vertical;
						int f = flags.Index( s, c, r );
						if( ( passable[f] & step ) != 0 ) {
							Step( layout, size, step, ref s, ref c, ref r );
							if( moveX ) {
								remainingX--;
							} else {
								remainingY--;
							}
						} else if( ( vacuum[f] & step ) != 0 ) {
							inVacuum = true;
							break;
						} else if( moveX ) {
							remainingX = 0;
							fx = 0f;
						} else {
							remainingY = 0;
							fy = 0f;
						}
					}
					if( inVacuum ) {
						return 0f;
					}

					int flagIndex = flags.Index( s, c, r );
					Direction open = passable[flagIndex];
					Direction missing = vacuum[flagIndex];
					int i = values.Index( s, c, r );
					float origin = input[i];
					bool eastWall = ( ( open | missing ) & Direction.East ) == 0;
					bool southWall = ( ( open | missing ) & Direction.South ) == 0;
					float east = ( open & Direction.East ) != 0 ? input[i + 1] : eastWall ? origin : 0f;
					float south = ( open & Direction.South ) != 0 ? input[i + stride] : southWall ? origin : 0f;
					float southEast;
					if( ( open & Direction.SouthEast ) != 0 ) {
						southEast = input[i + stride + 1];
					} else if( ( missing & Direction.SouthEast ) != 0 ) {
						southEast = 0f;
					} else if( southWall ) {
						southEast = east;
					} else if( eastWall ) {
						southEast = south;
					} else {
						southEast = origin;
					}

					float near = origin + ( ( east - origin ) * fx );
					float far = south + ( ( southEast - south ) * fx );
					return near + ( ( far - near ) * fy );
	}

	private static void Step(
		IGridChunkLayout layout,
		int size,
		Direction direction,
		ref int slot,
		ref int column,
		ref int row
	) {
		switch( direction ) {
			case Direction.East:
				if( ++column == size ) {
					column = 0;
					slot = layout.GetNeighbour( slot, Direction.East );
				}
				break;
			case Direction.West:
				if( --column < 0 ) {
					column = size - 1;
					slot = layout.GetNeighbour( slot, Direction.West );
				}
				break;
			case Direction.South:
				if( ++row == size ) {
					row = 0;
					slot = layout.GetNeighbour( slot, Direction.South );
				}
				break;
			default:
				if( --row < 0 ) {
					row = size - 1;
					slot = layout.GetNeighbour( slot, Direction.North );
				}
				break;
		}
	}

	private readonly struct LayerIndexer {

		private readonly int _chunkLength;
		private readonly int _stride;
		private readonly int _halo;

		public LayerIndexer(
			IGridLayer layer
		) {
			_chunkLength = layer.ChunkLength;
			_stride = layer.Stride;
			_halo = layer.Halo;
		}

		public int Index(
			int slot,
			int column,
			int row
		) => ( slot * _chunkLength ) + ( ( row + _halo ) * _stride ) + column + _halo;

	}

}
