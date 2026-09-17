using Kiyote.Buffers.Numerics;
using Kiyote.Imaging;
using Kiyote.Simulations.Grids;
using Kiyote.Simulations.Visualizer.GridDiffusion;

namespace Kiyote.Simulations.Visualizer.GridPressure;

// Demonstrates two independent gas species (oxygen, nitrogen) diffusing across
// a shared room while pumps inject each gas, aiming for a breathable mix of
// roughly 78% nitrogen / 21% oxygen partial pressure. A sensor on the far
// right wall reports the resulting mix once the run completes. Each frame
// visualizes the combined (total) partial pressure.
internal sealed class GridPressureVisualizer {

	public const int Size = 100;
	private const int SubStepsPerFrame = 10;

	private readonly IAnimationWriter _animWriter;
	private readonly IGridPressure _gridPressure;
	private readonly INumericBufferFactory _bufferFactory;
	private readonly INumericBufferOperator _bufferOperation;

	public GridPressureVisualizer(
		IAnimationWriter animWriter,
		IGridPressure gridPressure,
		INumericBufferFactory bufferFactory,
		INumericBufferOperator bufferOperator
	) {
		_animWriter = animWriter;
		_gridPressure = gridPressure;
		_bufferFactory = bufferFactory;
		_bufferOperation = bufferOperator;
	}

	public void Execute(
		string outputFolder
	) {
		INumericBuffer<byte> stretched = _bufferFactory.Create<byte>( Size, Size, 0 );
		INumericBuffer<double> total = _bufferFactory.Create<double>( Size, Size, 0 );

		INumericBuffer<double> oxygenBuffer = _bufferFactory.Create<double>( Size, Size, 0 );
		INumericBuffer<double> nitrogenBuffer = _bufferFactory.Create<double>( Size, Size, 0 );
		BufferGrid<double> oxygenGrid = new BufferGrid<double>( oxygenBuffer );
		BufferGrid<double> nitrogenGrid = new BufferGrid<double>( nitrogenBuffer );

		GasSpecies<double, BufferSetCellStrategy> oxygen = new(
			oxygenGrid,
			new BufferSetCellStrategy( oxygenBuffer )
		);
		GasSpecies<double, BufferSetCellStrategy> nitrogen = new(
			nitrogenGrid,
			new BufferSetCellStrategy( nitrogenBuffer )
		);
		List<GasSpecies<double, BufferSetCellStrategy>> gases = [ oxygen, nitrogen ];

		PressureDiffusionStrategy diffusion = new( 1.0 );
		AlwaysPassableStrategy passability = new();

		// Target a breathable mix of ~78% nitrogen / 21% oxygen by injecting
		// each gas in that same ratio; the pumps don't stop once that ratio is
		// reached, they just keep adding gas at a fixed rate for this demo.
		int sensorColumn = Size - 1;
		int sensorRow = Size / 2;
		List<PumpStrategy> oxygenPumps = [ new PumpStrategy( 20, 49, 21.0 ) ];
		List<PumpStrategy> nitrogenPumps = [ new PumpStrategy( 20, 51, 78.0 ) ];

		IAnimationBuilder builder = _animWriter.StartAnimation( Path.Combine( outputFolder, "gridpressure.gif" ), TimeSpan.FromMilliseconds( 100 ), 0 );
		UpdateTotal( oxygenBuffer, nitrogenBuffer, total );
		_bufferOperation.ScaleToRange( total, stretched );
		builder.AddFrame( stretched );

		for( int i = 0; i < 200; i++ ) {
			for( int sub = 0; sub < SubStepsPerFrame; sub++ ) {
				_gridPressure.Inject<double, double, PumpStrategy, AlwaysPassableStrategy, BufferSetCellStrategy>(
					oxygenGrid,
					oxygen,
					oxygenPumps,
					passability,
					1.0
				);
				_gridPressure.Inject<double, double, PumpStrategy, AlwaysPassableStrategy, BufferSetCellStrategy>(
					nitrogenGrid,
					nitrogen,
					nitrogenPumps,
					passability,
					1.0
				);

				_gridPressure.Diffuse<double, double, PressureDiffusionStrategy, AlwaysPassableStrategy, BufferSetCellStrategy>(
					oxygenGrid,
					gases,
					diffusion,
					passability,
					1.0
				);
			}

			UpdateTotal( oxygenBuffer, nitrogenBuffer, total );
			_bufferOperation.ScaleToRange( total, stretched );
			builder.AddFrame( stretched );
		}
		builder.FinishAnimation();

		ReportSensor( oxygenBuffer, nitrogenBuffer, sensorColumn, sensorRow );
	}

	private static void ReportSensor(
		INumericBuffer<double> oxygenBuffer,
		INumericBuffer<double> nitrogenBuffer,
		int column,
		int row
	) {
		double oxygen = oxygenBuffer[column, row];
		double nitrogen = nitrogenBuffer[column, row];
		double total = oxygen + nitrogen;
		double oxygenPercent = total == 0.0 ? 0.0 : oxygen / total * 100.0;
		double nitrogenPercent = total == 0.0 ? 0.0 : nitrogen / total * 100.0;

		Console.WriteLine( $"Sensor[{column},{row}]: O2={oxygen:F2} ({oxygenPercent:F1}%), N2={nitrogen:F2} ({nitrogenPercent:F1}%), total={total:F2}" );
	}

	private static void UpdateTotal(
		INumericBuffer<double> oxygenBuffer,
		INumericBuffer<double> nitrogenBuffer,
		INumericBuffer<double> total
	) {
		for( int row = 0; row < Size; row++ ) {
			for( int column = 0; column < Size; column++ ) {
				total[column, row] = oxygenBuffer[column, row] + nitrogenBuffer[column, row];
			}
		}
	}
}
