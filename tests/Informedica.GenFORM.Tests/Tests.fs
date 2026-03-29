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
        let mgPerKg = Units.Mass.milliGram |> Units.per Units.Weight.kiloGram
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
                            Age = MinMax.empty
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
                                { patCat.Age with
                                    Max = 7N |> ValueUnit.singleWithUnit Units.Time.day |> Limit.Inclusive |> Some
                                }
                        }
                        |> PatientCategory.filter filter
                        |> Expect.isFalse "should return false"
                    }

                    test "a filter with age 5 and a patient category with a max age of 7" {
                        { patCat with
                            Age =
                                { patCat.Age with
                                    Max = 7N |> ValueUnit.singleWithUnit Units.Time.day |> Limit.Inclusive |> Some
                                }
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
                                { patCat.Age with
                                    Min = 1N |> ValueUnit.singleWithUnit Units.Time.week |> Limit.Inclusive |> Some
                                }
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
                                { patCat.Age with
                                    Max = 30N |> ValueUnit.singleWithUnit Units.Time.day |> Limit.Inclusive |> Some
                                }
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
            ]
