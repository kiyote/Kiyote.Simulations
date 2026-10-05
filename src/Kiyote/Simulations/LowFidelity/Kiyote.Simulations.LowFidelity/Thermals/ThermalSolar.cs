using Kiyote.Geometry;
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
		IGridChunkLayout layout,
		Vector sunDirection,
		float flux,
		IGridLayer<float> absorptivityLayer,
		IGridLayer<Direction> vacuumLayer,
		IGridLayer<float> solarLayer
	) {
		Span<float> solar = solarLayer.Cells;
		solar.Clear();
		float length = MathF.Sqrt( ( sunDirection.X * sunDirection.X ) + ( sunDirection.Y * sunDirection.Y ) );
		Rect? bounds = layout.Bounds;
		if( length <= 0.0f || flux <= 0.0f || !bounds.HasValue ) {
			return;
		}
		float sx = sunDirection.X / length;
		float sy = sunDirection.Y / length;
		Span<float> absorptivity = absorptivityLayer.Cells;
		Span<Direction> vacuum = vacuumLayer.Cells;

		for( int slot = 0; slot < layout.SlotCount; slot++ ) {
			if( layout.GetState( slot ) == ChunkState.Empty ) {
				continue;
			}
			ReadOnlySpan<ulong> mask = layout.GetValidityMask( slot );
			Point origin = layout.GetOrigin( slot );
			for( int row = 0; row < ChunkSize; row++ ) {
				uint bits = GetRowMask( mask, row );
				if( bits == 0 ) {
					continue;
				}
				int start = solarLayer.IndexOf( slot, 0, row );
				for( int column = 0; column < ChunkSize; column++ ) {
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
						float x = origin.X + column + 0.5f + ( nx * 0.5f );
						float y = origin.Y + row + 0.5f + ( ny * 0.5f );
						if( IsLit( layout, bounds.Value, x, y, sx, sy ) ) {
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
		IGridChunkLayout layout,
		Rect bounds,
		float x,
		float y,
		float sx,
		float sy
	) {
		x += sx * RayStep;
		y += sy * RayStep;
		while( x >= bounds.X1 - 1 && x <= bounds.X2 + 1 && y >= bounds.Y1 - 1 && y <= bounds.Y2 + 1 ) {
			if( layout.TryGetCell( (int)MathF.Floor( x ), (int)MathF.Floor( y ), out _, out _ ) ) {
				return false;
			}
			x += sx * RayStep;
			y += sy * RayStep;
		}
		return true;
	}

}
