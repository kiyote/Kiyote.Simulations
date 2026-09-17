using Kiyote.Buffers.Numerics;
using Kiyote.Imaging;
using Kiyote.Simulations.Grids;
using Kiyote.Simulations.Visualizer.GridDiffusion;
using Kiyote.Simulations.Visualizer.GridFluid;
using Kiyote.Simulations.Visualizer.GridPressure;
namespace Kiyote.Simulations.Visualizer.GridAirflow;

// Demonstrates realistic bulk-airflow re-pressurization: room A starts full of
// air while room B starts near-vacuum, with a closed door between them. A pump
// keeps topping up room A's pressure while the door stays shut, then partway
// through the run the door opens and IGridAirflow couples the resulting
// pressure gradient into a velocity field (via IGridFluid) so air visibly
// rushes from A into B instead of only slowly diffusing across.
internal sealed class GridAirflowVisualizer {

	public const int Size = 45;
	private const int DoorOpenFrame = 30;

	private readonly IAnimationWriter _animWriter;
	private readonly IGridAirflow _gridAirflow;
	private readonly IGridPressure _gridPressure;
	private readonly INumericBufferFactory _bufferFactory;
	private readonly INumericBufferOperator _bufferOperation;

	public GridAirflowVisualizer(
		IAnimationWriter animWriter,
		IGridAirflow gridAirflow,
		IGridPressure gridPressure,
		INumericBufferFactory bufferFactory,
		INumericBufferOperator bufferOperator
	) {
		_animWriter = animWriter;
		_gridAirflow = gridAirflow;
		_gridPressure = gridPressure;
		_bufferFactory = bufferFactory;
		_bufferOperation = bufferOperator;
	}

	private static readonly (int Left, int Top, int Right, int Bottom) _roomA = ( 0, 0, 19, 19 );
	private static readonly (int Left, int Top, int Right, int Bottom) _roomB = ( 25, 0, 44, 19 );
	private static readonly (int Left, int Top, int Right, int Bottom) _corridor = ( 20, 8, 24, 11 );

	private static readonly (int Column, int Row) _pumpCell = ( ( _roomA.Left + _roomA.Right ) / 2, ( _roomA.Top + _roomA.Bottom ) / 2 );
	private static readonly (int Column, int Row) _sensorCell = ( ( _roomB.Left + _roomB.Right ) / 2, ( _roomB.Top + _roomB.Bottom ) / 2 );

	private const double PumpRate = 100.0;
	private const double ForcingGain = 0.02;
	private const double MaxForcingSpeed = 0.5;
	private const double Viscosity = 0.02;
	private const double DiffusionRate = 0.5;

	public void Execute(
		string outputFolder
	) {
		INumericBuffer<byte> stretched = _bufferFactory.Create<byte>( Size, Size, 0 );
		INumericBuffer<double> pressureBuffer = _bufferFactory.Create<double>( Size, Size, 0 );
		BufferGrid<double> pressure = new BufferGrid<double>( pressureBuffer );
		BufferSetCellStrategy pressureSetCell = new BufferSetCellStrategy( pressureBuffer );
		GasSpecies<double, BufferSetCellStrategy> air = new( pressure, pressureSetCell );

		VelocityGrid velocity = new VelocityGrid( Size, Size );
		IdentityVelocityAccessor velocityAccessor = new IdentityVelocityAccessor();
		VelocitySetCellStrategy velocitySetCell = new VelocitySetCellStrategy( velocity );
		VelocityDiffusionStrategy velocityDiffusion = new VelocityDiffusionStrategy( Viscosity );
		BilinearSampler sampler = new BilinearSampler();
		AirflowForcingStrategy forcing = new AirflowForcingStrategy( ForcingGain, MaxForcingSpeed );
		PressureDiffusionStrategy diffusion = new PressureDiffusionStrategy( DiffusionRate );

		DoorState doorState = new DoorState { IsOpen = false };
		AirflowBoundaryStrategy boundary = new AirflowBoundaryStrategy( _roomA, _roomB, _corridor, doorState );
		AirflowPassabilityAdapter passability = new AirflowPassabilityAdapter( boundary );

		List<PumpStrategy> pumps = [ new PumpStrategy( _pumpCell.Column, _pumpCell.Row, PumpRate ) ];
		List<GasSpecies<double, BufferSetCellStrategy>> gases = [ air ];

		IAnimationBuilder builder = _animWriter.StartAnimation( Path.Combine( outputFolder, "gridairflow.apng" ), TimeSpan.FromMilliseconds( 100 ), 0 );
		_bufferOperation.ScaleToRange( pressureBuffer, stretched );
		builder.AddFrame( stretched );

		for( int i = 0; i < 150; i++ ) {
			if( i == DoorOpenFrame ) {
				doorState.IsOpen = true;
			}

			_gridPressure.Inject<double, double, PumpStrategy, AirflowPassabilityAdapter, BufferSetCellStrategy>(
				pressure,
				air,
				pumps,
				passability,
				1.0
			);

			_gridPressure.Diffuse<double, double, PressureDiffusionStrategy, AirflowPassabilityAdapter, BufferSetCellStrategy>(
				pressure,
				gases,
				diffusion,
				passability,
				1.0
			);

			_gridAirflow.Advance<Velocity, IdentityVelocityAccessor, AirflowForcingStrategy, VelocityDiffusionStrategy, double, BilinearSampler, AirflowBoundaryStrategy, VelocitySetCellStrategy, BufferSetCellStrategy>(
				velocity,
				velocityAccessor,
				pressure,
				forcing,
				velocityDiffusion,
				gases,
				sampler,
				boundary,
				velocitySetCell,
				1.0
			);

			_bufferOperation.ScaleToRange( pressureBuffer, stretched );
			builder.AddFrame( stretched );
		}
		builder.FinishAnimation();

		ReportSensor( pressureBuffer );
	}

	private static void ReportSensor(
		INumericBuffer<double> pressureBuffer
	) {
		double roomAPressure = pressureBuffer[_pumpCell.Column, _pumpCell.Row];
		double roomBPressure = pressureBuffer[_sensorCell.Column, _sensorCell.Row];

		Console.WriteLine( $"Sensor RoomA[{_pumpCell.Column},{_pumpCell.Row}]={roomAPressure:F2}, RoomB[{_sensorCell.Column},{_sensorCell.Row}]={roomBPressure:F2}" );
	}

}
