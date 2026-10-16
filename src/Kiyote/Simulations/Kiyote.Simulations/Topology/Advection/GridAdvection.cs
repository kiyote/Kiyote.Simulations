using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using Kiyote.Buffers.ChunkedGrids;
using Kiyote.Geometry.Topology;

namespace Kiyote.Simulations.Topology.Advection;

// Backtraces are data-dependent gathers, so the general walk and sample are scalar per cell.
// Blocks of 8 cells whose displacements are all within [-1, 1) take a vectorized fast path:
// the backtrace can then only land in the cell itself or its W/N/NW neighbour, so every read
// is a contiguous load at a fixed offset and the walk becomes per-lane selects. The source
// values and both flag layers are gathered per chunk, with a one-cell border from the
// neighbouring chunks, into padded scratch so blocks at chunk edges stay vectorized. The
// scalar walk can cross any number of chunks, stepping through the chunk neighbours.
public sealed class GridAdvection : IGridAdvection {

	private const int Lanes = 8;
	private const int Halo = 1;
	private const int NoChunk = -1;

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
		return new AdvectionNeighbourhood( connectivity, compiled.CreateVacuumLayer() );
	}

	void IGridAdvection.Update(
		AdvectionNeighbourhood neighbourhood,
		IGridLayer<float> velocityX,
		IGridLayer<float> velocityY,
		IGridLayer<float> source,
		IGridLayer<float> destination
	) {
		ChunkedGridLayer<Direction> passableValues = neighbourhood.Passable.Values;
		ChunkedGridLayer<Direction> vacuumValues = neighbourhood.Vacuum.Values;
		ChunkedGridLayer<float> sourceValues = source.Values;
		ChunkedGridLayer<float> velocityXValues = velocityX.Values;
		ChunkedGridLayer<float> velocityYValues = velocityY.Values;
		ChunkedGridLayer<float> destinationValues = destination.Values;
		ChunkedGridLayout layout = source.Layout;
		int size = layout.ChunkSize;
		int stride = size + ( 2 * Halo );
		float timeStep = _timeStep;
		bool vectorize = Vector256.IsHardwareAccelerated;

		int paddedLength = ChunkPadding.GetPaddedLength( size, Halo );
		float[] inputScratch = ArrayPool<float>.Shared.Rent( paddedLength );
		Direction[] passableScratch = ArrayPool<Direction>.Shared.Rent( paddedLength );
		Direction[] vacuumScratch = ArrayPool<Direction>.Shared.Rent( paddedLength );
		try {
			foreach( int chunk in layout.EnumerateChunks() ) {
				if( layout.GetFill( chunk ) == ChunkFill.Empty ) {
					continue;
				}
				TopologyChunk.GatherPadded( sourceValues, chunk, inputScratch );
				TopologyChunk.GatherPadded( passableValues, chunk, passableScratch );
				TopologyChunk.GatherPadded( vacuumValues, chunk, vacuumScratch );
				ReadOnlySpan<ulong> presence = layout.GetPresence( chunk );
				int words = layout.PresenceWordsPerRow;

				ReadOnlySpan<float> vx = velocityXValues.GetValues( chunk );
				ReadOnlySpan<float> vy = velocityYValues.GetValues( chunk );
				Span<float> output = destinationValues.GetValues( chunk );
				ref float vxRef = ref MemoryMarshal.GetReference( vx );
				ref float vyRef = ref MemoryMarshal.GetReference( vy );
				ref float inputRef = ref MemoryMarshal.GetArrayDataReference( inputScratch );
				ref float outputRef = ref MemoryMarshal.GetReference( output );
				ref byte passableRef = ref Unsafe.As<Direction, byte>( ref MemoryMarshal.GetArrayDataReference( passableScratch ) );
				ref byte vacuumRef = ref Unsafe.As<Direction, byte>( ref MemoryMarshal.GetArrayDataReference( vacuumScratch ) );
				Walker walker = new Walker(
					layout,
					passableValues,
					vacuumValues,
					sourceValues,
					chunk,
					inputScratch,
					passableValues.GetValues( chunk ),
					vacuumValues.GetValues( chunk ),
					size,
					stride,
					timeStep
				);

				for( int row = 0; row < size; row++ ) {
					int xStart = row * size;
					int yStart = row * size;
					int outputStart = row * size;
					// The padded source and flag scratch share a stride, so one start serves both.
					int paddedStart = ( ( row + Halo ) * stride ) + Halo;
					// Unoccupied cells have no velocity and hold 0, so they always advect to 0; skip them.
					if( !TopologyChunk.AnyInRow( presence, words, row ) ) {
						continue;
					}
					int column = 0;
					if( vectorize ) {
						for( ; column + Lanes <= size; column += Lanes ) {
							if( TopologyChunk.Bits( presence, words, row, column, Lanes ) == 0 ) {
								continue;
							}
							if( !TryAdvectBlock(
								ref vxRef, ref vyRef, ref inputRef, ref outputRef, ref passableRef, ref vacuumRef,
								xStart + column, yStart + column, paddedStart + column, outputStart + column, paddedStart + column,
								stride, stride, timeStep
							) ) {
								for( int lane = column; lane < column + Lanes; lane++ ) {
									output[outputStart + lane] = walker.AdvectCell( vx[xStart + lane], vy[yStart + lane], lane, row );
								}
							}
						}
					}
					for( ; column < size; column++ ) {
						if( TopologyChunk.Bits( presence, words, row, column, 1 ) == 0 ) {
							continue;
						}
						output[outputStart + column] = walker.AdvectCell( vx[xStart + column], vy[yStart + column], column, row );
					}
				}
				destination.MarkDirty( chunk );
			}
		} finally {
			ArrayPool<Direction>.Shared.Return( vacuumScratch );
			ArrayPool<Direction>.Shared.Return( passableScratch );
			ArrayPool<float>.Shared.Return( inputScratch );
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

	// The general scalar walk and bilinear sample for one cell of the chunk being updated.
	// Reads within that chunk come from its padded scratch; once the walk leaves it, flags and
	// values are read from whichever chunk the walker or sample lands in, with missing chunks
	// reading as 0.
	private readonly ref struct Walker {

		private readonly ChunkedGridLayout _layout;
		private readonly ChunkedGridLayer<Direction> _passable;
		private readonly ChunkedGridLayer<Direction> _vacuum;
		private readonly ChunkedGridLayer<float> _source;
		private readonly int _chunk;
		private readonly ReadOnlySpan<float> _input;
		private readonly ReadOnlySpan<Direction> _chunkPassable;
		private readonly ReadOnlySpan<Direction> _chunkVacuum;
		private readonly int _size;
		private readonly int _stride;
		private readonly float _timeStep;

		public Walker(
			ChunkedGridLayout layout,
			ChunkedGridLayer<Direction> passable,
			ChunkedGridLayer<Direction> vacuum,
			ChunkedGridLayer<float> source,
			int chunk,
			ReadOnlySpan<float> input,
			ReadOnlySpan<Direction> chunkPassable,
			ReadOnlySpan<Direction> chunkVacuum,
			int size,
			int stride,
			float timeStep
		) {
			_layout = layout;
			_passable = passable;
			_vacuum = vacuum;
			_source = source;
			_chunk = chunk;
			_input = input;
			_chunkPassable = chunkPassable;
			_chunkVacuum = chunkVacuum;
			_size = size;
			_stride = stride;
			_timeStep = timeStep;
		}

		public float AdvectCell(
			float velocityX,
			float velocityY,
			int column,
			int row
		) {
			float self = _input[( ( row + Halo ) * _stride ) + column + Halo];
			float dx = -velocityX * _timeStep;
			float dy = -velocityY * _timeStep;
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
			int s = _chunk;
			int c = column;
			int r = row;
			ReadOnlySpan<Direction> passable = _chunkPassable;
			ReadOnlySpan<Direction> vacuum = _chunkVacuum;
			bool inVacuum = false;
			while( remainingX > 0 || remainingY > 0 ) {
				bool moveX = remainingX >= remainingY;
				Direction step = moveX ? horizontal : vertical;
				int f = ( r * _size ) + c;
				if( ( passable[f] & step ) != 0 ) {
					if( Step( step, ref s, ref c, ref r ) ) {
						passable = s == _chunk ? _chunkPassable : _passable.GetValues( s );
						vacuum = s == _chunk ? _chunkVacuum : _vacuum.GetValues( s );
					}
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

			int flagIndex = ( r * _size ) + c;
			Direction open = passable[flagIndex];
			Direction missing = vacuum[flagIndex];
			bool eastOpen = ( open & Direction.East ) != 0;
			bool southOpen = ( open & Direction.South ) != 0;
			bool southEastOpen = ( open & Direction.SouthEast ) != 0;
			float origin;
			float eastValue = 0f;
			float southValue = 0f;
			float southEastValue = 0f;
			if( s == _chunk ) {
				int i = ( ( r + Halo ) * _stride ) + c + Halo;
				origin = _input[i];
				if( eastOpen ) {
					eastValue = _input[i + 1];
				}
				if( southOpen ) {
					southValue = _input[i + _stride];
				}
				if( southEastOpen ) {
					southEastValue = _input[i + _stride + 1];
				}
			} else {
				origin = _source.GetValues( s )[flagIndex];
				if( eastOpen ) {
					eastValue = Sample( s, c + 1, r );
				}
				if( southOpen ) {
					southValue = Sample( s, c, r + 1 );
				}
				if( southEastOpen ) {
					southEastValue = Sample( s, c + 1, r + 1 );
				}
			}

			bool eastWall = ( ( open | missing ) & Direction.East ) == 0;
			bool southWall = ( ( open | missing ) & Direction.South ) == 0;
			float east = eastOpen ? eastValue : eastWall ? origin : 0f;
			float south = southOpen ? southValue : southWall ? origin : 0f;
			float southEast;
			if( southEastOpen ) {
				southEast = southEastValue;
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

		// Reads the value at a local cell of chunk s that may be one past its east or south edge,
		// resolving it in the neighbouring chunk; a missing chunk reads as 0.
		private float Sample(
			int s,
			int column,
			int row
		) {
			int dx = column == _size ? 1 : 0;
			int dy = row == _size ? 1 : 0;
			int target = s;
			if( dx != 0 || dy != 0 ) {
				target = _layout.GetNeighbours( s )[( ( dy + 1 ) * 3 ) + dx + 1];
				if( target == NoChunk ) {
					return 0f;
				}
				column -= dx * _size;
				row -= dy * _size;
			}
			return _source.GetValues( target )[( row * _size ) + column];
		}

		// Moves one cell in direction, returning true when the walker crossed into another chunk.
		private bool Step(
			Direction direction,
			ref int chunk,
			ref int column,
			ref int row
		) {
			switch( direction ) {
				case Direction.East:
					if( ++column < _size ) {
						return false;
					}
					column = 0;
					break;
				case Direction.West:
					if( --column >= 0 ) {
						return false;
					}
					column = _size - 1;
					break;
				case Direction.South:
					if( ++row < _size ) {
						return false;
					}
					row = 0;
					break;
				default:
					if( --row >= 0 ) {
						return false;
					}
					row = _size - 1;
					break;
			}
			chunk = _layout.GetNeighbours( chunk )[direction.ToNeighbourIndex()];
			return true;
		}

	}

}
