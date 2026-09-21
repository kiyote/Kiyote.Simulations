using System.Diagnostics.CodeAnalysis;
using Kiyote.Geometry.Grids;
using Kiyote.Simulations.Temperature;

namespace Kiyote.Simulations.Visualizer.Temperature;

[ExcludeFromCodeCoverage]
internal readonly struct HeatRadiationStrategy : IRadiativeLossStrategy<double> {

	private const double StefanBoltzmann = 5.670374419e-8;
	private const double AmbientKelvin = 2.7;

	private readonly double _emissivity;
	private readonly double _area;
	private readonly double _heatCapacity;

	public HeatRadiationStrategy(
		double emissivity,
		double area,
		double heatCapacity
	) {
		_emissivity = emissivity;
		_area = area;
		_heatCapacity = heatCapacity;
	}

	public double Apply(
		GridCell<double> cell,
		double timeStep
	) {
		double kelvin = cell.Cell + 273.15;
		double power = StefanBoltzmann * _emissivity * _area * ( Math.Pow( kelvin, 4 ) - Math.Pow( AmbientKelvin, 4 ) );
		double energyLost = power * timeStep;
		double deltaKelvin = energyLost / _heatCapacity;
		double updatedKelvin = Math.Max( AmbientKelvin, kelvin - deltaKelvin );
		return updatedKelvin - 273.15;
	}

}
