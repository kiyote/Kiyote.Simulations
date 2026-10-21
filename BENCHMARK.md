## Topology.GridDiffusion

| Method | Mean     | Error    | StdDev   | Allocated |
|--------|---------:|---------:|---------:|----------:|
| Update | 25.47 us | 0.523 us | 0.464 us |         - |

## Topology.GridPressure

| Method | Mean     | Error    | StdDev   | Allocated |
|--------|---------:|---------:|---------:|----------:|
| Update | 26.23 us | 0.965 us | 0.902 us |         - |

## Topology.GridProjection

| Method | Iterations | RetainedEmptyChunks | Mean     | Error    | StdDev   | Allocated |
|--------|-----------:|--------------------:|---------:|---------:|---------:|----------:|
| Update |          1 |                   0 |  99.36 us |  3.057 us |  2.710 us |       1 B |
| Update |          1 |                 128 | 106.06 us |  2.338 us |  2.187 us |       1 B |
| Update |         20 |                   0 | 538.38 us | 13.808 us | 11.530 us |       6 B |
| Update |         20 |                 128 | 578.04 us | 14.653 us | 12.989 us |       6 B |
| Update |         21 |                   0 | 552.50 us | 16.424 us | 14.559 us |       5 B |
| Update |         21 |                 128 | 616.69 us | 18.641 us | 17.437 us |       5 B |

## Topology.GridAdvection

| Method | Mean     | Error    | StdDev   | Allocated |
|--------|---------:|---------:|---------:|----------:|
| Update | 52.27 us | 1.603 us | 1.421 us |         - |

## Topology.GridAirflow

| Method | Iterations | RetainedEmptyChunks | Mean     | Error    | StdDev   | Allocated |
|--------|-----------:|--------------------:|---------:|---------:|---------:|----------:|
| Update |          1 |                   0 | 320.9 us |  8.68 us |  8.12 us |       3 B |
| Update |          1 |                 128 | 343.4 us | 10.10 us |  9.44 us |       3 B |
| Update |         20 |                   0 | 756.5 us | 28.67 us | 26.82 us |       6 B |
| Update |         20 |                 128 | 805.6 us | 20.38 us | 17.02 us |       6 B |
| Update |         21 |                   0 | 808.6 us | 41.22 us | 38.56 us |       6 B |
| Update |         21 |                 128 | 839.4 us | 38.78 us | 36.28 us |       6 B |

## LowFidelity.Atmosphere

| Method            | Mean      | Error    | StdDev   | Allocated |
|-------------------|----------:|---------:|---------:|----------:|
| Update            | 268.54 us | 7.109 us | 5.936 us |         - |
| ReadPressureFrame |  16.02 us | 0.388 us | 0.363 us |         - |

## LowFidelity.Thermal

| Method              | Mean     | Error   | StdDev  | Allocated |
|---------------------|---------:|--------:|--------:|----------:|
| Update              | 201.5 us | 7.61 us | 7.12 us |       1 B |
| UpdateWithMovingSun | 238.8 us | 6.54 us | 6.11 us |       1 B |

## LowFidelity.AtmosphericThermals

| Method | TicksPerUpdate | Mean       | Error     | StdDev    | Allocated |
|--------|---------------:|-----------:|----------:|----------:|----------:|
| Update |              1 |   810.9 us |  44.08 us |  41.23 us |       6 B |
| Update |              8 | 6,201.4 us | 259.29 us | 216.52 us |      47 B |

## LowFidelity.ConvectionTopologyBenchmarks

| Method          | Sparse | Mean      | Error     | StdDev    | Allocated |
|-----------------|--------|----------:|----------:|----------:|----------:|
| Update          | False  |  68.47 us |  1.492 us |  1.323 us |       1 B |
| RebuildTopology | False  | 129.86 us |  1.655 us |  1.548 us |  127985 B |
| Update          | True   | 153.52 us |  7.095 us |  6.290 us |       1 B |
| RebuildTopology | True   | 834.41 us | 25.802 us | 24.136 us |  217870 B |
