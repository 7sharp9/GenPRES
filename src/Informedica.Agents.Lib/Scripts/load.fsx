
#load "../../../scripts/load-dependencies.fsx"


#r "../../Informedica.Utils.Lib/bin/Debug/net10.0/Informedica.Utils.Lib.dll"

#load "../Agent.fs"
#load "../FileWriterAgent.fs"
#load "../FileDirectoryAgent.fs"

open System
open Informedica.Utils.Lib

fsi.AddPrinter<DateTime>(fun dt -> dt.ToString("dd-MMM-yy"))

