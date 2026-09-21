using System.Diagnostics.CodeAnalysis;
using Kiyote.Simulations.Grids;

namespace Kiyote.Simulations.Visualizer.GridAirflow;

// Tracks whether the doorway connecting the two rooms is currently open.
// A simple mutable class so the boundary strategy (a readonly struct) can
// still observe state changes the visualizer makes mid-run.
[ExcludeFromCodeCoverage]
internal sealed class DoorState {

	public bool IsOpen { get; set; }

}
