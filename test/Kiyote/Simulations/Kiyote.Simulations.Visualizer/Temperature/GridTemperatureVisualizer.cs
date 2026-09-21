using Kiyote.Buffers.Numerics;
using Kiyote.Imaging;
using Kiyote.Simulations.Temperature;
using Kiyote.Simulations.Visualizer;

namespace Kiyote.Simulations.Visualizer.Temperature;

internal sealed class GridTemperatureVisualizer {

	public const int Size = 50;
	public const int SteelMin = 1;
	public const int SteelMax = 48;

	private const double StartingTemperature = 21.0;
	private const double VacuumTemperature = -270.45; // 2.7K expressed in Celsius, for visualization contrast only.

	// A cell is treated as a 1m x 1m x 10mm plate of steel.
	private const double Emissivity = 0.85;
	private const double Area = 1.0;
	private const double Density = 7850.0;
	private const double SpecificHeatCapacity = 500.0;
	private const double Thickness = 0.01;
	private const double ThermalConductivity = 50.0;

	// Each tick represents an hour of simulated time so that radiative cooling,
	// which is slow near room temperature, is visible over a short animation.
	private const double TimeStep = 3_600.0;

	private readonly IAnimationWriter _animWriter;
	private readonly IGridTemperature _gridTemperature;
	private readonly INumericBufferFactory _bufferFactory;
	private readonly INumericBufferOperator _bufferOperation;

	public GridTemperatureVisualizer(
		IAnimationWriter animWriter,
		IGridTemperature gridTemperature,
		INumericBufferFactory bufferFactory,
		INumericBufferOperator bufferOperator
	) {
		_animWriter = animWriter;
		_gridTemperature = gridTemperature;
		_bufferFactory = bufferFactory;
		_bufferOperation = bufferOperator;
	}

	public void Execute(
		string outputFolder
	) {
		INumericBuffer<byte> stretched = _bufferFactory.Create<byte>( Size, Size, 0 );
		INumericBuffer<double> buffer = _bufferFactory.Create<double>( Size, Size, VacuumTemperature );
		BufferGrid<double> grid = new BufferGrid<double>( buffer );

		bool[][] mask = BuildShapeMask();
		for( int row = SteelMin; row <= SteelMax; row++ ) {
			for( int column = SteelMin; column <= SteelMax; column++ ) {
				if( mask[column][row] ) {
					buffer[column, row] = StartingTemperature;
				}
			}
		}

		double heatCapacity = Density * ( Area * Thickness ) * SpecificHeatCapacity;
		double diffusivity = ThermalConductivity / ( Density * SpecificHeatCapacity );
		HeatRadiationStrategy radiation = new HeatRadiationStrategy( Emissivity, Area, heatCapacity );
		HeatDiffusionStrategy diffusion = new HeatDiffusionStrategy( diffusivity );
		ExposedToSpaceStrategy exposed = new ExposedToSpaceStrategy( mask );
		SteelPassabilityStrategy passability = new SteelPassabilityStrategy( mask );
		BufferSetCellStrategy callback = new BufferSetCellStrategy( buffer );

		IAnimationBuilder builder = _animWriter.StartAnimation( Path.Combine( outputFolder, "gridtemperature.gif" ), TimeSpan.FromMilliseconds( 100 ), 0 );
		_bufferOperation.ScaleToRange( buffer, stretched );
		builder.AddFrame( stretched );
		for( int i = 0; i < 100; i++ ) {
			_gridTemperature.Update<double, double, HeatDiffusionStrategy, SteelPassabilityStrategy, ExposedToSpaceStrategy, HeatRadiationStrategy, BufferSetCellStrategy>(
				grid,
				diffusion,
				passability,
				exposed,
				radiation,
				callback,
				TimeStep
			);

			_bufferOperation.ScaleToRange( buffer, stretched );
			builder.AddFrame( stretched );
		}
		builder.FinishAnimation();
	}

	/// <summary>
	/// Builds an irregular (starburst-shaped, with a couple of interior cavities)
	/// mask instead of a plain square, so the boundary has both convex points and
	/// concave notches that cool at visibly different rates, alongside interior
	/// holes that radiate from the middle of the block outward as well.
	/// </summary>
	private static bool[][] BuildShapeMask() {
		bool[][] mask = new bool[Size][];
		for( int column = 0; column < Size; column++ ) {
			mask[column] = new bool[Size];
		}

		double centerX = ( SteelMin + SteelMax ) / 2.0;
		double centerY = ( SteelMin + SteelMax ) / 2.0;
		const double baseRadius = 20.0;
		const double lobeAmplitude = 5.0;
		const double lobeFrequency = 5.0;
		const double rippleAmplitude = 2.5;
		const double rippleFrequency = 11.0;

		for( int row = SteelMin; row <= SteelMax; row++ ) {
			for( int column = SteelMin; column <= SteelMax; column++ ) {
				double dx = column - centerX;
				double dy = row - centerY;
				double radius = Math.Sqrt( ( dx * dx ) + ( dy * dy ) );
				double angle = Math.Atan2( dy, dx );

				double shapeRadius = baseRadius
					+ ( lobeAmplitude * Math.Sin( lobeFrequency * angle ) )
					+ ( rippleAmplitude * Math.Cos( rippleFrequency * angle ) );

				mask[column][row] = radius <= shapeRadius;
			}
		}

		CarveCavity( mask, (int) ( centerX - 8 ), (int) ( centerY - 6 ), 4, 4 );
		CarveCavity( mask, (int) ( centerX + 4 ), (int) ( centerY + 3 ), 3, 5 );

		return mask;
	}

	private static void CarveCavity(
		bool[][] mask,
		int left,
		int top,
		int width,
		int height
	) {
		for( int row = top; row < top + height; row++ ) {
			for( int column = left; column < left + width; column++ ) {
				mask[column][row] = false;
			}
		}
	}
}
