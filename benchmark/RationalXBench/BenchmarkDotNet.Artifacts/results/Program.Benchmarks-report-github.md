```

BenchmarkDotNet v0.14.0, macOS 26.5.1 (25F80) [Darwin 25.5.0]
Apple M2 Max, 1 CPU, 12 logical and 12 physical cores
.NET SDK 10.0.100
  [Host]     : .NET 10.0.0 (10.0.25.52411), Arm64 RyuJIT AdvSIMD DEBUG
  DefaultJob : .NET 10.0.0 (10.0.25.52411), Arm64 RyuJIT AdvSIMD


```
| Method                | Mean     | Error    | StdDev   | Ratio | Gen0    | Allocated | Alloc Ratio |
|---------------------- |---------:|---------:|---------:|------:|--------:|----------:|------------:|
| MathNet_BigRational   | 71.79 μs | 0.066 μs | 0.062 μs |  1.00 | 18.5547 |  156096 B |        1.00 |
| RationalX_CrossReduce | 16.64 μs | 0.017 μs | 0.016 μs |  0.23 |       - |         - |        0.00 |
