using Kiyote.Buffers.Numerics;
using Kiyote.Imaging;
using Kiyote.Simulations.Grids;
using Kiyote.Simulations.Visualizer.GridDiffusion;

namespace Kiyote.Simulations.Visualizer.GridFluid;

internal sealed class GridFluidVisualizer {

	public const int Size = 100;

	private readonly IAnimationWriter _animWriter;
	private readonly IGridFluid _gridFluid;
	private readonly INumericBufferFactory _bufferFactory;
	private readonly INumericBufferOperator _bufferOperation;

	public GridFluidVisualizer(
		IAnimationWriter animWriter,
		IGridFluid gridFluid,
		INumericBufferFactory bufferFactory,
		INumericBufferOperator bufferOperator
	) {
		_animWriter = animWriter;
		_gridFluid = gridFluid;
		_bufferFactory = bufferFactory;
		_bufferOperation = bufferOperator;
	}

	// Two large square chambers on the left/right edges of the grid, joined by a
	// narrow horizontal corridor running through the vertical center.
	private static readonly (int Left, int Top, int Right, int Bottom) _boxA = ( 5, 5, 35, 35 );
	private static readonly (int Left, int Top, int Right, int Bottom) _boxB = ( Size - 36, 5, Size - 6, 35 );
	private static readonly (int Left, int Top, int Right, int Bottom) _corridor = ( _boxA.Right, 18, _boxB.Left, 22 );

	// The pump lives at the center of BoxA and injects gas at a fixed velocity so the
	// plume it creates visibly pushes toward the corridor and into BoxB.
	private static readonly (int Column, int Row) _pumpCell = ( ( _boxA.Left + _boxA.Right ) / 2, ( _boxA.Top + _boxA.Bottom ) / 2 );

	// The pump emits into an arc of directions rather than a single fixed vector -
	// CenterDegrees is measured clockwise from straight up (0 = up, 90 = right,
	// 180 = down, 270 = left), WidthDegrees is the total arc width centered on that
	// direction, Radius is how far from PumpCell the emission disc extends, and
	// Speed is the magnitude of the velocity given to cells within the arc.
	private const double PumpAngleCenterDegrees = 90.0;
	private const double PumpAngleWidthDegrees = 180.0;
	private const int PumpRadius = 3;
	private const double PumpSpeed = 4.0;

	private const double PumpTarget = 10000;
	private const double PumpMinRate = 1000;
	private const double PumpFillFraction = 0.1;

	public void Execute(
		string outputFolder
	) {
		INumericBuffer<byte> stretched = _bufferFactory.Create<byte>( Size, Size, 0 );
		INumericBuffer<double> concentrationBuffer = _bufferFactory.Create<double>( Size, Size, 0 );
		BufferGrid<double> concentration = new BufferGrid<double>( concentrationBuffer );

		VelocityGrid velocity = new VelocityGrid( Size, Size );

		IdentityVelocityAccessor velocityAccessor = new IdentityVelocityAccessor();
		VelocityDiffusionStrategy velocityDiffusion = new VelocityDiffusionStrategy( 0.02 );
		BilinearSampler sampler = new BilinearSampler();
		TwoBoxBoundaryStrategy boundary = new TwoBoxBoundaryStrategy( _boxA, _boxB, _corridor );
		VelocitySetCellStrategy velocitySetCell = new VelocitySetCellStrategy( velocity );
		BufferSetCellStrategy concentrationSetCell = new BufferSetCellStrategy( concentrationBuffer );

		List<GasSpecies<double, BufferSetCellStrategy>> gases = [
			new( concentration, concentrationSetCell ),
		];

		IAnimationBuilder builder = _animWriter.StartAnimation( Path.Combine( outputFolder, "gridfluid.apng" ), TimeSpan.FromMilliseconds( 100 ), 0 );
		_bufferOperation.ScaleToRange( concentrationBuffer, stretched );
		builder.AddFrame( stretched );
		for( int i = 0; i < 150; i++ ) {
			// The pump injects gas at the pump cell each frame, capped by its own
			// throughput limit (see PumpGas for the rate rule).
			PumpGas( concentrationBuffer, _pumpCell, PumpTarget, PumpMinRate, PumpFillFraction );

			// Only inject velocity within the pump's emission arc - everything else is
			// left for StepVelocity's diffuse/project/advect passes to propagate outward
			// on their own, so the flow field emerges organically frame over frame
			// instead of being repainted from scratch every time.
			SeedPumpArc( velocity, _pumpCell, PumpRadius, PumpAngleCenterDegrees, PumpAngleWidthDegrees, PumpSpeed );

			_gridFluid.StepVelocity<Velocity, IdentityVelocityAccessor, VelocityDiffusionStrategy, BilinearSampler, TwoBoxBoundaryStrategy, VelocitySetCellStrategy>(
				velocity,
				velocityAccessor,
				velocityDiffusion,
				sampler,
				boundary,
				velocitySetCell,
				1.0
			);

			_gridFluid.AdvectGases<Velocity, IdentityVelocityAccessor, double, BilinearSampler, TwoBoxBoundaryStrategy, BufferSetCellStrategy>(
				velocity,
				velocityAccessor,
				gases,
				sampler,
				boundary,
				1.0
			);

			_bufferOperation.ScaleToRange( concentrationBuffer, stretched );
			builder.AddFrame( stretched );
		}
		builder.FinishAnimation();
	}

	// Simulates a pump filling its space continuously: it injects a fraction of the
	// remaining gap to target each frame (a proportional taper, like a real pump
	// easing off as the space fills), floored at minRate so it doesn't stall out
	// infinitely close to the target, and never overshoots the target.
	private static void PumpGas(
		INumericBuffer<double> concentrationBuffer,
		(int Column, int Row) cell,
		double target,
		double minRate,
		double fillFraction
	) {
		double current = concentrationBuffer[cell.Column, cell.Row];
		double remaining = target - current;
		if( remaining <= 0 ) {
			return;
		}

		double amount = Math.Min( Math.Max( remaining * fillFraction, minRate ), remaining );
		concentrationBuffer[cell.Column, cell.Row] = current + amount;
	}

	// Injects a radially-outward velocity, at a fixed speed, into every cell within
	// radius of center whose direction from center falls inside the arc
	// [centerDegrees - width/2, centerDegrees + width/2], where 0 degrees is
	// straight up and angles increase clockwise. Cells outside the arc are left
	// untouched so StepVelocity's own diffuse/project/advect can shape them.
	private static void SeedPumpArc(
		VelocityGrid velocity,
		(int Column, int Row) center,
		int radius,
		double centerDegrees,
		double widthDegrees,
		double speed
	) {
		double halfWidth = widthDegrees / 2.0;
		for( int row = center.Row - radius; row <= center.Row + radius; row++ ) {
			for( int column = center.Column - radius; column <= center.Column + radius; column++ ) {
				double deltaColumn = column - center.Column;
				double deltaRow = row - center.Row;
				double distance = Math.Sqrt( ( deltaColumn * deltaColumn ) + ( deltaRow * deltaRow ) );
				if( distance > radius ) {
					continue;
				}

				double directionColumn;
				double directionRow;
				double angleDegrees;
				if( distance < 1.0 ) {
					// The center cell has no direction of its own - treat it as pointing
					// straight along the arc's center direction.
					angleDegrees = centerDegrees;
					double centerRadians = centerDegrees * Math.PI / 180.0;
					directionColumn = Math.Sin( centerRadians );
					directionRow = -Math.Cos( centerRadians );
				} else {
					// 0 degrees = up (0, -1), increasing clockwise.
					angleDegrees = Math.Atan2( deltaColumn, -deltaRow ) * 180.0 / Math.PI;
					if( angleDegrees < 0 ) {
						angleDegrees += 360.0;
					}
					directionColumn = deltaColumn / distance;
					directionRow = deltaRow / distance;
				}

				if( !IsWithinArc( angleDegrees, centerDegrees, halfWidth ) ) {
					continue;
				}

				velocity.Set( column, row, new Velocity( directionColumn, directionRow ) * speed );
			}
		}
	}

	private static bool IsWithinArc(
		double angleDegrees,
		double centerDegrees,
		double halfWidthDegrees
	) {
		double delta = Math.Abs( angleDegrees - centerDegrees );
		delta = Math.Min( delta, 360.0 - delta );
		return delta <= halfWidthDegrees;
	}
}
