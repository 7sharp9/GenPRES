
#time

// load demo or product cache


System.Environment.SetEnvironmentVariable("GENPRES_DEBUG", "1")
System.Environment.SetEnvironmentVariable("GENPRES_PROD", "1")
System.Environment.SetEnvironmentVariable("GENPRES_URL_ID", "1JHOrasAZ_2fcVApYpt1qT2lZBsqrAxN-9SvBisXkbsM")

#load "load.fsx"

open MathNet.Numerics
open Informedica.Utils.Lib.BCL
open Informedica.GenCore.Lib.Ranges
open Informedica.GenForm.Lib
open Informedica.GenUnits.Lib
open Informedica.GenOrder.Lib



module HelperFunctions =

    let print sl = sl |> List.iter (printfn "%s")


    let inline printOrderTable order =
        order
        |> Result.iter (Order.printTable ConsoleTables.Format.Minimal)

        order


    let solveOrder order =
        match order with
        | Error e -> $"Error solving order: {e}" |> failwith
        | Ok o ->
            o
            |> Order.solveMinMax true OrderLogging.noOp


    let run logger med cmds =
        let rec loop cmds ord =
            match cmds with
            | [] -> ord
            | cmd::rest ->
                match ord with
                | Error (_, msgs) ->
                    failwith $"Errors occured: {msgs}"
                | Ok ord ->
                   ord
                   |> cmd
                   |> OrderProcessor.processPipeline logger None
                   |> loop rest


        med
        |> Medication.toOrderDto
        |> Order.Dto.fromDto
        |> function
          | Error msg -> failwith $"{msg}"
          | Ok ord -> ord |> Ok |> loop cmds




module GenFormResult = Utils.GenFormResult
open HelperFunctions


let logger = OrderLogging.createConsoleLogger ()


let amfo =
    let au = Units.Weight.kiloGram
    let fu = Units.Volume.milliLiter
    let su = Units.Mass.milliGram
    let du = Units.Mass.milliGram |> Units.per au |> Units.per Units.Time.day
    let cu = su |> Units.per fu

    { Medication.template with
        Id = "1"
        Name = "amfotericine b liposomaal"
        Route = "INTRAVENEUS"
        Quantities = None //50N |> ValueUnit.singleWithUnit fu |> Some
        OrderType = DiscontinuousOrder
        Adjust = 14N |> ValueUnit.singleWithUnit au |> Some
        Frequencies =
            Units.Count.times
            |> Units.per Units.Time.day
            |> ValueUnit.singleWithValue 1N
            |> Some
        DoseCount = 1N |> ValueUnit.singleWithUnit Units.Count.times |> MinMax.createExact
        Components =
            [
                { Medication.productComponent with
                    Name = "amfotericine b liposomaal"
                    Form = "poeder voor oplossing voor infusie"
                    Quantities = 1N |> ValueUnit.singleWithUnit fu |> Some
                    Divisible = Some 10N
                    Substances =
                        [
                            { Medication.SubstanceItem.item with
                                Name = "saccharose"
                                Concentrations =
                                    Units.Mass.milliGram
                                    |> Units.per Units.Volume.milliLiter
                                    |> ValueUnit.singleWithValue 72N
                                    |> Some
                            }
                            { Medication.SubstanceItem.item with
                                Name = "amfotericine b liposomaal"
                                Concentrations =
                                    Units.Mass.milliGram
                                    |> Units.per Units.Volume.milliLiter
                                    |> ValueUnit.singleWithValue 4N
                                    |> Some
                                Dose =
                                    { DoseLimit.limit with
                                        DoseLimitTarget = "amfotericine b liposomaal" |> SubstanceLimitTarget
                                        AdjustUnit = au |> Some
                                        PerTimeAdjust =
                                            { MinMax.empty with
                                                Min = 3N |> ValueUnit.singleWithUnit du |> Limit.inclusive |> Some
                                                Max = 5N |> ValueUnit.singleWithUnit du |> Limit.inclusive |> Some
                                            }
                                    }
                                    |> Some
                                Solution =
                                    { SolutionLimit.limit with
                                        SolutionLimitTarget = "amfotericine b liposomaal" |> SubstanceLimitTarget
                                        Concentration =
                                            { MinMax.empty with
                                                Min =
                                                    su
                                                    |> Units.per Units.Volume.milliLiter
                                                    |> ValueUnit.singleWithValue (2N/10N)
                                                    |> Limit.inclusive
                                                    |> Some
                                                Max =
                                                    su
                                                    |> Units.per Units.Volume.milliLiter
                                                    |> ValueUnit.singleWithValue 2N
                                                    |> Limit.inclusive
                                                    |> Some
                                            }
                                    }
                                    |> Some
                            }
                        ]
                }
                { Medication.productComponent with
                    Name = "gluc 10%"
                    Form = "vloeistof"
                    Quantities = 1N |> ValueUnit.singleWithUnit fu |> Some
                    Divisible = Some 10N
                    Substances =
                        [
                            { Medication.SubstanceItem.item with
                                Name = "energie"
                                Concentrations =
                                    Units.Energy.kiloCalorie
                                    |> Units.per Units.Volume.milliLiter
                                    |> ValueUnit.singleWithValue (4N / 10N)
                                    |> Some
                            }
                            { Medication.SubstanceItem.item with
                                Name = "koolhydraat"
                                Concentrations =
                                    Units.Mass.gram
                                    |> Units.per Units.Volume.milliLiter
                                    |> ValueUnit.singleWithValue (1N / 10N)
                                    |> Some
                            }

                        ]
                }

            ]
    }


amfo
|> Medication.toString
|> print


[
    CalcMinMax
    CalcValues
    fun ord ->
        (ord, SetMedianComponentQuantity amfo.Components[0].Name)
        |> OrderCommand.ChangeProperty
    fun ord ->
        (ord, SetMedianComponentQuantity amfo.Components[1].Name)
        |> OrderCommand.ChangeProperty
]
|> run logger amfo
//|> printOrderTable
|> ignore


let morfCont =
    let au = Units.Weight.kiloGram
    let fu = Units.Volume.milliLiter
    let su = Units.Mass.milliGram
    let du = Units.Mass.microGram |> Units.per au |> Units.per Units.Time.hour
    let cu = su |> Units.per fu
    let ru = fu |> Units.per Units.Time.hour

    { Medication.template with
        Id = "1"
        Name = "morfine"
        Route = "INTRAVENEUS"
        Quantities = 50N |> ValueUnit.singleWithUnit fu |> Some
        Components = [
            { Medication.productComponent with
                Name = "morfine"
                Form = "injectievloeistof"
                Quantities = 1N |> ValueUnit.singleWithUnit fu |> Some
                Divisible = Some 10N
                Substances = [
                    { Medication.substanceItem with
                        Name = "morfin"
                        Concentrations =
                            [| 1N; 10N |]
                            |> ValueUnit.withUnit cu
                            |> Some
                        Dose =
                            { DoseLimit.limit with
                                DoseLimitTarget = "morfine" |> SubstanceLimitTarget
                                AdjustUnit = au |> Some
                                RateAdjust =
                                    { MinMax.empty with
                                        Min = 10N |> ValueUnit.singleWithUnit du |> Limit.inclusive |> Some
                                        Max = 40N |> ValueUnit.singleWithUnit du |> Limit.inclusive |> Some
                                    }
                            }
                            |> Some
                        Solution =
                            { SolutionLimit.limit with
                                SolutionLimitTarget = "morfine" |> SubstanceLimitTarget
                                Quantity = 10N |> ValueUnit.singleWithUnit su |> MinMax.createExact
                                Concentration =
                                    { MinMax.empty with
                                        Max =
                                            su
                                            |> Units.per Units.Volume.milliLiter
                                            |> ValueUnit.singleWithValue 1N
                                            |> Limit.inclusive
                                            |> Some
                                    }
                            }
                            |> Some
                    }
                ]
            }
            { Medication.productComponent with
                Name = "gluc 10%"
                Form = "iv fluid"
                Quantities = 1N |> ValueUnit.singleWithUnit fu |> Some
                Divisible = Some 10N
                Substances =
                    [
                        { Medication.SubstanceItem.item with
                            Name = "energie"
                            Concentrations =
                                Units.Energy.kiloCalorie
                                |> Units.per Units.Volume.milliLiter
                                |> ValueUnit.singleWithValue (4N / 10N)
                                |> Some
                        }
                        { Medication.SubstanceItem.item with
                            Name = "koolhydraat"
                            Concentrations =
                                Units.Mass.gram
                                |> Units.per Units.Volume.milliLiter
                                |> ValueUnit.singleWithValue (1N / 10N)
                                |> Some
                        }

                    ]
            }
        ]
        OrderType = ContinuousOrder
        Adjust = 14N |> ValueUnit.singleWithUnit au |> Some
        Dose =
            { DoseLimit.limit with
                DoseLimitTarget = OrderableLimitTarget
                AdjustUnit =  None
            }
            |> Some
        DoseCount = 1N |> ValueUnit.singleWithUnit Units.Count.times |> MinMax.createExact
    }


morfCont
|> Medication.toString
|> print


[
    CalcMinMax
    CalcValues
    fun ord ->
        (ord, SetMedianComponentQuantity morfCont.Components[0].Name)
        |> OrderCommand.ChangeProperty
    (*
    fun ord ->
        (ord, SetMedianComponentQuantity morfCont.Components[1].Name)
        |> OrderCommand.ChangeProperty
    *)
]
|> run logger morfCont
//|> printOrderTable
|> ignore


let pcmDrink =
    let au = Units.Weight.kiloGram
    let fu = Units.Volume.milliLiter
    let su = Units.Mass.milliGram
    let cu = su |> Units.per fu
    let tu = Units.Time.day

    { Medication.template with
        Id = "pcm-drank"
        Name = "paracetamol drank"
        Components =
            [
                {
                    Medication.productComponent with
                        Name = "paracetamol"
                        Form = "drank"
                        Quantities =
                            5N
                            |> ValueUnit.singleWithUnit fu
                            |> Some
                        Divisible = Some 1N
                        Substances =
                            [
                                {
                                    Medication.substanceItem with
                                        Name = "paracetamol"
                                        Concentrations =
                                            24N
                                            |> ValueUnit.singleWithUnit cu
                                            |> Some
                                        Dose =
                                            { DoseLimit.limit with
                                                DoseLimitTarget = "paracetamol" |> SubstanceLimitTarget
                                                AdjustUnit = su |> Some
                                                PerTimeAdjust =
                                                    MinMax.createInclIncl
                                                        (60N |> ValueUnit.singleWithUnit (su |> Units.per au |> Units.per tu))
                                                        (90N |> ValueUnit.singleWithUnit (su |> Units.per au |> Units.per tu))
                                            }
                                            |> Some
                                }
                            ]
                }
            ]
        Route = "or"
        OrderType = DiscontinuousOrder
        Adjust = 10N |> ValueUnit.singleWithUnit au |> Some
        Frequencies =
            [|3N; 4N; 6N |]
            |> ValueUnit.withUnit (Units.Count.times |> Units.per tu)
            |> Some
        DoseCount = 1N |> ValueUnit.singleWithUnit Units.Count.times |> MinMax.createExact
    }


open Types

pcmDrink
|> Medication.toString
|> print



let cotrim =
    let au = Units.Weight.kiloGram
    let fu = Units.Volume.milliLiter
    let su = Units.Mass.milliGram
    let cu = su |> Units.per fu
    let tu = Units.Time.day

    {
        Medication.template with
            Id = "1"
            Name = "cotrimoxazol"
            Components =
                [
                    {
                        Medication.productComponent with
                            Name = "cotrimoxazol"
                            Form = "drank"
                            Quantities =
                                1N
                                |> ValueUnit.singleWithUnit fu
                                |> Some
                            Divisible = Some 1N
                            Substances =
                                [
                                    {
                                        Medication.substanceItem with
                                            Name = "sulfamethoxazol"
                                            Concentrations =
                                                [| 40N; 400N; 800N |]
                                                |> ValueUnit.withUnit cu
                                                |> Some
                                            Dose =
                                                { DoseLimit.limit with
                                                    DoseLimitTarget = "sulfamethoxazol" |> SubstanceLimitTarget
                                                    AdjustUnit = su |> Some
                                                    QuantityAdjust =
                                                        MinMax.createInclIncl
                                                            (27N |> ValueUnit.singleWithUnit (su |> Units.per au))
                                                            (30N |> ValueUnit.singleWithUnit (su |> Units.per au))
                                                }
                                                |> Some
                                    }
                                    {
                                        Medication.substanceItem with
                                            Name = "trimethoprim"
                                            Concentrations =
                                                [| 8N; 80N; 160N |]
                                                |> ValueUnit.withUnit cu
                                                |> Some
                                            Dose =
                                                { DoseLimit.limit with
                                                    DoseLimitTarget = "trimethoprim" |> SubstanceLimitTarget
                                                    AdjustUnit = su |> Some
                                                    QuantityAdjust =
                                                        MinMax.createInclIncl
                                                            (6N - 6N / 10N |> ValueUnit.singleWithUnit (su |> Units.per au))
                                                            (6N |> ValueUnit.singleWithUnit (su |> Units.per au))
                                                }
                                                |> Some
                                    }
                                ]
                    }
                ]
            Route = "or"
            OrderType = DiscontinuousOrder
            Frequencies =
                [|2N |]
                |> ValueUnit.withUnit (Units.Count.times |> Units.per tu)
                |> Some
            Adjust = 10N |> ValueUnit.singleWithUnit au |> Some
            DoseCount = 1N |> ValueUnit.singleWithUnit Units.Count.times |> MinMax.createExact
            Dose =
                { DoseLimit.limit with
                    DoseLimitTarget = OrderableLimitTarget
                    AdjustUnit = au |> Some
                    QuantityAdjust =
                        { MinMax.empty with
                            Max =
                                10N
                                |> ValueUnit.singleWithUnit (fu |> Units.per au)
                                |> Limit.inclusive
                                |> Some
                        }
                }
                |> Some
    }


cotrim
|> Medication.toString
|> print


let tpnComplete =
    { Medication.template with
        Id = "f1adf475-919b-4b7d-9e26-6cc502b88e42"
        Name = "samenstelling c"
        Route = "INTRAVENEUS"
        OrderType = TimedOrder
        Adjust =
            11N
            |> ValueUnit.singleWithUnit Units.Weight.kiloGram
            |> Some
        Frequencies =
            1N
            |> ValueUnit.singleWithUnit (Units.Count.times |> Units.per Units.Time.day)
            |> Some
        Time =
            { MinMax.empty with
                Min =
                    20N
                    |> ValueUnit.singleWithUnit Units.Time.hour
                    |> Limit.inclusive
                    |> Some
                Max =
                    24N
                    |> ValueUnit.singleWithUnit Units.Time.hour
                    |> Limit.inclusive
                    |> Some
            }
        Dose =
            { DoseLimit.limit with
                DoseLimitTarget = OrderableLimitTarget
                AdjustUnit = Units.Weight.kiloGram |> Some
                QuantityAdjust =
                    { MinMax.empty with
                        Max =
                            (755N / 10N)
                            |> ValueUnit.singleWithUnit (Units.Volume.milliLiter |> Units.per Units.Weight.kiloGram)
                            |> Limit.inclusive
                            |> Some
                    }
            }
            |> Some
        DoseCount =
            { MinMax.empty with
                Min = 1N |> ValueUnit.singleWithUnit Units.Count.times |> Limit.inclusive |> Some
                Max = 1N |> ValueUnit.singleWithUnit Units.Count.times |> Limit.inclusive |> Some
            }
        Components =
            [
                // Samenstelling C component
                {
                    Medication.productComponent with
                        Name = "Samenstelling C"
                        Form = "vloeistof"
                        Quantities =
                            1N
                            |> ValueUnit.singleWithUnit Units.Volume.milliLiter
                            |> Some
                        Divisible = Some (1N)
                        Dose =
                            { DoseLimit.limit with
                                DoseLimitTarget = "Samenstelling C" |> ComponentLimitTarget
                                AdjustUnit = Units.Weight.kiloGram |> Some
                                QuantityAdjust =
                                    { MinMax.empty with
                                        Min =
                                            10N
                                            |> ValueUnit.singleWithUnit (Units.Volume.milliLiter |> Units.per Units.Weight.kiloGram)
                                            |> Limit.inclusive
                                            |> Some
                                        Max =
                                            25N
                                            |> ValueUnit.singleWithUnit (Units.Volume.milliLiter |> Units.per Units.Weight.kiloGram)
                                            |> Limit.inclusive
                                            |> Some
                                    }
                            }
                            |> Some
                        Substances =
                            [
                                {
                                    Medication.substanceItem with
                                        Name = "energie"
                                        Concentrations =
                                            (32N / 100N)
                                            |> ValueUnit.singleWithUnit (Units.Energy.kiloCalorie |> Units.per Units.Volume.milliLiter)
                                            |> Some
                                }
                                {
                                    Medication.substanceItem with
                                        Name = "eiwit"
                                        Concentrations =
                                            (8N / 100N)
                                            |> ValueUnit.singleWithUnit (Units.Mass.gram |> Units.per Units.Volume.milliLiter)
                                            |> Some
                                        Solution =
                                            { SolutionLimit.limit with
                                                SolutionLimitTarget = "eiwit" |> SubstanceLimitTarget
                                                Concentration =
                                                    { MinMax.empty with
                                                        Max =
                                                            5N / 100N
                                                            |> ValueUnit.singleWithUnit (Units.Mass.gram |> Units.per Units.Volume.milliLiter)
                                                            |> Limit.inclusive
                                                            |> Some
                                                    }
                                            }
                                            |> Some
                                }
                                {
                                    Medication.substanceItem with
                                        Name = "natrium"
                                        Concentrations =
                                            (1N / 100N)
                                            |> ValueUnit.singleWithUnit (Units.Molar.milliMole |> Units.per Units.Volume.milliLiter)
                                            |> Some
                                        Solution =
                                            { SolutionLimit.limit with
                                                SolutionLimitTarget = "natrium" |> LimitTarget.SubstanceLimitTarget
                                                Concentration =
                                                    { MinMax.empty with
                                                        Max =
                                                            (5N / 10N)
                                                            |> ValueUnit.singleWithUnit (Units.Molar.milliMole |> Units.per Units.Volume.milliLiter)
                                                            |> Limit.inclusive
                                                            |> Some
                                                    }
                                            }
                                            |> Some
                                }
                                {
                                    Medication.substanceItem with
                                        Name = "kalium"
                                        Concentrations =
                                            (2N / 100N)
                                            |> ValueUnit.singleWithUnit (Units.Molar.milliMole |> Units.per Units.Volume.milliLiter)
                                            |> Some
                                        Solution =
                                            { SolutionLimit.limit with
                                                SolutionLimitTarget = "kalium" |> LimitTarget.SubstanceLimitTarget
                                                Concentration =
                                                    { MinMax.empty with
                                                        Max =
                                                            (5N / 10N)
                                                            |> ValueUnit.singleWithUnit (Units.Molar.milliMole |> Units.per Units.Volume.milliLiter)
                                                            |> Limit.inclusive
                                                            |> Some
                                                    }
                                            }
                                            |> Some
                                }
                                {
                                    Medication.substanceItem with
                                        Name = "calcium"
                                        Concentrations =
                                            (3N / 100N)
                                            |> ValueUnit.singleWithUnit (Units.Molar.milliMole |> Units.per Units.Volume.milliLiter)
                                            |> Some
                                }
                                {
                                    Medication.substanceItem with
                                        Name = "fosfaat"
                                        Concentrations =
                                            (2N / 100N)
                                            |> ValueUnit.singleWithUnit (Units.Molar.milliMole |> Units.per Units.Volume.milliLiter)
                                            |> Some
                                }
                                {
                                    Medication.substanceItem with
                                        Name = "magnesium"
                                        Concentrations =
                                            (1N / 100N)
                                            |> ValueUnit.singleWithUnit (Units.Molar.milliMole |> Units.per Units.Volume.milliLiter)
                                            |> Some
                                }
                                {
                                    Medication.substanceItem with
                                        Name = "chloor"
                                        Concentrations =
                                            (7N / 100N)
                                            |> ValueUnit.singleWithUnit (Units.Molar.milliMole |> Units.per Units.Volume.milliLiter)
                                            |> Some
                                }
                            ]
                }
                // NaCl 3% component
                {
                    Medication.productComponent with
                        Name = "NaCl 3%"
                        Form = "vloeistof"
                        Quantities =
                            1N
                            |> ValueUnit.singleWithUnit Units.Volume.milliLiter
                            |> Some
                        Divisible = Some (1N)
                        Dose =
                            { DoseLimit.limit with
                                DoseLimitTarget = "NaCl 3%" |> LimitTarget.ComponentLimitTarget
                                AdjustUnit = Units.Weight.kiloGram |> Some
                                QuantityAdjust =
                                    { MinMax.empty with
                                        Min =
                                            6N
                                            |> ValueUnit.singleWithUnit (Units.Volume.milliLiter |> Units.per Units.Weight.kiloGram)
                                            |> Limit.inclusive
                                            |> Some
                                        Max =
                                            6N
                                            |> ValueUnit.singleWithUnit (Units.Volume.milliLiter |> Units.per Units.Weight.kiloGram)
                                            |> Limit.inclusive
                                            |> Some
                                    }
                            }
                            |> Some
                        Substances =
                            [
                                {
                                    Medication.substanceItem with
                                        Name = "natrium"
                                        Concentrations =
                                            (5N / 10N)
                                            |> ValueUnit.singleWithUnit (Units.Molar.milliMole |> Units.per Units.Volume.milliLiter)
                                            |> Some
                                        Solution =
                                            { SolutionLimit.limit with
                                                SolutionLimitTarget = "natrium" |> LimitTarget.SubstanceLimitTarget
                                                Concentration =
                                                    { MinMax.empty with
                                                        Max =
                                                            (5N / 10N)
                                                            |> ValueUnit.singleWithUnit (Units.Molar.milliMole |> Units.per Units.Volume.milliLiter)
                                                            |> Limit.inclusive
                                                            |> Some
                                                    }
                                            }
                                            |> Some
                                }
                                {
                                    Medication.substanceItem with
                                        Name = "chloor"
                                        Concentrations =
                                            (5N / 10N)
                                            |> ValueUnit.singleWithUnit (Units.Molar.milliMole |> Units.per Units.Volume.milliLiter)
                                            |> Some
                                }
                            ]
                }
                // KCl 7,4% component
                {
                    Medication.productComponent with
                        Name = "KCl 7,4%"
                        Form = "vloeistof"
                        Quantities =
                            1N
                            |> ValueUnit.singleWithUnit Units.Volume.milliLiter
                            |> Some
                        Divisible = Some (1N)
                        Dose =
                            { DoseLimit.limit with
                                DoseLimitTarget = "KCl 7,4%" |> LimitTarget.ComponentLimitTarget
                                AdjustUnit = Units.Weight.kiloGram |> Some
                                QuantityAdjust =
                                    { MinMax.empty with
                                        Min =
                                            2N
                                            |> ValueUnit.singleWithUnit (Units.Volume.milliLiter |> Units.per Units.Weight.kiloGram)
                                            |> Limit.inclusive
                                            |> Some
                                        Max =
                                            2N
                                            |> ValueUnit.singleWithUnit (Units.Volume.milliLiter |> Units.per Units.Weight.kiloGram)
                                            |> Limit.inclusive
                                            |> Some
                                    }
                            }
                            |> Some
                        Substances =
                            [
                                {
                                    Medication.substanceItem with
                                        Name = "kalium"
                                        Concentrations =
                                            1N
                                            |> ValueUnit.singleWithUnit (Units.Molar.milliMole |> Units.per Units.Volume.milliLiter)
                                            |> Some
                                        Solution =
                                            { SolutionLimit.limit with
                                                SolutionLimitTarget = "kalium" |> LimitTarget.SubstanceLimitTarget
                                                Concentration =
                                                    { MinMax.empty with
                                                        Max =
                                                            (5N / 10N)
                                                            |> ValueUnit.singleWithUnit (Units.Molar.milliMole |> Units.per Units.Volume.milliLiter)
                                                            |> Limit.inclusive
                                                            |> Some
                                                    }
                                            }
                                            |> Some
                                }
                                {
                                    Medication.substanceItem with
                                        Name = "chloor"
                                        Concentrations =
                                            1N
                                            |> ValueUnit.singleWithUnit (Units.Molar.milliMole |> Units.per Units.Volume.milliLiter)
                                            |> Some
                                }
                            ]
                }
                // gluc 10% component
                {
                    Medication.productComponent with
                        Name = "gluc 10%"
                        Form = "vloeistof"
                        Quantities =
                            1N
                            |> ValueUnit.singleWithUnit Units.Volume.milliLiter
                            |> Some
                        Divisible = Some (1N)
                        Substances =
                            [
                                {
                                    Medication.substanceItem with
                                        Name = "energie"
                                        Concentrations =
                                            (4N / 10N)
                                            |> ValueUnit.singleWithUnit (Units.Energy.kiloCalorie |> Units.per Units.Volume.milliLiter)
                                            |> Some
                                }
                                {
                                    Medication.substanceItem with
                                        Name = "koolhydraat"
                                        Concentrations =
                                            (1N / 10N)
                                            |> ValueUnit.singleWithUnit (Units.Mass.gram |> Units.per Units.Volume.milliLiter)
                                            |> Some
                                }
                            ]
                }
            ]
    }



let tpn =
    { Medication.template with
        Id = "f1adf475-919b-4b7d-9e26-6cc502b88e42"
        Name = "samenstelling c"
        Route = "INTRAVENEUS"
        OrderType = TimedOrder
        Adjust =
            11N
            |> ValueUnit.singleWithUnit Units.Weight.kiloGram
            |> Some
        Frequencies =
            1N
            |> ValueUnit.singleWithUnit (Units.Count.times |> Units.per Units.Time.day)
            |> Some
        Time =
            { MinMax.empty with
                Min =
                    20N
                    |> ValueUnit.singleWithUnit Units.Time.hour
                    |> Limit.inclusive
                    |> Some
                Max =
                    24N
                    |> ValueUnit.singleWithUnit Units.Time.hour
                    |> Limit.inclusive
                    |> Some
            }
        Dose =
            { DoseLimit.limit with
                DoseLimitTarget = OrderableLimitTarget
                AdjustUnit = Units.Weight.kiloGram |> Some
                QuantityAdjust =
                    { MinMax.empty with
                        Max =
                            (755N / 10N)
                            |> ValueUnit.singleWithUnit (Units.Volume.milliLiter |> Units.per Units.Weight.kiloGram)
                            |> Limit.inclusive
                            |> Some
                    }
            }
            |> Some
        DoseCount =
            { MinMax.empty with
                Min = 1N |> ValueUnit.singleWithUnit Units.Count.times |> Limit.inclusive |> Some
                Max = 1N |> ValueUnit.singleWithUnit Units.Count.times |> Limit.inclusive |> Some
            }
        Components =
            [
                // Samenstelling C component
                {
                    Medication.productComponent with
                        Name = "Samenstelling C"
                        Form = "vloeistof"
                        Quantities =
                            1N
                            |> ValueUnit.singleWithUnit Units.Volume.milliLiter
                            |> Some
                        Divisible = Some (1N)
                        Dose =
                            { DoseLimit.limit with
                                DoseLimitTarget = "Samenstelling C" |> LimitTarget.ComponentLimitTarget
                                AdjustUnit = Units.Weight.kiloGram |> Some
                                QuantityAdjust =
                                    { MinMax.empty with
                                        Min =
                                            10N
                                            |> ValueUnit.singleWithUnit (Units.Volume.milliLiter |> Units.per Units.Weight.kiloGram)
                                            |> Limit.inclusive
                                            |> Some
                                        Max =
                                            25N
                                            |> ValueUnit.singleWithUnit (Units.Volume.milliLiter |> Units.per Units.Weight.kiloGram)
                                            |> Limit.inclusive
                                            |> Some
                                    }
                            }
                            |> Some
                        Substances =
                            [
                                {
                                    Medication.substanceItem with
                                        Name = "eiwit"
                                        Concentrations =
                                            (8N / 100N)
                                            |> ValueUnit.singleWithUnit (Units.Mass.gram |> Units.per Units.Volume.milliLiter)
                                            |> Some
                                        Solution =
                                            { SolutionLimit.limit with
                                                SolutionLimitTarget = "eiwit" |> LimitTarget.SubstanceLimitTarget
                                                Concentration =
                                                    { MinMax.empty with
                                                        Max =
                                                            (5N / 100N)
                                                            |> ValueUnit.singleWithUnit (Units.Mass.gram |> Units.per Units.Volume.milliLiter)
                                                            |> Limit.inclusive
                                                            |> Some
                                                    }
                                            }
                                            |> Some
                                }
                                {
                                    Medication.substanceItem with
                                        Name = "natrium"
                                        Concentrations =
                                            (1N / 100N)
                                            |> ValueUnit.singleWithUnit (Units.Molar.milliMole |> Units.per Units.Volume.milliLiter)
                                            |> Some
                                        Solution =
                                            { SolutionLimit.limit with
                                                SolutionLimitTarget = "natrium" |> LimitTarget.SubstanceLimitTarget
                                                Concentration =
                                                    { MinMax.empty with
                                                        Max =
                                                            (5N / 10N)
                                                            |> ValueUnit.singleWithUnit (Units.Molar.milliMole |> Units.per Units.Volume.milliLiter)
                                                            |> Limit.inclusive
                                                            |> Some
                                                    }
                                            }
                                            |> Some
                                }
                                {
                                    Medication.substanceItem with
                                        Name = "kalium"
                                        Concentrations =
                                            (2N / 100N)
                                            |> ValueUnit.singleWithUnit (Units.Molar.milliMole |> Units.per Units.Volume.milliLiter)
                                            |> Some
                                        Solution =
                                            { SolutionLimit.limit with
                                                SolutionLimitTarget = "kalium" |> LimitTarget.SubstanceLimitTarget
                                                Concentration =
                                                    { MinMax.empty with
                                                        Max =
                                                            (5N / 10N)
                                                            |> ValueUnit.singleWithUnit (Units.Molar.milliMole |> Units.per Units.Volume.milliLiter)
                                                            |> Limit.inclusive
                                                            |> Some
                                                    }
                                            }
                                            |> Some
                                }
                            ]
                }
                // NaCl 3% component
                {
                    Medication.productComponent with
                        Name = "NaCl 3%"
                        Form = "vloeistof"
                        Quantities =
                            1N
                            |> ValueUnit.singleWithUnit Units.Volume.milliLiter
                            |> Some
                        Divisible = Some (1N)
                        Dose =
                            { DoseLimit.limit with
                                DoseLimitTarget = "NaCl 3%" |> LimitTarget.ComponentLimitTarget
                                AdjustUnit = Units.Weight.kiloGram |> Some
                                QuantityAdjust =
                                    { MinMax.empty with
                                        Min =
                                            6N
                                            |> ValueUnit.singleWithUnit (Units.Volume.milliLiter |> Units.per Units.Weight.kiloGram)
                                            |> Limit.inclusive
                                            |> Some
                                        Max =
                                            6N
                                            |> ValueUnit.singleWithUnit (Units.Volume.milliLiter |> Units.per Units.Weight.kiloGram)
                                            |> Limit.inclusive
                                            |> Some
                                    }
                            }
                            |> Some
                        Substances =
                            [
                                {
                                    Medication.substanceItem with
                                        Name = "natrium"
                                        Concentrations =
                                            (5N / 10N)
                                            |> ValueUnit.singleWithUnit (Units.Molar.milliMole |> Units.per Units.Volume.milliLiter)
                                            |> Some
                                        Solution =
                                            { SolutionLimit.limit with
                                                SolutionLimitTarget = "natrium" |> LimitTarget.SubstanceLimitTarget
                                                Concentration =
                                                    { MinMax.empty with
                                                        Max =
                                                            (5N / 10N)
                                                            |> ValueUnit.singleWithUnit (Units.Molar.milliMole |> Units.per Units.Volume.milliLiter)
                                                            |> Limit.inclusive
                                                            |> Some
                                                    }
                                            }
                                            |> Some
                                }
                            ]
                }
                // KCl 7,4% component
                {
                    Medication.productComponent with
                        Name = "KCl 7,4%"
                        Form = "vloeistof"
                        Quantities =
                            1N
                            |> ValueUnit.singleWithUnit Units.Volume.milliLiter
                            |> Some
                        Divisible = Some (1N)
                        Dose =
                            { DoseLimit.limit with
                                DoseLimitTarget = "KCl 7,4%" |> LimitTarget.ComponentLimitTarget
                                AdjustUnit = Units.Weight.kiloGram |> Some
                                QuantityAdjust =
                                    { MinMax.empty with
                                        Min =
                                            2N
                                            |> ValueUnit.singleWithUnit (Units.Volume.milliLiter |> Units.per Units.Weight.kiloGram)
                                            |> Limit.inclusive
                                            |> Some
                                        Max =
                                            2N
                                            |> ValueUnit.singleWithUnit (Units.Volume.milliLiter |> Units.per Units.Weight.kiloGram)
                                            |> Limit.inclusive
                                            |> Some
                                    }
                            }
                            |> Some
                        Substances =
                            [
                                {
                                    Medication.substanceItem with
                                        Name = "kalium"
                                        Concentrations =
                                            1N
                                            |> ValueUnit.singleWithUnit (Units.Molar.milliMole |> Units.per Units.Volume.milliLiter)
                                            |> Some
                                        Solution =
                                            { SolutionLimit.limit with
                                                SolutionLimitTarget = "kalium" |> LimitTarget.SubstanceLimitTarget
                                                Concentration =
                                                    { MinMax.empty with
                                                        Max =
                                                            (5N / 10N)
                                                            |> ValueUnit.singleWithUnit (Units.Molar.milliMole |> Units.per Units.Volume.milliLiter)
                                                            |> Limit.inclusive
                                                            |> Some
                                                    }
                                            }
                                            |> Some
                                }
                            ]
                }
                // gluc 10% component
                {
                    Medication.productComponent with
                        Name = "gluc 10%"
                        Form = "vloeistof"
                        Quantities =
                            1N
                            |> ValueUnit.singleWithUnit Units.Volume.milliLiter
                            |> Some
                        Divisible = Some (1N)
                        Substances =
                            [
                                {
                                    Medication.substanceItem with
                                        Name = "koolhydraat"
                                        Concentrations =
                                            (1N / 10N)
                                            |> ValueUnit.singleWithUnit (Units.Mass.gram |> Units.per Units.Volume.milliLiter)
                                            |> Some
                                }
                            ]
                }
            ]
        Quantities = failwith "Not Implemented"
    }


tpn
|> Medication.toString
|> print


let tpnConstraints =
    [
        OrderAdjust OrderVariable.Quantity.applyConstraints

        ScheduleFrequency OrderVariable.Frequency.applyConstraints
        ScheduleTime OrderVariable.Time.applyConstraints

        OrderableQuantity OrderVariable.Quantity.applyConstraints
        OrderableDoseCount OrderVariable.Count.applyConstraints
        OrderableDose Order.Orderable.Dose.applyConstraints

        ComponentOrderableQuantity ("", OrderVariable.Quantity.applyConstraints)

        ItemComponentConcentration ("", "", OrderVariable.Concentration.applyConstraints)
        ItemOrderableConcentration ("", "", OrderVariable.Concentration.applyConstraints)
    ]



let applyPropChange msg propChange ord =
    printfn $"=== Apply PropChange {msg} ==="
    let ord =
        ord
        |> Order.OrderPropertyChange.proc propChange
    ord
    |> Order.solveMinMax true Logging.noOp
    |> function
        | Ok ord -> ord
        | _ ->
            printfn $"=== ERROR {msg} ==="
            ord
    |> fun ord ->
        ord
        |> Order.printTable ConsoleTables.Format.Minimal

        ord


let run
    proteinPerc
    potassiumPerc
    sodiumPerc
    glucPerc
    tpn =

    tpn
    |> Medication.toOrderDto
    |> Order.Dto.fromDto
    |> Result.map (fun ord ->
        let ord =
            ord
            |> Order.OrderPropertyChange.proc tpnConstraints
    //        |> Order.applyConstraints

        ord
        |> Order.printTable ConsoleTables.Format.Minimal

        let ord =
            ord
            |> Order.solveMinMax true Logging.noOp //logger
            //|> Result.bind (Order.solveMinMax true logger)

        ord
        |> Result.iter (Order.printTable ConsoleTables.Format.Minimal)

        let ord =
            ord
            |> Result.map (fun ord ->
                ord
                |> applyPropChange
                    "Samenstelling C"
                    [
                        ComponentOrderableQuantity ("Samenstelling C", OrderVariable.Quantity.setPercValue proteinPerc)
                    ]
            )

        let ord =
            ord
            |> Result.map (fun ord ->
                ord
                |> applyPropChange
                    "KCl 7,4%"
                    [
                        ComponentOrderableQuantity ("KCl 7,4%", OrderVariable.Quantity.setPercValue potassiumPerc)
                    ]
            )

        let ord =
            ord
            |> Result.map (fun ord ->
                ord
                |> applyPropChange
                    "NaCl 3%"
                    [
                        ComponentOrderableQuantity ("NaCl 3%", OrderVariable.Quantity.setPercValue sodiumPerc)
                    ]
            )

        let ord =
            ord
            |> Result.map (fun ord ->
                ord
                |> applyPropChange
                    "gluc 10%"
                    [
                        ComponentOrderableQuantity ("gluc 10%", OrderVariable.Quantity.setPercValue glucPerc)
                    ]
            )

        ord
    )


tpn
|> run 50 0 5 0
|> ignore



let dataUrlId = "1JHOrasAZ_2fcVApYpt1qT2lZBsqrAxN-9SvBisXkbsM"


let provider : Resources.IResourceProvider =
        Api.getCachedProviderWithDataUrlId
            FormLogging.noOp
            dataUrlId


{ Filter.doseFilter with
    Generic = Some "Samenstelling C"
    DoseType = DoseType.Timed "dag 1" |> Some
    DoseFilter.Patient.Department = Some "ICK"
    DoseFilter.Patient.Weight =
        10N
        |> ValueUnit.singleWithUnit Units.Weight.kiloGram
        |> Some

}
|> Api.filterPrescriptionRules provider
|> Utils.GenFormResult.map (Array.collect (Medication.fromRule Logging.noOp))
|> Utils.GenFormResult.map (Array.skip 1)
|> Utils.GenFormResult.map (Array.map Medication.toString)
|> Utils.GenFormResult.map (Array.map print)
