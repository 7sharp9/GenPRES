//#I __SOURCE_DIRECTORY__

#load "load.fsx"

#time

open MathNet.Numerics
open Informedica.Utils.Lib
open Informedica.Utils.Lib.BCL


module Array =


    let inline pickNearestHigherElseLower target xs =
        if Array.isEmpty xs then
            invalidArg "xs" "Array cannot be empty"

        let ys = xs |> Array.sort

        match ys |> Array.tryFind (fun x -> x >= target) with
        | Some x -> x // smallest value >= target
        | None -> ys[ys.Length - 1] // no higher value exists: take highest lower
