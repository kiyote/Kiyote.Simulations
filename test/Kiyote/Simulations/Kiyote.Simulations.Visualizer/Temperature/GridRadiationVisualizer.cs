using Kiyote.Buffers.Numerics;
using Kiyote.Imaging;
using Kiyote.Simulations.Temperature;
using Kiyote.Simulations.Visualizer;

namespace Kiyote.Simulations.Visualizer.Temperature;

internal sealed class GridRadiationVisualizer {

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

	// Each tick represents an hour of simulated time so that radiative cooling,
	// which is slow near room temperature, is visible over a short animation.
	private const double TimeStep = 3_600.0;

	private readonly IAnimationWriter _animWriter;
	private readonly IGridRadiation _gridRadiation;
	private readonly INumericBufferFactory _bufferFactory;
	private readonly INumericBufferOperator _bufferOperation;

	public GridRadiationVisualizer(
		IAnimationWriter animWriter,
		IGridRadiation gridRadiation,
		INumericBufferFactory bufferFactory,
		INumericBufferOperator bufferOperator
	) {
		_animWriter = animWriter;
		_gridRadiation = gridRadiation;
		_bufferFactory = bufferFactory;
		_bufferOperation = bufferOperator;
	}

	public void Execute(
		string outputFolder
	) {
		INumericBuffer<byte> stretched = _bufferFactory.Create<byte>( Size, Size, 0 );
		INumericBuffer<double> buffer = _bufferFactory.Create<double>( Size, Size, VacuumTemperature );
		BufferGrid<double> grid = new BufferGrid<double>( buffer );

		for( int row = SteelMin; row <= SteelMax; row++ ) {
			for( int column = SteelMin; column <= SteelMax; column++ ) {
				buffer[column, row] = StartingTemperature;
			}
		}

		bool[][] mask = new bool[Size][];
		for( int column = 0; column < Size; column++ ) {
			mask[column] = new bool[Size];
		}
		for( int row = SteelMin; row <= SteelMax; row++ ) {
			for( int column = SteelMin; column <= SteelMax; column++ ) {
				mask[column][row] = true;
			}
		}

		double heatCapacity = Density * ( Area * Thickness ) * SpecificHeatCapacity;
		HeatRadiationStrategy radiation = new HeatRadiationStrategy( Emissivity, Area, heatCapacity );
		ExposedToSpaceStrategy exposed = new ExposedToSpaceStrategy( mask );
		BufferSetCellStrategy callback = new BufferSetCellStrategy( buffer );

		IAnimationBuilder builder = _animWriter.StartAnimation( Path.Combine( outputFolder, "gridradiation.gif" ), TimeSpan.FromMilliseconds( 100 ), 0 );
		_bufferOperation.ScaleToRange( buffer, stretched );
		builder.AddFrame( stretched );
		for( int i = 0; i < 100; i++ ) {
			_gridRadiation.Update<double, ExposedToSpaceStrategy, HeatRadiationStrategy, BufferSetCellStrategy>(
				grid,
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
}
