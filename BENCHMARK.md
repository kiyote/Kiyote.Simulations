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

# Topology.GridDiffusion
| Method | Mean     | Error   | StdDev  | Allocated |
|------- |---------:|--------:|--------:|----------:|
| Update | 25.04 us | 1.397 us | 1.306 us |         - |

# GridPressure
| Method | Mean     | Error     | StdDev    | Allocated |
|------- |---------:|----------:|----------:|----------:|
| Update | 1.151 ms | 0.0281 ms | 0.0234 ms |         - |

# Vectorized.GridPressure
| Method | Mean     | Error    | StdDev   | Allocated |
|------- |---------:|---------:|---------:|----------:|
| Update | 27.36 us | 0.558 us | 0.522 us |         - |

# Topology.GridPressure
| Method | Mean     | Error    | StdDev   | Allocated |
|------- |---------:|---------:|---------:|----------:|
| Update | 23.68 us | 0.390 us | 0.346 us |         - |

# GridProjection
| Method | Mean     | Error    | StdDev   | Allocated |
|------- |---------:|---------:|---------:|----------:|
| Update | 16.34 ms | 0.226 ms | 0.211 ms |         - |

# Vectorized.GridProjection
| Method | Mean     | Error     | StdDev    | Allocated |
|------- |---------:|----------:|----------:|----------:|
| Update | 1.063 ms | 0.0336 ms | 0.0314 ms |       9 B |

# Topology.GridProjection
| Method | Mean     | Error   | StdDev  | Allocated |
|------- |---------:|--------:|--------:|----------:|
| Update | 573.4 us | 5.21 us | 4.62 us |       6 B |

# GridAdvection
| Method | Mean     | Error   | StdDev  | Allocated |
|------- |---------:|--------:|--------:|----------:|
| Update | 339.9 us | 7.92 us | 7.41 us |         - |

# Vectorized.GridAdvection
| Method | Mean     | Error   | StdDev  | Allocated |
|------- |---------:|--------:|--------:|----------:|
| Update | 135.1 us | 1.95 us | 1.63 us |       1 B |

# Topology.GridAdvection
| Method | Mean     | Error   | StdDev  | Allocated |
|------- |---------:|--------:|--------:|----------:|
| Update | 57.24 us | 2.381 us | 2.227 us |         - |

# GridAirflow
| Method | Mean     | Error    | StdDev   | Allocated |
|------- |---------:|---------:|---------:|----------:|
| Update | 20.89 ms | 0.459 ms | 0.384 ms |         - |

# Vectorized.GridAirflow
| Method | Mean     | Error     | StdDev    | Allocated |
|------- |---------:|----------:|----------:|----------:|
| Update | 1.425 ms | 0.0316 ms | 0.0296 ms |      10 B |

# Topology.GridAirflow
| Method | Mean     | Error     | StdDev    | Allocated |
|------- |---------:|----------:|----------:|----------:|
| Update | 892.5 us | 36.53 us | 34.17 us |       6 B |

# LowFidelity.Atmosphere
| Method | Mean     | Error   | StdDev  | Allocated |
|------- |---------:|--------:|--------:|----------:|
| Update | 246.5 us | 4.21 us | 3.94 us |         - |

# LowFidelity.Thermal
| Method              | Mean     | Error   | StdDev  | Allocated |
|-------------------- |---------:|--------:|--------:|----------:|
| Update              | 196.5 us | 9.01 us | 7.99 us |         - |
| UpdateWithMovingSun | 223.9 us | 6.82 us | 6.38 us |       1 B |
