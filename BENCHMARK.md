# Benchmarks

```
BenchmarkDotNet v0.15.8, Windows 11 (10.0.26100.9106/24H2/2024Update/HudsonValley)
Intel Core i7-9700K CPU 3.60GHz (Coffee Lake), 1 CPU, 8 logical and 8 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
```

# GridDiffusion
| Method | Mean     | Error     | StdDev    | Allocated |
|------- |---------:|----------:|----------:|----------:|
| Update | 1.054 ms | 0.0302 ms | 0.0282 ms |         - |

# Vectorized.GridDiffusion
| Method | Mean     | Error    | StdDev   | Allocated |
|------- |---------:|---------:|---------:|----------:|
| Update | 26.44 us | 0.194 us | 0.172 us |         - |

# GridPressure
| Method | Mean     | Error     | StdDev    | Allocated |
|------- |---------:|----------:|----------:|----------:|
| Update | 1.151 ms | 0.0281 ms | 0.0234 ms |         - |

# Vectorized.GridPressure
| Method | Mean     | Error    | StdDev   | Allocated |
|------- |---------:|---------:|---------:|----------:|
| Update | 27.36 us | 0.558 us | 0.522 us |         - |

# GridProjection
| Method | Mean     | Error    | StdDev   | Allocated |
|------- |---------:|---------:|---------:|----------:|
| Update | 17.00 ms | 0.615 ms | 0.575 ms |  42.21 KB |

# Vectorized.GridProjection
| Method | Mean     | Error     | StdDev    | Allocated |
|------- |---------:|----------:|----------:|----------:|
| Update | 1.019 ms | 0.0520 ms | 0.0486 ms |       9 B |
