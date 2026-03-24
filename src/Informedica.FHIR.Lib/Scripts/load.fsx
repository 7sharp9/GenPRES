#I __SOURCE_DIRECTORY__

let stopWatch = System.Diagnostics.Stopwatch()
stopWatch.Start()

fsi.AddPrinter<System.DateTime> _.ToShortDateString()

#load "../../../scripts/load-dependencies.fsx"

#r "../../Informedica.Utils.Lib/bin/Debug/net10.0/Informedica.Utils.Lib.dll"
#r "../../Informedica.Logging.Lib/bin/Debug/net10.0/Informedica.Logging.Lib.dll"
#r "../../Informedica.GenUnits.Lib/bin/Debug/net10.0/Informedica.GenUnits.Lib.dll"
#r "../../Informedica.GenCore.Lib/bin/Debug/net10.0/Informedica.GenCore.Lib.dll"
#r "../../Informedica.GenSolver.Lib/bin/Debug/net10.0/Informedica.GenSolver.Lib.dll"
#r "../../Informedica.GenFORM.Lib/bin/Debug/net10.0/Informedica.GenFORM.Lib.dll"
#r "../../Informedica.ZIndex.Lib/bin/Debug/net10.0/Informedica.ZIndex.Lib.dll"
#r "../../Informedica.GenORDER.Lib/bin/Debug/net10.0/Informedica.GenOrder.Lib.dll"

open System
open Informedica.Utils.Lib

let zindexPath = __SOURCE_DIRECTORY__ |> Path.combineWith "../../../"
Environment.CurrentDirectory <- zindexPath

printfn $"elapsed time: {stopWatch.ElapsedMilliseconds / 1000L}"
