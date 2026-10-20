# Benchmarks

```text
BenchmarkDotNet v0.15.8, Windows 11 (10.0.26100.9448/24H2/2024Update/HudsonValley)
Intel Core i7-9700K CPU 3.60GHz (Coffee Lake), 1 CPU, 8 logical and 8 physical cores
.NET SDK 11.0.100-rc.2.26504.105
  [Host] : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v3
```

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
| Update            | 262.11 us | 9.045 us | 8.461 us |         - |
| ReadPressureFrame |  16.09 us | 0.274 us | 0.257 us |         - |

## LowFidelity.Thermal

| Method              | Mean     | Error   | StdDev  | Allocated |
|---------------------|---------:|--------:|--------:|----------:|
| Update              | 209.4 us | 4.65 us | 4.35 us |       1 B |
| UpdateWithMovingSun | 233.9 us | 6.30 us | 5.89 us |       3 B |
