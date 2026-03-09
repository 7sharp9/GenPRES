/// Prototype script for issue #55: exploring the removal of redundant `and` keywords
/// and a potential partial split to a Types module.
///
/// Context: ValueUnit.fs uses `namespace rec Informedica.GenUnits.Lib` and defines
/// all sub-unit types (CountUnit, MassUnit, etc.) with `and`. Since `namespace rec`
/// already provides full forward-reference capability, the `and` on sub-types is
/// redundant — they don't reference `Unit` back.
///
/// This script demonstrates two approaches:
///   1. Minimal fix: change `and XxxUnit` → `type XxxUnit` (zero behaviour change).
///   2. Types module split: types in a separate module, operators as `let inline` functions.
///
/// Run from this directory:
///   dotnet fsi TypesSplit.fsx
///
/// This script is self-contained — it does NOT load the existing ValueUnit.fs.
/// It demonstrates the type-reorganization concept in isolation.
#I __SOURCE_DIRECTORY__
#r "nuget: MathNet.Numerics.FSharp"

open MathNet.Numerics


// ============================================================
// Approach 1: Minimal fix — remove `and`, rely on `module rec`
// ============================================================
//
// In the source file (ValueUnit.fs), `namespace rec` handles all forward
// references. Here we use `module rec` (the FSI equivalent) to verify the
// same effect: sub-types can be plain `type` declarations without `and`.
//
// Key insight: `Unit` is self-recursive only via `CombiUnit of Unit * ...`,
// which is handled by `module rec`. Sub-types like `CountUnit` and `MassUnit`
// contain only `BigRational` — they never reference `Unit` themselves.
// Therefore `and` is purely cosmetic for them.

module rec MinimalFix =

    // Unit is self-recursive (CombiUnit) and references sub-types.
    // With module rec, both are fine.
    type Unit =
        | NoUnit
        | ZeroUnit
        | CombiUnit of Unit * Operator * Unit   // self-recursive: OK via module rec
        | Count of CountUnit                     // forward ref: OK via module rec
        | Mass of MassUnit

    // These are plain types — no `and` required.
    type CountUnit = Times of BigRational

    type MassUnit =
        | KiloGram of BigRational
        | Gram of BigRational
        | MilliGram of BigRational

    type Operator =
        | OpTimes
        | OpPer
        | OpPlus
        | OpMinus

    // ValueUnit also becomes a plain `type` (was already not using `and` in source).
    type ValueUnit = ValueUnit of BigRational [] * Unit


module MinimalFixVerification =
    open MinimalFix

    let u1 = Mass (KiloGram 1N)
    let u2 = Count (Times 1N)
    let u3 = CombiUnit (u1, Operator.OpPer, u2)  // Unit * Operator * Unit — recursive

    printfn "Approach 1 (minimal fix) compiles and works:"
    printfn "  u1 = %A" u1
    printfn "  u2 = %A" u2
    printfn "  u3 = %A" u3
    let vu = ValueUnit([| 500N |], u1)
    printfn "  vu = %A" vu


// ============================================================
// Approach 2: Types module split with `let inline` operators
// ============================================================
//
// If we want to move type definitions into a separate `Types.fs` file,
// the main constraint is the 10 operator overloads defined via
//   `type ValueUnit with static member (*)(vu1, vu2) = ...`
// in ValueUnit.fs.
//
// F# intrinsic type extensions (same file) add members to the original type.
// Optional type extensions (different file) become extension methods; for
// custom operators this can cause resolution issues.
//
// Solution: convert operators to `let inline` functions in a module.
// This is safe, idiomatic F#, and enables Types.fs / ValueUnit.fs split.

module rec TypesSplit =

    // Types module (would live in Types.fs)
    // ─────────────────────────────────────
    type Unit =
        | NoUnit
        | ZeroUnit
        | CombiUnit of Unit * Operator * Unit
        | Count of CountUnit
        | Mass of MassUnit

    type CountUnit = Times of BigRational

    type MassUnit =
        | KiloGram of BigRational
        | Gram of BigRational
        | MilliGram of BigRational

    type Operator =
        | OpTimes
        | OpPer
        | OpPlus
        | OpMinus

    // Bare ValueUnit type — only the data, no operators here.
    // Operators stay in the ValueUnit module (ValueUnit.fs).
    type ValueUnit = ValueUnit of BigRational [] * Unit

    // ValueUnit module (stays in ValueUnit.fs)
    // ─────────────────────────────────────────
    module ValueUnit =

        // Stub — in the real code this delegates to the full calc function.
        let calc (vu1 : ValueUnit) (vu2 : ValueUnit) =
            let (ValueUnit (v1, u1)) = vu1
            let (ValueUnit (v2, u2)) = vu2
            ValueUnit (Array.append v1 v2, u1)  // simplified: just shows the pattern

        // Operators as `let inline` — idiomatic, no intrinsic-extension required.
        // The original `static member (*)(vu1, vu2)` becomes:
        let inline ( *? ) vu1 vu2 = calc vu1 vu2
        let inline ( /? ) vu1 vu2 = calc vu1 vu2
        let inline ( +? ) vu1 vu2 = calc vu1 vu2
        let inline ( -? ) vu1 vu2 = calc vu1 vu2


module TypesSplitVerification =
    open TypesSplit

    let mg500 = ValueUnit([| 500N |], Mass (MassUnit.MilliGram 1N))
    let mg250 = ValueUnit([| 250N |], Mass (MassUnit.MilliGram 1N))

    let combined = ValueUnit.( *? ) mg500 mg250

    printfn "\nApproach 2 (Types module split) compiles and works:"
    printfn "  mg500   = %A" mg500
    printfn "  mg250   = %A" mg250
    printfn "  combined = %A" combined


// ============================================================
// Summary
// ============================================================
printfn """
Summary
-------
1. Minimal fix (remove `and` from sub-types):
   - Zero behaviour change.
   - `namespace rec` in ValueUnit.fs already provides all forward references.
   - Sub-types (CountUnit, MassUnit, etc.) become plain `type` declarations.
   - The `type ValueUnit with` extensions at the end of ValueUnit.fs stay as-is
     (they are intrinsic extensions — same file is fine).

2. Partial split (Types.fs + ValueUnit.fs):
   - Move bare type definitions to Types.fs.
   - Convert `type ValueUnit with static member (*)(...)` → `let inline ( *? ) ...`
     in the ValueUnit module (ValueUnit.fs).
   - This avoids optional-extension-method pitfalls with operator resolution.
   - The inline functions are zero-cost and keep the same semantics.

Both compile and work as shown above.
"""
