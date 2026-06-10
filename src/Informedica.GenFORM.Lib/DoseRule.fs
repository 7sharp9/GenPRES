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


    type DataGroupKey =
        {

            Source: string
            Indication: string
            Generic: GenericData
            Patient: PatientCategoryData
            Route: string
        }


    let dataGroupKey (d: DoseRuleData) =
        {
            Source = d.Source
            Indication = d.Indication
            Generic = d.Generic
            Patient = d.Patient
            Route = d.Route
        }


    type GroupedRuleData =
        {
            DataGroupKey: DataGroupKey
            DoseRuleData: DoseRuleData[]
            Forms: string[]
            Products: ProductComponent[]
        }


    let setDataHashIds (dd: DoseRuleData) =
        let optBrToStr = Option.map BigRational.toString >> Option.defaultValue ""

        let fields =
            [
                dd.Source
                dd.Generic.Name
                dd.Generic.Form
                dd.Generic.Brand
                dd.Generic.GPKs |> String.concat ";"
                dd.Generic.HPKs |> String.concat ";"
                dd.Route
                dd.Patient.Location
                dd.Patient.Dep
                dd.Patient.IsAdult |> sprintf "%b"
                dd.Patient.Gender |> Gender.toString
                dd.Patient.MinAge |> optBrToStr
                dd.Patient.MaxAge |> optBrToStr
                dd.Patient.MinWeight |> optBrToStr
                dd.Patient.MaxWeight |> optBrToStr
                dd.Patient.MinBSA |> optBrToStr
                dd.Patient.MaxBSA |> optBrToStr
                dd.Patient.MinGestAge |> optBrToStr
                dd.Patient.MaxGestAge |> optBrToStr
                dd.Patient.MinPMAge |> optBrToStr
                dd.Patient.MaxPMAge |> optBrToStr
                dd.ScheduleData.DoseType
                dd.ScheduleData.DoseText
            ]

        { dd with
            Id = fields |> String.sha1Short // full unique data dose rule id
            GrpId = fields[.. fields.Length - 3] |> String.sha1Short // grouped by similar dose rule data but different dose types
        }


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


    /// Reduce a row's product narrowing to a single mechanism by precedence
    /// HPKs > GPKs > Brand > Form: keep the highest-precedence field that is
    /// present and clear every lower-precedence one. Rows with no narrowing are
    /// left unchanged (they remain generic-wide rules).
    let withSingleNarrowing (g: GenericData) =
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


    let parseDoseRuleData data =
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
            // Keep one product-selection narrowing per row, by precedence
            // HPKs > GPKs > Brand > Form, then dedupe.
            |> Array.map (fun d -> { d with Generic = d.Generic |> withSingleNarrowing })
            |> Array.distinct
            |> Ok
        with exn ->
            Result.createError "getDataResult" exn


    let getData dataUrlId =
        Web.getDataFromSheet dataUrlId "DoseRules" |> parseDoseRuleData


    /// Pretty print dose rule data for  logging
    let doseRuleDataToString dd =
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


    /// Determine whether a raw DoseRuleData row is valid.
    /// Can only determine validity up to schedule data as
    /// Component and Substance related data has to be aggregated
    /// into a single dose rule
    let validateData dd =
        let doseType, _ = DoseType.parse dd.ScheduleData.DoseType dd.ScheduleData.DoseText

        let warning = $"%s{dd.Generic.Name} | %s{dd.Route} |"

        match doseType with
        | NoDoseType -> [ "Has no dose type" ]
        | Once _ -> []
        | OnceTimed _ ->
            [
                if dd.ScheduleData.TimeUnit |> String.isNullOrWhiteSpace then
                    "TimeUnit is missing"
            ]
        | Discontinuous _ ->
            [
                if dd.ScheduleData.Freqs.Length = 0 then
                    "Frequencies is empty"
                if dd.ScheduleData.FreqUnit |> String.isNullOrWhiteSpace then
                    "FreqUnit is missing"
            ]
        | Timed _ ->
            [
                if dd.ScheduleData.Freqs.Length = 0 then
                    "Frequencies is empty"
                if dd.ScheduleData.FreqUnit |> String.isNullOrWhiteSpace then
                    "FreqUnit is missing"
                if dd.ScheduleData.TimeUnit |> String.isNullOrWhiteSpace then
                    "TimeUnit is missing"
            ]
        | Continuous _ ->

            [
                if dd.ScheduleData.RateUnit |> String.isNullOrWhiteSpace then
                    "RateUnit is missing"
            ]
        |> List.map (sprintf "%s %s" warning)


    /// Cheap pre-filter: keep only products whose Generic matches a component
    /// used somewhere in the group, before the per-component product narrowing.
    let candidateProducts (prods: ProductComponent[]) (rs: DoseRuleData[]) =
        let cmps = rs |> Array.map _.ScheduleData.DoseLimitData.Component |> Array.distinct

        prods
        |> Array.filter (fun p -> cmps |> Array.exists (String.equalsCapInsens p.Generic))


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


    let setDoseRuleHashId unitGroup (dr: DoseRule) =
        dr.Generic.Products
        |> List.map (fun id ->
            match id with
            | Gpk s
            | Hpk s -> s
        )
        |> List.append
            [
                unitGroup
                dr.Source |> Source.toString
                dr.Generic.Label |> GenericLabel.toString
                dr.Generic.Form |> PharmaceuticalForm.toString
                dr.Route
                dr.Indication
                dr.PatientCategory |> PatientCategory.toString
                dr.DoseType |> DoseType.toString
            ]
        |> String.sha1Short


    let mapToComponentLimits prods dd =
        dd
        |> Array.map (fun r ->
            let cmp = r.ScheduleData.DoseLimitData.Component
            { r with Products = prods |> Array.filter (_.Generic >> (String.equalsCapInsens cmp)) }
        )
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
                    let hpks =
                        rs
                        |> Array.collect (_.Generic.HPKs >> Array.toList >> ProductId.hpks >> List.toArray)

                    rs
                    |> Array.collect (_.Generic.GPKs >> Array.toList >> ProductId.gpks >> List.toArray)
                    |> Array.append hpks
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
                    |> Array.filter (_.ScheduleData.DoseLimitData.Substance >> String.isNullOrWhiteSpace >> not)
                    |> getDoseLimits
            }
        )


    let mapToDoseRule (grp: GroupedRuleData) =
        let identifySource s =
            match s with
            | _ when s = "FTK" -> s |> Source.identified
            | _ when s = "NKF" -> s |> Source.identified
            | _ -> s |> Source.other

        let toLabel = GenericLabel.toLabel

        let toForm = PharmaceuticalForm.fromString

        let r = grp.DoseRuleData[0]

        if grp.Forms |> Array.isEmpty then
            [| r.Generic.Form |]
        else
            grp.Forms
        // expand the DoseRuleData to all available product forms
        |> Array.collect (fun frm ->
            if grp.Products |> Array.isEmpty then // make a virtual product component
                grp.DoseRuleData
                |> Array.map _.ScheduleData.DoseLimitData.Component
                |> Array.distinct
                |> Array.map (fun cmp ->
                    grp.DoseRuleData
                    |> Array.filter (_.ScheduleData.DoseLimitData.Component >> String.equalsCapInsens cmp)
                    |> Array.map _.ScheduleData.DoseLimitData.Substance
                    |> Array.filter String.notEmpty
                    |> Array.distinct
                    |> Product.create cmp grp.DataGroupKey.Generic.Form grp.DataGroupKey.Route
                )
            else
                grp.Products
            |> Array.filter (_.Form >> String.equalsCapInsens frm)
            |> Array.groupBy (_.FormUnit >> ValueUnit.Group.unitToGroup)
            |> Array.map (fun (unitGroup, prods) ->
                unitGroup |> Group.toString,
                {
                    Id = ""
                    DataId = r.Id
                    GroupId = r.GrpId
                    SortNo = r.SortNo
                    Source = r.Source |> identifySource
                    SourceText = r.SourceText
                    Indication = r.Indication
                    Generic =
                        // Label reflects the ORIGINAL source restriction (form/brand/none).
                        let lbl = toLabel r.Generic.Name r.Generic.Form r.Generic.Brand

                        let frm = frm |> toForm

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
                            Location =
                                if p.Location |> String.isNullOrWhiteSpace then
                                    None
                                else
                                    p.Location |> Some

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
                    ComponentLimits = grp.DoseRuleData |> mapToComponentLimits prods
                    RenalRuleSource = None
                }
            )
        )
        |> Array.map (fun (unitGroup, dr) -> { dr with Id = dr |> setDoseRuleHashId unitGroup })


    let group routeMapping (prods: ProductComponent[]) chunk =
        chunk
        |> Array.collect (fun (key, data) ->
            let candidates = data |> candidateProducts prods

            data
            // make sure that the chunked dose rule data
            // have ids set
            |> Array.map setDataHashIds
            // make sure the sort order is preserved
            |> Array.mapi (fun i dd -> { dd with SortNo = i })
            // perform an additional grouping to differentiate
            // between dose type, note dose type is part of the
            // dose rule hashed id
            |> Array.groupBy _.Id
            |> Array.map (fun (_, data) ->
                let cmps =
                    data |> Array.map _.ScheduleData.DoseLimitData.Component |> Array.distinct

                let prods =
                    cmps
                    |> Array.collect (fun cmp ->
                        candidates
                        |> Product.filter
                            routeMapping
                            key.Route
                            cmp
                            key.Generic.Form
                            key.Generic.Brand
                            key.Generic.GPKs
                            key.Generic.HPKs

                    )

                let frms = prods |> Array.map _.Form |> Array.distinct

                {
                    DataGroupKey = key
                    DoseRuleData = data
                    Forms = frms
                    Products = prods
                }
            )
        )


    let fromData routeMapping formRoutes prods (data: DoseRuleData[]) =
        // get the validated dose rule data from data
        // and the warnings
        let data, warnings =
            let valid, invalid =
                data
                // validate the dose rule data
                |> Array.map (fun d -> d, validateData d)
                |> Array.partition (snd >> List.isEmpty)

            valid |> Array.map fst, invalid |> Array.map (snd >> List.toArray) |> Array.collect id

        let addFormLimits = addFormLimits routeMapping formRoutes

        let grouped =
            data
            // group by dose rule data group
            |> Array.groupBy dataGroupKey
            // setup chunks of grouped dose rule data
            |> Array.chunkBySize Parallel.totalWorders
            // map to grouped dose rule data to map to a dose rule
            |> Array.map (fun chunk -> async { return chunk |> group routeMapping prods })
            |> Async.Parallel
            |> Async.RunSynchronously
            |> Array.collect id

        // Warm-up phase to enable safe parallelization below. mapToDoseRule /
        // addFormLimits are the first code to exercise the unit / dose-limit
        // modules; running a sample single-threaded forces their one-time
        // (static) initialization on one thread, avoiding a cold concurrent
        // type-initialization deadlock when the parallel tasks below trigger
        // those inits at once. We warm a whole chunk (not a single group) so the
        // initialized code paths do not depend on which group happens to be first.
        let warm, tail =
            if grouped |> Array.length <= Parallel.totalWorders then
                grouped, [||]
            else
                grouped[.. Parallel.totalWorders - 1], grouped[Parallel.totalWorders ..]

        let head = warm |> Array.collect (mapToDoseRule >> Array.map addFormLimits)

        tail
        |> Array.chunkBySize Parallel.totalWorders
        // map each grouped dose rule data to mapToDoseRule
        // that will expand for all available product forms
        // and form unit groups
        |> Array.map (fun grps ->
            async {
                let rules = grps |> Array.collect mapToDoseRule |> Array.map addFormLimits
                return rules
            }
        )
        |> Async.Parallel
        |> Async.RunSynchronously
        |> Array.collect id
        |> Array.append head,
        warnings |> Array.map Warning |> Array.toList


    /// Impure adapter: loads DoseRuleData via the `getData` thunk and delegates
    /// to the pure <c>fromData</c>, carrying the product warnings.
    /// Kept for existing callers/tests.
    let get getData routeMapping formRoutes prods =
        getData () |> fromData routeMapping formRoutes prods


    /// Build a GetDoseRules-shaped function from a custom data source.
    /// `getData` reads DoseRuleData rows from `path` (e.g. a Pass-4 TSV).
    /// The result matches the ResourceConfig.GetDoseRules field shape exactly:
    /// DoseRuleData[] -> RouteMapping[] -> FormRoute[] -> ProductComponent[] ->
    /// (DoseRule[] * Message list). The leading DoseRuleData[] (the
    /// resources-loaded rows) is ignored here because this adapter reads its
    /// rows from `path` instead.
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
