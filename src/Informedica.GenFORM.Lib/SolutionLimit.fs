namespace Informedica.GenForm.Lib

module SolutionLimit =

    open System
    open Informedica.GenUnits.Lib
    open Informedica.GenCore.Lib.Ranges


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


    let toString (sl: SolutionLimit) =
        let mmToStr =
            MinMax.toString
                ValueUnit.toStringDecimalEngShortWithoutGroup
                ValueUnit.toStringDecimalEngShortWithoutGroup
                "min "
                "min "
                "max "
                "max "

        [
                let qty = sl.Quantity |> mmToStr
                if not (String.IsNullOrWhiteSpace qty) then $"{FieldLabels.Quantity} {qty}"
                let qtyAdj = sl.QuantityAdj |> mmToStr
                if not (String.IsNullOrWhiteSpace qtyAdj) then $"{FieldLabels.QuantityAdjust} {qtyAdj}"

                sl.Quantities
                |> Option.map (fun vu ->
                    $"{FieldLabels.Quantities} {vu |> ValueUnit.toStringDecimalEngShortWithoutGroup}"

                ) |> Option.defaultValue ""

                let conc = sl.Concentration |> mmToStr
                if not (String.IsNullOrWhiteSpace conc) then $"{FieldLabels.Concentration} {conc}"
        ]
