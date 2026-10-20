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
| Update            | 266.94 us | 20.663 us | 19.328 us |         - |
| ReadPressureFrame |  15.72 us |  0.311 us |  0.276 us |         - |

## LowFidelity.Thermal

| Method              | Mean     | Error   | StdDev  | Allocated |
|---------------------|---------:|--------:|--------:|----------:|
| Update              | 198.6 us | 5.35 us | 4.74 us |       1 B |
| UpdateWithMovingSun | 247.1 us | 5.58 us | 4.95 us |       1 B |

## LowFidelity.AtmosphericThermals

| Method | TicksPerUpdate | Mean       | Error     | StdDev    | Allocated |
|--------|---------------:|-----------:|----------:|----------:|----------:|
| Update |              1 |   865.3 us |  14.12 us |  13.21 us |       6 B |
| Update |              8 | 6,357.2 us | 125.35 us | 117.26 us |      47 B |

## LowFidelity.ConvectionTopologyBenchmarks

| Method          | Sparse | Mean      | Error     | StdDev    | Allocated |
|-----------------|--------|----------:|----------:|----------:|----------:|
| Update          | False  |  66.31 us |  3.249 us |  3.039 us |       1 B |
| RebuildTopology | False  | 134.35 us |  3.176 us |  2.971 us |  127985 B |
| Update          | True   | 156.53 us | 10.560 us |  9.878 us |       1 B |
| RebuildTopology | True   | 817.88 us | 28.112 us | 26.296 us |  217870 B |
