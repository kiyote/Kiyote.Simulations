using System.Diagnostics.CodeAnalysis;
using Kiyote.Geometry.Grids;
using Kiyote.Simulations.Grids;

namespace Kiyote.Simulations.Visualizer;

[ExcludeFromCodeCoverage]
internal readonly struct FlowStrategy : IFlowStrategy<byte, int> {

	private readonly double _viscosity;

	public FlowStrategy() : this( 1.0 ) { }

	// viscosity ranges from 0 (no flow) to 1 (fully equalizes in a single step).
	public FlowStrategy(
		double viscosity
	) {
		_viscosity = viscosity;
	}

	public int CalculateTransfer(
		GridCell<byte> source,
		GridCell<byte> destination
	) {
		return (int)Math.Round( ( source.Cell - destination.Cell ) * _viscosity / 2, MidpointRounding.AwayFromZero );
	}

	public int Combine(
		int left,
		int right
	) {
		return left + right;
	}

	public int Negate(
		int value
	) {
		return -value;
	}

	public byte Apply(
		GridCell<byte> cell,
		int delta
	) {
		return (byte)Math.Clamp( cell.Cell + delta, 0, 255 );
	}

}
