using Kiyote.Buffers.ChunkedGrids;
using Kiyote.Geometry.Topology;
using static Kiyote.Simulations.LowFidelity.Atmospherics.AtmosphereSimd;

namespace Kiyote.Simulations.LowFidelity.Thermals;

// Grid space has +X east and +Y south, so a sun to the north is (0, -1).
internal sealed class ThermalSolar : IThermalSolar {

	private const float RayStep = 0.5f;

	private static readonly (Direction Face, float X, float Y)[] Faces = [
		( Direction.North, 0.0f, -1.0f ),
		( Direction.East, 1.0f, 0.0f ),
		( Direction.South, 0.0f, 1.0f ),
		( Direction.West, -1.0f, 0.0f )
	];

	void IThermalSolar.Illuminate(
		ChunkedGridLayout layout,
		Vector sunDirection,
		float flux,
		ChunkedGridLayer<float> absorptivityLayer,
		ChunkedGridLayer<Direction> vacuumLayer,
		ChunkedGridLayer<float> solarLayer
	) {
		solarLayer.ClearValues();
		float length = MathF.Sqrt( ( sunDirection.X * sunDirection.X ) + ( sunDirection.Y * sunDirection.Y ) );
		if( length <= 0.0f
			|| flux <= 0.0f
			|| !layout.TryGetPopulatedBounds( out int left, out int top, out int width, out int height )
		) {
			return;
		}
		Bounds bounds = new Bounds( left, top, left + width - 1, top + height - 1 );
		float sx = sunDirection.X / length;
		float sy = sunDirection.Y / length;

		foreach( int chunk in layout.EnumerateChunks() ) {
			ReadOnlySpan<ulong> presence = layout.GetPresence( chunk );
			ReadOnlySpan<float> absorptivity = absorptivityLayer.GetValues( chunk );
			ReadOnlySpan<Direction> vacuum = vacuumLayer.GetValues( chunk );
			Span<float> solar = solarLayer.GetValues( chunk );
			int originX = layout.GetChunkColumn( chunk ) << layout.ChunkShift;
			int originY = layout.GetChunkRow( chunk ) << layout.ChunkShift;
			for( int row = 0; row < ChunkSize; row++ ) {
				uint bits = GetRowMask( presence, row );
				int start = ChunkIndex( 0, row );
				for( int column = 0; bits != 0 && column < ChunkSize; column++ ) {
					int i = start + column;
					if( ( bits & ( 1u << column ) ) == 0 || vacuum[i] == Direction.None ) {
						continue;
					}
					float exposure = 0.0f;
					foreach( (Direction face, float nx, float ny) in Faces ) {
						float facing = ( nx * sx ) + ( ny * sy );
						if( facing <= 0.0f || ( vacuum[i] & face ) == 0 ) {
							continue;
						}
						// Start at the face's midpoint, just outside the cell.
						float x = originX + column + 0.5f + ( nx * 0.5f );
						float y = originY + row + 0.5f + ( ny * 0.5f );
						if( IsLit( layout, bounds, x, y, sx, sy ) ) {
							exposure += facing;
						}
					}
					solar[i] = flux * absorptivity[i] * exposure;
				}
			}
		}
	}

	// Marches towards the sun until the ray leaves the grid's bounds or hits an occupied cell.
	private static bool IsLit(
		ChunkedGridLayout layout,
		Bounds bounds,
		float x,
		float y,
		float sx,
		float sy
	) {
		x += sx * RayStep;
		y += sy * RayStep;
		while( x >= bounds.X1 - 1 && x <= bounds.X2 + 1 && y >= bounds.Y1 - 1 && y <= bounds.Y2 + 1 ) {
			if( layout.IsPopulated( (int)MathF.Floor( x ), (int)MathF.Floor( y ) ) ) {
				return false;
			}
			x += sx * RayStep;
			y += sy * RayStep;
		}
		return true;
	}

	private readonly record struct Bounds(
		int X1,
		int Y1,
		int X2,
		int Y2
	);

}
