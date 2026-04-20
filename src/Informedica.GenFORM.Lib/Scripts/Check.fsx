
#time

#load "load.fsx"

open System
Informedica.Utils.Lib.Env.loadDotEnv () |> ignore
Environment.SetEnvironmentVariable("GENPRES_PROD", "1")
let dataUrlId = Environment.GetEnvironmentVariable("GENPRES_URL_ID")


#load "../Types.fs"
#load "../Utils.fs"
#load "../Logging.fs"
#load "../Mapping.fs"
#load "../Patient.fs"
#load "../Product.fs"
#load "../Filter.fs"
#load "../LimitTarget.fs"
#load "../DoseLimit.fs"
#load "../DoseType.fs"
#load "../DoseRule.fs"
#load "../Check.fs"
#load "../SolutionLimit.fs"
#load "../SolutionRule.fs"
#load "../RenalRule.fs"
#load "../PrescriptionRule.fs"
#load "../FormLogging.fs"
#load "../Api.fs"


open FsToolkit.ErrorHandling
open MathNet.Numerics
open Informedica.GenUnits.Lib
open Informedica.GenForm.Lib


module GenFormResult =

    let defaultValue value res =
        res |> Result.map fst |> Result.defaultValue value


let provider: Resources.IResourceProvider =
    Api.getCachedProviderWithDataUrlId FormLogging.noOp dataUrlId


let pat =
    { Patient.patient with
        Age = 5N |> ValueUnit.singleWithUnit Units.Time.year |> Some
        Weight = 22N |> ValueUnit.singleWithUnit Units.Weight.kiloGram |> Some
        Height = 117N |> ValueUnit.singleWithUnit Units.Height.centiMeter |> Some
    }


let rules =
    Api.getDoseRules provider
    |> Array.filter (fun dr ->
        dr.PatientCategory |> PatientCategory.filterPatient pat &&
        dr.Generic = "aciclovir" &&
        dr.Indication = "Herpes encefalitis (normale immuunrespons), primaire/recidiverende Varicella zoster-infectie bij immuungecompromitteerden" &&
        dr.Route = "INTRAVENEUS"
    )


let gstandRules =
    rules
    |> Array.map (Check.matchWithZIndex (provider.GetRouteMappings ()) pat)


let mapping =
    gstandRules
    |> Array.map Check.createMapping


// Shadowed Check module that ignores range comparisons when either the
// test-range (genForm) or the reference-range (G-Standaard) is MinMax.empty
// (both Min and Max are None).
//
// checkInRangeOf and checkDoseRule are shadowed; all other helpers are
// re-used from Informedica.GenForm.Lib.Check via `open`.
module Check =

    open Informedica.Utils.Lib
    open Informedica.Utils.Lib.BCL
    open MathNet.Numerics
    open Informedica.GenUnits.Lib
    open Informedica.GenCore.Lib.Ranges
    open Informedica.GenForm.Lib
    open Informedica.GenForm.Lib.Check
    open Utils


    let checkInRangeOf sn (refRange: MinMax) (testRange: MinMax) : bool option * string =
        if testRange |> MinMax.isEmpty || refRange |> MinMax.isEmpty then
            None, ""
        else
            let getTimeUnit mm =
                match mm.Min |> Option.map Limit.getValueUnit, mm.Max |> Option.map Limit.getValueUnit with
                | Some vu, _
                | _, Some vu ->
                    match vu |> ValueUnit.getUnit |> ValueUnit.getUnits with
                    | [ _; tu ]
                    | [ _; _; tu ] when tu |> ValueUnit.Group.eqsGroup Units.Time.day -> Some tu
                    | _ -> None
                | _ -> None
                |> Option.map unitToString
                |> Option.defaultValue ""

            ((testRange.Min |> Option.isNone
              || testRange.Min |> Option.map Limit.getValueUnit |> MinMax.inRange refRange)
             && (testRange.Max |> Option.isNone
                 || testRange.Max |> Option.map Limit.getValueUnit |> MinMax.inRange refRange))
            |> fun b ->
                let u =
                    match testRange.Min, testRange.Max with
                    | Some l, _
                    | _, Some l -> l |> Limit.getValueUnit |> ValueUnit.getUnit |> Some
                    | _ -> None

                let toStr mm =
                    if u |> Option.isNone then
                        mm
                    else
                        let convert =
                            Option.map (Limit.getValueUnit >> ValueUnit.convertTo u.Value >> Limit.inclusive)

                        {
                            Min = mm.Min |> convert
                            Max = mm.Max |> convert
                        }
                    |> MinMax.toString "min " "min " "max " "max "

                Some b,
                $"""%s{sn} {testRange |> toStr} {if b then "" else "niet "}in bereik van %s{refRange |> toStr}"""
                |> String.replace "<TIMEUNIT>" (testRange |> getTimeUnit)


    let checkDoseRule routeMapping (pat: Patient) (dr: DoseRule) =
        let m = dr |> matchWithZIndex routeMapping pat |> createMapping

        let eqsAny (candidates: DoseType list) (dt: DoseType) =
            candidates |> List.exists (DoseType.eqsType dt)

        let toMinMax vuOpt =
            {
                Min =
                    vuOpt
                    |> Option.map ((*) ([| 90N / 100N |] |> ValueUnit.withUnit Units.Count.times))
                    |> Option.map Limit.inclusive
                Max =
                    vuOpt
                    |> Option.map ((*) ([| 110N / 100N |] |> ValueUnit.withUnit Units.Count.times))
                    |> Option.map Limit.inclusive
            }

        // Derive rate fields for a given dose-limit target from m.zindex.dosages,
        // mirroring the perTimeAdjust* pattern in Check.fs createMapping (lines 403-428).
        let rateFieldsFor (target: string) =
            let empty =
                {|
                    rateNorm = MinMax.empty
                    rateAbs = MinMax.empty
                    rateAdjustNorm = MinMax.empty
                    rateAdjustAbs = MinMax.empty
                |}

            m.zindex.dosages
            |> List.tryFind (fun g -> target |> String.equalsCapInsens g.target)
            |> Option.bind _.dosage
            |> Option.map (fun x ->
                let dr, rateUnit = x.RateDosage

                if rateUnit = NoUnit then
                    empty
                else
                    {|
                        rateNorm = dr.Norm |> setAdjustAndOrTimeUnit None (Some rateUnit)
                        rateAbs = dr.Abs |> setAdjustAndOrTimeUnit None (Some rateUnit)
                        rateAdjustNorm =
                            if dr.NormWeight |> fst = MinMax.empty then
                                if dr.NormBSA |> fst = MinMax.empty then
                                    MinMax.empty
                                else
                                    dr.NormBSA
                                    |> fst
                                    |> setAdjustAndOrTimeUnit (Some Units.BSA.m2) (Some rateUnit)
                            else
                                dr.NormWeight
                                |> fst
                                |> setAdjustAndOrTimeUnit (Some Units.Weight.kiloGram) (Some rateUnit)
                        rateAdjustAbs =
                            if dr.AbsWeight |> fst = MinMax.empty then
                                if dr.AbsBSA |> fst = MinMax.empty then
                                    MinMax.empty
                                else
                                    dr.AbsBSA
                                    |> fst
                                    |> setAdjustAndOrTimeUnit (Some Units.BSA.m2) (Some rateUnit)
                            else
                                dr.AbsWeight
                                |> fst
                                |> setAdjustAndOrTimeUnit (Some Units.Weight.kiloGram) (Some rateUnit)
                    |}
            )
            |> Option.defaultValue empty

        // Extract adjust unit from a MinMax (mirrors getAdj inside Check.checkAdjustUnit,
        // Check.fs lines 28-38).
        let getAdjUnit (mm: MinMax) =
            match mm.Min |> Option.map Limit.getValueUnit, mm.Max |> Option.map Limit.getValueUnit with
            | Some vu, _
            | _, Some vu ->
                match vu |> ValueUnit.getUnit |> ValueUnit.getUnits with
                | [ _; adj ]
                | [ _; adj; _ ]
                | [ _; _; adj ] when adj = Units.Weight.kiloGram || adj = Units.Weight.gram || adj = Units.BSA.m2 ->
                    Some adj
                | _ -> None
            | _ -> None

        m.mapping.doseLimits
        |> Array.collect (fun dl ->
            match dl.gstand with
            | None -> [| (None: bool option), "" |]
            | Some gstand ->
                let dt = m.doseRule.DoseType
                let p = m.doseRule.PatientCategory |> PatientCategory.toString
                let r = m.doseRule.Route

                let inRangeOf msg refRange testRange =
                    try
                        checkInRangeOf $"{gstand.doseLimitTarget}\t{r}\t{p}\t{msg}: " refRange testRange
                    with e ->
                        ConsoleWriter.NewLineNoTime.writeErrorMessage $"{e}"
                        Some true,
                        $"{gstand.doseLimitTarget}\t{r}\t{p}\t{msg}: kan niet worden gechecked vanwege foutmelding"

                match dt with
                | NoDoseType ->
                    [|
                        None,
                        $"{m.doseRule.Generic}\t{r}\t{p}\tgeen doseer type — check kan niet plaats vinden"
                    |]
                | _ ->
                    let rates = rateFieldsFor gstand.doseLimitTarget

                    let runQuantity =
                        dt
                        |> eqsAny [ Once ""; Discontinuous ""; Timed ""; OnceTimed "" ]

                    let runPerTime = dt |> eqsAny [ Discontinuous ""; Timed "" ]
                    let runRate = dt |> eqsAny [ Continuous ""; Timed ""; OnceTimed "" ]
                    let runFrequencies = dt |> eqsAny [ Discontinuous ""; Timed "" ]

                    let freqRow () =
                        match m.mapping.frequencies.genform, m.mapping.frequencies.gstand with
                        | None, _
                        | _, None -> None, ""
                        | Some vuG, Some vuS ->
                            let b = vuS |> ValueUnit.isSubset vuG

                            let s1 = vuG |> ValueUnit.toStringDecimalDutchShortWithPrec -1
                            let s2 = vuS |> ValueUnit.toStringDecimalDutchShortWithPrec -1

                            if not b then
                                Some b, $"{m.doseRule.Generic}\t{r}\t{p}\tfrequenties {s1} niet gelijk aan {s2}"
                            else
                                Some b, $"{m.doseRule.Generic}\t{r}\t{p}\tfrequenties {s1} is subset van {s2}"

                    let quantityChecks () =
                        [|
                            dl.genForm.Quantity |> inRangeOf "keer dosering" gstand.quantityNorm

                            dl.genForm.Quantity |> inRangeOf "keer dosering" gstand.quantityAbs

                            match dl.genForm.QuantityAdjust |> checkAdjustUnit gstand.quantityAdjustNorm with
                            | None -> ()
                            | Some adj ->
                                let adj = adj |> unitToString

                                dl.genForm.QuantityAdjust
                                |> inRangeOf $"keer dosering per %s{adj}" gstand.quantityAdjustNorm

                            match dl.genForm.QuantityAdjust |> checkAdjustUnit gstand.quantityAdjustAbs with
                            | None -> ()
                            | Some adj ->
                                let adj = adj |> unitToString

                                dl.genForm.QuantityAdjust
                                |> inRangeOf $"keer dosering per %s{adj}" gstand.quantityAdjustAbs

                            let mm = dl.genForm.QuantityAdjust |> DoseLimit.getNormDose |> toMinMax

                            match mm |> checkAdjustUnit gstand.quantityAdjustNorm with
                            | None -> ()
                            | Some adj ->
                                let adj = adj |> unitToString
                                mm |> inRangeOf $"keer dosering per %s{adj}" gstand.quantityAdjustNorm

                            match mm |> checkAdjustUnit gstand.quantityAdjustAbs with
                            | None -> ()
                            | Some adj ->
                                let adj = adj |> unitToString
                                mm |> inRangeOf $"keer dosering per %s{adj}" gstand.quantityAdjustAbs
                        |]

                    let perTimeChecks () =
                        [|
                            dl.genForm.PerTime |> inRangeOf "dosering per <TIMEUNIT>" gstand.perTimeNorm

                            dl.genForm.PerTime |> inRangeOf "dosering per <TIMEUNIT>" gstand.perTimeAbs

                            match dl.genForm.PerTimeAdjust |> checkAdjustUnit gstand.perTimeAdjustNorm with
                            | None -> ()
                            | Some adj ->
                                let adj = adj |> unitToString

                                dl.genForm.PerTimeAdjust
                                |> inRangeOf $"dosering per %s{adj} per <TIMEUNIT>" gstand.perTimeAdjustNorm

                            match dl.genForm.PerTimeAdjust |> checkAdjustUnit gstand.perTimeAdjustAbs with
                            | None -> ()
                            | Some adj ->
                                let adj = adj |> unitToString

                                dl.genForm.PerTimeAdjust
                                |> inRangeOf $"dosering per %s{adj} per <TIMEUNIT>" gstand.perTimeAdjustAbs

                            let mm = dl.genForm.PerTimeAdjust |> DoseLimit.getNormDose |> toMinMax

                            match mm |> checkAdjustUnit gstand.perTimeAdjustNorm with
                            | None -> ()
                            | Some adj ->
                                let adj = adj |> unitToString
                                mm |> inRangeOf $"dosering per %s{adj} per <TIMEUNIT>" gstand.perTimeAdjustNorm

                            match mm |> checkAdjustUnit gstand.perTimeAdjustAbs with
                            | None -> ()
                            | Some adj ->
                                let adj = adj |> unitToString
                                mm |> inRangeOf $"dosering per %s{adj} per <TIMEUNIT>" gstand.perTimeAdjustAbs
                        |]

                    let rateChecks () =
                        [|
                            dl.genForm.Rate |> inRangeOf "dosering per <TIMEUNIT>" rates.rateNorm

                            dl.genForm.Rate |> inRangeOf "dosering per <TIMEUNIT>" rates.rateAbs

                            match dl.genForm.RateAdjust |> checkAdjustUnit rates.rateAdjustNorm with
                            | None -> ()
                            | Some adj ->
                                let adj = adj |> unitToString

                                dl.genForm.RateAdjust
                                |> inRangeOf $"dosering per %s{adj} per <TIMEUNIT>" rates.rateAdjustNorm

                            match dl.genForm.RateAdjust |> checkAdjustUnit rates.rateAdjustAbs with
                            | None -> ()
                            | Some adj ->
                                let adj = adj |> unitToString

                                dl.genForm.RateAdjust
                                |> inRangeOf $"dosering per %s{adj} per <TIMEUNIT>" rates.rateAdjustAbs

                            let mm = dl.genForm.RateAdjust |> DoseLimit.getNormDose |> toMinMax

                            match mm |> checkAdjustUnit rates.rateAdjustNorm with
                            | None -> ()
                            | Some adj ->
                                let adj = adj |> unitToString
                                mm |> inRangeOf $"dosering per %s{adj} per <TIMEUNIT>" rates.rateAdjustNorm

                            match mm |> checkAdjustUnit rates.rateAdjustAbs with
                            | None -> ()
                            | Some adj ->
                                let adj = adj |> unitToString
                                mm |> inRangeOf $"dosering per %s{adj} per <TIMEUNIT>" rates.rateAdjustAbs
                        |]

                    // "non matching adjust units" detection. Only fires when both
                    // sides of the (DoseType-applicable) adjust ranges carry a
                    // detectable unit AND no pair of them is in the same unit
                    // group. Emits at most one row per DoseLimit.
                    let mismatchRow () =
                        let gstandSides =
                            [
                                if runQuantity then
                                    yield gstand.quantityAdjustNorm
                                    yield gstand.quantityAdjustAbs
                                if runPerTime then
                                    yield gstand.perTimeAdjustNorm
                                    yield gstand.perTimeAdjustAbs
                                if runRate then
                                    yield rates.rateAdjustNorm
                                    yield rates.rateAdjustAbs
                            ]
                            |> List.choose (fun mm -> getAdjUnit mm |> Option.map (fun u -> mm, u))

                        let genFormSides =
                            [
                                if runQuantity then yield dl.genForm.QuantityAdjust
                                if runPerTime then yield dl.genForm.PerTimeAdjust
                                if runRate then yield dl.genForm.RateAdjust
                            ]
                            |> List.choose (fun mm -> getAdjUnit mm |> Option.map (fun u -> mm, u))

                        match gstandSides, genFormSides with
                        | [], _
                        | _, [] -> None
                        | gs, gf ->
                            let anyMatch =
                                gf
                                |> List.exists (fun (gfMm, _) ->
                                    gs
                                    |> List.exists (fun (gsMm, _) ->
                                        checkAdjustUnit gfMm gsMm |> Option.isSome
                                    )
                                )

                            if anyMatch then
                                None
                            else
                                let gfU = gf |> List.head |> snd |> unitToString
                                let gsU = gs |> List.head |> snd |> unitToString

                                Some(
                                    Some false,
                                    $"{gstand.doseLimitTarget}\t{r}\t{p}\tnon matching adjust units (genForm: %s{gfU}, gstand: %s{gsU})"
                                )

                    [|
                        if runFrequencies then
                            yield freqRow ()
                        if runQuantity then
                            yield! quantityChecks ()
                        if runPerTime then
                            yield! perTimeChecks ()
                        if runRate then
                            yield! rateChecks ()
                        match mismatchRow () with
                        | Some entry -> yield entry
                        | None -> ()
                    |]
        )
        |> fun xs ->
            let didNotPass =
                xs
                |> Array.choose (function
                    | Some false, s -> Some s
                    | _ -> None)

            let didPass =
                xs
                |> Array.choose (function
                    | Some true, s -> Some s
                    | _ -> None)

            {| m with
                didNotPass = didNotPass
                didPass = didPass
            |}


let checkedRules =
    rules
    |> Array.map (Check.checkDoseRule (provider.GetRouteMappings ()) pat)


// Before the fix: "empty" range comparisons such as
//   aciclovir\tINTRAVENEUS\tleeftijd 3 maanden tot 18 jaar\tkeer dosering:   in bereik van
// were reported as passing even though both testRange and refRange were
// MinMax.empty (no values compared). After the fix, only the frequency
// subset check remains in didPass for aciclovir IV rules.
printfn "--- didPass ---"
checkedRules |> Array.iter (_.didPass >> (String.concat "\n") >> (printfn "%s"))

printfn "--- didNotPass ---"
checkedRules |> Array.iter (_.didNotPass >> (String.concat "\n") >> (printfn "%s"))
