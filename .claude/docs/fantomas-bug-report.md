# Fantomas Bug Report: `module rec` with custom operators + external call produces invalid F# code

## Title

Formatting `module rec` with `let inline` custom operators produces invalid F# when operators are called from another module

## Code

```fsharp
#r "nuget: MathNet.Numerics.FSharp"
open MathNet.Numerics

module rec TypesSplit =

    type Unit =
        | NoUnit
        | CombiUnit of Unit * Operator * Unit
        | Mass of MassUnit

    type MassUnit =
        | KiloGram of BigRational
        | MilliGram of BigRational

    type Operator = OpTimes | OpPer

    type ValueUnit = ValueUnit of BigRational [] * Unit

    module ValueUnit =

        let calc (vu1 : ValueUnit) (vu2 : ValueUnit) =
            let (ValueUnit (v1, u1)) = vu1
            let (ValueUnit (v2, _)) = vu2
            ValueUnit (Array.append v1 v2, u1)

        let inline ( *? ) vu1 vu2 = calc vu1 vu2
        let inline ( /? ) vu1 vu2 = calc vu1 vu2

module Verification =
    open TypesSplit
    let mg500 = ValueUnit([| 500N |], Mass (MassUnit.MilliGram 1N))
    let mg250 = ValueUnit([| 250N |], Mass (MassUnit.MilliGram 1N))
    let combined = ValueUnit.( *? ) mg500 mg250
    printfn "  combined = %A" combined
```

## Problem description

The input is valid F# code — it compiles and runs successfully with `dotnet fsi`. However, Fantomas 7.0.5 reports:

```text
Failed to format file: repro.fsx : Formatting repro.fsx leads to invalid F# code
```

The bug requires all three of:

1. `module rec` containing type definitions and a nested module with `let inline` custom operators (`let inline ( *? ) ...`)
2. A **separate** module that calls the custom operator via qualified access (`ValueUnit.( *? )`)
3. The operators are defined as `let inline` in a nested module (not as `static member` on the type)

Removing any one of these makes formatting succeed:

- Removing the `Verification` module → formats OK
- Removing `module rec` → formats OK
- Changing `let inline ( *? )` to `static member (*)` on a type extension → formats OK

**Note:** `static member` operators in `namespace rec` / `module rec` format correctly. This is only triggered by `let inline` custom operators in a nested module + qualified operator call syntax from another module.

## Related issues

- #1805 — Lazy causes indentation to produce invalid F# (closed, similar class of soundness bug)
- #1609 — Idempotency problem with lazy (closed)

## Extra information

- [x] The formatted result breaks the code.
- [ ] The formatted result gives compiler warnings.
- [ ] I or my company would be willing to help fix this.

## Version

Fantomas v7.0.5

## .editorconfig

Bug reproduces with default Fantomas configuration (no .editorconfig settings needed).
