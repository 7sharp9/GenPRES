/// <summary>
/// Prototype: Local product support for issue #18
/// "Have to find a solution for 9 million product numbers"
///
/// Context
/// -------
/// The GenFORM data model currently only supports products whose GPKODE in the
/// Formulary sheet parses as an integer — i.e. a real Z-index GPK code.
/// Locally manufactured medications (e.g. hospital-compounded TPN compositions,
/// locally produced eye-drops, etc.) have no Z-index entry and therefore no
/// integer GPK.  Their GPKODE column in the Formulary sheet contains a free-form
/// string identifier (e.g. "Samenstelling C").
///
/// Architecture (as described by @halcwb in issue #18, 2026-03-13)
/// -----------------------------------------------------------------
/// A two-level solution:
///
///   Level 1 — Type-safe product identity:
///     Replace raw `int`/`string` GPK fields with a discriminated union:
///       type ProductId = | ZIndex of int | Local of string
///     This makes the distinction explicit in the type system and eliminates
///     silent filtering of local products.
///
///   Level 2 — Formulary sheet data:
///     The existing "Formulary" Google Sheet already carries the data needed for
///     local products (generic name, unit, department assignments, nutritional
///     columns, etc.).  The loader can create Product records directly from those
///     rows for local medications, instead of looking up a Z-index GenPresProduct.
///
/// Current state
/// -------------
/// - Parenteral and enteral local products ALREADY work (Product.Parenteral.get
///   and Product.Enteral.get build Product records directly from FormularyProduct).
/// - Local *medications* (ProductType = MedicationProduct) with a non-integer
///   GPKODE are currently dropped silently in Product.get because:
///     fp.GPK |> Int32.tryParse |> Option.map ...   (* returns None *)
///
/// This script
/// -----------
/// Demonstrates:
///   1. The `ProductId` DU and helpers.
///   2. A `parseProductId` function for GPKODE column values.
///   3. A `createLocalMedication` function that builds a Product record for a
///      local medication without a Z-index entry.
///   4. An extended `get` function that handles both Z-index and local products.
///
/// Usage
/// -----
/// Run from this directory:
///   cd src/Informedica.GenFORM.Lib/Scripts
///   dotnet fsi LocalProducts.fsx
/// </summary>

#I __SOURCE_DIRECTORY__
#load "load.fsx"

open System
open MathNet.Numerics
open Informedica.Utils.Lib
open Informedica.GenUnits.Lib
open Informedica.GenForm.Lib


// =============================================================================
// 1. ProductId discriminated union
//    Replaces raw int / string GPK fields with an explicit type distinction.
// =============================================================================

/// Identifies a product — either a real Z-index GPK (integer) or a locally
/// defined product with a hospital-specific string identifier.
[<RequireQualifiedAccess>]
type ProductId =
    | ZIndex of int   // standard Z-index GPK code
    | Local  of string // hospital / pharmacy local identifier (e.g. "Samenstelling C")


module ProductId =

    /// Parse a GPKODE column value into a ProductId.
    /// An all-digit string (> 0) is treated as a Z-index GPK; everything else
    /// becomes a Local identifier.
    let parse (s: string) : ProductId option =
        if String.IsNullOrWhiteSpace s || s = "0" then None
        else
            match Int32.TryParse s with
            | true, n when n > 0 -> Some (ProductId.ZIndex n)
            | _                  -> Some (ProductId.Local s)

    /// Return the string representation used as the `GPK` field in a Product record.
    let toGpkString = function
        | ProductId.ZIndex n -> string n
        | ProductId.Local  s -> s

    /// True for Z-index products.
    let isZIndex = function ProductId.ZIndex _ -> true | _ -> false

    /// True for locally defined products.
    let isLocal = function ProductId.Local _ -> true | _ -> false


// =============================================================================
// 2. Local medication builder
//    Mirror of Product.Parenteral.createProduct / Enteral.createProduct, but
//    targeted at ProductType.MedicationProduct rows in the Formulary sheet.
// =============================================================================

module LocalMedication =

    open Informedica.GenForm.Lib.Product

    /// Build a minimal Product record for a locally manufactured medication.
    /// The substance list comes from the Formulary sheet's nutritional / ingredient
    /// columns via `getAdditionalSubstances`.
    let create (unitMapping: Mapping.UnitMapping array) (fp: Types.FormularyProduct) : Types.Product option =
        let formUnit =
            fp.Unit
            |> Option.bind Units.fromString

        match formUnit with
        | None ->
            // Cannot build a Product without a form unit — skip this entry.
            None
        | Some fu ->
            let name = fp.Generic |> String.toLower
            let substs = fp |> getAdditionalSubstances

            let product = {
                GPK     = fp.GPK
                ATC     = ""
                MainGroup   = ""
                SubGroup    = ""
                Generic     = name
                UseGenericName = fp.UseGenName
                UseForm     = fp.UseForm
                UseBrand    = fp.UseBrand
                TallMan     = fp.TallMan
                Synonyms    = [||]
                Product     = name
                Label       = name
                Form        = "vloeistof"   // default; could be read from Form column
                Routes      = [| "ORAAL"; "SUBLINGUAAL" |]  // conservative default
                FormQuantities =
                    fu
                    |> ValueUnit.singleWithValue 1N
                FormUnit    = fu
                RequiresReconstitution = fp.IsReconste
                Reconstitution = [||]
                Divisible   = fp.Divisible |> Option.map BigRational.fromInt
                Substances  =
                    substs
                    |> List.choose (fun (s, q) ->
                        let n, u =
                            match s |> String.split " " with
                            | [ n; u ] -> n |> String.trim, u |> String.trim
                            | _        -> failwith $"cannot parse substance '{s}'"
                        Substance.create n q u fu unitMapping
                    )
                    |> List.filter (fun s ->
                        s.Name |> String.notEmpty &&
                        (s.Concentration |> Option.isSome ||
                         s.MolarConcentration |> Option.isSome)
                    )
                    |> List.toArray
            }
            Some product


// =============================================================================
// 3. Extended Product.Medication.get that handles both Z-index and local products
// =============================================================================

module Medication =

    open Informedica.ZIndex.Lib
    open Informedica.GenForm.Lib.Product

    /// Extended version of Product.Medication.get.
    ///
    /// Differences from the original:
    ///   - Uses `ProductId.parse` instead of `Int32.tryParse` to classify each row.
    ///   - Handles local medications (ProductType = MedicationProduct, non-integer
    ///     GPKODE) via `LocalMedication.create`.
    ///   - Z-index products follow the existing path unchanged.
    let get
        unitMapping
        routeMapping
        validForms
        formRoutes
        reconstitution
        (formularyProducts: Types.FormularyProduct[])
        =
        // Partition formulary products by identity type -------------------------
        let zIndexRows, localMedRows =
            formularyProducts
            |> Array.choose (fun fp ->
                fp.GPK
                |> ProductId.parse
                |> Option.map (fun pid -> pid, fp)
            )
            |> Array.partition (fun (pid, _) -> ProductId.isZIndex pid)

        // Z-index path: same as current Product.Medication.get -------------------
        let zIndexProducts =
            zIndexRows
            |> Array.collect (fun (pid, fp) ->
                let gpk = match pid with ProductId.ZIndex n -> n | _ -> 0
                gpk
                |> GenPresProduct.findByGPK
                |> Array.map (fun gpp -> gpk, fp, gpp)
            )
            |> Array.collect (fun (gpk, fp, gpp) ->
                gpp.GenericProducts
                |> Array.filter (fun gp ->
                    gp.Id = gpk &&
                    validForms |> Array.exists (String.equalsCapInsens gp.Form) &&
                    gp.Substances |> Array.exists (fun s -> s.SubstanceQuantity > 0.)
                )
                |> Array.map (fun gp -> fp, gp)
            )
            |> Array.map (fun (fp, gp) ->
                let name         = fp.Generic |> String.toLower
                let synonyms     =
                    gp.PrescriptionProducts
                    |> Array.collect (fun pp -> pp.TradeProducts |> Array.map _.Brand)
                    |> Array.distinct
                    |> Array.filter String.notEmpty
                let formQuantities =
                    gp.PrescriptionProducts
                    |> Array.map _.Quantity
                    |> Array.choose BigRational.fromFloat
                    |> Array.filter (fun br -> br > 0N)
                    |> Array.distinct
                    |> fun xs -> if xs |> Array.isEmpty then [| 1N |] else xs

                gp
                |> Product.Medication.map
                       unitMapping routeMapping formRoutes reconstitution
                       name synonyms formQuantities fp
            )

        // Local medication path ---------------------------------------------------
        let localProducts =
            localMedRows
            |> Array.filter (fun (_, fp) -> fp.ProductType.IsMedicationProduct)
            |> Array.choose (fun (_, fp) ->
                LocalMedication.create unitMapping fp
            )

        Array.append zIndexProducts localProducts


// =============================================================================
// 4. Quick smoke test
// =============================================================================

printfn "=== ProductId.parse smoke test ==="

let cases = [
    "104299",       Some (ProductId.ZIndex 104299)    // real GPK
    "Samenstelling C", Some (ProductId.Local "Samenstelling C")  // local TPN
    "0",            None                              // dummy / placeholder
    "",             None                              // empty
    "abc123",       Some (ProductId.Local "abc123")   // arbitrary local id
]

for (input, expected) in cases do
    let result = ProductId.parse input
    let ok = result = expected
    printfn "  parse %A => %A  [%s]" input result (if ok then "OK" else "FAIL")

printfn ""
printfn "=== Summary ==="
printfn "This prototype shows:"
printfn "  1. ProductId DU — type-safe Z-index / Local distinction"
printfn "  2. LocalMedication.create — builds Product records for local meds"
printfn "  3. Medication.get (extended) — handles both Z-index and local products"
printfn ""
printfn "To integrate, the maintainer would:"
printfn "  a. Add ProductId to Types.fs (or a new Types file)"
printfn "  b. Replace Int32.tryParse with ProductId.parse in Product.Medication.get"
printfn "  c. Append LocalMedication.create results for MedicationProduct rows"
printfn "  d. Update the GPK field in Product/FormularyProduct types if desired"
