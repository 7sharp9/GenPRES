namespace Informedica.GenForm.Lib

module SolutionLimit =

    open System
    open Informedica.GenCore.Lib.Ranges
    open Utils


    /// Field labels for deterministic parsing
    module FieldLabels =
        let [<Literal>] Quantity = "[qty]"
        let [<Literal>] QuantityAdjust = "[qty-adj]"
        let [<Literal>] Quantities = "[qts]"
        let [<Literal>] Concentration = "[conc]"


    /// An empty SolutionLimit.
    let limit =
        {
            SolutionLimitTarget = NoLimitTarget
            Quantity = MinMax.empty
            QuantityAdj = MinMax.empty
            Quantities = None
            Concentration = MinMax.empty
            Products = [||]
        }


    let minMaxToString (minMax : MinMax) =
        if minMax = MinMax.empty then ""
        else
            minMax
            |> MinMax.toString
                "min "
                "min "
                "max "
                "max "


    let toString (sl: SolutionLimit) =
        [
                let qty = sl.Quantity |> minMaxToString
                if not (String.IsNullOrWhiteSpace qty) then $"{FieldLabels.Quantity} {qty}"
                let qtyAdj = sl.QuantityAdj |> minMaxToString
                if not (String.IsNullOrWhiteSpace qtyAdj) then $"{FieldLabels.QuantityAdjust} {qtyAdj}"

                sl.Quantities
                |> Option.map (fun vu ->
                    $"{FieldLabels.Quantities} {vu |> ValueUnit.toString 2}"

                ) |> Option.defaultValue ""

                let conc = sl.Concentration |> minMaxToString
                if not (String.IsNullOrWhiteSpace conc) then $"{FieldLabels.Concentration} {conc}"
        ]
