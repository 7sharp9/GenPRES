open System

Informedica.Utils.Lib.Env.loadDotEnv () |> ignore
Environment.SetEnvironmentVariable("GENPRES_LOG", "i")
Environment.SetEnvironmentVariable("GENPRES_PROD", "1")
Environment.SetEnvironmentVariable("GENPRES_DEBUG", "1")

#load "load.fsx"

open Shared.Types
open Shared


open Informedica.Logging.Lib

let tryGetEnv key =
    match Environment.GetEnvironmentVariable key with
    | x when String.IsNullOrWhiteSpace x -> None
    | x -> Some x
