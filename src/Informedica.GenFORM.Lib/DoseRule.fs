namespace Informedica.GenForm.Lib


module DoseRule =

    open System
    open MathNet.Numerics
    open FsToolkit.ErrorHandling.ResultCE

    open FSharp.Data
    open FSharp.Data.JsonExtensions

    open Informedica.Utils.Lib
    open Informedica.Utils.Lib.BCL
    open Informedica.GenCore.Lib.Ranges

    open Utils


    module Print =


        open Informedica.GenUnits.Lib

        let printFreqs (r: DoseRule) =
            r.Frequencies
            |> Option.map (fun vu -> vu |> Utils.ValueUnit.toString 0)
            |> Option.defaultValue ""


        let printInterval (dr: DoseRule) =
            if dr.IntervalTime = MinMax.empty then
                ""
            else
                dr.IntervalTime
                |> MinMax.toString "min. interval " "min. interval " "max. interval " "max. interval "


        let printTime (dr: DoseRule) =
            if dr.AdministrationTime = MinMax.empty then
                ""
            else
                dr.AdministrationTime |> MinMax.toString "min. " "min. " "max. " "max. "


        let printDuration (dr: DoseRule) =
            if dr.Duration = MinMax.empty then
                ""
            else
                dr.Duration
                |> MinMax.toString "min. duur " "min. duur " "max. duur " "max. duur "


        let printDose wrap (dr: DoseRule) =
            let substDls = dr.ComponentLimits |> Array.collect _.SubstanceLimits

            let formDls = dr.ComponentLimits |> Array.choose _.Limit

            let useSubstDl = substDls |> Array.length > 0
            // only use form dose limits if there are no substance dose limits
            if useSubstDl then substDls else formDls
            |> Array.map (fun dl ->
                let perDose = "/dosis"
                let emptyS = ""
                let printMinMaxDose = DoseLimit.printMinMaxDose ""

                [
                    $"%s{dl.Rate |> printMinMaxDose emptyS}"
                    $"%s{dl.RateAdjust |> printMinMaxDose emptyS}"
                    $"%s{dl.PerTimeAdjust |> printMinMaxDose emptyS}"

                    $"%s{dl.PerTime |> printMinMaxDose emptyS}"
                    $"%s{dl.QuantityAdjust |> printMinMaxDose perDose}"

                    $"%s{dl.Quantity |> printMinMaxDose perDose}"
                ]
                |> List.map String.trim
                |> List.filter (String.IsNullOrEmpty >> not)
                |> String.concat ", "
                |> fun s -> $"%s{dl.DoseLimitTarget |> LimitTarget.toString} %s{wrap}%s{s}{wrap}"
            )


        // get all medications from Kinderformularium
        let kinderFormUrl = "https://www.kinderformularium.nl/geneesmiddelen.json"


        let farmocoTherapeutischKompas =
            "https://www.farmacotherapeutischkompas.nl/bladeren/preparaatteksten/n/GENERIEK#doseringen"


        let private _medications () =
            let res = JsonValue.Load kinderFormUrl

            [
                for v in res do
                    {|
                        id = v?id.AsString()
                        generic = v?generic_name.AsString().Trim().ToLower()
                    |}
            ]
            |> List.distinct


        let getKFMedications = Memoization.memoize _medications


        let getLink = Source.getLink (getKFMedications ())


        /// See for use of anonymous record in
        /// fold: https://github.com/dotnet/fsharp/issues/6699
        let toMarkdown (rules: DoseRule array) =
            let generic_md generic = $"\n\n# %s{generic}\n\n---\n"

            let route_md route products synonyms =
                if synonyms |> String.isNullOrWhiteSpace then
                    $"\n\n### Route: %s{route}\n\n#### Producten\n%s{products}\n"
                else
                    $"\n\n### Route: %s{route}\n\n#### Producten\n%s{products}\n\n#### Synoniemen\n%s{synonyms}\n"

            let product_md product = $"* %s{product}"

            let synonyms_md names =
                if names |> Seq.isEmpty then
                    ""
                else
                    let names = names |> String.concat ", "
                    $"* %s{names}"

            let indication_md indication =
                $"\n\n## Indicatie: %s{indication}\n\n---\n"

            let doseCapt_md = "\n\n#### Doseringen\n\n"

            let dose_md dt dose freqs intv time dur =
                let dt = dt |> DoseType.toDescription

                let freqs =
                    if freqs |> String.isNullOrWhiteSpace then
                        ""
                    else
                        $" in %s{freqs}"

                let s =
                    [
                        if intv |> String.isNullOrWhiteSpace |> not then
                            $" %s{intv}"
                        if time |> String.isNullOrWhiteSpace |> not then
                            $" inloop tijd %s{time}"
                        if dur |> String.isNullOrWhiteSpace |> not then
                            $" %s{dur}"
                    ]
                    |> String.concat ", "
                    |> fun s ->
                        if s |> String.isNullOrWhiteSpace then
                            ""
                        else
                            $" (%s{s |> String.trim})"

                $"* *%s{dt}*: %s{dose}%s{freqs}%s{s}"

            let patient_md patient =
                let patient =
                    if patient |> String.notEmpty then
                        patient
                    else
                        "alle patienten"

                $"\n\n##### Patient: **%s{patient}**\n\n"

            let printDoses (rules: DoseRule array) =
                ("", rules |> Array.groupBy _.DoseType)
                ||> Array.fold (fun acc (dt, ds) ->
                    let pedForm =
                        let link =
                            ds
                            |> Array.tryHead
                            |> Option.bind (fun dr -> dr.Generic.Label |> getLink dr.Source)
                            |> Option.defaultValue "*Lokaal*"

                        ds
                        |> Array.map _.ScheduleText
                        |> Array.distinct
                        |> function
                            | [| s |] -> $"\n\n%s{link}: %s{s}"
                            | _ -> ""

                    ds
                    |> Array.fold
                        (fun acc r ->
                            let dose = r |> printDose "" |> Array.distinct |> String.concat ", "

                            let freqs = r |> printFreqs
                            let intv = r |> printInterval
                            let time = r |> printTime
                            let dur = r |> printDuration

                            let md = dose_md dt dose freqs intv time dur

                            if acc |> String.containsCapsInsens md then
                                acc // prevent duplicate doserule per form print
                            else
                                $"%s{acc}\n%s{md}%s{pedForm}"

                        )
                        acc
                )

            ({|
                md = ""
                rules = [||]
             |},
             rules |> Array.groupBy _.Generic)
            ||> Array.fold (fun acc (generic, rs) ->
                {| acc with
                    md = generic_md (generic |> Generic.toString)
                    rules = rs
                |}
                |> fun r ->
                    if r.rules = Array.empty then
                        r
                    else
                        (r, r.rules |> Array.groupBy _.Indication)
                        ||> Array.fold (fun acc (indication, rs) ->
                            {| acc with
                                md = acc.md + (indication_md indication)
                                rules = rs
                            |}
                            |> fun r ->
                                if r.rules = Array.empty then
                                    r
                                else
                                    (r, r.rules |> Array.groupBy _.Route)
                                    ||> Array.fold (fun acc (route, rs) ->
                                        let prods =
                                            rs
                                            |> Array.collect _.ComponentLimits
                                            |> Array.collect _.Products
                                            |> Array.sortBy (fun p ->
                                                p.Substances
                                                |> Array.sumBy (fun s ->
                                                    s.Concentration
                                                    |> Option.map ValueUnit.getValue
                                                    |> Option.bind Array.tryHead
                                                    |> Option.defaultValue 0N
                                                )
                                            )
                                            |> Array.map (fun p ->
                                                if p.GPK |> String.IsNullOrWhiteSpace then
                                                    p.Label
                                                else
                                                    $"{p.GPK} - {p.Label}"
                                                |> product_md
                                            )
                                            |> Array.distinct
                                            |> String.concat "\n"

                                        let synonyms =
                                            rs
                                            |> Array.collect _.ComponentLimits
                                            |> Array.collect _.Products
                                            |> Array.collect _.Synonyms
                                            |> Array.distinct
                                            |> synonyms_md

                                        {| acc with
                                            md = acc.md + (route_md route prods synonyms) + doseCapt_md
                                            rules = rs
                                        |}
                                        |> fun r ->
                                            if r.rules = Array.empty then
                                                r
                                            else
                                                (r,
                                                 r.rules
                                                 |> Array.sortBy (fun d ->
                                                     d.PatientCategory |> PatientCategory.sortBy
                                                 )
                                                 |> Array.groupBy _.PatientCategory)
                                                ||> Array.fold (fun acc (pat, rs) ->
                                                    let doses =
                                                        rs
                                                        |> Array.sortBy (fun r -> r.DoseType |> DoseType.sortBy)
                                                        |> printDoses

                                                    let pat = pat |> PatientCategory.toString

                                                    {| acc with
                                                        rules = rs
                                                        md = acc.md + (patient_md pat) + $"\n{doses}"
                                                    |}
                                                )
                                    )
                        )
            )
            |> _.md


        let printGenerics generics (doseRules: DoseRule[]) =
            doseRules
            |> generics
            |> Array.sort
            |> Array.map (fun g -> doseRules |> Array.filter (fun dr -> dr.Generic = g) |> toMarkdown)


    open Informedica.GenUnits.Lib

    module GenPresProduct = Informedica.ZIndex.Lib.GenPresProduct
    module GenericProduct = Informedica.ZIndex.Lib.GenericProduct


    /// <summary>
    /// Reconstitute the products in a DoseRule that require reconstitution.
    /// </summary>
    /// <param name="mapping"></param>
    /// <param name="dep">The Department to select the reconstitution</param>
    /// <param name="loc">The VenousAccess location to select the reconstitution</param>
    /// <param name="dr">The DoseRule</param>
    let reconstitute mapping loc dep (dr: DoseRule) =
        let warns = ResizeArray<string>()
        let reconstitute = Product.reconstitute mapping loc dep

        let dr =
            { dr with
                ComponentLimits =
                    dr.ComponentLimits
                    |> Array.map (fun dl ->
                        { dl with
                            Products =
                                dl.Products
                                |> Array.collect (fun prod ->
                                    let prods, newWarns = reconstitute dr.Route prod
                                    warns.AddRange(newWarns)
                                    prods
                                )
                        }
                    )
            }

        dr, warns |> Seq.distinct


    let fromTupleInclExcl = MinMax.fromTuple Inclusive Exclusive


    let fromTupleInclIncl = MinMax.fromTuple Inclusive Inclusive


    /// Trim, collapse internal whitespace (tabs/newlines included),
    /// lowercase. Tab is the key separator, so it must not survive
    /// inside a field.
    let normaliseIdField (s: string) =
        if isNull s then
            ""
        else
            System.Text.RegularExpressions.Regex.Replace(s, @"\s+", " ").Trim().ToLowerInvariant()


    /// Hash a key field list to a 12-char lowercase hex, the same
    /// mechanism used for the Pass-4 dose rule data id
    /// (Informedica.NLP.Lib.fsx, Phase4Pure.sha1Short).
    let sha1Short (fields: string list) =
        let joined = fields |> String.concat "\t"
        let bytes = System.Text.Encoding.UTF8.GetBytes joined

        use sha = System.Security.Cryptography.SHA1.Create()

        sha.ComputeHash bytes
        |> Array.take 6
        |> Array.map (fun b -> b.ToString "x2")
        |> String.concat ""


    /// Content hash of a DoseRule's documented identity (Source,
    /// Generic label + grouping form, Route, Indication,
    /// PatientCategory, DoseType including its dose text payload).
    /// Substance and component are intentionally excluded, so the
    /// substance fan-outs of one logical rule share a single Id.
    let hashId (dr: DoseRule) =
        [
            dr.Source |> Source.toString
            dr.Generic.Label |> GenericLabel.toString
            dr.Generic.Form |> PharmaceuticalForm.toString
            dr.Route
            dr.Indication
            dr.PatientCategory |> PatientCategory.toString
            dr.DoseType |> DoseType.toString
        ]
        |> List.map normaliseIdField
        |> sha1Short


    let mapToDoseRule (r: DoseRuleData) =
        let identifySource s =
            match s with
            | _ when s = "FTK" -> s |> Source.identified
            | _ when s = "NKF" -> s |> Source.identified
            | _ -> s |> Source.other

        let toLabel = GenericLabel.toLabel

        let toForm = PharmaceuticalForm.fromString

        try
            {
                Id = r.Id
                DataId = r.Id
                GroupId = r.GrpId
                SortNo = r.SortNo
                Source = r.Source |> identifySource
                SourceText = r.SourceText
                Indication = r.Indication
                Generic =
                    // Label reflects the ORIGINAL source restriction (form/brand/none).
                    let lbl = toLabel r.Generic.Name r.Generic.Form r.Generic.Brand
                    // Grouping form is derived from the rule's attached products
                    // (they all share one pharmaceutical form after the per-form
                    // expansion); fall back to the source form when there are no
                    // products (placeholder).
                    let frm =
                        r.Products
                        |> Array.tryHead
                        |> Option.map (fun p -> p.Form |> toForm)
                        |> Option.defaultValue (r.Generic.Form |> toForm)

                    let ids =
                        match r.Generic.GPKs, r.Generic.HPKs with
                        | _ when r.Generic.GPKs.Length > 0 -> r.Generic.GPKs |> Array.toList |> ProductId.gpks
                        | _ when r.Generic.HPKs.Length > 0 -> r.Generic.HPKs |> Array.toList |> ProductId.hpks
                        | _ -> []

                    Generic.create lbl frm ids
                Route = r.Route
                ScheduleText = r.ScheduleText
                PatientText = r.PatientText
                PatientCategory =
                    let p = r.Patient

                    {
                        Location = None
                        Department =
                            if p.Dep |> String.isNullOrWhiteSpace then
                                None
                            else
                                p.Dep |> Some
                        Gender = p.Gender
                        Age =
                            if p.IsAdult then
                                IsAdult
                            else
                                (p.MinAge, p.MaxAge) |> fromTupleInclExcl (Some Utils.Units.day) |> AbsoluteAge
                        Weight = (p.MinWeight, p.MaxWeight) |> fromTupleInclExcl (Some Utils.Units.weightGram)
                        BSA = (p.MinBSA, p.MaxBSA) |> fromTupleInclExcl (Some Utils.Units.bsaM2)
                        GestAge = (p.MinGestAge, p.MaxGestAge) |> fromTupleInclExcl (Some Utils.Units.day)
                        PMAge = (p.MinPMAge, p.MaxPMAge) |> fromTupleInclExcl (Some Utils.Units.day)
                        Access = AnyAccess
                    }
                DoseType = r.ScheduleData.DoseText |> DoseType.fromString r.ScheduleData.DoseType
                AdjustUnit = r.ScheduleData.AdjustUnit |> Units.adjustUnit
                Frequencies =
                    match r.ScheduleData.FreqUnit |> Units.freqUnit with
                    | None -> None
                    | Some u ->
                        if r.ScheduleData.Freqs |> Array.isEmpty then
                            None
                        else
                            r.ScheduleData.Freqs |> ValueUnit.withUnit u |> Some
                AdministrationTime =
                    (r.ScheduleData.MinTime, r.ScheduleData.MaxTime)
                    |> fromTupleInclIncl (r.ScheduleData.TimeUnit |> Utils.Units.timeUnit)
                IntervalTime =
                    (r.ScheduleData.MinInt, r.ScheduleData.MaxInt)
                    |> fromTupleInclIncl (r.ScheduleData.IntUnit |> Utils.Units.timeUnit)
                Duration =
                    (r.ScheduleData.MinDur, r.ScheduleData.MaxDur)
                    |> fromTupleInclIncl (r.ScheduleData.DurUnit |> Utils.Units.timeUnit)
                FormLimit = None
                ComponentLimits = [||]
                RenalRuleSource = None
            }
            // DataId keeps the source-data back-reference (r.Id); Id is a
            // content hash of the DoseRule's own documented identity.
            |> fun dr -> { dr with Id = hashId dr }
            |> Ok
        with exn ->
            ("mapToDoseRule", Some exn) |> ErrorMsg |> Error


    /// Reduce a row's product narrowing to a single mechanism by precedence
    /// HPKs > GPKs > Brand > Form: keep the highest-precedence field that is
    /// present and clear every lower-precedence one. Rows with no narrowing are
    /// left unchanged (they remain generic-wide rules).
    let withSingleNarrowing (g: GenericData) : GenericData =
        let hasHpks = g.HPKs |> Array.isEmpty |> not
        let hasGpks = g.GPKs |> Array.isEmpty |> not
        let hasBrand = g.Brand |> String.notEmpty

        if hasHpks then
            { g with
                GPKs = [||]
                Brand = ""
                Form = ""
            }
        elif hasGpks then
            { g with
                Brand = ""
                Form = ""
            }
        elif hasBrand then
            { g with Form = "" }
        else
            g


    let parseDoseRuleData (data: string[][]) : Result<_, Message list> =
        try
            data
            |> fun data ->
                let getColumn = data |> Array.head |> Csv.getStringColumn

                data
                |> Array.tail
                |> Array.distinctBy (fun row -> row |> Array.tail)
                |> Array.map (fun r ->
                    let get = getColumn r

                    let getOpt col =
                        try
                            get col
                        with _ ->
                            ""

                    let getInt = getOpt >> Int32.tryParse
                    let toBrOpt = BigRational.toBrs >> Array.tryHead

                    let getBool =
                        get
                        >> fun s ->
                            let v = s.Trim().ToLowerInvariant()
                            v = "true" || v = "x" || v = "1" || v = "yes"

                    {
                        Id = getOpt "Id"
                        GrpId = getOpt "GrpId"
                        SortNo = getInt "SortNo" |> Option.defaultValue 1
                        Source = get "Source"
                        Generic =
                            {
                                Name = get "Generic"
                                Form = get "Form"
                                Brand = get "Brand"
                                GPKs =
                                    get "GPKs"
                                    |> String.splitAt ';'
                                    |> Array.map String.trim
                                    |> Array.filter String.notEmpty
                                    |> Array.distinct
                                HPKs =
                                    get "HPKs"
                                    |> String.splitAt ';'
                                    |> Array.map String.trim
                                    |> Array.filter String.notEmpty
                                    |> Array.distinct
                            }
                        Route = get "Route"
                        Indication = get "Indication"
                        SourceText = getOpt "SourceText"
                        PatientText = getOpt "PatientText"
                        Patient =
                            {
                                Location = getOpt "Loc"
                                Dep = get "Dep"
                                IsAdult = getBool "IsAdult"
                                Gender = get "Gender" |> Gender.fromString
                                MinAge = get "MinAge" |> toBrOpt
                                MaxAge = get "MaxAge" |> toBrOpt
                                MinWeight = get "MinWeight" |> toBrOpt
                                MaxWeight = get "MaxWeight" |> toBrOpt
                                MinBSA = get "MinBSA" |> toBrOpt
                                MaxBSA = get "MaxBSA" |> toBrOpt
                                MinGestAge = get "MinGestAge" |> toBrOpt
                                MaxGestAge = get "MaxGestAge" |> toBrOpt
                                MinPMAge = get "MinPMAge" |> toBrOpt
                                MaxPMAge = get "MaxPMAge" |> toBrOpt
                            }
                        ScheduleText = getOpt "ScheduleText"
                        ScheduleData =
                            {
                                DoseType = get "DoseType"
                                DoseText = get "DoseText"
                                Freqs = get "Freqs" |> BigRational.toBrs
                                AdjustUnit = get "AdjustUnit"
                                FreqUnit = get "FreqUnit"
                                RateUnit = get "RateUnit"
                                MinTime = get "MinTime" |> toBrOpt
                                MaxTime = get "MaxTime" |> toBrOpt
                                TimeUnit = get "TimeUnit"
                                MinInt = get "MinInt" |> toBrOpt
                                MaxInt = get "MaxInt" |> toBrOpt
                                IntUnit = get "IntUnit"
                                MinDur = get "MinDur" |> toBrOpt
                                MaxDur = get "MaxDur" |> toBrOpt
                                DurUnit = get "DurUnit"
                                DoseLimitData =
                                    {
                                        CmpBased = getBool "CmpBased"
                                        Component = get "Component"
                                        Substance = get "Substance"
                                        DoseUnit = get "DoseUnit"
                                        MinQty = get "MinQty" |> toBrOpt
                                        MaxQty = get "MaxQty" |> toBrOpt
                                        MinQtyAdj = get "MinQtyAdj" |> toBrOpt
                                        MaxQtyAdj = get "MaxQtyAdj" |> toBrOpt
                                        MinPerTime = get "MinPerTime" |> toBrOpt
                                        MaxPerTime = get "MaxPerTime" |> toBrOpt
                                        MinPerTimeAdj = get "MinPerTimeAdj" |> toBrOpt
                                        MaxPerTimeAdj = get "MaxPerTimeAdj" |> toBrOpt
                                        MinRate = get "MinRate" |> toBrOpt
                                        MaxRate = get "MaxRate" |> toBrOpt
                                        MinRateAdj = get "MinRateAdj" |> toBrOpt
                                        MaxRateAdj = get "MaxRateAdj" |> toBrOpt
                                    }
                            }
                        Products = [||]
                    }
                )
            // Reduce each row to a single product narrowing by precedence
            // HPKs > GPKs > Brand > Form before deduping.
            |> Array.map (fun d -> { d with Generic = d.Generic |> withSingleNarrowing })
            |> Array.distinct
            |> Ok
        with exn ->
            Result.createError "getDataResult" exn


    let getData dataUrlId : Result<_, Message list> =
        Web.getDataFromSheet dataUrlId "DoseRules" |> parseDoseRuleData


    /// Pretty print dose rule data for  logging
    let doseRuleDataToString (dd: DoseRuleData) =
        let showOpt = Option.map string >> Option.defaultValue "-"

        let showStr s =
            if s |> String.isNullOrWhiteSpace then "-" else s

        let showArray toStr xs =
            if xs |> Array.isEmpty then
                "-"
            else
                xs |> Array.map toStr |> String.concat ","

        // Bind the deeply nested schedule and dose-limit fields to short locals
        // so the interpolated strings below stay readable (and Fantomas does not
        // wrap the long member-access chains into unreadable indentation).
        let sd = dd.ScheduleData
        let dl = sd.DoseLimitData

        let gen = dd.Generic.Name |> showStr
        let comp = dl.Component |> showStr
        let subst = dl.Substance |> showStr
        let gpks = dd.Generic.GPKs |> showArray id

        let route = dd.Route |> showStr
        let form = dd.Generic.Form |> showStr
        let brand = dd.Generic.Brand |> showStr
        let dept = dd.Patient.Dep |> showStr
        let ind = dd.Indication |> showStr

        let doseType = sd.DoseType |> showStr
        let doseText = sd.DoseText |> showStr
        let doseUnit = dl.DoseUnit |> showStr
        let adjUnit = sd.AdjustUnit |> showStr

        let freq = sd.Freqs |> showArray string
        let freqUnit = sd.FreqUnit |> showStr
        let maxTime = sd.MaxTime |> showOpt
        let timeUnit = sd.TimeUnit |> showStr
        let maxRate = dl.MaxRate |> showOpt
        let maxRateAdj = dl.MaxRateAdj |> showOpt
        let rateUnit = sd.RateUnit |> showStr

        let src = dd.Source |> showStr

        [
            $"Id   : Gen=%s{gen} | Comp=%s{comp} | Subst=%s{subst} | GPKs=%s{gpks}"
            $"Ctx  : Route=%s{route} | Form=%s{form} | Brand=%s{brand} | Dept=%s{dept} | Ind=%s{ind}"
            $"Dose : Type=%s{doseType} | Text=%s{doseText} | DoseUnit=%s{doseUnit} | AdjUnit=%s{adjUnit}"
            $"Rate : Freq=%s{freq} %s{freqUnit} | MaxTime=%s{maxTime} %s{timeUnit} | MaxRate=%s{maxRate} | MaxRateAdj=%s{maxRateAdj} %s{rateUnit}"
            $"Meta : Src=%s{src}"
        ]
        |> List.map String.trim
        |> List.filter String.notEmpty
        |> String.concat "\n"


    /// Pure: determine whether a raw DoseRuleData row is valid, returning the
    /// validity flag together with an optional concise, dedup-friendly warning
    /// (Some when invalid and the dose type is non-empty). The warning combines
    /// the invalid-reason details and any dose-type parse warning. No console IO,
    /// so callers can surface the warning through the resources Message list.
    let doseRuleDataValidity (dd: DoseRuleData) =
        let missing cond reason = if cond then None else Some reason

        let doseType, doseTypeWarn =
            DoseType.parse dd.ScheduleData.DoseType dd.ScheduleData.DoseText

        let isValid =
            match doseType with
            | NoDoseType -> false
            | Once _ -> true
            | OnceTimed _ ->
                (dd.ScheduleData.MaxTime.IsSome && dd.ScheduleData.TimeUnit |> String.notEmpty)
                || dd.ScheduleData.DoseLimitData.MaxRate.IsSome
                || dd.ScheduleData.DoseLimitData.MaxRateAdj.IsSome
            | Discontinuous _ ->
                dd.ScheduleData.Freqs |> Array.length > 0
                && dd.ScheduleData.FreqUnit |> String.notEmpty
            | Timed _ ->
                dd.ScheduleData.Freqs |> Array.length > 0
                && dd.ScheduleData.FreqUnit |> String.notEmpty
                && dd.ScheduleData.MaxTime.IsSome
                && dd.ScheduleData.TimeUnit |> String.notEmpty
            | Continuous _ ->
                dd.ScheduleData.RateUnit |> String.notEmpty
                && (dd.ScheduleData.DoseLimitData.MaxRate.IsSome
                    || dd.ScheduleData.DoseLimitData.MaxRateAdj.IsSome)

        let invalidReasons =
            if isValid then
                []
            else
                match doseType with
                | NoDoseType -> []
                | Once _ -> []
                | OnceTimed _ ->
                    [
                        missing
                            ((dd.ScheduleData.MaxTime.IsSome && dd.ScheduleData.TimeUnit |> String.notEmpty)
                             || dd.ScheduleData.DoseLimitData.MaxRate.IsSome
                             || dd.ScheduleData.DoseLimitData.MaxRateAdj.IsSome)
                            "MaxTime (with TimeUnit) or MaxRate or MaxRateAdj is missing"
                    ]
                    |> List.choose id
                | Discontinuous _ ->
                    [
                        missing (dd.ScheduleData.Freqs |> Array.length > 0) "Frequencies is empty"
                        missing (dd.ScheduleData.FreqUnit |> String.notEmpty) "FreqUnit is missing"
                    ]
                    |> List.choose id
                | Timed _ ->
                    [
                        missing (dd.ScheduleData.Freqs |> Array.length > 0) "Frequencies is empty"
                        missing (dd.ScheduleData.FreqUnit |> String.notEmpty) "FreqUnit is missing"
                        missing dd.ScheduleData.MaxTime.IsSome "MaxTime is missing"
                        missing (dd.ScheduleData.TimeUnit |> String.notEmpty) "TimeUnit is missing"
                    ]
                    |> List.choose id
                | Continuous _ ->
                    [
                        missing (dd.ScheduleData.RateUnit |> String.notEmpty) "RateUnit is missing"
                        missing
                            (dd.ScheduleData.DoseLimitData.MaxRate.IsSome
                             || dd.ScheduleData.DoseLimitData.MaxRateAdj.IsSome)
                            "both MaxRate and MaxRateAdj are missing"
                    ]
                    |> List.choose id

        let warning =
            if not isValid && dd.ScheduleData.DoseType |> String.notEmpty then
                let why = invalidReasons @ (doseTypeWarn |> Option.toList) |> String.concat "; "

                let why =
                    if why |> String.isNullOrWhiteSpace then
                        "no specific reason"
                    else
                        why

                Some $"%s{dd.Generic.Name} | %s{dd.Route} | %s{why}"
            else
                None

        isValid, warning


    /// Pure predicate: true when the raw DoseRuleData row is valid. Thin wrapper
    /// over <c>doseRuleDataValidity</c> that discards the warning. No console IO.
    let doseRuleDataIsValid (dd: DoseRuleData) = dd |> doseRuleDataValidity |> fst


    /// Selection key that groups raw dose rule rows into independent units of
    /// work: rows sharing this key describe the same clinical context and are
    /// expanded against products together.
    type ProductGroupKey =
        {
            Source: string
            Indication: string
            Generic: GenericData
            Patient: PatientCategoryData
            Route: string
        }


    /// Keep only the valid rows; return them together with the deduped validity
    /// warnings (invalid dose rule data / invalid dose type), as data — no IO.
    let partitionValidRows (data: DoseRuleData[]) : DoseRuleData[] * Message list =
        let validated = data |> Array.map (fun d -> d, d |> doseRuleDataValidity)

        let validRows =
            validated
            |> Array.choose (fun (d, (isValid, _)) -> if isValid then Some d else None)

        let warnings =
            validated
            |> Array.choose (fun (_, (_, warn)) -> warn)
            |> Array.distinct
            |> Array.map Warning
            |> Array.toList

        validRows, warnings


    /// The grouping key of a row. Rows are normalised to a single narrowing at
    /// parse time (withSingleNarrowing), so a row never restricts both form and
    /// brand here.
    let groupKey (d: DoseRuleData) : ProductGroupKey =
        {
            Source = d.Source
            Indication = d.Indication
            Generic = d.Generic
            Patient = d.Patient
            Route = d.Route
        }


    /// Group rows into independent (key, rows) units of work.
    let groupRows (data: DoseRuleData[]) : (ProductGroupKey * DoseRuleData[])[] = data |> Array.groupBy groupKey


    /// Cheap pre-filter: keep only products whose Generic matches a component
    /// used somewhere in the group, before the per-row narrowing.
    let candidateProducts (prods: ProductComponent[]) (rs: DoseRuleData[]) : ProductComponent[] =
        let cmps = rs |> Array.map _.ScheduleData.DoseLimitData.Component

        prods
        |> Array.filter (fun p -> cmps |> Array.exists (String.equalsCapInsens p.Generic))


    /// Apply a row's narrowing (form/brand/gpks/hpks) to candidate products.
    let matchRowProducts routeMapping route (g: GenericData) cmp (prods: ProductComponent[]) : ProductComponent[] =
        prods |> Product.filter routeMapping route cmp g.Form g.Brand g.GPKs g.HPKs


    /// Expand a row over its matched products: one row variant per pharmaceutical
    /// Form and per FormUnit unit-group, each carrying that bucket's products.
    /// The original restriction stays on the generic; the grouping form is derived
    /// from the attached products in mapToDoseRule.
    let expandRowByForm
        routeMapping
        (grp: ProductGroupKey)
        (r: DoseRuleData)
        (matched: ProductComponent[])
        : DoseRuleData[]
        =
        let cmp = r.ScheduleData.DoseLimitData.Component

        matched
        |> Array.groupBy _.Form
        |> Array.collect (fun (_, formProds) ->
            // ensure all products in a variant share one unit group
            formProds
            |> Array.groupBy (_.FormUnit >> ValueUnit.Group.unitToGroup)
            |> Array.collect (fun (_, ps) ->
                ps
                |> Array.map (fun product ->
                    { r with
                        Generic = grp.Generic
                        Products =
                            ps
                            |> Product.filter
                                routeMapping
                                grp.Route
                                cmp
                                product.Form
                                ""
                                r.Generic.GPKs
                                r.Generic.HPKs
                    }
                )
                |> Array.distinct
            )
        )


    /// No-products fallback row: a single synthetic product built from the
    /// substances of the group's rows that share this row's component.
    let placeholderRow (grp: ProductGroupKey) (rs: DoseRuleData[]) (r: DoseRuleData) : DoseRuleData =
        let cmp = r.ScheduleData.DoseLimitData.Component

        let substances =
            rs
            |> Array.filter (_.ScheduleData.DoseLimitData.Component >> String.equalsCapInsens cmp)
            |> Array.map _.ScheduleData.DoseLimitData.Substance
            |> Array.filter String.notEmpty
            |> Array.distinct

        { r with
            Products =
                [|
                    substances |> Product.create grp.Generic.Name grp.Generic.Form grp.Route
                |]
        }


    /// Deduplication key + message for a row whose narrowing matched no products.
    let noProductsWarning (grp: ProductGroupKey) (r: DoseRuleData) : string * string =
        let key = $"{grp.Generic.Name} {grp.Route}"

        let msg =
            $"no products found for {r.ScheduleData.DoseLimitData.Component} - {r.Generic.Form} - {r.Generic.Brand} - {r.Generic.GPKs |> Array.toList} - {r.Generic.HPKs |> Array.toList}"

        key, msg


    /// Pure: expand every row of a group against the products, collecting the
    /// expanded rows and any (key, message) no-products warnings (no IO, no
    /// shared mutable state).
    let processGroup
        routeMapping
        (prods: ProductComponent[])
        (grp: ProductGroupKey, rs: DoseRuleData[])
        : DoseRuleData[] * (string * string) list
        =
        let candidates = candidateProducts prods rs

        let rowChunks, warns =
            rs
            |> Array.fold
                (fun (rowAcc, warnAcc) r ->
                    let matched =
                        candidates
                        |> matchRowProducts routeMapping grp.Route grp.Generic r.ScheduleData.DoseLimitData.Component

                    if matched |> Array.isEmpty then
                        [| placeholderRow grp rs r |] :: rowAcc, noProductsWarning grp r :: warnAcc
                    else
                        expandRowByForm routeMapping grp r matched :: rowAcc, warnAcc
                )
                ([], [])

        (rowChunks |> List.rev |> List.toArray |> Array.collect id), (warns |> List.rev)


    /// Run all groups chunked + in parallel, concatenating rows and warnings in
    /// deterministic group order.
    let runGroupsParallel
        routeMapping
        (prods: ProductComponent[])
        (groups: (ProductGroupKey * DoseRuleData[])[])
        : DoseRuleData[] * (string * string) list
        =
        let results =
            groups
            |> Array.chunkBySize Parallel.totalWorders
            |> Array.collect (Array.map (fun g -> async { return processGroup routeMapping prods g }))
            |> Async.Parallel
            |> Async.RunSynchronously

        results |> Array.collect fst, results |> Array.collect (snd >> List.toArray) |> Array.toList


    /// Dedup no-products warnings by key (first occurrence wins), sort by key,
    /// format as "key: message" warnings.
    let dedupeNoProductWarnings (warns: (string * string) list) : Message list =
        warns
        |> List.fold
            (fun (seen: Set<string>, acc) (k, m) ->
                if seen.Contains k then
                    seen, acc
                else
                    seen.Add k, (k, m) :: acc
            )
            (Set.empty, [])
        |> snd
        |> List.sortBy fst
        |> List.map (fun (k, m) -> $"%s{k}: %s{m}" |> Warning)


    let addProductsWithWarnings
        (prods: ProductComponent[])
        routeMapping
        data
        : Result<DoseRuleData[] * Message list, Message list>
        =
        let validRows, validityWarnings = partitionValidRows data

        let rows, noProdWarns =
            validRows |> groupRows |> runGroupsParallel routeMapping prods

        (rows, validityWarnings @ dedupeNoProductWarnings noProdWarns) |> Ok


    /// Stable wrapper: same product matching, warnings discarded. Kept so
    /// existing callers (and the NLP Phase-5 diagnose path) that only need the
    /// data keep their `Result<DoseRuleData[], Message list>` signature.
    let addProducts (prods: ProductComponent[]) routeMapping data : Result<DoseRuleData[], Message list> =
        addProductsWithWarnings prods routeMapping data |> Result.map fst


    let addFormLimits routeMapping formRoutes (dr: DoseRule) =
        let prods = dr.ComponentLimits |> Array.collect _.Products

        let droplets =
            prods
            |> Array.filter (fun p -> p.Form |> String.containsCapsInsens "druppel")
            |> Array.choose _.Divisible
            |> Array.distinct
            |> Array.tryExactlyOne

        let setDroplet vu =
            let v, u = vu |> ValueUnit.get

            match droplets with
            | None -> vu
            | Some m -> u |> Units.Volume.dropletSetDropsPerMl m |> ValueUnit.withValue v

        if dr.Generic.Form |> PharmaceuticalForm.toString |> String.isNullOrWhiteSpace then
            dr
        else
            prods
            |> Array.map _.FormUnit
            |> Array.tryExactlyOne
            |> Option.defaultValue NoUnit
            |> Mapping.filterFormRoutes
                routeMapping
                formRoutes
                dr.Route
                (dr.Generic.Form |> PharmaceuticalForm.toString)
            |> Array.map (fun rsu ->
                { DoseLimit.limit with
                    DoseLimitTarget = OrderableLimitTarget
                    Quantity =
                        {
                            Min = rsu.MinDoseQty |> Option.map Limit.Inclusive
                            Max = rsu.MaxDoseQty |> Option.map Limit.Inclusive
                        }
                    QuantityAdjust =
                        match dr.AdjustUnit with
                        | Some unt when unt |> Units.eqsUnit Units.Weight.kiloGram ->
                            {
                                Min = rsu.MinDoseQtyPerKg |> Option.map Limit.Inclusive
                                Max = rsu.MaxDoseQtyPerKg |> Option.map Limit.Inclusive
                            }
                        | _ -> DoseLimit.limit.QuantityAdjust
                }
                |> fun dl ->
                    if droplets |> Option.isNone then
                        dl
                    else
                        { dl with
                            DoseUnit =
                                droplets
                                |> Option.map Units.Volume.dropletWithDropsPerMl
                                |> Option.defaultValue rsu.DoseUnit
                            Quantity =
                                {
                                    Min = dl.Quantity.Min |> Option.map (Limit.apply setDroplet setDroplet)
                                    Max = dl.Quantity.Max |> Option.map (Limit.apply setDroplet setDroplet)
                                }

                        }
            )
            |> Array.distinct
            |> Array.tryExactlyOne
            |> function
                | None -> dr
                | Some formLimit ->
                    if formLimit |> DoseLimit.hasNoLimits then
                        dr
                    else
                        { dr with FormLimit = Some formLimit }


    let getDoseLimits (rs: DoseRuleData[]) =
        rs
        |> Array.map (fun r ->
            // the adjust unit
            let adj = r.ScheduleData.AdjustUnit |> Utils.Units.adjustUnit
            // the dose unit
            let du = r.ScheduleData.DoseLimitData.DoseUnit |> UnitsParse.fromString
            // the adjusted dose unit
            let duAdj =
                match adj, du with
                | Some adj, Some du -> du |> ValueUnit.per adj |> Some
                | _ -> None
            // the time unit
            let tu = r.ScheduleData.FreqUnit |> Utils.Units.timeUnit
            // the dose unit per time unit
            let duTime =
                match du, tu with
                | Some du, Some tu -> du |> ValueUnit.per tu |> Some
                | _ -> None
            // the adjusted dose unit per time unit
            let duAdjTime =
                match duAdj, tu with
                | Some duAdj, Some tu -> duAdj |> ValueUnit.per tu |> Some
                | _ -> None
            // the rate unit
            let ru = r.ScheduleData.RateUnit |> UnitsParse.fromString
            // the dose unit per rate unit
            let duRate =
                match du, ru with
                | Some du, Some ru -> du |> ValueUnit.per ru |> Some
                | _ -> None
            // the adjusted dose unit per rate unit
            let duAdjRate =
                match duAdj, ru with
                | Some duAdj, Some ru -> duAdj |> ValueUnit.per ru |> Some
                | _ -> None

            {
                DoseLimitTarget =
                    if r.ScheduleData.DoseLimitData.Substance |> String.isNullOrWhiteSpace then
                        r.ScheduleData.DoseLimitData.Component |> ComponentLimitTarget
                    else
                        r.ScheduleData.DoseLimitData.Substance |> SubstanceLimitTarget
                AdjustUnit = adj
                DoseUnit = du |> Option.defaultValue NoUnit
                Quantity =
                    (r.ScheduleData.DoseLimitData.MinQty, r.ScheduleData.DoseLimitData.MaxQty)
                    |> fromTupleInclIncl du
                QuantityAdjust =
                    (r.ScheduleData.DoseLimitData.MinQtyAdj, r.ScheduleData.DoseLimitData.MaxQtyAdj)
                    |> fromTupleInclIncl duAdj
                PerTime =
                    (r.ScheduleData.DoseLimitData.MinPerTime, r.ScheduleData.DoseLimitData.MaxPerTime)
                    |> fromTupleInclIncl duTime
                PerTimeAdjust =
                    (r.ScheduleData.DoseLimitData.MinPerTimeAdj, r.ScheduleData.DoseLimitData.MaxPerTimeAdj)
                    |> fromTupleInclIncl duAdjTime
                Rate =
                    (r.ScheduleData.DoseLimitData.MinRate, r.ScheduleData.DoseLimitData.MaxRate)
                    |> fromTupleInclIncl duRate
                RateAdjust =
                    (r.ScheduleData.DoseLimitData.MinRateAdj, r.ScheduleData.DoseLimitData.MaxRateAdj)
                    |> fromTupleInclIncl duAdjRate
            }
        )


    let addDoseLimits routeMapping formRoutes (rs: DoseRuleData[]) (dr: DoseRule) =
        // Refine the no-restriction label: when the rule has exactly one component
        // whose name equals the generic name, the generic matches a component
        // directly, so the canonical name (the component's substances) applies
        // instead of a shorthand. Multi-component rules and form/brand-restricted
        // labels are left untouched.
        let refineLabel (dr: DoseRule) =
            match dr.Generic.Label with
            | Shorthand g ->
                match dr.ComponentLimits with
                | [| cl |] when cl.Name |> String.equalsCapInsens g ->
                    let substs =
                        cl.SubstanceLimits
                        |> Array.map (_.DoseLimitTarget >> LimitTarget.toString)
                        |> Array.filter String.notEmpty
                        |> Array.distinct
                        |> Array.toList

                    let substs = if substs |> List.isEmpty then [ cl.Name ] else substs
                    { dr with DoseRule.Generic.Label = GenericLabel.fromCanonical substs }
                | _ -> dr
            | _ -> dr

        { dr with
            ComponentLimits =
                rs
                |> Array.groupBy _.ScheduleData.DoseLimitData.Component
                |> Array.map (fun (cmp, rs) ->
                    let lim =
                        rs
                        // if no substance the dose limit is a component limit
                        |> Array.filter (_.ScheduleData.DoseLimitData.Substance >> String.isNullOrWhiteSpace)
                        |> getDoseLimits
                        |> Array.filter (DoseLimit.hasNoLimits >> not)
                        |> Array.tryExactlyOne

                    {
                        Name = cmp
                        ProductIds =
                            rs
                            |> Array.collect (_.Generic.GPKs >> Array.toList >> ProductId.gpks >> List.toArray)
                        Limit = lim
                        Products =
                            let dosis = "dosis" |> Units.General.general

                            rs
                            |> Array.collect _.Products
                            |> Array.filter (fun p ->
                                match lim with
                                | None -> true
                                | Some l ->
                                    // special case where dosis is the dose unit
                                    l.DoseUnit |> Units.eqsUnit dosis
                                    ||
                                    // special case where dosis is count unit
                                    l.DoseUnit |> ValueUnit.Group.eqsGroup Units.Count.times
                                    || l.DoseUnit |> ValueUnit.Group.eqsGroup p.FormUnit
                            )
                            |> Array.distinct
                        SubstanceLimits =
                            rs
                            // if a substance the limit is a substance limit
                            |> Array.filter (
                                _.ScheduleData.DoseLimitData.Substance >> String.isNullOrWhiteSpace >> not
                            )
                            |> getDoseLimits
                    }
                )
        }
        |> refineLabel
        |> addFormLimits routeMapping formRoutes


    /// <summary>
    /// Pure: build the DoseRules from raw DoseRuleData plus the route mapping,
    /// form/route table and the product set. Performs no IO and no console/timing
    /// side effects, so it is directly testable. Returns the built rules together
    /// with the "no products found" warnings collected during product matching.
    /// </summary>
    let fromDataWithWarnings
        routeMapping
        formRoutes
        prods
        (data: DoseRuleData[])
        : Result<DoseRule[] * Message list, Message list>
        =
        let addDoseLimits = addDoseLimits routeMapping formRoutes

        result {
            let! data, warns = data |> addProductsWithWarnings prods routeMapping
            // split in ok and error results
            let rules, _ =
                data
                |> Array.map (fun d -> d, d |> mapToDoseRule)
                |> Array.partition (snd >> Result.isOk)
            // process ok results
            let rules =
                let chunkBySize = Parallel.totalWorders

                let grouped =
                    rules |> Array.map (fun (d, r) -> r |> Result.get, d) |> Array.groupBy fst

                grouped
                |> Array.chunkBySize chunkBySize
                |> Array.map (fun rs ->
                    async { return rs |> Array.map (fun (dr, rs) -> dr |> addDoseLimits (rs |> Array.map snd)) }
                )
                |> Async.Parallel
                |> Async.RunSynchronously
                |> Array.collect id

            return rules, warns
        }


    /// Stable wrapper: builds the DoseRules and discards the warnings. Kept so
    /// existing callers keep the `Result<DoseRule[], Message list>` signature.
    let fromData routeMapping formRoutes prods (data: DoseRuleData[]) : Result<DoseRule[], Message list> =
        fromDataWithWarnings routeMapping formRoutes prods data |> Result.map fst


    /// Impure adapter: loads DoseRuleData via the `getData` thunk and delegates
    /// to the pure <c>fromDataWithWarnings</c>, carrying the product warnings.
    /// Kept for existing callers/tests.
    let get getData routeMapping formRoutes prods : Result<DoseRule[] * Message list, Message list> =
        getData () |> Result.bind (fromDataWithWarnings routeMapping formRoutes prods)


    /// Build a GetDoseRules-shaped function from a custom data source.
    /// `getData` reads DoseRuleData rows from `path` (e.g. a Pass-4 TSV).
    /// The result matches the ResourceConfig.GetDoseRules field shape exactly:
    /// DoseRuleData[] -> RouteMapping[] -> FormRoute[] -> ProductComponent[] ->
    /// Result. The leading DoseRuleData[] (the resources-loaded rows) is ignored
    /// here because this adapter reads its rows from `path` instead.
    let getFromGetData getData path =
        fun (_: DoseRuleData[]) -> get (fun () -> getData path)


    /// <summary>
    /// Filter the DoseRules according to the Filter.
    /// </summary>
    /// <param name="routeMapping"></param>
    /// <param name="filter">The Filter</param>
    /// <param name="drs">The DoseRule array</param>
    let filter routeMapping (filter: DoseFilter) (drs: DoseRule array) =
        // if the filter is 'empty' just return all
        if filter = Filter.doseFilter then
            drs
        else
            let eqs a b =
                a |> Option.map (String.equalsCapInsens b) |> Option.defaultValue true

            [|
                fun (dr: DoseRule) -> dr.Indication |> eqs filter.Indication
                fun (dr: DoseRule) -> dr.Generic.Label |> GenericLabel.toString |> eqs filter.Generic
                fun (dr: DoseRule) -> dr.Generic.Form |> PharmaceuticalForm.toString |> eqs filter.Form
                fun (dr: DoseRule) ->
                    filter.Route |> Option.isNone
                    || dr.Route |> Mapping.eqsRoute routeMapping filter.Route
                // don't filter on patients if patient is not set
                if filter.Patient = Patient.patient |> not then
                    fun (dr: DoseRule) -> dr.PatientCategory |> PatientCategory.filter filter
                fun (dr: DoseRule) -> filter.DoseType |> Option.map ((=) dr.DoseType) |> Option.defaultValue true
            |]
            |> Array.fold (fun (acc: DoseRule[]) pred -> acc |> Array.filter pred) drs


    let private getMember getter (drs: DoseRule[]) =
        drs
        |> Array.map getter
        |> Array.map String.trim
        |> Array.distinctBy String.toLower
        |> Array.sortBy String.toLower


    /// Extract all indications from the DoseRules.
    let indications = getMember _.Indication


    /// Extract all the generics from the DoseRules.
    let generics = getMember (_.Generic.Label >> GenericLabel.toString)


    /// Extract all the pharmaceutical forms from the DoseRules.
    let forms = getMember (_.Generic.Form >> PharmaceuticalForm.toString)


    /// Extract all the routes from the DoseRules.
    let routes = getMember _.Route


    let doseTypes (dsrs: DoseRule[]) =
        dsrs |> Array.map _.DoseType |> Array.distinct


    /// Extract all the departments from the DoseRules.
    let departments =
        getMember (fun dr -> dr.PatientCategory.Department |> Option.defaultValue "")


    /// Extract all genders from the DoseRules.
    let genders = getMember (fun dr -> dr.PatientCategory.Gender |> Gender.toString)


    /// Extract all patient categories from the DoseRules as strings.
    let patientCategories (drs: DoseRule array) =
        drs
        |> Array.map _.PatientCategory
        |> Array.sortBy PatientCategory.sortBy
        |> Array.map PatientCategory.toString
        |> Array.distinct


    /// Extract all frequencies from the DoseRules as strings.
    let frequencies (drs: DoseRule array) =
        drs |> Array.map Print.printFreqs |> Array.distinct


    let useAdjust (dr: DoseRule) =
        let substUseAdj =
            dr.ComponentLimits
            |> Array.collect _.SubstanceLimits
            |> Array.exists DoseLimit.useAdjust

        let compUseAdj =
            dr.ComponentLimits |> Array.choose _.Limit |> Array.exists DoseLimit.useAdjust

        let formUseAdj =
            dr.FormLimit |> Option.map DoseLimit.useAdjust |> Option.defaultValue false

        substUseAdj || compUseAdj || formUseAdj


    let rec getNormDose (dr: DoseRule) =
        dr.ComponentLimits
        |> Array.collect _.SubstanceLimits
        |> Array.collect (fun dl ->
            [|
                match dl.PerTimeAdjust |> DoseLimit.getNormDose with
                | Some norm -> (dl.DoseLimitTarget, norm) |> NormPerTimeAdjust |> Some
                | _ -> None

                match dl.QuantityAdjust |> DoseLimit.getNormDose with
                | Some norm -> (dl.DoseLimitTarget, norm) |> NormQuantityAdjust |> Some
                | _ -> None
            |]
        )
        |> Array.choose id
        |> Array.tryHead
