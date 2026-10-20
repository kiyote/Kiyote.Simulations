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
| Update            | 249.14 us | 9.515 us | 8.900 us |         - |
| ReadPressureFrame |  15.19 us | 0.418 us | 0.391 us |         - |

## LowFidelity.Thermal

| Method              | Mean     | Error   | StdDev  | Allocated |
|---------------------|---------:|--------:|--------:|----------:|
| Update              | 199.7 us | 8.13 us | 7.21 us |       1 B |
| UpdateWithMovingSun | 234.9 us | 5.90 us | 5.52 us |       1 B |

## LowFidelity.AtmosphericThermals

| Method | TicksPerUpdate | Mean     | Error     | StdDev    | Allocated |
|--------|---------------:|---------:|----------:|----------:|----------:|
| Update |              1 | 1.180 ms | 0.0543 ms | 0.0482 ms |         - |
| Update |              8 | 9.511 ms | 0.2705 ms | 0.2530 ms |      62 B |
