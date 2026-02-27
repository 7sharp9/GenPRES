

#load "load.fsx"

open System
open MathNet.Numerics

open Informedica.GenUnits.Lib
open Informedica.Utils.Lib.BCL


open FParsec

"6 mos[Time]"
|> run Parser.parseUnit


module Array =


    let inline pickNearestHigherElseLower target xs =
        if Array.isEmpty xs then invalidArg "xs" "Array cannot be empty"
        let ys = xs |> Array.sort
        match ys |> Array.tryFind (fun x -> x >= target) with
        | Some x -> x                   // smallest value >= target
        | None -> ys[ys.Length - 1]     // no higher value exists: take highest lower



module ValueUnit =



    let pickNearestHigherElseLower (target: ValueUnit) (candidates: ValueUnit) =
        if candidates |> ValueUnit.isEmpty then candidates
        elif candidates |> ValueUnit.eqsGroup target |> not then candidates
        else
            candidates
            |> ValueUnit.toBase
            |> ValueUnit.applyToValue (fun brs1 ->
                target
                |> ValueUnit.getBaseValue
                |> Array.tryExactlyOne
                |> Option.map (fun br ->
                    [| brs1 |> Array.pickNearestHigherElseLower br |]
                )
                |> Option.defaultValue brs1
            ) // set selected base value
            |> ValueUnit.toUnit
