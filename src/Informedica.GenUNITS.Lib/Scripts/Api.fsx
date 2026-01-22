

#load "load.fsx"

open System
open MathNet.Numerics

open Informedica.GenUnits.Lib
open Informedica.Utils.Lib.BCL


[|1N|]
|> ValueUnit.withUnit Units.Count.times
|> ValueUnit.toStringEngShort
|> ValueUnit.fromString

"1;3;5 x[Count]"
|> ValueUnit.fromString

"[10;20;30] mg[Mass]"
|> ValueUnit.fromString
|> Result.map ValueUnit.toStringEngShort
