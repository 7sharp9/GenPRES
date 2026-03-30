open System

open Informedica.Utils.Lib
open Informedica.GenForm.Lib

open Informedica.MCP.Lib


[<EntryPoint>]
let main _ =
    Env.loadDotEnv () |> ignore
    Environment.SetEnvironmentVariable("GENPRES_PROD", "1")
    Environment.SetEnvironmentVariable("GENPRES_DEBUG", "0")

    // Set working directory to repo root so data paths resolve
    let exeDir = AppContext.BaseDirectory

    // bin/Debug/net10.0 → MCP.Server → src → GenPRES (5 levels)
    let repoRoot = exeDir |> Path.combineWith "../../../../../" |> IO.Path.GetFullPath

    if IO.Directory.Exists(IO.Path.Combine(repoRoot, "data")) then
        Environment.CurrentDirectory <- repoRoot
    else
        eprintfn "[MCP] Warning: could not find repo root from %s, using current directory" exeDir

    let dataUrlId = Environment.GetEnvironmentVariable "GENPRES_URL_ID"

    let provider = Api.getCachedProviderWithDataUrlId FormLogging.noOp dataUrlId

    McpServer.run provider
    0
