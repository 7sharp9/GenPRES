
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


let serverApi =
    async {
        let! logger = Logging.getLogger Logging.FormularyLogger |> Logging.setComponentName (Some "ServerApi")
        Console.WriteLine "logger activated"
        let provider = ()
            (*
            tryGetEnv "GENPRES_URL_ID"
            |> Option.defaultValue "1IZ3sbmrM4W4OuSYELRmCkdxpN9SlBI-5TLSvXWhHVmA"
            |> Informedica.GenForm.Lib.Api.getCachedProviderWithDataUrlId (Logging.getLogger ()).Logger
            *)

        return provider //|> createServerApi
    } |> Async.RunSynchronously


(*
printfn "start"
async { do! (Logging.getLogger ()).StartAsync (Some "Test.log") Level.Informative |> Async.Ignore }
|> Async.RunSynchronously
printfn "stop"
*)
