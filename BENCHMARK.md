# Benchmark

## GridDiffusion

| Method | Mean     | Error    | StdDev   | Allocated |
|--------|---------:|---------:|---------:|----------:|
| Update | 24.62 us | 0.680 us | 0.636 us |         - |

## GridPressure

| Method | Mean     | Error    | StdDev   | Allocated |
|--------|---------:|---------:|---------:|----------:|
| Update | 25.00 us | 0.434 us | 0.406 us |         - |

## GridProjection

| Method | Iterations | RetainedEmptyChunks | Mean      | Error     | StdDev    | Allocated |
|--------|-----------:|--------------------:|----------:|----------:|----------:|----------:|
| Update |          1 |                   0 |  95.77 us |  3.266 us |  2.895 us |       1 B |
| Update |          1 |                 128 | 104.25 us |  2.140 us |  2.002 us |       1 B |
| Update |         20 |                   0 | 529.92 us | 19.516 us | 18.256 us |       6 B |
| Update |         20 |                 128 | 545.12 us | 10.652 us |  8.317 us |       6 B |
| Update |         21 |                   0 | 532.60 us | 10.769 us | 10.073 us |       6 B |
| Update |         21 |                 128 | 600.13 us | 22.472 us | 21.020 us |       6 B |

## GridAdvection

| Method | Mean     | Error    | StdDev   | Allocated |
|--------|---------:|---------:|---------:|----------:|
| Update | 50.29 us | 1.250 us | 1.169 us |         - |

## GridAirflow

| Method | Iterations | RetainedEmptyChunks | Mean     | Error    | StdDev   | Allocated |
|--------|-----------:|--------------------:|---------:|---------:|---------:|----------:|
| Update |          1 |                   0 | 313.9 us |  7.48 us |  6.63 us |       3 B |
| Update |          1 |                 128 | 330.7 us |  8.37 us |  7.42 us |       3 B |
| Update |         20 |                   0 | 760.9 us | 23.07 us | 21.58 us |       6 B |
| Update |         20 |                 128 | 772.5 us | 11.29 us |  9.43 us |       6 B |
| Update |         21 |                   0 | 775.6 us | 33.66 us | 28.11 us |       6 B |
| Update |         21 |                 128 | 843.5 us | 31.86 us | 29.81 us |       6 B |

## Atmosphere

| Method            | Mean      | Error    | StdDev   | Allocated |
|-------------------|----------:|---------:|---------:|----------:|
| PublishInputs     |  25.81 us | 0.753 us | 0.704 us |         - |
| Update            | 280.96 us | 8.031 us | 7.513 us |       2 B |
| ReadPressureFrame |  16.65 us | 0.295 us | 0.276 us |         - |

## Thermal

| Method              | Mean     | Error   | StdDev  | Allocated |
|---------------------|---------:|--------:|--------:|----------:|
| Update              | 213.2 us | 7.12 us | 6.31 us |       1 B |
| UpdateWithMovingSun | 253.3 us | 4.18 us | 3.91 us |       3 B |

## AtmosphericThermals

| Method | TicksPerUpdate | Mean       | Error     | StdDev    | Allocated |
|--------|---------------:|-----------:|----------:|----------:|----------:|
| Update |              1 |   883.1 us |  24.19 us |  21.44 us |       6 B |
| Update |              8 | 6,437.6 us | 251.83 us | 235.56 us |      45 B |

## ConvectionTopologyBenchmarks

| Method          | Sparse | Mean      | Error    | StdDev   | Allocated |
|-----------------|--------|----------:|---------:|---------:|----------:|
| Update          | False  |  69.14 us | 0.975 us | 0.761 us |       1 B |
| RebuildTopology | False  | 133.57 us | 4.047 us | 3.786 us |  127985 B |
| Update          | True   | 152.68 us | 3.223 us | 2.857 us |       1 B |
| RebuildTopology | True   | 888.53 us | 5.252 us | 4.913 us |  217870 B |
