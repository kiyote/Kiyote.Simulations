# Benchmarks

```
BenchmarkDotNet v0.15.8, Windows 11 (10.0.26100.9106/24H2/2024Update/HudsonValley)
Intel Core i7-9700K CPU 3.60GHz (Coffee Lake), 1 CPU, 8 logical and 8 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
```

# SealedAnalyzer
| Method   | Mean     | Error   | StdDev  | Allocated |
|--------- |---------:|--------:|--------:|----------:|
| IsSealed | 324.4 us | 7.77 us | 7.26 us |         - |

# GridDiffusion
| Method | Mean     | Error   | StdDev  | Allocated |
|------- |---------:|--------:|--------:|----------:|
| Flow   | 476.7 us | 9.76 us | 9.13 us |       5 B |

# GridFluid
| Method       | Mean       | Error    | StdDev   | Allocated |
|------------- |-----------:|---------:|---------:|----------:|
| StepVelocity | 4,290.9 us | 64.60 us | 57.27 us |         - |
| AdvectGases  |   202.1 us |  2.03 us |  1.80 us |       1 B |

# GridPressure
| Method  | Mean      | Error    | StdDev   | Allocated |
|-------- |----------:|---------:|---------:|----------:|
| Diffuse | 586.30 us | 5.657 us | 4.724 us |       6 B |
| Inject  |  53.93 us | 1.373 us | 1.217 us |         - |

# GridAirflow
| Method               | Mean       | Error     | StdDev    | Allocated |
|--------------------- |-----------:|----------:|----------:|----------:|
| ApplyPressureForcing |   342.4 us |   6.40 us |   5.34 us |         - |
| Advance              | 5,049.3 us | 122.85 us | 114.91 us |      26 B |
