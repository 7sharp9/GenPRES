

#load "load.fsx"

open System
open MathNet.Numerics

open Informedica.GenUnits.Lib
open Informedica.Utils.Lib.BCL


[|1N;3N|]
|> ValueUnit.withUnit Units.Count.times
|> ValueUnit.toStringEngShort
|> ValueUnit.fromString
