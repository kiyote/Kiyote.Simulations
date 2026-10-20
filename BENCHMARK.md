## Topology.GridDiffusion

| Method | Mean     | Error    | StdDev   | Allocated |
|--------|---------:|---------:|---------:|----------:|
| Update | 26.77 us | 0.439 us | 0.411 us |         - |

## Topology.GridPressure

| Method | Mean     | Error    | StdDev   | Allocated |
|--------|---------:|---------:|---------:|----------:|
| Update | 26.59 us | 1.231 us | 1.151 us |         - |

## Topology.GridProjection

| Method | Iterations | RetainedEmptyChunks | Mean     | Error    | StdDev   | Allocated |
|--------|-----------:|--------------------:|---------:|---------:|---------:|----------:|
| Update |          1 |                   0 | 100.1 us |  2.09 us |  1.95 us |       1 B |
| Update |          1 |                 128 | 103.1 us |  4.37 us |  4.08 us |       1 B |
| Update |         20 |                   0 | 545.7 us | 33.24 us | 31.09 us |       6 B |
| Update |         20 |                 128 | 593.6 us | 10.66 us |  9.45 us |       5 B |
| Update |         21 |                   0 | 583.5 us |  8.71 us |  7.72 us |       6 B |
| Update |         21 |                 128 | 634.6 us | 12.20 us | 11.41 us |       6 B |

## Topology.GridAdvection

| Method | Mean     | Error    | StdDev   | Allocated |
|--------|---------:|---------:|---------:|----------:|
| Update | 53.21 us | 1.338 us | 1.252 us |         - |

## Topology.GridAirflow

| Method | Iterations | RetainedEmptyChunks | Mean     | Error    | StdDev   | Allocated |
|--------|-----------:|--------------------:|---------:|---------:|---------:|----------:|
| Update |          1 |                   0 | 326.6 us |  8.13 us |  7.20 us |       2 B |
| Update |          1 |                 128 | 327.8 us | 12.63 us | 11.82 us |       3 B |
| Update |         20 |                   0 | 839.4 us | 35.17 us | 31.18 us |       6 B |
| Update |         20 |                 128 | 860.0 us | 15.50 us | 14.50 us |       6 B |
| Update |         21 |                   0 | 806.6 us | 20.99 us | 19.63 us |       6 B |
| Update |         21 |                 128 | 818.9 us | 26.55 us | 24.83 us |       6 B |

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
