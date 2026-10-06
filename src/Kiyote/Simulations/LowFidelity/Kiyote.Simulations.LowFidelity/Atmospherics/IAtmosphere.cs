using Kiyote.Geometry;
using Kiyote.Geometry.Topology;

namespace Kiyote.Simulations.LowFidelity.Atmospherics;

// Layers are exposed for reading.  Change gas, heat and condensate through the methods.
public interface IAtmosphere : IDisposable {

	IGasRegistry Gases { get; }

	// kPa, result of the last Update.
	IGridLayer<float> Pressure { get; }

	// K
	IGridLayer<float> Temperature { get; }

	// kPa of dynamic pressure, +x = east.
	IGridLayer<float> WindX { get; }

	// kPa of dynamic pressure, +y = south.
	IGridLayer<float> WindY { get; }

	// Total gas lost to space during the last Update.
	float Vented { get; }

	IGridLayer<float> GetGas(
		GasIndex gas
	);

	// Only available for gases with a condensation point.
	IGridLayer<float> GetCondensate(
		GasIndex gas
	);

	Vector GetWind(
		int column,
		int row
	);

	// Returns the number of fixed steps run.
	int Update(
		TimeSpan elapsed
	);

	// Safe to call from one reader thread while another thread calls Update.
	// Returns the most recently published frame, which the simulation will not touch until
	// a later AcquireFrame swaps it out.  Only one frame may be held at a time.
	IAtmosphereFrame AcquireFrame();

	void ReleaseFrame(
		IAtmosphereFrame frame
	);

	// Copies values back into the cells.
	void Commit();

	void InvalidateTopology(
		Rect area
	);

	void InvalidateTopology();

	void AddGas(
		int column,
		int row,
		GasIndex gas,
		float amount,
		float? temperature = null
	);

	float RemoveGas(
		int column,
		int row,
		GasIndex gas,
		float amount
	);

	// Negative removes energy.  Returns the energy actually applied.
	float AddEnergy(
		int column,
		int row,
		float joules
	);

	float GetTotalGas(
		int column,
		int row
	);

	void AddCondensate(
		int column,
		int row,
		GasIndex gas,
		float amount
	);

	float RemoveCondensate(
		int column,
		int row,
		GasIndex gas,
		float amount
	);

	float GetFraction(
		int column,
		int row,
		GasIndex gas
	);

}
