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
| Update | 1.039 ms | 0.0212 ms | 0.0188 ms |         - |

# GridPressure
| Method | Mean     | Error     | StdDev    | Allocated |
|------- |---------:|----------:|----------:|----------:|
| Update | 1.104 ms | 0.0275 ms | 0.0244 ms |      10 B |

# GridProjection
| Method | Mean     | Error    | StdDev   | Allocated |
|------- |---------:|---------:|---------:|----------:|
| Update | 22.95 ms | 0.375 ms | 0.333 ms |         - |
