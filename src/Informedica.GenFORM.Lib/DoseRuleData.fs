namespace Informedica.GenForm.Lib


module DoseRuleData =

    open Informedica.Utils.Lib.BCL


    let headers =
        [
            "Id"
            "GrpId"
            "SortNo"
            "Source"
            "Generic"
            "Form"
            "Brand"
            "Route"
            "GPKs"
            "HPKs"
            "Indication"
            "SourceText"
            "PatientText"
            "ScheduleText"
            "Dep"
            "IsAdult"
            "Gender"
            "MinAge"
            "MaxAge"
            "MinWeight"
            "MaxWeight"
            "MinBSA"
            "MaxBSA"
            "MinGestAge"
            "MaxGestAge"
            "MinPMAge"
            "MaxPMAge"
            "DoseType"
            "DoseText"
            "CmpBased"
            "Component"
            "Substance"
            "Freqs"
            "DoseUnit"
            "AdjustUnit"
            "FreqUnit"
            "RateUnit"
            "MinTime"
            "MaxTime"
            "TimeUnit"
            "MinInt"
            "MaxInt"
            "IntUnit"
            "MinDur"
            "MaxDur"
            "DurUnit"
            "MinQty"
            "MaxQty"
            "MinQtyAdj"
            "MaxQtyAdj"
            "MinPerTime"
            "MaxPerTime"
            "MinPerTimeAdj"
            "MaxPerTimeAdj"
            "MinRate"
            "MaxRate"
            "MinRateAdj"
            "MaxRateAdj"
        ]
        |> String.concat "\t"
        |> List.singleton


    let distinctByDoseLimit (d: DoseRuleData) =
        d.ScheduleData.DoseType,
        d.ScheduleData.DoseLimitData.Substance |> String.isNullOrWhiteSpace,
        d.ScheduleData.AdjustUnit |> String.isNullOrWhiteSpace,
        d.ScheduleData.DoseLimitData.MinQty.IsSome,
        d.ScheduleData.DoseLimitData.MaxQty.IsSome,
        d.ScheduleData.DoseLimitData.MinQtyAdj.IsSome,
        d.ScheduleData.DoseLimitData.MaxQtyAdj.IsSome,
        d.ScheduleData.DoseLimitData.MinPerTime.IsSome,
        d.ScheduleData.DoseLimitData.MaxPerTime.IsSome,
        d.ScheduleData.DoseLimitData.MinPerTimeAdj.IsSome,
        d.ScheduleData.DoseLimitData.MaxPerTimeAdj.IsSome,
        d.ScheduleData.DoseLimitData.MinRate.IsSome,
        d.ScheduleData.DoseLimitData.MaxRate.IsSome,
        d.ScheduleData.DoseLimitData.MinRateAdj.IsSome,
        d.ScheduleData.DoseLimitData.MaxRateAdj.IsSome
