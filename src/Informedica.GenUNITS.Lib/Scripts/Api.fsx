

#load "load.fsx"

open System
open MathNet.Numerics

open Informedica.GenUnits.Lib
open Informedica.Utils.Lib.BCL


[|1N|]
|> ValueUnit.withUnit Units.Count.times
|> ValueUnit.toStringEngShort
|> ValueUnit.fromString

"1;3;5 x"
|> ValueUnit.fromString

"10;20;30 mg/ml"
|> ValueUnit.fromString
|> Result.map ValueUnit.toStringEngShort


1N
|> ValueUnit.singleWithUnit (Units.General.general "stuk")
|> ValueUnit.toStringEngShort
|> ValueUnit.fromString


"stuk"
|> Units.groupIsGeneralOrNone

"120 stuk"
|> ValueUnit.fromString


"120;240;500;1000;125;250;60;30;360;90;750;180 mg/stuk"
|> ValueUnit.fromString
