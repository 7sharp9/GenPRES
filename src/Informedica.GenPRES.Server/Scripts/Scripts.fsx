
open System

Environment.SetEnvironmentVariable("GENPRES_LOG", "1")
Environment.SetEnvironmentVariable("GENPRES_PROD", "1")
Environment.SetEnvironmentVariable("GENPRES_DEBUG", "v")
Environment.SetEnvironmentVariable("GENPRES_URL_ID", "1JHOrasAZ_2fcVApYpt1qT2lZBsqrAxN-9SvBisXkbsM")

#load "load.fsx"

open Shared.Types
open Shared


open Informedica.Logging.Lib

let tryGetEnv key =
    match Environment.GetEnvironmentVariable key with
    | x when String.IsNullOrWhiteSpace x -> None
    | x -> Some x

