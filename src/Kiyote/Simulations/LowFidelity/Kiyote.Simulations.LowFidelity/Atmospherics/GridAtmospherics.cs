using System;
using System.Collections.Generic;
using System.Text;
using Kiyote.Geometry.Grids;
using Kiyote.Geometry.Grids.Connectivity;

namespace Kiyote.Simulations.LowFidelity.Atmospherics;

public class GridAtmospherics<TCell> : IGridAtmospherics<TCell> {

	private static readonly Vector[] _directions =
	[
			new Vector(0f, 1f),						// 0: North
            new Vector(0.7071068f, 0.7071068f),		// 1: North-East
            new Vector(1f, 0f),						// 2: East
            new Vector(0.7071068f, -0.7071068f),	// 3: South-East
            new Vector(0f, -1f),					// 4: South
            new Vector(-0.7071068f, -0.7071068f),	// 5: South-West
            new Vector(-1f, 0f),					// 6: West
            new Vector(-0.7071068f, 0.7071068f)		// 7: North-West
	];

	void IGridAtmospherics<TCell>.Update(
		IGrid<TCell> grid,
		IConnectivityGrid<TCell> connectivity,
		IGrid<CellAtmosphere> input,
		IGrid<CellAtmosphere> output
	) {
		throw new NotImplementedException();
	}
}
