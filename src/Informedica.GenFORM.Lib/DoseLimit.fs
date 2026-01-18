namespace Informedica.GenForm.Lib


module DoseLimit =

    open System
    open Informedica.Utils.Lib.BCL
    open Informedica.GenCore.Lib.Ranges
    open Informedica.GenUnits.Lib

    open Utils

    let create
        tar
        aun
        dun
        qty
        qta
        ptm
        pta
        rte
        rta : DoseLimit =

        {
            DoseLimitTarget = tar
            AdjustUnit = aun
            DoseUnit = dun
            Quantity = qty
            QuantityAdjust = qta
            PerTime = ptm
            PerTimeAdjust = pta
            Rate = rte
            RateAdjust = rta
        }


    /// An empty DoseLimit.
    let limit =
        {
            DoseLimitTarget = NoLimitTarget
            AdjustUnit = None
            DoseUnit = NoUnit
            Quantity = MinMax.empty
            QuantityAdjust = MinMax.empty
            PerTime = MinMax.empty
            PerTimeAdjust = MinMax.empty
            Rate = MinMax.empty
            RateAdjust = MinMax.empty
        }


    /// <summary>
    /// Check whether an adjust is used in
    /// the DoseLimit.
    /// </summary>
    /// <remarks>
    /// If any of the adjust values is not None
    /// then an adjust is used.
    /// </remarks>
    let useAdjust (dl : DoseLimit) =
        [
            dl.QuantityAdjust = MinMax.empty
            dl.PerTimeAdjust = MinMax.empty
            dl.RateAdjust = MinMax.empty
        ]
        |> List.forall id
        |> not


    let hasNoLimits (dl : DoseLimit) =
        { limit with
            DoseLimitTarget = dl.DoseLimitTarget
            AdjustUnit = dl.AdjustUnit
            DoseUnit = dl.DoseUnit
        } = dl


    let isSubstanceLimit (dl : DoseLimit) = dl.DoseLimitTarget |> LimitTarget.isSubstanceTarget


    let isComponentLimit (dl : DoseLimit) = dl.DoseLimitTarget |> LimitTarget.isComponentTarget


    let isShapeLimit (dl : DoseLimit) = dl.DoseLimitTarget |> LimitTarget.isOrderableTarget


    let getNormDose minMax =
        match minMax.Min, minMax.Max with
        | Some minLimit, Some maxLimit ->
            if minLimit |> Limit.eq maxLimit then
                minLimit |> Limit.getValueUnit |> Some
            else None
        | _ -> None


    let isNormDose = getNormDose >> Option.isSome


    let printMinMaxDose perDose (minMax : MinMax) =
        let toStr mm =
            if mm = MinMax.empty then ""
            else
                mm
                |> MinMax.toString
                    "min "
                    "min "
                    "max "
                    "max "
                |> fun s ->
                    $"{s}{perDose}"

        if minMax |> isNormDose then
            minMax.Min
            |> Option.map (fun minLim ->
                minLim
                |> Limit.getValueUnit
                |> fun vu -> $"{vu |> Utils.ValueUnit.toString 3}{perDose}"
            )
            |> Option.defaultValue ""
        else minMax |> toStr


    let toString (dl: DoseLimit) =
        [
            let perDose = "/dosis"
            let emptyS = ""
            [
                $"{dl.DoseLimitTarget |> LimitTarget.toString}"

                $"{dl.Rate |> printMinMaxDose emptyS}"
                $"{dl.RateAdjust |> printMinMaxDose emptyS}"

                $"{dl.PerTimeAdjust |> printMinMaxDose emptyS}"
                $"{dl.PerTime |> printMinMaxDose emptyS}"

                $"{dl.QuantityAdjust |> printMinMaxDose perDose}"
                $"{dl.Quantity |> printMinMaxDose perDose}"
            ]
            |> List.map String.trim
            |> List.filter (String.IsNullOrEmpty >> not)
            |> String.concat ", "
        ]
