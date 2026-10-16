using Kiyote.Buffers.ChunkedGrids;

namespace Kiyote.Simulations.LowFidelity.Atmospherics;

// Published copies of the atmosphere for one reader thread (e.g. rendering) while another thread
// calls IAtmosphere.Update. Each quantity is its own TripleBufferedChunkedGrid<float>, published
// at the end of every Update: acquiring a lease never blocks the simulation, and the simulation
// never writes a leased buffer.
//
// Quantities are leased independently, so two leases may come from different Updates.
// Only one thread may acquire leases; dispose each lease before acquiring the same quantity
// again, treat leased buffers as read-only and do not use a lease after disposing it.
// When the topology changes the atmosphere publishes into new grids; leases on the previous
// grids stay valid until disposed.
public interface IAtmosphereFrames {

	IGasRegistry Gases { get; }

	// Total fixed steps run when the most recent frames were published.
	long StepCount { get; }

	// Total gas lost to space during the Update that published the most recent frames.
	float Vented { get; }

	// kPa
	TripleBufferedLease<float> AcquirePressure();

	// K
	TripleBufferedLease<float> AcquireTemperature();

	// kPa of dynamic pressure, +x = east.
	TripleBufferedLease<float> AcquireWindX();

	// kPa of dynamic pressure, +y = south.
	TripleBufferedLease<float> AcquireWindY();

	TripleBufferedLease<float> AcquireGas(
		GasIndex gas
	);

	// Only available for gases with a condensation point.
	TripleBufferedLease<float> AcquireCondensate(
		GasIndex gas
	);

}
