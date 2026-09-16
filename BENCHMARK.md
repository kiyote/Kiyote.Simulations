# Benchmarks

```
BenchmarkDotNet v0.15.8, Windows 11 (10.0.26100.9106/24H2/2024Update/HudsonValley)
Intel Core i7-9700K CPU 3.60GHz (Coffee Lake), 1 CPU, 8 logical and 8 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
```

# GridFlow
| Method | Mean     | Error   | StdDev  | Allocated |
|------- |---------:|--------:|--------:|----------:|
| Flow   | 257.9 us | 8.34 us | 7.40 us |         - |

# SealedAnalzyer
| Method   | Mean     | Error   | StdDev  | Allocated |
|--------- |---------:|--------:|--------:|----------:|
| IsSealed | 319.4 us | 9.05 us | 8.47 us |         - |
