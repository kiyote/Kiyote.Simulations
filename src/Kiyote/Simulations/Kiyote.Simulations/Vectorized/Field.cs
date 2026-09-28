using Kiyote.Geometry.Grids.Connectivity;

namespace Kiyote.Simulations.Vectorized;

// A dense float field laid out to match one GridTopology: Values.Length == CellCount,
// indexed by the topology's combined-buffer indices. Only valid for the topology it
// was created with; the caller is responsible for recompiling when topology changes.
public sealed class Field<TCell> {

	private readonly float[] _values;

	public Field(
		GridTopology<TCell> topology
	) {
		Topology = topology;
		_values = new float[topology.CellCount];
	}

	public GridTopology<TCell> Topology { get; }

	public Span<float> Values => _values;

}
