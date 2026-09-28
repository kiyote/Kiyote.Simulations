using Kiyote.Geometry.Grids;
using Kiyote.Geometry.Grids.Connectivity;

namespace Kiyote.Simulations.Vectorized;

public sealed class FieldCompiler : IFieldCompiler {

	Field<TCell> IFieldCompiler.Compile<TCell, TSelector>(
		GridTopology<TCell> topology,
		TSelector selector
	) {
		Field<TCell> field = new Field<TCell>( topology );
		Span<float> values = field.Values;
		foreach( TopologyLeaf<TCell> leaf in topology.Leaves ) {
			IGrid<TCell> grid = leaf.Grid;
			for( int row = 0; row < leaf.Height; row++ ) {
				int index = leaf.Offset + ( row * leaf.Width );
				for( int column = 0; column < leaf.Width; column++ ) {
					values[index + column] = selector.GetValue( grid[grid.Column + column, grid.Row + row]! );
				}
			}
		}
		return field;
	}

	void IFieldCompiler.Decompile<TCell, TSelector>(
		Field<TCell> field,
		TSelector selector
	) {
		ReadOnlySpan<float> values = field.Values;
		foreach( TopologyLeaf<TCell> leaf in field.Topology.Leaves ) {
			if( leaf.Grid is not IMutableGrid<TCell> grid ) {
				throw new InvalidOperationException( "Every leaf must be an IMutableGrid<TCell> to be decompiled." );
			}
			IGrid<TCell> bounds = grid;
			for( int row = 0; row < leaf.Height; row++ ) {
				int index = leaf.Offset + ( row * leaf.Width );
				for( int column = 0; column < leaf.Width; column++ ) {
					int cellColumn = bounds.Column + column;
					int cellRow = bounds.Row + row;
					grid[cellColumn, cellRow] = selector.SetValue( bounds[cellColumn, cellRow]!, values[index + column] );
				}
			}
		}
	}

}
