

#load "load.fsx"

open System
open MathNet.Numerics

open Informedica.GenUnits.Lib
open Informedica.Utils.Lib.BCL


open FParsec

"6 mos[Time]"
|> run Parser.parseUnit
