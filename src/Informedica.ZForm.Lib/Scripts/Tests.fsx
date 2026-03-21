

#r "nuget: MathNet.Numerics.FSharp"
#r "nuget: Expecto"
#r "nuget: Expecto.FsCheck"
#r "nuget: Unquote"



#load "../../../scripts/Expecto.fsx"


#load "load.fsx"




module Tests =

    open Expecto
    open Expecto.Flip


    open MathNet.Numerics

    open Informedica.Utils.Lib
    open Informedica.Utils.Lib.BCL
    open Informedica.GenCore.Lib.Ranges
    open Informedica.ZForm.Lib

    module ValueUnit = Informedica.GenUnits.Lib.ValueUnit

    let vuFromStr v u =
        ValueUnit.unitFromZIndexString u
        |> ValueUnit.singleWithValue v
        |> Some


    // Pure tests (MinMax, Patient, DoseRange, DoseRule) have been
    // migrated to tests/Informedica.ZForm.Tests/Tests.fs and run in CI.


    module MappingTests =

        open Informedica.GenUnits.Lib


        let tests = testList "Mapping" [

            test "all units that can be mapped have a mapping" {
                // Test all unit mappings
                Informedica.ZIndex.Lib.Names.getFormUnits ()
                |> Array.append (Informedica.ZIndex.Lib.Names.getGenericUnits ())
                |> Array.distinct
                |> Array.map Mapping.stringToUnit
                |> Array.forall ((<>) NoUnit)
                |> Expect.isTrue "should all have a unit"
            }

            test "all routes can be mapped" {
                let gpp =
                    Informedica.ZIndex.Lib.GenPresProduct.get []
                    |> Array.collect (fun gpp -> gpp.Routes)
                    |> Array.distinct
                    |> Array.sort
                    |> Array.map (fun s ->
                        s,
                        s |> Informedica.ZIndex.Lib.Route.fromString (Informedica.ZIndex.Lib.Route.routeMapping ())
                    )
                    |> Array.filter (snd >> ((=) Route.NoRoute))

                let dr =
                    Informedica.ZIndex.Lib.DoseRule.get []
                    |> Array.collect (fun dr -> dr.Routes)
                    |> Array.distinct
                    |> Array.sort
                    |> Array.map (fun s ->
                        s,
                        s |> Informedica.ZIndex.Lib.Route.fromString (Informedica.ZIndex.Lib.Route.routeMapping ())
                    )
                    |> Array.filter (snd >> ((=) Route.NoRoute))

                ((gpp |> Array.isEmpty) && (dr |> Array.isEmpty))
                |> Expect.isTrue "should be true"
            }

            test "all frequencies can be mapped" {
                Informedica.ZIndex.Lib.DoseRule.get []
                |> Array.map (fun dr -> dr.Freq.Frequency, dr.Freq.Time |> String.replace "per " "")
                |> Array.distinct
                |> Array.map (fun (v, s) -> v, s, Mapping.mapFrequency v s)
                |> Array.filter (fun (_, _, u) -> u |> Option.isNone)
                |> Expect.equal "should be true" [| 99.99, "dag", None |]
            }
        ]


    let tests =
        [
            MappingTests.tests
        ]
        |> testList "ZForm"


open Expecto


Tests.tests
|> Expecto.run




module Temp =


    open Aether
    open MathNet.Numerics

    open Informedica.GenUnits.Lib
    open Informedica.ZForm.Lib


    let vuFromStr = Tests.vuFromStr


    module DosageTests =

        module Dosage = DoseRule.Dosage

        let setNormMinStartDose = Optic.set Dosage.Optics.inclMinNormStartDosagePrism
        let setAbsMaxStartDose = Optic.set Dosage.Optics.inclMaxAbsStartDosagePrism

        let setNormMinSingleDose = Optic.set Dosage.Optics.inclMinNormSingleDosagePrism
        let setAbsMaxSingleDose = Optic.set Dosage.Optics.inclMaxAbsSingleDosagePrism

        let setNormMaxSingleDose = Optic.set Dosage.Optics.inclMaxNormSingleDosagePrism

        let setNormMinRateDose = Optic.set Dosage.Optics.inclMinNormRateDosagePrism
        let setNormMaxRateDose = Optic.set Dosage.Optics.inclMaxNormRateDosagePrism
        let setRateUnit = Optic.set Dosage.Optics.rateUnitRateDosagePrism

        let toString () =
            Dosage.empty
            |> setNormMinStartDose (vuFromStr 10N "milligram")
            |> setAbsMaxStartDose (vuFromStr 1N "gram")
            |> setNormMinSingleDose (vuFromStr 10N "milligram")
            |> setAbsMaxSingleDose (vuFromStr 1N "gram")
            |> Dosage.toString true


        let convert () =
            Dosage.empty
            |> setNormMinSingleDose (vuFromStr (1N / 100N) "milligram")
            |> setNormMaxSingleDose (vuFromStr 1N "milligram")
            |> Dosage.convertSubstanceUnitTo (ValueUnit.Units.mcg)
            |> Dosage.toString false


        let convertRate () =
            Dosage.empty
            |> setNormMinRateDose (vuFromStr (1N / 100N) "milligram")
            |> setNormMaxRateDose (vuFromStr 1N "milligram")
            |> setRateUnit (ValueUnit.Units.hour)
            |> Dosage.convertSubstanceUnitTo (ValueUnit.Units.mcg)
            |> Dosage.convertRateUnitTo (ValueUnit.Units.min)
            |> Dosage.toString false



    module PatientTests =


        module Patient = PatientCategory.Optics

        let toString () =
            PatientCategory.empty
            |> Patient.setInclMinGestAge (28.  |> ValueUnit.ageInWk |> Some)
            |> Patient.setExclMaxGestAge (33.  |> ValueUnit.ageInWk |> Some)
            |> Patient.setExclMinAge (1. |> ValueUnit.ageInMo |> Some)
            |> Patient.setInclMaxAge (120. |> ValueUnit.ageInWk |> Some)
            |> Patient.setInclMinWeight (0.15  |> ValueUnit.weightInKg |> Some)
            |> Patient.setInclMaxWeight (4.0  |> ValueUnit.weightInKg |> Some)
            |> Patient.setInclMinBSA (0.15  |> ValueUnit.bsaInM2 |> Some)
            |> Patient.setInclMaxBSA (1.0  |> ValueUnit.bsaInM2 |> Some)
            |> (fun p -> p |> (Optic.set PatientCategory.Gender_) Gender.Male)
            |> PatientCategory.toString



    module GStandTests =

        open GStand
        open Informedica.Utils.Lib.BCL

        module Dosage = DoseRule.Dosage

        module RF = Informedica.ZIndex.Lib.RuleFinder
        module DR = Informedica.ZIndex.Lib.DoseRule
        module GPP = Informedica.ZIndex.Lib.GenPresProduct

        let cfg = { GPKs = [] ; IsRate = false ; SubstanceUnit = None ; TimeUnit = None }

        let cfgmcg = { cfg with SubstanceUnit = (Some ValueUnit.Units.mcg) }

        let createWithCfg cfg = GStand.createDoseRules cfg None None None None

        let createDoseRules = createWithCfg cfg

        let createCont su tu =
            let cfg = { cfg with IsRate = true ; SubstanceUnit = su ; TimeUnit = tu }
            GStand.createDoseRules cfg None None None None

        let mdText = """
    ## _Stofnaam_: {generic}
    Synoniemen: {synonym}

    ---

    ### _ATC code_: {atc}

    ### _Therapeutische groep_: {thergroup}

    ### _Therapeutische subgroep_: {thersub}

    ### _Generiek groep_: {gengroup}

    ### _Generiek subgroep_: {gensub}

    """

        let mdIndicationText = """

    ---

    ### _Indicatie_: {indication}
    """


        let mdRouteText = """
    * _Route_: {route}
    """

        let mdFormText = """
    * _Vorm_: {form}
    * _Producten_:
    * {products}
    """

        let mdPatientText = """
        * _Patient_: __{patient}__
    """

        let mdDosageText = """
        {dosage}

    """


        let mdConfig =
            {
                DoseRule.mdConfig with
                    MainText = mdText
                    IndicationText = mdIndicationText
                    RouteText = mdRouteText
                    FormText = mdFormText
                    PatientText = mdPatientText
                    DosageText = mdDosageText
            }


        let toStr = DoseRule.toStringWithConfig mdConfig false


        let printDoseRules rs =
            rs
            |> Seq.iter (fun dr ->
                dr
                |> toStr
                |> printfn "%s" //Markdown.toBrowser
            )


        let mapFrequency () =
            DR.get []
            |> Seq.map (fun dr -> dr.Freq)
            |> Seq.distinct
            |> Seq.sortBy (fun fr -> fr.Time, fr.Frequency)
            |> Seq.map (fun fr -> fr, fr |> mapFreqToValueUnit)
            |> Seq.iter (fun (fr, vu) ->
                printfn "%A %s = %s" fr.Frequency fr.Time (vu |> ValueUnit.toStringDecimalDutchShortWithPrec 0)
            )

        let tests () =
            // Doserules for cotrimoxazol
            createDoseRules "trimethoprim/sulfamethoxazol" "" "intraveneus"
            |> printDoseRules

            // Doserules for clonidin orally
            createDoseRules "clonidine" "" "oraal"
            |> printDoseRules

            // Doserules for newborn with 12?
            GStand.createDoseRules cfg (Some 0.) (Some 12.) None None "paracetamol" "" "oraal"
            |> printDoseRules

            // Doserules for 100 mo and gpk = 167541
            GStand.createDoseRules cfg (Some 100.) None (None) (Some 167541) "" "" ""
            |> printDoseRules
            |> (printfn "%A")

            // Doserules for gentamicin
            GStand.createDoseRules cfg (Some 0.) (Some 1.5) None None "gentamicine" "" "intraveneus"
            |> printDoseRules

            // Doserules for fentanyl
            createWithCfg cfgmcg "fentanyl" "" "intraveneus"
            |> printDoseRules

            // Doserules for dopamin
            createCont (Some ValueUnit.Units.mcg) (Some ValueUnit.Units.min) "dopamine" "" "intraveneus"
            |> printDoseRules

            // Doserules for digoxin
            createWithCfg cfgmcg "digoxine" "" ""
            |> printDoseRules

            // Doserules for paracetamol
            RF.createFilter None None None None "paracetamol" "" ""
            |> RF.find []
            |> getSubstanceDoses cfg
            |> Seq.iter (fun r ->
                printfn "Indication %s" (r.indications |> String.concat ", ")
                printfn "%s" (r.dosage |> Dosage.toString true)
            )

            // Doserules for gentamicin
            RF.createFilter None None None None "gentamicine" "" ""
            |> RF.find []
            |> getPatients cfg
            |> Seq.iter (fun r ->
                printfn "%s" (r.patientCategory |> PatientCategory.toString)
                r.substanceDoses
                |> Seq.iter (fun sd ->
                printfn "Indication %s" (sd.indications |> String.concat ", ")
                printfn "%s" (sd.dosage |> Dosage.toString true)
                )
            )

            // Doserules with frequency per hour
            DR.get []
            |> Seq.filter (fun dr ->
                dr.Freq.Frequency = 1. &&
                dr.Freq.Time = "per uur" &&
                dr.Routes = [|"intraveneus"|]
            )
            |> Seq.collect (fun dr -> dr.GenericProduct |> Seq.map (fun gp -> gp.Name))
            |> Seq.distinct
            |> Seq.sort
            |> Seq.iter (printfn "%s")

            // Dose rules for salbutamol
            DR.get []
            |> Seq.filter (fun dr ->
                dr.GenericProduct
                |> Seq.map (fun gp -> gp.Name)
                |> Seq.exists (String.startsWithCapsInsensitive "salbutamol")
            )
            //|> Seq.collect (fun dr ->
            //    dr.GenericProduct
            //    |> Seq.map (fun gp -> gp.Name, dr.Routes)
            //)
            |> Seq.map (DR.toString ",")
            |> Seq.distinct
            |> Seq.iter (printfn "%A")

            // Doserules with fentanyl once
            DR.get []
            |> Seq.filter (fun dr ->
                dr.GenericProduct
                |> Seq.map (fun gp -> gp.Name)
                |> Seq.exists (String.startsWithCapsInsensitive "fentanyl") &&
                dr.Freq.Time |> String.startsWithCapsInsensitive "eenmalig"
            )
            //|> Seq.collect (fun dr ->
            //    dr.GenericProduct
            //    |> Seq.map (fun gp -> gp.Name, dr.Routes)
            //)
            |> Seq.map (DR.toString ",")
            |> Seq.distinct
            |> Seq.iter (printfn "%A")

            // Doserules for fentanyl
            DR.get []
            |> Seq.filter (fun dr ->
                dr.GenericProduct
                |> Seq.map (fun gp -> gp.Name)
                |> Seq.exists (String.startsWithCapsInsensitive "fentanyl")
            )
            //|> Seq.collect (fun dr ->
            //    dr.GenericProduct
            //    |> Seq.map (fun gp -> gp.Name, dr.Routes)
            //)
            |> Seq.map (fun dr -> dr.Freq.Time)
            |> Seq.distinct
            |> Seq.iter (printfn "%A")

            // GenPresProducts = paracetamol
            GPP.get []
            |> Seq.filter (fun gpp -> gpp.Name |> String.equalsCapInsens "paracetamol")
            |> Seq.iter (fun gpp ->
                gpp
                |> (printfn "%A")
            )

            // All routes in doserules
            DR.get []
            |> Seq.collect (fun r -> r.Routes)
            |> Seq.distinct
            |> Seq.sort
            |> Seq.iter (printfn "%s")

            // GenPresProducts with route = parenteraal
            GPP.get []
            |> Seq.filter (fun gpp ->
                gpp.Routes |> Seq.exists (fun r -> r |> String.equalsCapInsens "parenteraal")
            )
            |> Seq.distinct
            |> Seq.sort
            |> Seq.iter (GPP.toString >> printfn "%s")

            // Filter GenPresProducts with route = oraal
            GPP.filter "" "" "oraal"
            |> Seq.length
            |> ignore

            printfn "DoseRule routes without products"
            DR.routes ()
            |> Seq.filter (fun r ->

                GPP.getRoutes ()
                |> Seq.exists (fun r' -> r = r')
                |> not
            )
            |> Seq.sort
            |> Seq.iter (printfn "|%s|")
            printfn ""
            printfn "GenPresProduct routes without doserules"
            GPP.getRoutes ()
            |> Seq.filter (fun r ->
                DR.routes ()
                |> Seq.exists (fun r' -> r = r')
                |> not
            )
            |> Seq.sort
            |> Seq.iter (printfn "|%s|")
