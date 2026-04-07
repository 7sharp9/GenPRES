/// Prototype: Porting BSA Calculations to Shared Library
///
/// Context (issue #273 — Use units of measure):
///   The maintainer clarified that UoM is needed for values that are directly
///   involved in calculations *outside* the GenSOLVER pipeline — in particular,
///   BSA and related clinical calculations that should be available both
///   server-side and client-side (Fable-compatible).
///
///   This script demonstrates how the BSA (Body Surface Area) formulas from
///   `Informedica.GenCore.Lib.Calculations.BSA` can be adapted to work with
///   the Shared library's existing UoM types (`gram`, `cm`) and produce a
///   result that is usable in both .NET and Fable (JavaScript) environments.
///
/// Design decisions:
///   1. Use `float` (not `decimal`) for the formula internals — Fable compiles
///      F# `float` to native JS `number`, while `decimal` is emulated and has
///      much worse performance on the client.
///   2. The Shared `Weight` type stores weight in `int<gram>` and Height in
///      `int<cm>`; converters are provided to lift these to `float<kg>` / `float<cm>`
///      for the formula inputs.
///   3. UoM on the *result* (`float<bsa>`) makes the return type self-documenting
///      and prevents accidental unit confusion at call sites.
///   4. No external library dependencies — only `FSharp.Core`, which the Shared
///      project already references, and standard .NET `System.Math` (available
///      in Fable via `System.Math` or direct `sqrt` / `**` operators).
///
/// Usage:
///   Run this file in FSI from the Shared/Scripts directory, or load it from
///   any of the library Scripts/load.fsx chains.
///   > dotnet fsi BsaCalculations.fsx

// ---------------------------------------------------------------------------
// Re-declare the Shared UoM types so the script can run stand-alone
// (in production these come from Shared.Types, no re-declaration needed)
// ---------------------------------------------------------------------------
[<Measure>] type gram
[<Measure>] type cm
[<Measure>] type m
[<Measure>] type kg
[<Measure>] type bsa = m^2   // m² — standard unit for body surface area


// ---------------------------------------------------------------------------
// Unit conversion helpers (gram ↔ kg, int ↔ float)
// ---------------------------------------------------------------------------
module Conversions =

    /// Convert integer grams to float kilograms.
    let gramToKg (w: int<gram>) : float<kg> =
        (float w / 1000.0) * 1.0<kg>

    /// Convert integer centimetres to float centimetres (lifts int→float).
    let intCmToFloat (h: int<cm>) : float<cm> =
        float h * 1.0<cm>


// ---------------------------------------------------------------------------
// BSA formulas
//
// Each formula takes weight in kg and height in cm as *plain floats* (after
// stripping the UoM phantom types) and returns a plain float representing m².
// The public wrapper adds back the <bsa> measure at the boundary.
// ---------------------------------------------------------------------------
module BSA =

    // -- Internal raw formulas (dimensionless float → dimensionless float) ---

    let private mosteller w h = sqrt (w * h / 3600.0)

    let private duBois w h = 0.007184 * (w ** 0.425) * (h ** 0.725)

    let private haycock w h = 0.024265 * (w ** 0.5378) * (h ** 0.3964)

    let private gehanAndGeorge w h = 0.0235 * (w ** 0.51456) * (h ** 0.42246)

    let private fujimoto w h = 0.008883 * (w ** 0.444) * (h ** 0.663)


    // -- Public typed wrappers -----------------------------------------------

    /// Calculate BSA (m²) using the Mosteller formula.
    /// weight: patient weight in integer grams (from Shared.Types.Weight)
    /// height: patient height in integer centimetres (from Shared.Types.Height)
    let calcMosteller (weight: int<gram>) (height: int<cm>) : float<bsa> =
        let w = weight |> Conversions.gramToKg |> float
        let h = height |> Conversions.intCmToFloat |> float
        mosteller w h * 1.0<bsa>

    /// Calculate BSA (m²) using the Du Bois formula.
    let calcDuBois (weight: int<gram>) (height: int<cm>) : float<bsa> =
        let w = weight |> Conversions.gramToKg |> float
        let h = height |> Conversions.intCmToFloat |> float
        duBois w h * 1.0<bsa>

    /// Calculate BSA (m²) using the Haycock formula.
    let calcHaycock (weight: int<gram>) (height: int<cm>) : float<bsa> =
        let w = weight |> Conversions.gramToKg |> float
        let h = height |> Conversions.intCmToFloat |> float
        haycock w h * 1.0<bsa>

    /// Calculate BSA (m²) using the Gehan & George formula.
    let calcGehanAndGeorge (weight: int<gram>) (height: int<cm>) : float<bsa> =
        let w = weight |> Conversions.gramToKg |> float
        let h = height |> Conversions.intCmToFloat |> float
        gehanAndGeorge w h * 1.0<bsa>

    /// Calculate BSA (m²) using the Fujimoto formula.
    let calcFujimoto (weight: int<gram>) (height: int<cm>) : float<bsa> =
        let w = weight |> Conversions.gramToKg |> float
        let h = height |> Conversions.intCmToFloat |> float
        fujimoto w h * 1.0<bsa>

    // -- Optional: from Shared Patient type ---------------------------------

    /// Attempt to calculate BSA from a Shared Patient value, using the
    /// *measured* weight and height when available, falling back to estimated
    /// median (P50) values.  Returns None if neither is available.
    ///
    /// In practice the caller chooses which formula to use; Du Bois is the
    /// clinical default for adults, Haycock for paediatric patients.
    let tryCalcFromPatient formula (patient: {| Weight: {| Measured: int<gram> option; Estimated: int<gram> option |};
                                                Height: {| Measured: int<cm>   option; Estimated: int<cm>   option |} |}) =
        let w = patient.Weight.Measured |> Option.orElse patient.Weight.Estimated
        let h = patient.Height.Measured |> Option.orElse patient.Height.Estimated
        match w, h with
        | Some w, Some h -> formula w h |> Some
        | _ -> None


// ---------------------------------------------------------------------------
// Sanity-check examples (run in FSI to verify)
// ---------------------------------------------------------------------------

// Reference values from literature:
//   70 kg, 170 cm → DuBois  ≈ 1.80 m²
//                   Mosteller ≈ 1.81 m²
//   3.5 kg, 50 cm (neonate) → DuBois ≈ 0.22 m²

let weight70kg  = 70_000<gram>  // 70 kg in grams
let height170cm = 170<cm>

let weight3_5kg = 3_500<gram>   // 3.5 kg neonate
let height50cm  = 50<cm>

printfn "=== BSA Sanity Checks (issue #273 prototype) ==="

printfn "\nAdult (70 kg, 170 cm):"
printfn "  DuBois    = %.4f m²" (BSA.calcDuBois    weight70kg height170cm |> float)
printfn "  Mosteller = %.4f m²" (BSA.calcMosteller weight70kg height170cm |> float)
printfn "  Haycock   = %.4f m²" (BSA.calcHaycock   weight70kg height170cm |> float)

printfn "\nNeonate (3.5 kg, 50 cm):"
printfn "  DuBois    = %.4f m²" (BSA.calcDuBois    weight3_5kg height50cm |> float)
printfn "  Mosteller = %.4f m²" (BSA.calcMosteller weight3_5kg height50cm |> float)
printfn "  Haycock   = %.4f m²" (BSA.calcHaycock   weight3_5kg height50cm |> float)

printfn "\nDone."

/// Prototype notes for maintainer review:
///
/// Migration path to Shared/Calculations.fs (new file):
///   1. Copy the `BSA` module above (and the Conversions helpers) into a new
///      `Shared/Calculations.fs`, placing it after `Types.fs` in the project.
///   2. Update `Informedica.GenPRES.Shared.fsproj` to include the new file.
///   3. The `Informedica.GenCORE.Lib.Calculations.BSA` references in
///      `GenFORM.Lib/Patient.fs` and `GenCORE.Lib/Patient.fs` can be
///      replaced or supplemented — but that is a .fs change the maintainer
///      decides.
///   4. Client-side (Fable): import Shared and call `BSA.calcDuBois` directly
///      in the patient data component wherever BSA display is needed.
///
/// Fable compatibility:
///   - `float` arithmetic, `sqrt`, and `**` are all native in Fable.
///   - F# UoM annotations are erased at compile time; no runtime overhead.
///   - `System.Math` is available in Fable via the `Fable.Core.JS` interop
///     layer, but `sqrt` and `**` use the built-in F# operators which compile
///     to `Math.sqrt` and `Math.pow` in JS automatically.
