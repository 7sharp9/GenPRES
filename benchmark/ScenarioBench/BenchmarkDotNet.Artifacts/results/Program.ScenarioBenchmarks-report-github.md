```

BenchmarkDotNet v0.14.0, macOS 26.5.1 (25F80) [Darwin 25.5.0]
Apple M2 Max, 1 CPU, 12 logical and 12 physical cores
.NET SDK 10.0.100
  [Host]     : .NET 10.0.0 (10.0.25.52411), Arm64 RyuJIT AdvSIMD DEBUG
  DefaultJob : .NET 10.0.0 (10.0.25.52411), Arm64 RyuJIT AdvSIMD


```
| Method            | Mean     | Error   | StdDev  | Gen0       | Gen1     | Allocated |
|------------------ |---------:|--------:|--------:|-----------:|---------:|----------:|
| SolveAllScenarios | 118.3 ms | 2.19 ms | 3.47 ms | 23800.0000 | 400.0000 | 190.99 MB |
