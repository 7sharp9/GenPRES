namespace Informedica.GenForm.Tests


/// Create the necessary test generators
module Generators =


    open Expecto
    open FsCheck
    open MathNet.Numerics

    let bigRGen (n, d) =
        let d = if d = 0 then 1 else d
        let n = abs (n) |> BigRational.FromInt
        let d = abs (d) |> BigRational.FromInt
        n / d


    let bigRGenOpt (n, _) = bigRGen (n, 1) |> Some


    let bigRGenerator =
        gen {
            let! n = Arb.generate<int>
            let! d = Arb.generate<int>
            return bigRGen (n, d)
        }


    type BigRGenerator() =
        static member BigRational() =
            { new Arbitrary<BigRational>() with
                override x.Generator = bigRGenerator
            }


    type MinMax = MinMax of BigRational * BigRational

    let minMaxArb () =
        bigRGenerator
        |> Gen.map abs
        |> Gen.two
        |> Gen.map (fun (br1, br2) ->
            let br1 = br1.Numerator |> BigRational.FromBigInt
            let br2 = br2.Numerator |> BigRational.FromBigInt

            if br1 >= br2 then br2, br1 else br1, br2
            |> fun (br1, br2) -> if br1 = br2 then br1, br2 + 1N else br1, br2
        )
        |> Arb.fromGen
        |> Arb.convert MinMax (fun (MinMax(min, max)) -> min, max)


    type ListOf37<'a> = ListOf37 of 'a List

    let listOf37Arb () =
        Gen.listOfLength 37 Arb.generate
        |> Arb.fromGen
        |> Arb.convert ListOf37 (fun (ListOf37 xs) -> xs)


    let config =
        { FsCheckConfig.defaultConfig with
            arbitrary =
                [
                    typeof<BigRGenerator>
                    typeof<ListOf37<_>>.DeclaringType
                    typeof<MinMax>.DeclaringType
                ]
                @ FsCheckConfig.defaultConfig.arbitrary
            maxTest = 1000
        }


    let testProp testName prop =
        prop |> testPropertyWithConfig config testName


module GenericLabelTests =


    open Expecto
    open Expecto.Flip
    open Informedica.GenForm.Lib


    /// Regression tests for the generic-name vs generic-label distinction.
    /// External lookups (G-Standaard / dose checking) must key on the base
    /// substance name, while selection/display uses the full label.
    let tests =
        testList
            "GenericLabel name vs label"
            [
                test "genericName strips the brand qualifier" {
                    GenericBrand("glycopyrronium", "Sialanar")
                    |> GenericLabel.genericName
                    |> Expect.equal "should be the base substance name" "glycopyrronium"
                }

                test "genericName strips the form qualifier" {
                    GenericForm("paracetamol", "tablet")
                    |> GenericLabel.genericName
                    |> Expect.equal "should be the base substance name" "paracetamol"
                }

                test "toString keeps the brand qualifier for display/selection" {
                    GenericBrand("glycopyrronium", "Sialanar")
                    |> GenericLabel.toString
                    |> Expect.equal "should be the full label" "glycopyrronium (Sialanar)"
                }

                test "Generic.genericName delegates to the label" {
                    Generic.create (GenericBrand("glycopyrronium", "Sialanar")) (Solid "drank") []
                    |> Generic.genericName
                    |> Expect.equal "should be the base substance name" "glycopyrronium"
                }

                test "shorthand and canonical names are returned as-is" {
                    Shorthand "amoxicilline"
                    |> GenericLabel.genericName
                    |> Expect.equal "shorthand is the name" "amoxicilline"

                    Canonical [ "amoxicilline"; "clavulaanzuur" ]
                    |> GenericLabel.genericName
                    |> Expect.equal "canonical joins with /" "amoxicilline/clavulaanzuur"
                }
            ]


module ProductFilterTests =


    open MathNet.Numerics
    open Informedica.GenUnits.Lib
    open Expecto
    open Expecto.Flip
    open Informedica.GenForm.Lib


    /// Route mapping covering the routes used by the test fixtures.
    let routeMapping =
        [|
            {
                Long = "oraal"
                Short = "or"
            }
            {
                Long = "iv"
                Short = "iv"
            }
        |]


    /// Build a Substance with only its name set.
    let subst name : Substance =
        {
            Name = name
            Concentration = None
            MolarConcentration = None
        }


    let para = subst "paracetamol"
    let sorbitol = subst "sorbitol"
    let amox = subst "amoxicilline"


    /// Build a TradeProduct.
    let trade hpk brand substs : TradeProduct =
        {
            HPK = hpk
            Brand = brand
            Substances = substs
        }


    /// A ProductComponent with neutral defaults; only the fields that
    /// drive Product.filter are overridden per fixture.
    let baseProd: ProductComponent =
        {
            GPK = ""
            ATC = ""
            MainGroup = ""
            SubGroup = ""
            Generic = ""
            TallMan = ""
            Synonyms = [||]
            ProductLabels = []
            Label = ""
            Form = ""
            Routes = [||]
            FormQuantities = ValueUnit.singleWithUnit Units.Mass.milliGram 1N
            FormUnit = Units.Mass.milliGram
            RequiresReconstitution = false
            Reconstitution = [||]
            Divisible = None
            Substances = [||]
            TradeProducts = []
        }


    // paracetamol tablet, oraal, two brands
    let prodA =
        { baseProd with
            GPK = "1001"
            Generic = "paracetamol"
            Form = "tablet"
            Routes = [| "oraal" |]
            Substances = [| para |]
            TradeProducts =
                [
                    trade "H1" "BrandX" [ para ]
                    trade "H2" "BrandY" [ para ]
                ]
        }

    // paracetamol drank, oraal; carries sorbitol that the trade product does not
    let prodB =
        { baseProd with
            GPK = "1002"
            Generic = "paracetamol"
            Form = "drank"
            Routes = [| "oraal" |]
            Substances = [| para; sorbitol |]
            TradeProducts = [ trade "H3" "BrandZ" [ para ] ]
        }

    // amoxicilline poeder, iv — should never match a paracetamol/oraal filter
    let prodC =
        { baseProd with
            GPK = "2001"
            Generic = "amoxicilline"
            Form = "poeder"
            Routes = [| "iv" |]
            Substances = [| amox |]
            TradeProducts = [ trade "H9" "BrandQ" [ amox ] ]
        }

    let prods = [| prodA; prodB; prodC |]


    let tests =
        testList
            "Product.filter tests"
            [

                test "filter by required cmp name and route returns matching products" {
                    prods
                    |> Product.filter routeMapping "oraal" "paracetamol" "" "" [||] [||]
                    |> Array.map _.GPK
                    |> Array.sort
                    |> Expect.equal "should return both paracetamol oraal products" [| "1001"; "1002" |]
                }

                test "filter excludes products whose route does not match" {
                    prods
                    |> Product.filter routeMapping "iv" "paracetamol" "" "" [||] [||]
                    |> Expect.isEmpty "no paracetamol product has route iv"
                }

                test "filter excludes products whose cmp name does not match" {
                    prods
                    |> Product.filter routeMapping "iv" "amoxicilline" "" "" [||] [||]
                    |> Array.map _.GPK
                    |> Expect.equal "only the amoxicilline product matches" [| "2001" |]
                }

                test "form refines the selected set" {
                    prods
                    |> Product.filter routeMapping "oraal" "paracetamol" "tablet" "" [||] [||]
                    |> Array.map _.GPK
                    |> Expect.equal "only the tablet remains" [| "1001" |]
                }

                test "empty form does not refine the selected set" {
                    prods
                    |> Product.filter routeMapping "oraal" "paracetamol" "" "" [||] [||]
                    |> Array.length
                    |> Expect.equal "both forms still pass" 2
                }

                test "gpk list refines the selected set" {
                    prods
                    |> Product.filter routeMapping "oraal" "paracetamol" "" "" [| "1002" |] [||]
                    |> Array.map _.GPK
                    |> Expect.equal "only the listed gpk remains" [| "1002" |]
                }

                test "brand refines to products carrying that trade product" {
                    let result =
                        prods |> Product.filter routeMapping "oraal" "paracetamol" "" "BrandX" [||] [||]

                    result
                    |> Array.map _.GPK
                    |> Expect.equal "only the product with a BrandX trade product remains" [| "1001" |]

                    result[0].TradeProducts
                    |> List.map _.HPK
                    |> Expect.equal "trade products narrowed to the BrandX trade product" [ "H1" ]
                }

                test "hpk list refines and narrows substances to the trade product" {
                    let result =
                        prods |> Product.filter routeMapping "oraal" "paracetamol" "" "" [||] [| "H3" |]

                    result
                    |> Array.map _.GPK
                    |> Expect.equal "only the product carrying HPK H3 remains" [| "1002" |]

                    result[0].TradeProducts
                    |> List.map _.HPK
                    |> Expect.equal "trade products narrowed to H3" [ "H3" ]

                    result[0].Substances
                    |> Array.map _.Name
                    |> Expect.equal "substances narrowed to those carried by H3" [| "paracetamol" |]
                }

                test "without brand or hpk the substances are not narrowed" {
                    let result =
                        prods
                        |> Product.filter routeMapping "oraal" "paracetamol" "" "" [||] [||]
                        |> Array.find (fun p -> p.GPK = "1002")

                    result.Substances
                    |> Array.map _.Name
                    |> Expect.equal "all substances of the product kept" [| "paracetamol"; "sorbitol" |]
                }
            ]


module DoseRuleProductTests =


    open Expecto
    open Expecto.Flip
    open Informedica.GenForm.Lib


    /// String non-empty (avoids depending on a BCL open in this module).
    let private ne s =
        s |> System.String.IsNullOrWhiteSpace |> not


    module PT = ProductFilterTests

    let private bp = PT.baseProd
    let private sCita = PT.subst "citalopram"
    let private sBup = PT.subst "bupropion"
    let private sAdr = PT.subst "adrenaline"
    let private sEth = PT.subst "ethanol"


    /// Route mapping covering the routes used by the fixtures.
    let routeMapping: RouteMapping[] =
        [|
            {
                Long = "ORAAL"
                Short = "or"
            }
            {
                Long = "INTRAMUSCULAIR"
                Short = "im"
            }
        |]


    /// Minimal product set reproducing the real attachment behaviour:
    /// two citalopram forms, two bupropion brands (one shared, one Wellbutrin-only),
    /// three adrenaline injection products (two listed, one not).
    let prods: ProductComponent[] =
        [|
            { bp with
                GPK = "182729"
                Generic = "citalopram"
                Form = "tablet"
                Routes = [| "ORAAL" |]
                Substances = [| sCita |]
                TradeProducts = [ PT.trade "2905779" "" [ sCita ] ]
            }
            { bp with
                GPK = "106496"
                Generic = "citalopram"
                Form = "druppels voor oraal gebruik"
                Routes = [| "ORAAL" |]
                Substances = [| sEth; sCita |]
                TradeProducts = [ PT.trade "1165933" "CIPRAMIL" [ sCita ] ]
            }
            { bp with
                GPK = "109347"
                Generic = "bupropion"
                Form = "tablet met gereguleerde afgifte"
                Routes = [| "ORAAL" |]
                Substances = [| sBup |]
                TradeProducts =
                    [
                        PT.trade "1181572" "ZYBAN" [ sBup ]
                        PT.trade "1848437" "WELLBUTRIN" [ sBup ]
                    ]
            }
            { bp with
                GPK = "126969"
                Generic = "bupropion"
                Form = "tablet met gereguleerde afgifte"
                Routes = [| "ORAAL" |]
                Substances = [| sBup |]
                TradeProducts = [ PT.trade "1848445" "WELLBUTRIN" [ sBup ] ]
            }
            { bp with
                GPK = "170925"
                Generic = "adrenaline"
                Form = "injectievloeistof"
                Routes = [| "INTRAMUSCULAIR"; "INTRAVENEUS" |]
                Substances = [| sAdr |]
                TradeProducts = [ PT.trade "786993" "EPIPEN" [ sAdr ] ]
            }
            { bp with
                GPK = "170933"
                Generic = "adrenaline"
                Form = "injectievloeistof"
                Routes = [| "INTRAMUSCULAIR" |]
                Substances = [| sAdr |]
                TradeProducts = [ PT.trade "787000" "EPIPEN JUNIOR" [ sAdr ] ]
            }
            { bp with
                GPK = "170976"
                Generic = "adrenaline"
                Form = "injectievloeistof"
                Routes = [| "INTRAMUSCULAIR"; "INTRAVENEUS" |]
                Substances = [| sAdr |]
                TradeProducts = [ PT.trade "2590654" "" [ sAdr ] ]
            }
        |]


    // Neutral DoseRuleData defaults; mkData overrides only the narrowing fields.
    let private emptyGen: GenericData =
        {
            Name = ""
            Form = ""
            Brand = ""
            GPKs = [||]
            HPKs = [||]
        }

    let private emptyDL: DoseLimitData =
        {
            CmpBased = false
            Component = ""
            Substance = ""
            DoseUnit = ""
            MinQty = None
            MaxQty = None
            MinQtyAdj = None
            MaxQtyAdj = None
            MinPerTime = None
            MaxPerTime = None
            MinPerTimeAdj = None
            MaxPerTimeAdj = None
            MinRate = None
            MaxRate = None
            MinRateAdj = None
            MaxRateAdj = None
        }

    let private emptySched: ScheduleData =
        {
            // Once is always valid under doseRuleDataValidity, so each fixture
            // survives addProductsWithWarnings without schedule plumbing.
            DoseType = "once"
            DoseText = ""
            Freqs = [||]
            AdjustUnit = ""
            FreqUnit = ""
            RateUnit = ""
            MinTime = None
            MaxTime = None
            TimeUnit = ""
            MinInt = None
            MaxInt = None
            IntUnit = ""
            MinDur = None
            MaxDur = None
            DurUnit = ""
            DoseLimitData = emptyDL
        }

    let private emptyPat: PatientCategoryData =
        {
            Location = ""
            Dep = ""
            IsAdult = false
            Gender = AnyGender
            MinAge = None
            MaxAge = None
            MinWeight = None
            MaxWeight = None
            MinBSA = None
            MaxBSA = None
            MinGestAge = None
            MaxGestAge = None
            MinPMAge = None
            MaxPMAge = None
        }

    let private baseData: DoseRuleData =
        {
            Id = ""
            GrpId = ""
            SortNo = 1
            Source = "FTK"
            SourceText = ""
            Generic = emptyGen
            Indication = "test"
            Route = ""
            PatientText = ""
            Patient = emptyPat
            ScheduleText = ""
            ScheduleData = emptySched
            Products = [||]
        }


    /// Build a DoseRuleData row with the given generic, route and narrowing.
    /// Component is set to the generic so products match by component name.
    let mkData gen rte form brand gpks hpks : DoseRuleData =
        { baseData with
            Route = rte
            Generic =
                { emptyGen with
                    Name = gen
                    Form = form
                    Brand = brand
                    GPKs = gpks
                    HPKs = hpks
                }
            ScheduleData = { emptySched with DoseLimitData = { emptyDL with Component = gen } }
        }


    /// Build the DoseRules for the given raw rows (empty FormRoutes is safe:
    /// addFormLimits only sets FormLimit, product attachment is unaffected).
    let buildRules (data: DoseRuleData[]) =
        DoseRule.fromData routeMapping [||] prods data |> Result.toOption |> Option.get


    /// Sorted, distinct GPKs attached to a set of DoseRules.
    let attachedGpks (rules: DoseRule[]) =
        rules
        |> Array.collect (fun r -> r.ComponentLimits |> Array.collect _.Products)
        |> Array.map _.GPK
        |> Array.filter ne
        |> Array.distinct
        |> Array.sort


    let builtGpks data = data |> buildRules |> attachedGpks


    /// Apply the production single-narrowing normaliser (as parseDoseRuleData does).
    let normalize (d: DoseRuleData) =
        { d with Generic = d.Generic |> DoseRule.withSingleNarrowing }


    let formRow = mkData "citalopram" "ORAAL" "tablet" "" [||] [||]
    let brandRow = mkData "bupropion" "ORAAL" "" "Zyban" [||] [||]

    let gpksRow =
        mkData "adrenaline" "INTRAMUSCULAIR" "" "" [| "170925"; "170933" |] [||]
    // Same gpks narrowing, plus a Form that would (if applied) exclude every
    // adrenaline injection product — proves Form is dropped when GPKs win.
    let gpksFormRow =
        mkData "adrenaline" "INTRAMUSCULAIR" "tablet" "" [| "170925"; "170933" |] [||]


    let tests =
        testList
            "DoseRule.fromData product attachment"
            [

                test "form narrowing attaches only the matching-form product" {
                    builtGpks [| formRow |]
                    |> Expect.equal "only the tablet GPK attaches (drank/druppels excluded)" [| "182729" |]
                }

                test "brand narrowing attaches only the branded product" {
                    let rules = buildRules [| brandRow |]

                    rules
                    |> attachedGpks
                    |> Expect.equal "only the Zyban-carrying GPK attaches" [| "109347" |]

                    rules
                    |> Array.collect (fun r -> r.ComponentLimits |> Array.collect _.Products)
                    |> Array.collect (_.TradeProducts >> List.toArray)
                    |> Array.map _.Brand
                    |> Array.distinct
                    |> Array.sort
                    |> Expect.equal "trade products narrowed to the Zyban brand" [| "ZYBAN" |]
                }

                test "gpks narrowing attaches exactly the listed gpks" {
                    builtGpks [| gpksRow |]
                    |> Expect.equal "the two listed GPKs attach (170976 excluded)" [| "170925"; "170933" |]
                }

                test "precedence: GPKs win over Form (Form is ignored)" {
                    let normalized = builtGpks [| normalize gpksFormRow |]

                    normalized
                    |> Expect.equal "normalised row attaches the GPK-narrowed products" [| "170925"; "170933" |]

                    // Without normalisation the conflicting Form would exclude all
                    // injection products: this contrast proves Form was dropped.
                    let raw = builtGpks [| gpksFormRow |]

                    raw |> Expect.isEmpty "raw (unnormalised) gpks+form row attaches nothing"

                    normalized |> Expect.notEqual "normalisation changes the outcome" raw
                }

                test "each narrowed row yields exactly one DoseRule with products" {
                    for row in [| formRow; brandRow; gpksRow |] do
                        let rules = buildRules [| row |]

                        rules |> Array.length |> Expect.equal "one DoseRule built" 1

                        rules[0].ComponentLimits
                        |> Array.collect _.Products
                        |> Array.isEmpty
                        |> Expect.isFalse "the DoseRule carries attached products"
                }

                // --- unit tests for the extracted addProductsWithWarnings helpers ---

                test "groupRows groups by selection key" {
                    let r1 = mkData "citalopram" "ORAAL" "tablet" "" [||] [||]
                    let r3 = mkData "citalopram" "INTRAMUSCULAIR" "tablet" "" [||] [||]

                    DoseRule.groupRows [| r1; r1; r3 |]
                    |> Array.length
                    |> Expect.equal "same key merges, different route splits" 2
                }

                test "candidateProducts keeps only products matching a group component" {
                    DoseRule.candidateProducts prods [| mkData "citalopram" "ORAAL" "" "" [||] [||] |]
                    |> Array.map _.Generic
                    |> Array.distinct
                    |> Array.sort
                    |> Expect.equal "only citalopram products" [| "citalopram" |]
                }

                test "expandRowByForm yields one row per pharmaceutical form" {
                    let cit = mkData "citalopram" "ORAAL" "" "" [||] [||]
                    let citProds = prods |> Array.filter (fun p -> p.Generic = "citalopram")

                    DoseRule.expandRowByForm routeMapping (DoseRule.groupKey cit) cit citProds
                    |> Array.map (fun r -> r.Products |> Array.map _.GPK |> Array.sort)
                    |> Array.sortBy (Array.tryHead >> Option.defaultValue "")
                    |> Expect.equal
                        "one row per form, each with that form's product"
                        [| [| "106496" |]; [| "182729" |] |]
                }

                test "processGroup with no matching products yields a placeholder row and one warning" {
                    let nope = mkData "citalopram" "ORAAL" "" "" [| "999999" |] [||]

                    let rows, warns =
                        DoseRule.processGroup routeMapping prods (DoseRule.groupKey nope, [| nope |])

                    rows |> Array.length |> Expect.equal "one placeholder row" 1

                    rows
                    |> Array.collect _.Products
                    |> Array.map _.GPK
                    |> Expect.equal "placeholder carries no real GPK" [| "" |]

                    warns |> List.length |> Expect.equal "one no-products warning" 1
                }

                test "processGroup with matching products yields expanded rows and no warnings" {
                    let adr = mkData "adrenaline" "INTRAMUSCULAIR" "" "" [| "170925"; "170933" |] [||]

                    let rows, warns =
                        DoseRule.processGroup routeMapping prods (DoseRule.groupKey adr, [| adr |])

                    warns |> Expect.isEmpty "no warnings"

                    rows
                    |> Array.collect _.Products
                    |> Array.map _.GPK
                    |> Array.filter ne
                    |> Array.distinct
                    |> Array.sort
                    |> Expect.equal "expanded rows carry the gpks-narrowed products" [| "170925"; "170933" |]
                }
            ]


module Tests =


    open MathNet.Numerics
    open Expecto
    open Expecto.Flip

    open Informedica.GenCore.Lib.Ranges
    open Informedica.GenUnits.Lib
    open Informedica.GenForm.Lib


    module DoseLimitTests =

        open Informedica.Utils.Lib.BCL

        let tests =
            testList
                "Dose Limit to string tests"
                [

                    test "printMinMaxDose with empty MinMax returns empty string" {
                        let result = DoseLimit.printMinMaxDose "[qty]" "/dosis" MinMax.empty

                        result |> Expect.equal "should be empty" ""
                    }

                    test "printMinMaxDose with label and empty perDose" {
                        let minMax =
                            {
                                Min = Some(Limit.Inclusive(ValueUnit.singleWithUnit Units.Mass.milliGram 10N))
                                Max = Some(Limit.Inclusive(ValueUnit.singleWithUnit Units.Mass.milliGram 20N))
                            }

                        let result = DoseLimit.printMinMaxDose "[rate]" "" minMax

                        result |> Expect.isNonEmpty "should contain value"

                        result
                        |> fun s -> s.Contains("[rate]")
                        |> Expect.isTrue "should contain label"
                    }

                    test "printMinMaxDose with label and perDose suffix" {
                        let minMax =
                            {
                                Min = Some(Limit.Inclusive(ValueUnit.singleWithUnit Units.Mass.milliGram 10N))
                                Max = Some(Limit.Inclusive(ValueUnit.singleWithUnit Units.Mass.milliGram 20N))
                            }

                        let result = DoseLimit.printMinMaxDose "[qty]" "/dosis" minMax

                        result
                        |> fun s -> s.Contains("/dosis")
                        |> Expect.isTrue "should contain perDose suffix"

                        result |> (fun s -> s.Contains("[qty]")) |> Expect.isTrue "should contain label"
                    }

                    test "printMinMaxDose with empty label uses decimal format" {
                        let minMax =
                            {
                                Min = Some(Limit.Inclusive(ValueUnit.singleWithUnit Units.Mass.milliGram 10N))
                                Max = Some(Limit.Inclusive(ValueUnit.singleWithUnit Units.Mass.milliGram 20N))
                            }

                        let result = DoseLimit.printMinMaxDose "" "/dosis" minMax

                        result |> Expect.isNonEmpty "should contain value"

                        // When label is empty, uses range format "10 - 20 mg/dosis" (not "min"/"max" prefixes)
                        result
                        |> fun s ->
                            s.Contains("10")
                            && s.Contains("20")
                            && s.Contains("-")
                            && s.Contains("mg")
                            && s.Contains("/dosis")
                        |> Expect.isTrue "should contain range with values, unit and perDose suffix"
                    }

                    test "printMinMaxDose with norm dose (min equals max) returns single value" {
                        let minMax =
                            {
                                Min = Some(Limit.Inclusive(ValueUnit.singleWithUnit Units.Mass.milliGram 15N))
                                Max = Some(Limit.Inclusive(ValueUnit.singleWithUnit Units.Mass.milliGram 15N))
                            }

                        let result = DoseLimit.printMinMaxDose "[qty]" "/dosis" minMax

                        result |> Expect.isNonEmpty "should contain value"

                        // Should not contain "min" or "max" when it's a norm dose
                        result
                        |> String.toLower
                        |> fun s -> s.Contains("min") || s.Contains("max")
                        |> Expect.isFalse "should not contain min/max for norm dose"
                    }

                    test "toString with empty DoseLimit returns only target" {
                        let dl = DoseLimit.limit

                        let result = dl |> DoseLimit.toString

                        result |> Expect.isNonEmpty "should contain at least target"
                    }

                    test "toString with quantity includes [qty] label and /dosis suffix" {
                        let dl =
                            { DoseLimit.limit with
                                Quantity =
                                    {
                                        Min = Some(Limit.Inclusive(ValueUnit.singleWithUnit Units.Mass.milliGram 10N))
                                        Max = Some(Limit.Inclusive(ValueUnit.singleWithUnit Units.Mass.milliGram 20N))
                                    }
                            }

                        let result = dl |> DoseLimit.toString |> List.head

                        result
                        |> fun s -> s.Contains("[qty]")
                        |> Expect.isTrue "should contain [qty] label"

                        result
                        |> fun s -> s.Contains("/dosis")
                        |> Expect.isTrue "should contain /dosis suffix"
                    }

                    test "toString with PerTime includes [per-time] label" {
                        let dl =
                            { DoseLimit.limit with
                                PerTime =
                                    {
                                        Min = Some(Limit.Inclusive(ValueUnit.singleWithUnit Units.Time.day 1N))
                                        Max = Some(Limit.Inclusive(ValueUnit.singleWithUnit Units.Time.day 2N))
                                    }
                            }

                        let result = dl |> DoseLimit.toString |> List.head

                        result
                        |> fun s -> s.Contains("[per-time]")
                        |> Expect.isTrue "should contain [per-time] label"
                    }

                    test "toString with multiple fields includes all labels" {
                        let dl =
                            { DoseLimit.limit with
                                Quantity =
                                    {
                                        Min = Some(Limit.Inclusive(ValueUnit.singleWithUnit Units.Mass.milliGram 10N))
                                        Max = Some(Limit.Inclusive(ValueUnit.singleWithUnit Units.Mass.milliGram 20N))
                                    }
                                PerTime =
                                    {
                                        Min = Some(Limit.Inclusive(ValueUnit.singleWithUnit Units.Time.day 1N))
                                        Max = Some(Limit.Inclusive(ValueUnit.singleWithUnit Units.Time.day 2N))
                                    }
                            }

                        let result = dl |> DoseLimit.toString |> List.head

                        result
                        |> fun s -> s.Contains("[qty]")
                        |> Expect.isTrue "should contain [qty] label"

                        result
                        |> fun s -> s.Contains("[per-time]")
                        |> Expect.isTrue "should contain [per-time] label"
                    }

                    test "toString with PerTimeAdjust includes adjust labels" {
                        let dl =
                            { DoseLimit.limit with
                                PerTimeAdjust =
                                    {
                                        Min = Some(Limit.Inclusive(ValueUnit.singleWithUnit Units.Time.day 1N))
                                        Max = Some(Limit.Inclusive(ValueUnit.singleWithUnit Units.Time.day 3N))
                                    }
                            }

                        let result = dl |> DoseLimit.toString |> List.head

                        result
                        |> fun s -> s.Contains("[per-time-adj]")
                        |> Expect.isTrue "should contain [per-time-adj] label"
                    }

                    test "isNormDose returns true when min equals max" {
                        let minMax =
                            {
                                Min = Some(Limit.Inclusive(ValueUnit.singleWithUnit Units.Mass.milliGram 15N))
                                Max = Some(Limit.Inclusive(ValueUnit.singleWithUnit Units.Mass.milliGram 15N))
                            }

                        minMax |> DoseLimit.isNormDose |> Expect.isTrue "should be norm dose"
                    }

                    test "isNormDose returns false when min differs from max" {
                        let minMax =
                            {
                                Min = Some(Limit.Inclusive(ValueUnit.singleWithUnit Units.Mass.milliGram 10N))
                                Max = Some(Limit.Inclusive(ValueUnit.singleWithUnit Units.Mass.milliGram 20N))
                            }

                        minMax |> DoseLimit.isNormDose |> Expect.isFalse "should not be norm dose"
                    }

                    test "getNormDose returns Some when min equals max" {
                        let minMax =
                            {
                                Min = Some(Limit.Inclusive(ValueUnit.singleWithUnit Units.Mass.milliGram 15N))
                                Max = Some(Limit.Inclusive(ValueUnit.singleWithUnit Units.Mass.milliGram 15N))
                            }

                        minMax |> DoseLimit.getNormDose |> Expect.isSome "should return Some"
                    }

                    test "getNormDose returns None when min differs from max" {
                        let minMax =
                            {
                                Min = Some(Limit.Inclusive(ValueUnit.singleWithUnit Units.Mass.milliGram 10N))
                                Max = Some(Limit.Inclusive(ValueUnit.singleWithUnit Units.Mass.milliGram 20N))
                            }

                        minMax |> DoseLimit.getNormDose |> Expect.isNone "should return None"
                    }
                ]


    module AdjustDoseLimitTests =

        let mkLimit v u =
            Limit.Inclusive(ValueUnit.singleWithUnit u v)

        let mg = Units.Mass.milliGram
        let mgPerKg = Units.Mass.milliGram |> ValueUnit.per Units.Weight.kiloGram
        let kg = Units.Weight.kiloGram

        let pat15kg =
            { Patient.patient with Weight = Some(ValueUnit.singleWithUnit kg 15N) }

        let tests =
            testList
                "adjustDoseLimitToPatient tests"
                [
                    test "no change when adjust min * adj < absolute max" {
                        let dl =
                            { DoseLimit.limit with
                                AdjustUnit = Some kg
                                DoseUnit = mg
                                Quantity =
                                    {
                                        Min = None
                                        Max = Some(mkLimit 100N mg)
                                    }
                                QuantityAdjust =
                                    {
                                        Min = Some(mkLimit 1N mgPerKg)
                                        Max = None
                                    }
                            }

                        let result = dl |> PrescriptionRule.adjustDoseLimitToPatient None pat15kg

                        result.QuantityAdjust
                        |> Expect.notEqual "adjust should be preserved" MinMax.empty
                    }

                    test "pins to max when adjust min * adj >= absolute max" {
                        let dl =
                            { DoseLimit.limit with
                                AdjustUnit = Some kg
                                DoseUnit = mg
                                Quantity =
                                    {
                                        Min = None
                                        Max = Some(mkLimit 10N mg)
                                    }
                                QuantityAdjust =
                                    {
                                        Min = Some(mkLimit 1N mgPerKg)
                                        Max = None
                                    }
                            }

                        let result = dl |> PrescriptionRule.adjustDoseLimitToPatient None pat15kg

                        result.QuantityAdjust |> Expect.equal "adjust should be cleared" MinMax.empty

                        result.Quantity.Min
                        |> Expect.equal "min should be pinned to max" result.Quantity.Max
                    }

                    test "no change when adjust max * adj > absolute min" {
                        let dl =
                            { DoseLimit.limit with
                                AdjustUnit = Some kg
                                DoseUnit = mg
                                Quantity =
                                    {
                                        Min = Some(mkLimit 10N mg)
                                        Max = None
                                    }
                                QuantityAdjust =
                                    {
                                        Min = None
                                        Max = Some(mkLimit 1N mgPerKg)
                                    }
                            }

                        let result = dl |> PrescriptionRule.adjustDoseLimitToPatient None pat15kg

                        result.QuantityAdjust
                        |> Expect.notEqual "adjust should be preserved" MinMax.empty
                    }

                    test "pins to min when adjust max * adj <= absolute min" {
                        let dl =
                            { DoseLimit.limit with
                                AdjustUnit = Some kg
                                DoseUnit = mg
                                Quantity =
                                    {
                                        Min = Some(mkLimit 20N mg)
                                        Max = None
                                    }
                                QuantityAdjust =
                                    {
                                        Min = None
                                        Max = Some(mkLimit 1N mgPerKg)
                                    }
                            }

                        let result = dl |> PrescriptionRule.adjustDoseLimitToPatient None pat15kg

                        result.QuantityAdjust |> Expect.equal "adjust should be cleared" MinMax.empty

                        result.Quantity.Max
                        |> Expect.equal "max should be pinned to min" result.Quantity.Min
                    }

                    test "no change when no adjust unit" {
                        let dl =
                            { DoseLimit.limit with
                                DoseUnit = mg
                                Quantity =
                                    {
                                        Min = Some(mkLimit 10N mg)
                                        Max = Some(mkLimit 100N mg)
                                    }
                            }

                        let result = dl |> PrescriptionRule.adjustDoseLimitToPatient None pat15kg

                        result |> Expect.equal "should be unchanged" dl
                    }
                ]


    module PatientCategoryTests =


        let tests =
            testList
                "PatientCategory"
                [
                    let filter = Filter.doseFilter

                    let patCat =
                        {
                            Location = None
                            Department = None
                            Gender = AnyGender
                            Age = MinMax.empty |> AbsoluteAge
                            Weight = MinMax.empty
                            BSA = MinMax.empty
                            GestAge = MinMax.empty
                            PMAge = MinMax.empty
                            Access = AnyAccess
                        }

                    test "an empty filter and empty patient category" {
                        patCat |> PatientCategory.filter filter |> Expect.isTrue "should return true"
                    }

                    test "an empty filter and patient category with female gender" {
                        { patCat with Gender = Female }
                        |> PatientCategory.filter filter
                        |> Expect.isFalse "should return false"
                    }

                    test "a filter with female gender and patient category with female gender" {
                        { patCat with Gender = Female }
                        |> PatientCategory.filter { filter with DoseFilter.Patient.Gender = Female }
                        |> Expect.isTrue "should return true"
                    }

                    test "a filter with female gender and patient category with no gender" {
                        { patCat with Gender = AnyGender }
                        |> PatientCategory.filter { filter with DoseFilter.Patient.Gender = Female }
                        |> Expect.isTrue "should return true"
                    }

                    test "an empty filter and a patient category with a max age of 7" {
                        { patCat with
                            Age =
                                { MinMax.empty with
                                    Max = 7N |> ValueUnit.singleWithUnit Units.Time.day |> Limit.Inclusive |> Some
                                }
                                |> AbsoluteAge
                        }
                        |> PatientCategory.filter filter
                        |> Expect.isFalse "should return false"
                    }

                    test "a filter with age 5 and a patient category with a max age of 7" {
                        { patCat with
                            Age =
                                { MinMax.empty with
                                    Max = 7N |> ValueUnit.singleWithUnit Units.Time.day |> Limit.Inclusive |> Some
                                }
                                |> AbsoluteAge
                        }
                        |> PatientCategory.filter
                            { filter with
                                Patient =
                                    { filter.Patient with Age = 5N |> ValueUnit.singleWithUnit Units.Time.day |> Some }
                            }
                        |> Expect.isTrue "should return true"
                    }

                    test "a filter with age 5 and a patient category with a min age of 1 week" {
                        { patCat with
                            Age =
                                { MinMax.empty with
                                    Min = 1N |> ValueUnit.singleWithUnit Units.Time.week |> Limit.Inclusive |> Some
                                }
                                |> AbsoluteAge
                        }
                        |> PatientCategory.filter
                            { filter with
                                Patient =
                                    { filter.Patient with Age = 5N |> ValueUnit.singleWithUnit Units.Time.day |> Some }
                            }
                        |> Expect.isFalse "should return false"
                    }

                    test "a filter with age 5 and a patient category with a min age of 3 and max age of 7" {
                        { patCat with
                            Age =
                                {
                                    Min = 5N |> ValueUnit.singleWithUnit Units.Time.day |> Limit.Inclusive |> Some
                                    Max = 7N |> ValueUnit.singleWithUnit Units.Time.day |> Limit.Inclusive |> Some
                                }
                                |> AbsoluteAge
                        }
                        |> PatientCategory.filter
                            { filter with
                                Patient =
                                    { filter.Patient with Age = 5N |> ValueUnit.singleWithUnit Units.Time.day |> Some }
                            }
                        |> Expect.isTrue "should return true"
                    }

                    test
                        "a filter with age 5 with a patient category with a min age of 3 and max age of 7 and gender female" {
                        { patCat with
                            Gender = Female
                            Age =
                                {
                                    Min = 5N |> ValueUnit.singleWithUnit Units.Time.day |> Limit.Inclusive |> Some
                                    Max = 7N |> ValueUnit.singleWithUnit Units.Time.day |> Limit.Inclusive |> Some
                                }
                                |> AbsoluteAge
                        }
                        |> PatientCategory.filter
                            { filter with
                                Patient =
                                    { filter.Patient with Age = 5N |> ValueUnit.singleWithUnit Units.Time.day |> Some }
                            }
                        |> Expect.isFalse "should return false"
                    }

                    test
                        "a filter with age 5, gender female with a patient category with a min age of 3 and max age of 7 and gender female" {
                        { patCat with
                            Gender = Female
                            Age =
                                {
                                    Min = 5N |> ValueUnit.singleWithUnit Units.Time.day |> Limit.Inclusive |> Some
                                    Max = 7N |> ValueUnit.singleWithUnit Units.Time.day |> Limit.Inclusive |> Some
                                }
                                |> AbsoluteAge
                        }
                        |> PatientCategory.filter
                            { filter with
                                Patient =
                                    { filter.Patient with
                                        Gender = Female
                                        Age = 5N |> ValueUnit.singleWithUnit Units.Time.day |> Some
                                    }
                            }
                        |> Expect.isTrue "should return true"
                    }

                    test "a filter with age 0 and gestational age 30 weeks with an empty patient category" {
                        patCat
                        |> PatientCategory.filter
                            { filter with
                                Patient =
                                    { filter.Patient with
                                        Age = 0N |> ValueUnit.singleWithUnit Units.Time.day |> Some
                                        GestAge = 30N |> ValueUnit.singleWithUnit Units.Time.week |> Some
                                    }
                            }
                        |> Expect.isTrue "should return true"
                    }

                    test "a filter with age 0 and gestational age 30 weeks with a patient category with min age = 0" {
                        { patCat with
                            Age =
                                {
                                    Min = 0N |> ValueUnit.singleWithUnit Units.Time.day |> Limit.Inclusive |> Some
                                    Max = None
                                }
                                |> AbsoluteAge
                        }
                        |> PatientCategory.filter
                            { filter with
                                Patient =
                                    { filter.Patient with
                                        Age = 0N |> ValueUnit.singleWithUnit Units.Time.day |> Some
                                        GestAge = 30N |> ValueUnit.singleWithUnit Units.Time.week |> Some
                                    }
                            }
                        |> Expect.isFalse "should return false"
                    }

                    test
                        "a filter with age 0 and gestational age 28 weeks, weight = 1.15 kg, height = 46 cm, with a patient category with min age = 0" {
                        { patCat with
                            Age =
                                {
                                    Min = 0N |> ValueUnit.singleWithUnit Units.Time.day |> Limit.Inclusive |> Some
                                    Max = None
                                }
                                |> AbsoluteAge
                        }
                        |> PatientCategory.filter
                            { filter with
                                Patient =
                                    { filter.Patient with
                                        Age = 0N |> ValueUnit.singleWithUnit Units.Time.day |> Some
                                        GestAge = 28N |> ValueUnit.singleWithUnit Units.Time.week |> Some
                                        Weight = (115N / 100N) |> ValueUnit.singleWithUnit Units.Weight.kiloGram |> Some
                                        Height = 45N |> ValueUnit.singleWithUnit Units.Height.centiMeter |> Some
                                    }
                            }
                        |> Expect.isFalse "should return false"
                    }

                    test
                        "a filter with age 0 and gestational age 30 weeks with a patient category with a min age of 0 and max age of 28 and gestational age min 28 and max 32 weeks" {
                        { patCat with
                            Age =
                                {
                                    Min = 0N |> ValueUnit.singleWithUnit Units.Time.day |> Limit.Inclusive |> Some
                                    Max = 28N |> ValueUnit.singleWithUnit Units.Time.day |> Limit.Inclusive |> Some
                                }
                                |> AbsoluteAge
                            GestAge =
                                {
                                    Min = 28N |> ValueUnit.singleWithUnit Units.Time.week |> Limit.Inclusive |> Some
                                    Max = 32N |> ValueUnit.singleWithUnit Units.Time.week |> Limit.Inclusive |> Some
                                }
                        }
                        |> PatientCategory.filter
                            { filter with
                                Patient =
                                    { filter.Patient with
                                        Age = 0N |> ValueUnit.singleWithUnit Units.Time.day |> Some
                                        GestAge = 30N |> ValueUnit.singleWithUnit Units.Time.week |> Some
                                    }
                            }
                        |> Expect.isTrue "should return true"
                    }

                    test
                        "a filter with age 0 and gestational age 37 weeks with a patient category with a min age of 0 and max age of 28 and gestational age min 28 and max 32 weeks" {
                        { patCat with
                            Age =
                                {
                                    Min = 0N |> ValueUnit.singleWithUnit Units.Time.day |> Limit.Inclusive |> Some
                                    Max = 28N |> ValueUnit.singleWithUnit Units.Time.day |> Limit.Inclusive |> Some
                                }
                                |> AbsoluteAge
                            GestAge =
                                {
                                    Min = 28N |> ValueUnit.singleWithUnit Units.Time.week |> Limit.Inclusive |> Some
                                    Max = 32N |> ValueUnit.singleWithUnit Units.Time.week |> Limit.Inclusive |> Some
                                }
                        }
                        |> PatientCategory.filter
                            { filter with
                                Patient =
                                    { filter.Patient with
                                        Age = 0N |> ValueUnit.singleWithUnit Units.Time.day |> Some
                                        GestAge = 33N |> ValueUnit.singleWithUnit Units.Time.week |> Some
                                    }
                            }
                        |> Expect.isFalse "should return false"
                    }

                    test
                        "a filter with age 8 and gestational age 27 weeks with a patient category with a min age of 0 and max age of 28 and gestational age min 28 and max 32 weeks" {
                        { patCat with
                            Age =
                                {
                                    Min = 0N |> ValueUnit.singleWithUnit Units.Time.day |> Limit.Inclusive |> Some
                                    Max = 28N |> ValueUnit.singleWithUnit Units.Time.day |> Limit.Inclusive |> Some
                                }
                                |> AbsoluteAge
                            GestAge =
                                {
                                    Min = 28N |> ValueUnit.singleWithUnit Units.Time.week |> Limit.Inclusive |> Some
                                    Max = 32N |> ValueUnit.singleWithUnit Units.Time.week |> Limit.Inclusive |> Some
                                }
                        }
                        |> PatientCategory.filter
                            { filter with
                                Patient =
                                    { filter.Patient with
                                        Age = 8N |> ValueUnit.singleWithUnit Units.Time.day |> Some
                                        GestAge = 27N |> ValueUnit.singleWithUnit Units.Time.week |> Some
                                    }
                                    |> Patient.calcPMAge
                            }
                        |> Expect.isFalse "should return false"
                    }

                    test
                        "a filter with age 0 and gestational age 30 weeks with a patient category with a min age of 0 and max age of 28 and pm age min 28 and max 32 weeks" {
                        { patCat with
                            Age =
                                {
                                    Min = 0N |> ValueUnit.singleWithUnit Units.Time.day |> Limit.Inclusive |> Some
                                    Max = 28N |> ValueUnit.singleWithUnit Units.Time.day |> Limit.Inclusive |> Some
                                }
                                |> AbsoluteAge
                            PMAge =
                                {
                                    Min = 28N |> ValueUnit.singleWithUnit Units.Time.week |> Limit.Inclusive |> Some
                                    Max = 32N |> ValueUnit.singleWithUnit Units.Time.week |> Limit.Inclusive |> Some
                                }
                        }
                        |> PatientCategory.filter
                            { filter with
                                Patient =
                                    { filter.Patient with
                                        Age = 0N |> ValueUnit.singleWithUnit Units.Time.day |> Some
                                        GestAge = 30N |> ValueUnit.singleWithUnit Units.Time.week |> Some
                                    }
                                    |> Patient.calcPMAge
                            }

                        |> Expect.isTrue "should return true"
                    }

                    test
                        "a filter with age 0 and gestational age 37 weeks with a patient category with a min age of 0 and max age of 28 and pm age min 28 and max 32 weeks" {
                        { patCat with
                            Age =
                                {
                                    Min = 0N |> ValueUnit.singleWithUnit Units.Time.day |> Limit.Inclusive |> Some
                                    Max = 28N |> ValueUnit.singleWithUnit Units.Time.day |> Limit.Inclusive |> Some
                                }
                                |> AbsoluteAge
                            PMAge =
                                {
                                    Min = 28N |> ValueUnit.singleWithUnit Units.Time.week |> Limit.Inclusive |> Some
                                    Max = 32N |> ValueUnit.singleWithUnit Units.Time.week |> Limit.Inclusive |> Some
                                }
                        }
                        |> PatientCategory.filter
                            { filter with
                                Patient =
                                    { filter.Patient with
                                        Age = 0N |> ValueUnit.singleWithUnit Units.Time.day |> Some
                                        GestAge = 37N |> ValueUnit.singleWithUnit Units.Time.week |> Some
                                    }
                                    |> Patient.calcPMAge
                            }

                        |> Expect.isFalse "should return false"
                    }

                    test
                        "a filter with age 8 and gestational age 27 weeks with a patient category with a min age of 0 and max age of 28 and pm age min 28 and max 32 weeks" {
                        { patCat with
                            Age =
                                {
                                    Min = 0N |> ValueUnit.singleWithUnit Units.Time.day |> Limit.Inclusive |> Some
                                    Max = 28N |> ValueUnit.singleWithUnit Units.Time.day |> Limit.Inclusive |> Some
                                }
                                |> AbsoluteAge
                            PMAge =
                                {
                                    Min = 28N |> ValueUnit.singleWithUnit Units.Time.week |> Limit.Inclusive |> Some
                                    Max = 32N |> ValueUnit.singleWithUnit Units.Time.week |> Limit.Inclusive |> Some
                                }
                        }
                        |> PatientCategory.filter
                            { filter with
                                Patient =
                                    { filter.Patient with
                                        Age = 8N |> ValueUnit.singleWithUnit Units.Time.day |> Some
                                        GestAge = 27N |> ValueUnit.singleWithUnit Units.Time.week |> Some
                                    }
                                    |> Patient.calcPMAge
                            }
                        |> Expect.isTrue "should return true"
                    }

                    test
                        "a filter with age 0, ga = 32 and weight 1.45 with a patient category with max age = 30 and max gest 37 and max weight 1.5" {
                        { patCat with
                            Age =
                                { MinMax.empty with
                                    Max = 30N |> ValueUnit.singleWithUnit Units.Time.day |> Limit.Inclusive |> Some
                                }
                                |> AbsoluteAge
                            Weight =
                                { patCat.Weight with
                                    Max =
                                        1.5m
                                        |> BigRational.FromDecimal
                                        |> ValueUnit.singleWithUnit Units.Weight.kiloGram
                                        |> Limit.Inclusive
                                        |> Some
                                }
                            GestAge =
                                { patCat.GestAge with
                                    Max = 37N |> ValueUnit.singleWithUnit Units.Time.week |> Limit.Inclusive |> Some
                                }
                        }
                        |> PatientCategory.filter
                            { filter with
                                Patient =
                                    { filter.Patient with
                                        Age = 0N |> ValueUnit.singleWithUnit Units.Time.day |> Some
                                        Weight =
                                            1.45m
                                            |> BigRational.FromDecimal
                                            |> ValueUnit.singleWithUnit Units.Weight.kiloGram
                                            |> Some
                                        GestAge = 32N |> ValueUnit.singleWithUnit Units.Time.week |> Some
                                    }
                            }

                        |> Expect.isTrue "should return true"
                    }

                    test
                        "a filter with age 0, ga = 32 and weight 1.45 with a patient category with min age = 30 and max age = 720" {
                        { patCat with
                            Age =
                                {
                                    Min = 30N |> ValueUnit.singleWithUnit Units.Time.day |> Limit.Inclusive |> Some
                                    Max = 720N |> ValueUnit.singleWithUnit Units.Time.day |> Limit.Inclusive |> Some
                                }
                                |> AbsoluteAge
                        }
                        |> PatientCategory.filter
                            { filter with
                                Patient =
                                    { filter.Patient with
                                        Age = 0N |> ValueUnit.singleWithUnit Units.Time.day |> Some
                                        Weight = (145N / 10N) |> ValueUnit.singleWithUnit Units.Weight.kiloGram |> Some
                                        GestAge = 32N |> ValueUnit.singleWithUnit Units.Time.week |> Some
                                    }
                            }

                        |> Expect.isFalse "should return false"
                    }

                    test
                        "a filter with age 0, ga = None and weight 3500 gram with a patient category with pm age = 36 and max age = 37" {
                        { patCat with
                            Age =
                                {
                                    Min = 0N |> ValueUnit.singleWithUnit Units.Time.day |> Limit.Inclusive |> Some
                                    Max = 28N |> ValueUnit.singleWithUnit Units.Time.day |> Limit.Inclusive |> Some
                                }
                                |> AbsoluteAge
                            PMAge =
                                {
                                    Min = 36N |> ValueUnit.singleWithUnit Units.Time.week |> Limit.Inclusive |> Some
                                    Max = 37N |> ValueUnit.singleWithUnit Units.Time.week |> Limit.Exclusive |> Some
                                }
                        }
                        |> PatientCategory.filter
                            { filter with
                                Patient =
                                    { filter.Patient with
                                        Age = 0N |> ValueUnit.singleWithUnit Units.Time.day |> Some
                                        Weight = 3500N |> ValueUnit.singleWithUnit Units.Weight.gram |> Some
                                    }
                            }

                        |> Expect.isFalse "should return false"
                    }

                    test "an empty patient category is a match with another empty patient category" {
                        PatientCategory.empty
                        |> PatientCategory.isMatch PatientCategory.empty
                        |> Expect.isTrue "should return true"
                    }

                    testList
                        "patient category tests"
                        [

                            fun minAge maxAge ->
                                let minAge = if minAge < 0N then None else Some minAge
                                let maxAge = if maxAge < 0N then None else Some maxAge
                                let minAge, maxAge = if minAge > maxAge then maxAge, minAge else minAge, maxAge

                                let patCatToMatch =
                                    { PatientCategory.empty with
                                        Age =
                                            {
                                                Min =
                                                    minAge
                                                    |> Option.map (
                                                        ValueUnit.singleWithUnit Units.Time.day >> Limit.Inclusive
                                                    )
                                                Max =
                                                    maxAge
                                                    |> Option.map (
                                                        ValueUnit.singleWithUnit Units.Time.day >> Limit.Inclusive
                                                    )
                                            }
                                            |> AbsoluteAge
                                    }

                                let emptyPatCat = PatientCategory.empty
                                emptyPatCat |> PatientCategory.isMatch patCatToMatch
                            |> Generators.testProp
                                "a patient cat with age should always match an empty patient category"

                            fun minWeight maxWeight ->
                                let minWeight = if minWeight < 0N then None else Some minWeight
                                let maxWeight = if maxWeight < 0N then None else Some maxWeight

                                let minWeight, maxWeight =
                                    if minWeight > maxWeight then
                                        maxWeight, minWeight
                                    else
                                        minWeight, maxWeight

                                let patCatToMatch =
                                    { PatientCategory.empty with
                                        Weight =
                                            {
                                                Min =
                                                    minWeight
                                                    |> Option.map (
                                                        ValueUnit.singleWithUnit Units.Weight.kiloGram
                                                        >> Limit.Inclusive
                                                    )
                                                Max =
                                                    maxWeight
                                                    |> Option.map (
                                                        ValueUnit.singleWithUnit Units.Weight.kiloGram
                                                        >> Limit.Inclusive
                                                    )
                                            }
                                    }

                                let emptyPatCat = PatientCategory.empty
                                emptyPatCat |> PatientCategory.isMatch patCatToMatch
                            |> Generators.testProp
                                "a patient cat with weight should always match an empty patient category"

                            fun gender ->
                                let patCatToMatch = { PatientCategory.empty with Gender = gender }
                                let emptyPatCat = PatientCategory.empty
                                emptyPatCat |> PatientCategory.isMatch patCatToMatch
                            |> Generators.testProp
                                "a patient cat with a gender should always match an empty patient category"

                            fun location ->
                                let patCatToMatch = { PatientCategory.empty with Access = location }
                                let emptyPatCat = PatientCategory.empty
                                emptyPatCat |> PatientCategory.isMatch patCatToMatch
                            |> Generators.testProp
                                "a patient cat with a location should always match an empty patient category"

                            fun minAge maxAge ->
                                let minAge = if minAge < 0N then Some 0N else Some minAge
                                let maxAge = if maxAge < 0N then Some 0N else Some maxAge
                                let minAge, maxAge = if minAge > maxAge then maxAge, minAge else minAge, maxAge

                                let notEmptyPatCat =
                                    { PatientCategory.empty with
                                        Age =
                                            {
                                                Min =
                                                    minAge
                                                    |> Option.map (
                                                        ValueUnit.singleWithUnit Units.Time.day >> Limit.Inclusive
                                                    )
                                                Max =
                                                    maxAge
                                                    |> Option.map (
                                                        ValueUnit.singleWithUnit Units.Time.day >> Limit.Inclusive
                                                    )
                                            }
                                            |> AbsoluteAge
                                    }

                                let patCatToWatch = PatientCategory.empty
                                notEmptyPatCat |> PatientCategory.isMatch patCatToWatch |> not
                            |> Generators.testProp "an empty pat cat should never match an patient category with age"

                        ]
                ]


    module DoseTypeTests =


        let tests =
            testList
                "DoseType"
                [

                    test "sortBy Once = 0" { DoseType.sortBy (Once "") |> Expect.equal "Once should sort first" 0 }

                    test "sortBy OnceTimed = 0" {
                        DoseType.sortBy (OnceTimed "") |> Expect.equal "OnceTimed should sort first" 0
                    }

                    test "sortBy Discontinuous = 3" {
                        DoseType.sortBy (Discontinuous "")
                        |> Expect.equal "Discontinuous should sort at 3" 3
                    }

                    test "sortBy Timed = 3" { DoseType.sortBy (Timed "") |> Expect.equal "Timed should sort at 3" 3 }

                    test "sortBy Continuous = 4" {
                        DoseType.sortBy (Continuous "") |> Expect.equal "Continuous should sort at 4" 4
                    }

                    test "sortBy NoDoseType = 100" {
                        DoseType.sortBy NoDoseType |> Expect.equal "NoDoseType should sort last" 100
                    }

                    test "fromString 'once' produces Once constructor" {
                        let dt = DoseType.fromString "once" "eenmalig"

                        match dt with
                        | Once _ -> ()
                        | other -> failtest $"expected Once, got %A{other}"
                    }

                    test "fromString 'continuous' produces Continuous constructor" {
                        let dt = DoseType.fromString "continuous" "continu"

                        match dt with
                        | Continuous _ -> ()
                        | other -> failtest $"expected Continuous, got %A{other}"
                    }

                    test "fromString unknown input produces NoDoseType" {
                        DoseType.fromString "unknown" ""
                        |> Expect.equal "unknown type should give NoDoseType" NoDoseType
                    }

                    test "toString returns type for Once with empty text" {
                        DoseType.toString (Once "") |> Expect.equal "should be 'once'" "once"
                    }

                    test "toString returns type and text for Timed with text" {
                        DoseType.toString (Timed "onderhoud")
                        |> Expect.equal "should be 'timed onderhoud'" "timed onderhoud"
                    }

                    test "getText returns payload for Discontinuous" {
                        DoseType.getText (Discontinuous "onderhoud")
                        |> Expect.equal "should return 'onderhoud'" "onderhoud"
                    }

                    test "getText returns empty string for NoDoseType" {
                        DoseType.getText NoDoseType |> Expect.equal "NoDoseType → empty" ""
                    }

                    test "toDescription returns Dutch fallback for Once with empty text" {
                        DoseType.toDescription (Once "")
                        |> Expect.equal "Once fallback should be 'eenmalig'" "eenmalig"
                    }

                    test "toDescription returns Dutch fallback for Continuous with empty text" {
                        DoseType.toDescription (Continuous "")
                        |> Expect.equal "Continuous fallback should be 'continu'" "continu"
                    }

                    test "toDescription returns text when set" {
                        DoseType.toDescription (Once "special")
                        |> Expect.equal "explicit text should be returned" "special"
                    }

                    test "eqs is true for same constructor same text case-insensitive" {
                        DoseType.eqs (Once "A") (Once "a")
                        |> Expect.isTrue "case-insensitive eqs should match"
                    }

                    test "eqs is false for different constructors" {
                        DoseType.eqs (Once "a") (Continuous "a")
                        |> Expect.isFalse "different constructors should not be equal"
                    }

                    test "eqsType is true for same constructor different text" {
                        DoseType.eqsType (Once "A") (Once "B")
                        |> Expect.isTrue "same type regardless of text"
                    }

                    test "eqsType is false for different constructors" {
                        DoseType.eqsType (Once "") (Timed "")
                        |> Expect.isFalse "different constructors → false"
                    }

                    test "setDescription replaces text payload" {
                        DoseType.setDescription "new" (Once "old")
                        |> DoseType.getText
                        |> Expect.equal "payload should be replaced" "new"
                    }

                    test "setDescription preserves constructor" {
                        let dt = DoseType.setDescription "x" (Discontinuous "old")

                        match dt with
                        | Discontinuous _ -> ()
                        | other -> failtest $"constructor changed: %A{other}"
                    }
                ]


    module LimitTargetTests =


        let tests =
            testList
                "LimitTarget"
                [

                    test "toString NoLimitTarget returns empty string" {
                        LimitTarget.toString NoLimitTarget |> Expect.equal "should be empty" ""
                    }

                    test "toString OrderableLimitTarget returns empty string" {
                        LimitTarget.toString OrderableLimitTarget |> Expect.equal "should be empty" ""
                    }

                    test "toString SubstanceLimitTarget returns label" {
                        LimitTarget.toString (SubstanceLimitTarget "paracetamol")
                        |> Expect.equal "should return label" "paracetamol"
                    }

                    test "toString ComponentLimitTarget returns label" {
                        LimitTarget.toString (ComponentLimitTarget "comp1")
                        |> Expect.equal "should return label" "comp1"
                    }

                    test "isOrderableTarget true only for OrderableLimitTarget" {
                        LimitTarget.isOrderableTarget OrderableLimitTarget
                        |> Expect.isTrue "OrderableLimitTarget should match"
                    }

                    test "isOrderableTarget false for SubstanceLimitTarget" {
                        LimitTarget.isOrderableTarget (SubstanceLimitTarget "x")
                        |> Expect.isFalse "SubstanceLimitTarget should not match"
                    }

                    test "isComponentTarget true for ComponentLimitTarget" {
                        LimitTarget.isComponentTarget (ComponentLimitTarget "c")
                        |> Expect.isTrue "ComponentLimitTarget should match"
                    }

                    test "isSubstanceTarget true for SubstanceLimitTarget" {
                        LimitTarget.isSubstanceTarget (SubstanceLimitTarget "s")
                        |> Expect.isTrue "SubstanceLimitTarget should match"
                    }

                    test "isSubstanceTarget false for ComponentLimitTarget" {
                        LimitTarget.isSubstanceTarget (ComponentLimitTarget "c")
                        |> Expect.isFalse "ComponentLimitTarget is not substance target"
                    }

                    test "componentTargetToString returns label for ComponentLimitTarget" {
                        LimitTarget.componentTargetToString (ComponentLimitTarget "comp")
                        |> Expect.equal "should return label" "comp"
                    }

                    test "componentTargetToString returns empty for SubstanceLimitTarget" {
                        LimitTarget.componentTargetToString (SubstanceLimitTarget "s")
                        |> Expect.equal "non-component → empty" ""
                    }

                    test "substanceTargetToString returns label for SubstanceLimitTarget" {
                        LimitTarget.substanceTargetToString (SubstanceLimitTarget "sub")
                        |> Expect.equal "should return label" "sub"
                    }

                    test "substanceTargetToString returns empty for ComponentLimitTarget" {
                        LimitTarget.substanceTargetToString (ComponentLimitTarget "c")
                        |> Expect.equal "non-substance → empty" ""
                    }

                    test "DoseLimit.isSubstanceLimit true when SubstanceLimitTarget is set" {
                        let dl =
                            { DoseLimit.limit with DoseLimitTarget = SubstanceLimitTarget "paracetamol" }

                        dl
                        |> DoseLimit.isSubstanceLimit
                        |> Expect.isTrue "should detect substance limit"
                    }

                    test "DoseLimit.isComponentLimit true when ComponentLimitTarget is set" {
                        let dl = { DoseLimit.limit with DoseLimitTarget = ComponentLimitTarget "comp" }

                        dl
                        |> DoseLimit.isComponentLimit
                        |> Expect.isTrue "should detect component limit"
                    }

                    test "DoseLimit.isShapeLimit true when OrderableLimitTarget is set" {
                        let dl = { DoseLimit.limit with DoseLimitTarget = OrderableLimitTarget }

                        dl
                        |> DoseLimit.isShapeLimit
                        |> Expect.isTrue "should detect shape/orderable limit"
                    }
                ]


    /// IR Doseringscontrole V-5-0-1 conformance fixes (see code review
    /// docs/code-reviews/genform-check-vs-ir-doseringscontrole-v5-0-1.md).
    module CheckTests =

        open Informedica.Utils.Lib.BCL

        module ZFDR = Informedica.ZForm.Lib.DoseRule

        let private maxVal (mm: MinMax) =
            mm.Max
            |> Option.map (Limit.getValueUnit >> ValueUnit.getValue >> Array.map BigRational.toDouble)

        let private minVal (mm: MinMax) =
            mm.Min
            |> Option.map (Limit.getValueUnit >> ValueUnit.getValue >> Array.map BigRational.toDouble)

        let private mk v u =
            v |> ValueUnit.singleWithUnit u |> Limit.inclusive

        let private mmOf vmin vmax u =
            {
                Min = Some(mk vmin u)
                Max = Some(mk vmax u)
            }

        let private adjUnitStr (mm: MinMax) =
            match mm.Min |> Option.map Limit.getValueUnit with
            | Some vu -> vu |> ValueUnit.getUnit |> Check.unitToString
            | None -> "<empty>"

        let private convKg = Check.setAdjustAndOrTimeUnit (Some Units.Weight.kiloGram) None
        let private convM2 = Check.setAdjustAndOrTimeUnit (Some Units.BSA.m2) None

        let private mkMgMax (v: BigRational) =
            { MinMax.empty with Max = v |> ValueUnit.singleWithUnit Units.Mass.milliGram |> Limit.inclusive |> Some }

        let private mkDosage normMax absMax : Informedica.ZForm.Lib.Types.Dosage =
            { ZFDR.Dosage.empty with
                SingleDosage =
                    { ZFDR.DoseRange.empty with
                        Norm = mkMgMax normMax
                        Abs = mkMgMax absMax
                    }
            }

        let tests =
            testList
                "Check IR-doseringscontrole fixes"
                [
                    test "MEDIUM-1 pickAdjust prefers BSA when both present" {
                        Check.pickAdjust
                            (mmOf 5N 10N Units.Mass.milliGram)
                            (mmOf 50N 100N Units.Mass.milliGram)
                            convKg
                            convM2
                        |> adjUnitStr
                        |> Expect.equal "BSA wins" "mg/m2"
                    }

                    test "MEDIUM-1 pickAdjust weight only falls back to kg" {
                        Check.pickAdjust (mmOf 5N 10N Units.Mass.milliGram) MinMax.empty convKg convM2
                        |> adjUnitStr
                        |> Expect.equal "kg" "mg/kg"
                    }

                    test "MEDIUM-1 pickAdjust neither yields empty" {
                        Check.pickAdjust MinMax.empty MinMax.empty convKg convM2
                        |> Expect.equal "empty" MinMax.empty
                    }

                    test "MEDIUM-2 classify within/advisory/absolute/under" {
                        Check.classify 5N (Some 10N) (Some 20N) (Some 2N)
                        |> Expect.equal "within" Check.Within

                        Check.classify 15N (Some 10N) (Some 20N) (Some 2N)
                        |> Expect.equal "advisory" Check.AdvisoryOverNorm

                        Check.classify 25N (Some 10N) (Some 20N) (Some 2N)
                        |> Expect.equal "absolute" Check.OverAbsolute

                        Check.classify 1N (Some 10N) (Some 20N) (Some 2N)
                        |> Expect.equal "under" Check.UnderNorm
                    }

                    test "HIGH-1 marginedTestRange is risk-aware and one-sided" {
                        let vu = 100N |> ValueUnit.singleWithUnit Units.Mass.milliGram |> Some

                        Check.marginedTestRange true (12N / 10N) vu
                        |> maxVal
                        |> Expect.equal "risk: no margin" (Some [| 100.0 |])

                        Check.marginedTestRange false (12N / 10N) vu
                        |> maxVal
                        |> Expect.equal "non-risk: 120%" (Some [| 120.0 |])

                        Check.marginedTestRange false (12N / 10N) vu
                        |> minVal
                        |> Expect.equal "min unchanged" (Some [| 100.0 |])
                    }

                    test "MEDIUM-3 monthsMinMaxToDays at 30 days/month" {
                        let d = Check.monthsMinMaxToDays (mmOf 1N 3N Units.Time.month)
                        d |> minVal |> Expect.equal "1mo -> 30d" (Some [| 30.0 |])
                        d |> maxVal |> Expect.equal "3mo -> 90d" (Some [| 90.0 |])
                    }

                    test "LOW-3 interchangeable frequency time units" {
                        Check.interchangeable "per maand" "per 4 weken"
                        |> Expect.isTrue "maand ~ 4 weken"

                        Check.interchangeable "per 12 weken" "per 3 maanden"
                        |> Expect.isFalse "12 weken != 3 maanden"
                    }

                    test "LOW-4 freqMsg granularity" {
                        Check.freqMsg true false |> Expect.equal "24" "tekst 24 (aantal verschilt)"

                        Check.freqMsg false true
                        |> Expect.equal "25" "tekst 25 (tijdseenheid verschilt)"

                        Check.freqMsg true true
                        |> Expect.equal "8" "tekst 8 (aantal en/of tijdseenheid verschilt)"
                    }

                    test "HIGH-2 rateScopeLabel labels or drops" {
                        Check.rateScopeLabel Check.LabelRateChecksOutOfScope "x"
                        |> Expect.equal "label" (Some "[buiten G-Standaard doseringscontrole] x")

                        Check.rateScopeLabel Check.DropRateChecks "x" |> Expect.equal "drop" None
                    }

                    test "BUG-B maximizeDosages merges Abs from Abs (not Norm)" {
                        [ mkDosage 3N 5N; mkDosage 3N 9N ]
                        |> Check.maximizeDosages
                        |> Option.bind (fun d -> d.SingleDosage.Abs.Max)
                        |> Option.map (Limit.getValueUnit >> ValueUnit.getValue >> Array.map BigRational.toDouble)
                        |> Expect.equal "Abs.Max 9 (was 3)" (Some [| 9.0 |])
                    }

                    test "BUG-A per-m2 absolute single-dose limit is picked up" {
                        // mirrors createMapping.quantityAdjustAbs: both ranges from
                        // SingleDosage (the m2 branch previously read StartDosage).
                        let single = { ZFDR.DoseRange.empty with AbsBSA = (mkMgMax 100N, Units.BSA.m2) }

                        Check.pickAdjust (single.AbsWeight |> fst) (single.AbsBSA |> fst) convKg convM2
                        |> maxVal
                        |> Expect.equal "picks m2 abs" (Some [| 100.0 |])
                    }
                ]


    [<Tests>]
    let tests =
        testList
            "GenForm Tests"
            [
                DoseLimitTests.tests
                AdjustDoseLimitTests.tests
                PatientCategoryTests.tests
                DoseTypeTests.tests
                LimitTargetTests.tests
                GenericLabelTests.tests
                ProductFilterTests.tests
                DoseRuleProductTests.tests
                CheckTests.tests
            ]
