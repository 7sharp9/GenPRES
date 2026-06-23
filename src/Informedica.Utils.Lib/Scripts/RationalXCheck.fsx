#I __SOURCE_DIRECTORY__
#r "nuget: MathNet.Numerics.FSharp, 5.0.0"
#r "../bin/Debug/net10.0/Informedica.Utils.Lib.dll"

// Correctness gate: RationalX (via the BigRational alias) must agree with
// MathNet's BigRational on arithmetic, comparison, and set-distinct behavior.

open Informedica.Utils.Lib.BCL

type RX = Informedica.Utils.Lib.BCL.BigRational      // = RationalX
type MB = MathNet.Numerics.BigRational

let rnd = System.Random 1234

// random fractions with denominators that are products of small primes,
// mirroring GenPRES base-unit magnitudes, plus some larger ones
let denoms = [| 1L; 2L; 3L; 5L; 7L; 10L; 1000L; 86400L; 86400000L; 100000000L |]

let randPair () =
    let n = int64 (rnd.Next(-100000, 100000))
    let d = denoms[rnd.Next(denoms.Length)] * int64 (rnd.Next(1, 1000))
    n, d

let toRX (n: int64, d: int64) = RX.FromInt64Fraction(n, d)
let toMB (n: int64, d: int64) = MB.FromBigIntFraction(bigint n, bigint d)

let close (a: float) (b: float) =
    let scale = (abs a + abs b + 1.0)
    abs (a - b) <= 1e-9 * scale

let mutable failures = 0
let report name cond detail =
    if not cond then
        failures <- failures + 1
        printfn "FAIL %s: %s" name detail

// 1. arithmetic agreement (+ - * /) via ToDouble
for _ in 1 .. 20000 do
    let p1 = randPair ()
    let p2 = randPair ()
    let x, y = toRX p1, toRX p2
    let a, b = toMB p1, toMB p2
    report "add" (close (RX.ToDouble(x + y)) (MB.ToDouble(a + b))) $"{p1} + {p2}"
    report "sub" (close (RX.ToDouble(x - y)) (MB.ToDouble(a - b))) $"{p1} - {p2}"
    report "mul" (close (RX.ToDouble(x * y)) (MB.ToDouble(a * b))) $"{p1} * {p2}"
    if snd p2 <> 0L && fst p2 <> 0L then
        report "div" (close (RX.ToDouble(x / y)) (MB.ToDouble(a / b))) $"{p1} / {p2}"
    // comparison sign must match
    report "cmp" (sign (compare x y) = sign (compare a b)) $"cmp {p1} {p2}"

// 2. spill correctness: force overflow of int64 then narrow back
let big = RX.FromBigInt(bigint System.Int64.MaxValue)
report "spill-mul-eq" (RX.ToDouble(big * big) |> close <| MB.ToDouble(MB.FromBigInt(bigint System.Int64.MaxValue) * MB.FromBigInt(bigint System.Int64.MaxValue))) "maxint^2"
report "spill-narrow" ((big * big) / big = big) "(max^2)/max = max (re-narrows to SX)"

// 3. equality/hash consistency across tiers: a value built two ways must dedupe
let half1 = RX.FromInt64Fraction(1L, 2L)
let half2 = RX.FromInt64Fraction(50L, 100L)         // reduces to 1/2
let half3 = RX.FromBigInt(1I) / RX.FromBigInt(2I)
report "eq-reduce" (half1 = half2 && half2 = half3) "1/2 built three ways equal"
report "hash-reduce" (half1.GetHashCode() = half2.GetHashCode() && half2.GetHashCode() = half3.GetHashCode()) "hash equal"
let distinctCount = [| half1; half2; half3; RX.One; RX.FromInt 1 |] |> Array.distinct |> Array.length
report "distinct" (distinctCount = 2) $"expected 2 distinct (1/2 and 1), got {distinctCount}"

// 4. default value behaves as zero (struct zero-init = SX(0,0))
let dflt = Unchecked.defaultof<RX>
report "default-zero-eq" (dflt = RX.Zero) "default = 0"
report "default-zero-cmp" (compare dflt (RX.FromInt 1) < 0) "default < 1"

// 5. round trips used by the codebase
report "toInt32" (RX.ToInt32(RX.FromInt 42) = 42) "ToInt32 42"
report "toBigInt-floor" (RX.ToBigInt(RX.FromInt64Fraction(-3L, 2L)) = MB.ToBigInt(MB.FromBigIntFraction(-3I, 2I))) "ToBigInt floor -3/2"
report "parse" (RX.Parse "3/4" = RX.FromInt64Fraction(3L, 4L)) "parse 3/4"
report "num/den" ((RX.FromInt64Fraction(6L, 8L)).Numerator = 3I && (RX.FromInt64Fraction(6L, 8L)).Denominator = 4I) "6/8 -> 3/4"

if failures = 0 then printfn "ALL CHECKS PASSED" else printfn "TOTAL FAILURES: %d" failures
