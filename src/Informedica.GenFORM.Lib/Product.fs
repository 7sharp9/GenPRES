namespace Informedica.GenForm.Lib



module Product =


    open MathNet.Numerics
    open Informedica.Utils.Lib
    open ConsoleWriter.NewLineNoTime
    open Informedica.Utils.Lib.BCL

    open Informedica.GenUnits.Lib

    open Utils

    module GenPresProduct = Informedica.ZIndex.Lib.GenPresProduct
    module ATCGroup = Informedica.ZIndex.Lib.ATCGroup


    let getFormularyProducts dataUrlId =
        try
            Web.GoogleSheets.getCsvDataFromSheetSync dataUrlId "Formulary"
            |> Result.bind (fun data ->
                let getColumn =
                    data
                    |> Array.head
                    |> Csv.getStringColumn

                let products =
                    data
                    |> Array.tail
                    |> Array.map (fun r ->
                        let get = getColumn r

                        // Extract departments from the columns marked with 'x'
                        let departments =
                            ["UMCU"; "ICC"; "NEO"; "ICK"; "HCK"]
                            |> List.filter (fun dept -> (get dept) = "x")

                        // Determine product type - this is a placeholder, actual logic needed
                        let getProdType s =
                            match s with
                            | "medication" -> ProductType.MedicationProduct
                            | "enteral" -> ProductType.EnteralProduct
                            | "parenteral" -> ProductType.ParenteralProduct
                            | _ -> ProductType.NoProduct

                        {
                            GPK = get "GPKODE"
                            ProductType = get "Type" |> getProdType
                            Departments = departments
                            Generic = get "Generic"
                            TallMan = get "TallMan"
                            Divisible = get "Divisible" |> Int32.tryParse
                            UseGenName = get "UseGenName" = "x"
                            UseForm = get "UseForm" = "x"
                            UseBrand = get "UseBrand" = "x"
                            Mmol = get "Mmol" |> Double.tryParse |> Option.bind BigRational.fromFloat
                            Form = get "Form" |> Option.ofObj
                            Brand = get "Brand" |> Option.ofObj
                            GenName = get "GenName" |> Option.ofObj
                            Unit = get "Unit" |> Option.ofObj
                            EnergyKCal = get "Energy kCal" |> Double.tryParse |> Option.bind BigRational.fromFloat
                            CarbG = get "Carb g" |> Double.tryParse |> Option.bind BigRational.fromFloat
                            ProtG = get "Prot g" |> Double.tryParse |> Option.bind BigRational.fromFloat
                            LipG = get "Lip g" |> Double.tryParse |> Option.bind BigRational.fromFloat
                            SodMmol = get "Sod mmol" |> Double.tryParse |> Option.bind BigRational.fromFloat
                            PotMmol = get "Pot mmol" |> Double.tryParse |> Option.bind BigRational.fromFloat
                            CalcMmol = get "Calc mmol" |> Double.tryParse |> Option.bind BigRational.fromFloat
                            PosphMmol = get "Posph mmol" |> Double.tryParse |> Option.bind BigRational.fromFloat
                            MagnMmol = get "Magn mmol" |> Double.tryParse |> Option.bind BigRational.fromFloat
                            ChlorMmol = get "Chlor mmol" |> Double.tryParse |> Option.bind BigRational.fromFloat
                            IronMmol = get "Iron mmol" |> Double.tryParse |> Option.bind BigRational.fromFloat
                            VitDIE = get "VitD IE" |> Double.tryParse |> Option.bind BigRational.fromFloat
                            IsReconste = get "IsReconste" = "x"
                            IsDilute = get "IsDilute" = "x"
                            IsAdditive = get "IsAdditive" = "x"
                        }
                    )
                    // filter out dummy
                    |> Array.filter (fun fp -> fp.GPK = "0" |> not)
                Ok products
            )
            |> Result.mapError (fun err -> [ err |> Warning ])
        with
        | exn -> GenFormResult.createError "getFormularyProducts" exn


    let getAdditionalSubstances (fp: FormularyProduct) =
        [

            "glucose g", fp.CarbG
            "energie kCal", fp.EnergyKCal
            "eiwit g", fp.ProtG
            "koolhydraat g", fp.CarbG
            "vet g", fp.LipG
            "natrium mmol", fp.SodMmol
            "kalium mmol", fp.PotMmol
            "calcium mmol", fp.CalcMmol
            "fosfaat mmol", fp.PosphMmol
            "magnesium mmol", fp.MagnMmol
            "ijzer mmol", fp.IronMmol
            "VitD IE", fp.VitDIE
            "chloor mmol", fp.ChlorMmol
        ]


    module Location =


        /// Get a string representation of the VenousAccess.
        let toString = function
            | PVL -> "PVL"
            | CVL -> "CVL"
            | AnyAccess -> ""


        /// Get a VenousAccess from a string.
        let fromString s =
            match s with
            | _ when s |> String.equalsCapInsens "PVL" -> PVL
            | _ when s |> String.equalsCapInsens "CVL" -> CVL
            | _ -> AnyAccess


    module FormRoute =


        let isSolution (mapping : FormRoute[]) form =
            mapping
            |> Array.tryFind (fun sr ->
                sr.Form |> String.equalsCapInsens form
            )
            |> Option.map _.IsSolution
            |> Option.defaultValue false


    module Reconstitution =


        let get dataUrlId : Result<_, _> =
            try
                Web.getDataFromSheet dataUrlId "Reconstitution"
                |> fun data ->

                    let getColumn =
                        data
                        |> Array.head
                        |> Csv.getStringColumn

                    data
                    |> Array.tail
                    |> Array.map (fun r ->
                        let get = getColumn r
                        let toBrOpt = BigRational.toBrs >> Array.tryHead

                        {
                            GPK = get "GPK"
                            Route = get "Route"
                            Location = get "Loc" |> fun s -> if s |> String.isNullOrWhiteSpace then None else Some s
                            Department = get "Dep" |> fun s -> if s |> String.isNullOrWhiteSpace then None else Some s
                            DiluentVolume =
                                get "DiluentVol"
                                |> toBrOpt
                                |> Option.map (ValueUnit.singleWithUnit Units.Volume.milliLiter)
                                |> Option.defaultValue (ValueUnit.singleWithUnit Units.Volume.milliLiter 1N)
                            ExpansionVolume =
                                get "ExpansionVol"
                                |> toBrOpt
                                |> Option.map (ValueUnit.singleWithUnit Units.Volume.milliLiter)
                            Diluents =
                                get "Diluents"
                                |> String.splitAt ';'
                                |> Array.map String.trim
                        }
                    )
                |> Ok
            with
            | exn -> GenFormResult.createError "Reconstiution.get" exn


        let filter routeMapping (filter : DoseFilter) (rs : Reconstitution []) =
            let eqsOpt a b =
                match a, b with
                | Some a, Some b -> a = b
                | _ -> true

            [|
                // should match the route if filter.Route is given
                fun (r : Reconstitution) -> r.Route |> Mapping.eqsRoute routeMapping filter.Route
                // if both filter and rule have location, they must match; if either is None, pass
                fun (r : Reconstitution) -> r.Location |> eqsOpt filter.Patient.Location
                // if both filter and rule have department, they must match; if either is None, pass
                fun (r : Reconstitution) -> r.Department |> eqsOpt filter.Patient.Department
            |]
            |> Array.fold (fun (acc : Reconstitution[]) pred ->
                acc |> Array.filter pred
            ) rs


    let createSubstance
        name
        conc
        unit
        formUnit
        unitMapping
        =
        match conc with
        | Some br when br > 0N ->
            let conc =
                unit
                |> Mapping.mapUnit unitMapping
                |> function
                    | None ->
                        writeErrorMessage $"cannot map unit: {unit}"
                        None
                    | Some u ->
                        let isMolar =
                            u |> ValueUnit.Group.eqsGroup Units.Molar.milliMole
                        let u =
                            u
                            |> Units.per formUnit

                        (isMolar, br |> ValueUnit.singleWithUnit u)
                        |> Some
            {
                Name = name
                Concentration = conc |> Option.map snd
                MolarConcentration =
                    conc
                    |> Option.bind (fun (isMolar, vu) ->
                        if isMolar then vu |> Some else None
                    )
            }
            |> Some
        | _ -> None


    module Enteral =


        let createProduct
            name
            formUnit
            unitMapping
            substs
            =
            {
                GPK =  name
                ATC = ""
                MainGroup = ""
                SubGroup = ""
                Generic = name
                UseGenericName = false
                UseForm = false
                UseBrand = false
                TallMan = "" //r.TallMan
                Synonyms = [||]
                Product = name
                Label = name
                Form = "voeding"
                Routes = [| "ORAAL" |]
                FormQuantities =
                    formUnit
                    |> ValueUnit.singleWithValue 1N
                FormUnit = formUnit
                RequiresReconstitution = false
                Reconstitution = [||]
                Divisible = Some 10N
                Substances =
                    substs
                    |> List.choose (fun (s, q) ->
                        let n, u =
                            match s |> String.split " " with
                            | [n; u] -> n |> String.trim, u |> String.trim
                            | _ -> failwith $"cannot parse substance {s}"
                        createSubstance n q u formUnit unitMapping
                    )
                    |> List.filter (fun s ->
                        s.Name |> String.notEmpty &&
                        (s.Concentration |> Option.isSome ||
                        s.MolarConcentration |> Option.isSome)
                    )
                    |> List.toArray
            }


        let get unitMapping (prods: FormularyProduct []) =
                prods
                |> Array.filter _.ProductType.IsEnteralProduct
                |> Array.choose (fun fp ->
                    let formUnit =
                        fp.Unit
                        |> Option.bind Units.fromString

                    match formUnit with
                    | None -> None
                    | Some fu ->
                        fp
                        |> getAdditionalSubstances
                        |> createProduct fp.Generic fu unitMapping
                        |> Some
                )


    module Parenteral =

        let createProduct
            name
            unitMapping
            substs
            =
            {
                GPK =  name
                ATC = ""
                MainGroup = ""
                SubGroup = ""
                Generic = name
                UseGenericName = false
                UseForm = false
                UseBrand = false
                TallMan = "" //r.TallMan
                Synonyms = [||]
                Product = name
                Label = name
                Form = "vloeistof"
                Routes = [| "INTRAVENEUS"; "ORAAL" |]
                FormQuantities =
                    Units.Volume.milliLiter
                    |> ValueUnit.singleWithValue 1N
                FormUnit =
                    Units.Volume.milliLiter
                RequiresReconstitution = false
                Reconstitution = [||]
                Divisible = Some 10N
                Substances =
                    substs
                    |> List.choose (fun (s, q) ->
                        let n, u =
                            match s |> String.split " " with
                            | [n; u] -> n |> String.trim, u |> String.trim
                            | _ -> failwith $"cannot parse substance {s}"
                        createSubstance n q u Units.Volume.milliLiter unitMapping
                    )
                    |> List.filter (fun s ->
                        s.Name |> String.notEmpty &&
                        (s.Concentration |> Option.isSome ||
                        s.MolarConcentration |> Option.isSome)
                    )
                    |> List.toArray
                    |> Array.filter (fun s ->
                        s.Name |> String.notEmpty &&
                        (s.Concentration |> Option.isSome ||
                        s.MolarConcentration |> Option.isSome)
                    )
            }

        let get unitMapping (prods: FormularyProduct []) =
            prods
            |> Array.filter _.ProductType.IsParenteralProduct
                |> Array.map (fun fp ->
                    fp
                    |> getAdditionalSubstances
                    |> createProduct fp.Generic unitMapping
                )


    let create gen rte substs =
        {
            GPK = ""
            ATC = ""
            MainGroup = ""
            SubGroup = ""
            Generic = gen
            UseGenericName = false
            UseForm = false
            UseBrand = false
            TallMan = gen
            Synonyms = [||]
            Product = gen
            Label = gen
            Form = gen
            Routes = [| rte  |]
            FormQuantities = ValueUnit.empty
            FormUnit = NoUnit
            RequiresReconstitution = false
            Reconstitution = [||]
            Divisible = None
            Substances =
                substs
                |> Array.map (fun s ->
                    {
                        Name = s
                        Concentration = None
                        MolarConcentration = None
                    }
                )
        }


    let rename defaultName useGenName (subst : Informedica.ZIndex.Lib.Types.ProductSubstance) =
        if useGenName then subst.GenericName
        else defaultName
        |> String.toLower


    let map
        unitMapping
        routeMapping
        formRoutes
        (reconstitution : Reconstitution[])
        name
        synonyms
        formQuantities
        (fp: FormularyProduct)
        (gp : Informedica.ZIndex.Lib.Types.GenericProduct)
        =

        let atc =
            gp.ATC
            |> ATCGroup.findByATC5

        let formUnit =
            gp.Substances[0].FormUnit
            |> Mapping.mapUnit unitMapping
            |> Option.defaultValue NoUnit

        let reqReconst =
            Mapping.requiresReconstitution routeMapping formRoutes (gp.Route, formUnit, gp.Form)

        let formUnit =
            if not reqReconst then formUnit
            else
                Units.Volume.milliLiter

        {
            GPK =  $"{gp.Id}"
            ATC = gp.ATC |> String.trim
            MainGroup =
                atc
                |> Array.map _.AnatomicalGroup
                |> Array.tryHead
                |> Option.defaultValue ""
            SubGroup =
                atc
                |> Array.map _.TherapeuticSubGroup
                |> Array.tryHead
                |> Option.defaultValue ""
            Generic = name
            UseGenericName = fp.UseGenName
            UseForm = fp.UseForm
            UseBrand = fp.UseBrand
            TallMan = ""
            Synonyms = synonyms
            Product =
                gp.PrescriptionProducts
                |> Array.collect (fun pp ->
                    pp.TradeProducts
                    |> Array.map _.Label
                )
                |> Array.distinct
                |> function
                | [| p |] -> p
                | _ -> ""
            Label = gp.Label
            Form = gp.Form |> String.toLower
            Routes = gp.Route |> Array.choose (Mapping.mapRoute routeMapping)
            FormQuantities =
                formQuantities
                |> ValueUnit.withUnit formUnit
            FormUnit = formUnit
            RequiresReconstitution = reqReconst
            Reconstitution =
                reconstitution
                |> Array.filter (fun r ->
                    r.GPK = $"{gp.Id}"
                )
            Divisible =
                match fp.Divisible with
                | Some d -> d |> BigRational.fromInt |> Some
                | None ->
                    let rs =
                        Mapping.filterFormRoutes
                            routeMapping
                            formRoutes "" (gp.Form.ToLower()) NoUnit
                    if rs |> Array.length = 0 then None
                    else
                        rs[0].Divisibility
            Substances =
                let ppAdditional =
                    gp.PrescriptionProducts
                    |> Array.collect (fun pp ->
                        pp.TradeProducts
                        |> Array.collect _.Substances
                    )
                    |> Array.filter (fun s ->
                        s.SubstanceQuantity > 0. &&
                        s.IsAdditional
                    )

                let fpAdditional =
                    fp
                    |> getAdditionalSubstances
                    |> List.choose (fun (s, q) ->
                        let n, u =
                            match s |> String.split " " with
                            | [n; u] -> n |> String.trim, u |> String.trim
                            | _ -> failwith $"cannot parse substance {s}"
                        createSubstance n q u formUnit unitMapping
                    )
                    |> List.filter (fun s ->
                        s.Name |> String.notEmpty &&
                        (s.Concentration |> Option.isSome ||
                        s.MolarConcentration |> Option.isSome)
                    )
                    |> List.toArray

                gp.Substances
                |> Array.filter (fun ps ->
                    ps.SubstanceQuantity > 0.
                )
                |> Array.append ppAdditional
                // TODO should be group by as additional can have different concentrations
                |> Array.distinctBy _.SubstanceId
                |> Array.map (fun s ->
                    let su =
                        s.SubstanceUnit
                        |> Mapping.mapUnit unitMapping
                        |> Option.map (fun u ->
                            CombiUnit(u, OpPer, formUnit)
                        )
                        |> Option.defaultValue NoUnit
                    {
                        Name = s |> rename s.SubstanceName fp.UseGenName
                        Concentration =
                            s.SubstanceQuantity
                            |> BigRational.fromFloat
                            |> Option.map (ValueUnit.singleWithUnit su)
                        MolarConcentration =
                            if fp.Mmol |> Option.isNone ||
                               s.SubstanceName |> String.equalsCapInsens name |> not then None
                            // only apply mmol to substance with the same name as the product
                            else
                                let u = Units.Molar.milliMole |> Units.per formUnit
                                fp.Mmol
                                |> Option.map (ValueUnit.singleWithUnit u)
                    }
                )
                |> Array.append fpAdditional
        }



    let get
        unitMapping
        routeMapping
        validForms
        formRoutes
        reconstitution
        parenteral
        enteral
        (formularyProducts: FormularyProduct[])
        =
        fun () ->
            formularyProducts
            |> Array.choose (fun fp ->
                fp.GPK
                |> Int32.tryParse
                |> Option.map (fun gpk -> gpk, fp)
            )
            // find the matching GenPresProducts
            |> Array.collect (fun (gpk, fp) ->
                gpk
                |> GenPresProduct.findByGPK
                |> Array.map (fun gpp -> (gpk, fp, gpp))
            )
            // collect the GenericProducts
            // filtered by "valid form" and
            // at least one substance quantity > 0
            |> Array.collect (fun (gpk, fp, gpp) ->
                gpp.GenericProducts
                |> Array.filter (fun gp ->
                    gp.Id = gpk &&
                    validForms
                    |> Array.exists (String.equalsCapInsens gp.Form) &&
                    gp.Substances
                    |> Array.exists (fun s ->
                        s.SubstanceQuantity > 0.
                    )
                )
                |> Array.map (fun gp -> fp, gp)
            )
            // create the Product records
            |> Array.map (fun (fp, gp) ->
                let name = fp.Generic |> String.toLower

                let synonyms =
                    gp.PrescriptionProducts
                    |> Array.collect (fun pp ->
                        pp.TradeProducts
                        |> Array.map _.Brand
                    )
                    |> Array.distinct
                    |> Array.filter String.notEmpty

                let formQuantities =
                    gp.PrescriptionProducts
                    |> Array.map _.Quantity
                    |> Array.choose BigRational.fromFloat
                    |> Array.filter (fun br -> br > 0N)
                    |> Array.distinct
                    |> fun xs ->
                        if xs |> Array.isEmpty then [| 1N |] else xs

                gp
                |> map
                       unitMapping
                       routeMapping
                       formRoutes
                       reconstitution
                       name
                       synonyms
                       formQuantities
                       fp
            )
            |> Array.append parenteral
            |> Array.append enteral

        |> StopWatch.clockFunc "created products"


    /// <summary>
    /// Reconstitute the given product according to
    /// route, DoseType, department and VenousAccess location.
    /// </summary>
    /// <param name="mapping"></param>
    /// <param name="dep">The department</param>
    /// <param name="loc">The venous access location</param>
    /// <param name="rte">The route</param>
    /// <param name="prod">The product</param>
    /// <returns>
    /// The reconstituted product or None if the product
    /// does not require reconstitution.
    /// </returns>
    let reconstitute mapping loc dep rte (prod : Product) =
        let warnings = ResizeArray<string>()
        let eqsRoute = Mapping.eqsRoute mapping

        let eqsOpt a b =
            match a, b with
            | Some a, Some b -> a = b
            | _ -> true

        let prods =
            [|
                // if reconstitution is not required, the
                // original product is returned as well
                if prod.RequiresReconstitution |> not then [| prod |]
                else
                    // calculate the reconstituted products
                    prod.Reconstitution
                    |> Array.filter (fun r ->
                        // return true if route is not given or matches
                        (rte |> String.isNullOrWhiteSpace || r.Route |> eqsRoute (Some rte)) &&
                        // return true if either department is None, or both match
                        r.Department |> eqsOpt dep &&
                        // return true if either location is None, or both match
                        r.Location |> eqsOpt loc
                    )
                    |> fun xs ->
                        if xs |> Array.isEmpty then
                            warnings.Add $"no reconstitution rules found for {prod.Generic} ({prod.Form}) with route {rte} and department {dep}"
                        xs
                    |> Array.map (fun r ->
                        let vol =
                            r.ExpansionVolume
                            |> Option.map (fun v -> v + r.DiluentVolume)
                            |> Option.defaultValue r.DiluentVolume

                        { prod with
                            FormUnit = Units.Volume.milliLiter
                            FormQuantities = vol
                            Substances =
                                prod.Substances
                                |> Array.map (fun s ->
                                    { s with
                                        Concentration =
                                            s.Concentration
                                            |> Option.map (fun q ->
                                                let one =
                                                    Units.Volume.milliLiter
                                                    |> ValueUnit.singleWithValue 1N
                                                one * q / vol
                                            )
                                    }
                                )
                        }
                    )
            |]
            |> Array.collect id

        prods,
        warnings |> Seq.distinct


    /// <summary>
    /// Filter the Product array to get all the products
    /// </summary>
    /// <param name="mapping"></param>
    /// <param name="filter">The Filter</param>
    /// <param name="prods">The array of Products</param>
    let filter mapping (filter : DoseFilter) (prods : Product []) =
        let eqsRoute = Mapping.eqsRoute mapping
        let recFilter = Reconstitution.filter mapping

        let repl s =
            s
            |> String.replace "/" ""
            |> String.replace "+" ""
            |> String.replace "(" ""
            |> String.replace ")" ""
            |> String.trim

        let eqs s1 s2 =
            match s1, s2 with
            | Some s1, s2 ->
                let s1 = s1 |> repl
                let s2 = s2 |> repl
                s1 |> String.equalsCapInsens s2
            | _ -> true

        prods
        |> Array.filter (fun p ->
            p.Generic |> eqs filter.Generic &&
            p.Form |> eqs filter.Form &&
            p.Routes |> Array.exists (eqsRoute filter.Route)
        )
        |> Array.map (fun p ->
            { p with
                Reconstitution =
                    p.Reconstitution
                    |> recFilter filter
            }
        )


    /// Get all Generics from the given Product array.
    let generics (products : Product array) =
        products
        |> Array.map _.Generic
        |> Array.distinct


    /// Get all Synonyms from the given Product array.
    let synonyms (products : Product array) =
        products
        |> Array.collect _.Synonyms
        |> Array.append (generics products)
        |> Array.distinct


    /// Get all pharmaceutical forms from the given Product array.
    let forms  (products : Product array) =
        products
        |> Array.map _.Form
        |> Array.distinct
