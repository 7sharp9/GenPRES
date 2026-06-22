```

BenchmarkDotNet v0.14.0, macOS 26.5.1 (25F80) [Darwin 25.5.0]
Apple M2 Max, 1 CPU, 12 logical and 12 physical cores
.NET SDK 10.0.100
  [Host]     : .NET 10.0.0 (10.0.25.52411), Arm64 RyuJIT AdvSIMD DEBUG
  DefaultJob : .NET 10.0.0 (10.0.25.52411), Arm64 RyuJIT AdvSIMD


```
| Method               | Mean     | Error    | StdDev   | Gen0      | Gen1      | Gen2      | Allocated |
|--------------------- |---------:|---------:|---------:|----------:|----------:|----------:|----------:|
| Mul_mgPerMl_x_mL_400 | 20.09 ms | 0.394 ms | 0.369 ms |  718.7500 |  718.7500 |  718.7500 |   21.1 MB |
| Add_times_600        | 39.44 ms | 0.171 ms | 0.160 ms | 3230.7692 | 1923.0769 | 1923.0769 |     49 MB |
