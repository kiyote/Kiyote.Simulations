## Topology.GridDiffusion

| Method | Mean     | Error    | StdDev   | Allocated |
|--------|---------:|---------:|---------:|----------:|
| Update | 25.42 us | 0.558 us | 0.522 us |         - |

## Topology.GridPressure

| Method | Mean     | Error    | StdDev   | Allocated |
|--------|---------:|---------:|---------:|----------:|
| Update | 27.34 us | 0.624 us | 0.584 us |         - |

## Topology.GridProjection

| Method | Mean     | Error   | StdDev  | Allocated |
|--------|---------:|--------:|--------:|----------:|
| Update | 555.0 us | 7.73 us | 6.85 us |       5 B |

## Topology.GridAdvection

| Method | Mean     | Error    | StdDev   | Allocated |
|--------|---------:|---------:|---------:|----------:|
| Update | 51.97 us | 1.316 us | 1.166 us |         - |

## Topology.GridAirflow

| Method | Mean     | Error    | StdDev   | Allocated |
|--------|---------:|---------:|---------:|----------:|
| Update | 765.4 us | 23.98 us | 20.03 us |       5 B |

## LowFidelity.Atmosphere

| Method            | Mean      | Error    | StdDev   | Allocated |
|-------------------|----------:|---------:|---------:|----------:|
| Update            | 253.55 us | 11.992 us | 11.217 us |         - |
| ReadPressureFrame |  15.47 us |  0.272 us |  0.254 us |         - |

## LowFidelity.Thermal

| Method              | Mean     | Error   | StdDev  | Allocated |
|---------------------|---------:|--------:|--------:|----------:|
| Update              | 200.6 us | 10.41 us | 9.23 us |       1 B |
| UpdateWithMovingSun | 228.4 us |  8.55 us | 7.99 us |       1 B |

## LowFidelity.AtmosphericThermals

| Method | TicksPerUpdate | Mean       | Error     | StdDev    | Allocated |
|--------|---------------:|-----------:|----------:|----------:|----------:|
| Update |              1 |   788.3 us |  25.95 us |  24.28 us |       6 B |
| Update |              8 | 6,453.5 us | 168.96 us | 158.04 us |      45 B |

## LowFidelity.ConvectionTopologyBenchmarks

| Method          | Sparse | Mean      | Error     | StdDev    | Allocated |
|-----------------|--------|----------:|----------:|----------:|----------:|
| Update          | False  |  63.46 us |  0.678 us |  0.601 us |       1 B |
| RebuildTopology | False  | 122.62 us |  3.173 us |  2.968 us |  127985 B |
| Update          | True   | 147.67 us |  4.233 us |  3.959 us |       1 B |
| RebuildTopology | True   | 872.31 us | 20.559 us | 19.231 us |  217876 B |
