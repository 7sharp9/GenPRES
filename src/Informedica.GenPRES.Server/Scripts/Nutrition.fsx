
#load "load.fsx"

#time

open System

open Informedica.Utils.Lib
open Informedica.Utils.Lib.BCL
open Informedica.ZIndex.Lib


Informedica.Utils.Lib.Env.loadDotEnv () |> ignore
Environment.SetEnvironmentVariable(FilePath.GENPRES_PROD, "1")


module DoseRule =

    open Informedica.GenForm.Lib
    open Informedica.GenForm.Lib.Types
    open Informedica.GenForm.Lib.DoseRule


    /// Pretty print dose rule data for  logging
    let doseRuleDataToString (dd: DoseRuleData) =
        let showOpt = Option.map string >> Option.defaultValue "-"
        let showStr =
            fun s ->
                if s |> String.isNullOrWhiteSpace then "-"
                else s

        let showArray toStr xs =
            if xs |> Array.isEmpty then "-"
            else
                xs
                |> Array.map toStr
                |> String.concat ","

        [
            $"Id   : Gen={dd.Generic |> showStr} | Comp={dd.Component |> showStr} | Subst={dd.Substance |> showStr}"
            $"Ctx  : Route={dd.Route |> showStr} | Form={dd.Form |> showStr} | Brand={dd.Brand |> showStr} | Dept={dd.Department |> showStr} | Ind={dd.Indication |> showStr}"
            $"Dose : Type={dd.DoseType |> showStr} | Text={dd.DoseText |> showStr} | DoseUnit={dd.DoseUnit |> showStr} | AdjUnit={dd.AdjustUnit |> showStr}"
            $"Rate : Freq={dd.Frequencies |> showArray string} {dd.FreqUnit |> showStr} | MaxTime={dd.MaxTime |> showOpt} {dd.TimeUnit |> showStr} | MaxRate={dd.MaxRate |> showOpt} | MaxRateAdj={dd.MaxRateAdj |> showOpt} {dd.RateUnit |> showStr}"
            $"Meta : GPKs={dd.GPKs |> showArray id} | Src={dd.Source |> showStr}"
        ]
        |> List.map String.trim
        |> List.filter String.notEmpty
        |> String.concat "\n"



    let doseRuleDataIsValid (dd: DoseRuleData) =
        let missing cond reason =
            if cond then None
            else Some reason

        let doseType = dd.DoseText |> DoseType.fromString dd.DoseType

        let isValid =
            match doseType with
            | NoDoseType -> false
            | Once _ -> true
            | OnceTimed _ ->
                dd.MaxTime.IsSome && dd.TimeUnit |> String.notEmpty
            | Discontinuous _ ->
                dd.Frequencies |> Array.length > 0 && dd.FreqUnit |> String.notEmpty
            | Timed _ ->
                dd.Frequencies |> Array.length > 0 && dd.FreqUnit |> String.notEmpty &&
                dd.MaxTime.IsSome && dd.TimeUnit |> String.notEmpty
            | Continuous _ ->
                dd.RateUnit |> String.notEmpty &&
                (dd.MaxRate.IsSome || dd.MaxRateAdj.IsSome)

        let invalidReasons =
            if isValid then []
            else
                match doseType with
                | NoDoseType -> []
                | Once _ -> []
                | OnceTimed _ ->
                    [
                        missing dd.MaxTime.IsSome "MaxTime is missing"
                        missing (dd.TimeUnit |> String.notEmpty) "TimeUnit is missing"
                    ]
                    |> List.choose id
                | Discontinuous _ ->
                    [
                        missing (dd.Frequencies |> Array.length > 0) "Frequencies is empty"
                        missing (dd.FreqUnit |> String.notEmpty) "FreqUnit is missing"
                    ]
                    |> List.choose id
                | Timed _ ->
                    [
                        missing (dd.Frequencies |> Array.length > 0) "Frequencies is empty"
                        missing (dd.FreqUnit |> String.notEmpty) "FreqUnit is missing"
                        missing dd.MaxTime.IsSome "MaxTime is missing"
                        missing (dd.TimeUnit |> String.notEmpty) "TimeUnit is missing"
                    ]
                    |> List.choose id
                | Continuous _ ->
                    [
                        missing (dd.RateUnit |> String.notEmpty) "RateUnit is missing"
                        missing (dd.MaxRate.IsSome || dd.MaxRateAdj.IsSome) "both MaxRate and MaxRateAdj are missing"
                    ]
                    |> List.choose id

        if not isValid && dd.DoseType |> String.notEmpty then
            if invalidReasons |> List.isEmpty then
                $"Not valid dose rule data:\n{dd |> doseRuleDataToString}\n"
                |> ConsoleWriter.NewLineNoTime.writeWarningMessage
            else
                let why = invalidReasons |> String.concat "; "

                $"Not valid dose rule data (why: {why}):\n{dd |> doseRuleDataToString}\n"
                |> ConsoleWriter.NewLineNoTime.writeWarningMessage

        isValid




let dataUrl = "1rfOo5UjGoVHT5h-bJxR7FS-Qgz4faRrNGLeu2Yj8SS8" //Environment.GetEnvironmentVariable("GENPRES_URL_ID")


printfn $"dataurl: {dataUrl}"


let data =
    Web.GoogleSheets.getCsvDataFromSheetResultSync dataUrl "Formulary"
    |> Result.defaultValue [||]


/// The Assortment Product that is
/// available as a GenericProduct.
type AssortmentProduct =
    {
        /// The GPK code
        GPK: string
        /// The generic name
        Generic: string
        /// The TallMan alternative name
        TallMan : string
        /// The Divisibility of the product
        Divisible : int
    }