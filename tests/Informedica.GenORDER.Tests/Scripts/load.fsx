
#I __SOURCE_DIRECTORY__

let stopWatch = System.Diagnostics.Stopwatch()

stopWatch.Start()

fsi.AddPrinter<System.DateTime> _.ToShortDateString()


#load "../../../scripts/load-dependencies.fsx"


#r "../../../src/Informedica.Utils.Lib/bin/Debug/net10.0/Informedica.Utils.Lib.dll"
#r "../../../src/Informedica.Logging.Lib/bin/Debug/net10.0/Informedica.Logging.Lib.dll"
#r "../../../src/Informedica.GenUnits.Lib/bin/Debug/net10.0/Informedica.GenUnits.Lib.dll"
#r "../../../src/Informedica.GenCore.Lib/bin/Debug/net10.0/Informedica.GenCore.Lib.dll"
#r "../../../src/Informedica.GenSolver.Lib/bin/Debug/net10.0/Informedica.GenSolver.Lib.dll"
#r "../../../src/Informedica.GenForm.Lib/bin/Debug/net10.0/Informedica.GenForm.Lib.dll"
#r "../../../src/Informedica.ZIndex.Lib/bin/Debug/net10.0/Informedica.ZIndex.Lib.dll"
#r "../../../src/Informedica.GenOrder.Lib/bin/Debug/net10.0/Informedica.GenOrder.Lib.dll"


open System
open Informedica.Utils.Lib


let zindexPath = __SOURCE_DIRECTORY__ |> Path.combineWith "../../../"
Environment.CurrentDirectory <- zindexPath

printfn $"elapsed time: {stopWatch.ElapsedMilliseconds / 1000L}"
