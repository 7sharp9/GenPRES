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


    let dataToCsv dataUrlId prods routeMapping distBy =
        let grouped =
            dataUrlId
            |> DoseRule.addProducts prods routeMapping
            |> Result.toOption
            |> function
                | None -> [||]
                | Some data -> data
            |> Array.groupBy (DoseRule.mapToDoseRule >> Result.toOption)
            |> Array.filter (fst >> Option.isSome)
            |> Array.map (fun (dr, details) -> dr.Value, details)

        let distinct =
            grouped
            |> Array.collect snd
            |> Array.filter (_.Products >> Array.isEmpty >> not)
            |> Array.filter (_.Generic.Form >> String.notEmpty)
            |> Array.distinctBy distBy

        grouped
        |> Array.filter (snd >> Array.exists (fun d -> distinct |> Array.exists ((=) d)))
        |> Array.collect snd
        |> Array.toList
        |> List.map (fun d ->
            let bigRatToStringList =
                Array.map BigRational.toDouble >> Array.map string >> String.concat ";"

            let bigRatOptToString =
                Option.map (BigRational.toDouble >> string) >> Option.defaultValue ""

            [
                d.Id
                d.GrpId
                string d.SortNo
                d.Source
                d.Generic.Name
                d.Generic.Form
                d.Generic.Brand
                d.Generic.GPKs |> String.concat ";"
                d.Generic.HPKs |> String.concat ";"
                d.Route
                d.Indication
                d.SourceText
                d.PatientText
                d.ScheduleText
                d.Patient.Dep
                (if d.Patient.IsAdult then "x" else "")
                d.Patient.Gender |> Gender.toString
                d.Patient.MinAge |> bigRatOptToString
                d.Patient.MaxAge |> bigRatOptToString
                d.Patient.MinWeight |> bigRatOptToString
                d.Patient.MaxWeight |> bigRatOptToString
                d.Patient.MinBSA |> bigRatOptToString
                d.Patient.MaxBSA |> bigRatOptToString
                d.Patient.MinGestAge |> bigRatOptToString
                d.Patient.MaxGestAge |> bigRatOptToString
                d.Patient.MinPMAge |> bigRatOptToString
                d.Patient.MaxPMAge |> bigRatOptToString
                d.ScheduleData.DoseType
                d.ScheduleData.DoseText
                (if d.ScheduleData.DoseLimitData.CmpBased then "x" else "")
                d.ScheduleData.DoseLimitData.Component
                d.ScheduleData.DoseLimitData.Substance
                d.ScheduleData.Freqs |> bigRatToStringList
                d.ScheduleData.DoseLimitData.DoseUnit
                d.ScheduleData.AdjustUnit
                d.ScheduleData.FreqUnit
                d.ScheduleData.RateUnit
                d.ScheduleData.MinTime |> bigRatOptToString
                d.ScheduleData.MaxTime |> bigRatOptToString
                d.ScheduleData.TimeUnit
                d.ScheduleData.MinInt |> bigRatOptToString
                d.ScheduleData.MaxInt |> bigRatOptToString
                d.ScheduleData.IntUnit
                d.ScheduleData.MinDur |> bigRatOptToString
                d.ScheduleData.MaxDur |> bigRatOptToString
                d.ScheduleData.DurUnit
                d.ScheduleData.DoseLimitData.MinQty |> bigRatOptToString
                d.ScheduleData.DoseLimitData.MaxQty |> bigRatOptToString
                d.ScheduleData.DoseLimitData.MinQtyAdj |> bigRatOptToString
                d.ScheduleData.DoseLimitData.MaxQtyAdj |> bigRatOptToString
                d.ScheduleData.DoseLimitData.MinPerTime |> bigRatOptToString
                d.ScheduleData.DoseLimitData.MaxPerTime |> bigRatOptToString
                d.ScheduleData.DoseLimitData.MinPerTimeAdj |> bigRatOptToString
                d.ScheduleData.DoseLimitData.MaxPerTimeAdj |> bigRatOptToString
                d.ScheduleData.DoseLimitData.MinRate |> bigRatOptToString
                d.ScheduleData.DoseLimitData.MaxRate |> bigRatOptToString
                d.ScheduleData.DoseLimitData.MinRateAdj |> bigRatOptToString
                d.ScheduleData.DoseLimitData.MaxRateAdj |> bigRatOptToString
            ]
            |> String.concat "\t"
        )
        |> List.append headers
