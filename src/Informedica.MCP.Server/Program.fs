open System

open Informedica.Utils.Lib
open Informedica.GenForm.Lib

open Informedica.MCP.Lib


let findRepoRoot (startDir: string) =
    let rec search (dir: string) =
        if
            IO.Directory.Exists(IO.Path.Combine(dir, "data"))
            && IO.File.Exists(IO.Path.Combine(dir, "GenPRES.sln"))
        then
            Some dir
        else
            let parent = IO.Directory.GetParent(dir)

            if isNull parent then None else search parent.FullName

    search startDir


[<EntryPoint>]
let main _ =
    Env.loadDotEnv () |> ignore
    Environment.SetEnvironmentVariable("GENPRES_PROD", "1")
    Environment.SetEnvironmentVariable("GENPRES_DEBUG", "0")

    // Set working directory to repo root so data paths resolve
    let exeDir = AppContext.BaseDirectory

    match findRepoRoot exeDir with
    | Some root -> Environment.CurrentDirectory <- root
    | None -> eprintfn $"[MCP] Warning: could not find repo root from %s{exeDir}, using current directory"

    let dataUrlId =
        match Environment.GetEnvironmentVariable "GENPRES_URL_ID" with
        | null
        | "" -> invalidOp "GENPRES_URL_ID environment variable must be set before starting the MCP server."
        | value -> value

    let provider = Api.getCachedProviderWithDataUrlId FormLogging.noOp dataUrlId

    McpServer.run provider
    0
