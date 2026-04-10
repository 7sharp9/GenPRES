//module Server

open Giraffe
open Saturn
open Fable.Remoting.Server
open Fable.Remoting.Giraffe
open Shared.Api
open ServerApi
open Informedica.Utils.Lib.ConsoleWriter.NewLineTime
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.DependencyInjection
open System.Threading.Tasks

open Informedica.Utils.Lib.BCL
open Informedica.Utils.Lib

open Microsoft.AspNetCore.Http


let getClientIP (context: HttpContext) =
    match context.Request.Headers.TryGetValue("X-Forwarded-For") with
    | true, values when values.Count > 0 ->
        values[0].Split(',')
        |> Array.tryHead
        |> Option.map String.trim
        |> Option.defaultValue "unknown"
    | _ ->
        match context.Connection.RemoteIpAddress with
        | null -> "unknown"
        | ip -> ip.ToString()


let tryGetEnv key = Env.getItem key


// Banner display strings. The empty-string filter matches the runtime checks
// in `validateProductionPassword` and the `provider` binding below: the
// Dockerfile declares `ENV GENPRES_PASSWORD=` / `ENV GENPRES_URL_ID=` with
// empty defaults so the variables are discoverable by container management
// UIs (Plesk, Portainer, ...). An empty value must be reported as not-set,
// otherwise the banner would mislead an operator into thinking a real value
// was injected.
let password =
    tryGetEnv "GENPRES_PASSWORD"
    |> Option.filter (System.String.IsNullOrWhiteSpace >> not)
    |> Option.map (fun _ -> "***")
    |> Option.defaultValue "NOT SET (admin operations disabled)"


// Show only the last 5 characters of GENPRES_URL_ID, prefixed with `***`,
// so the full proprietary Sheet ID never lands in startup logs / screenshots
// / bug reports while still giving operators enough of a fingerprint to
// confirm the right ID is loaded.
let urlId =
    tryGetEnv "GENPRES_URL_ID"
    |> Option.filter (System.String.IsNullOrWhiteSpace >> not)
    |> Option.map (fun s -> if s.Length > 5 then s.Substring(s.Length - 5) else s)
    |> Option.defaultValue "NO GENPRES_URL_ID"


$"""

=== Environmental variables ===
GENPRES_URL_ID = ***{urlId}
GENPRES_LOG ={tryGetEnv "GENPRES_LOG" |> Option.defaultValue "0"}
GENPRES_PROD = {tryGetEnv "GENPRES_PROD" |> Option.defaultValue "0"}
GENPRES_DEBUG = {tryGetEnv "GENPRES_DEBUG" |> Option.defaultValue "i"}
GENPRES_PASSWORD = {password}

=== System Info ===

{Env.getSystemInfo ()}

"""
|> writeInfoMessage


let port =
    "SERVER_PORT" |> tryGetEnv |> Option.map uint16 |> Option.defaultValue 8085us


// SECURITY: when running in production mode (GENPRES_PROD=1), refuse to start
// if GENPRES_PASSWORD is missing or shorter than 16 characters. The dev
// password "genpres" must never reach production. The check is intentionally
// fail-closed and runs before any HTTP listener is bound.
//
// In demo/dev mode (GENPRES_PROD≠1) any value (or none) is accepted; admin
// operations remain disabled when GENPRES_PASSWORD is unset (see
// ServerApi.Command.fs:11-14 and ServerApi.Services.fs ReloadResources).
let private minProductionPasswordLength = 16


let private validateProductionPassword () =
    let isProd =
        tryGetEnv "GENPRES_PROD"
        |> Option.map (fun v -> v = "1")
        |> Option.defaultValue false

    if isProd then
        // Treat empty / whitespace-only as unset. The Dockerfile declares
        // `ENV GENPRES_PASSWORD=` with an empty default so the variable is
        // discoverable by container management UIs (Plesk, Portainer, ...);
        // operators inject the real value at runtime. An operator that
        // forgets to inject it must hit the "not set" path, not the
        // "shorter than 16 characters" path.
        match
            tryGetEnv "GENPRES_PASSWORD"
            |> Option.filter (System.String.IsNullOrWhiteSpace >> not)
        with
        | None ->
            failwith
                "GENPRES_PROD=1 but GENPRES_PASSWORD is not set (or is empty). \
                 Refusing to start in production without an admin password. \
                 Generate one with `openssl rand -base64 32` and inject it via a secret store. \
                 See DEVELOPMENT.md → Password policy."
        | Some pwd when pwd.Length < minProductionPasswordLength ->
            failwith
                $"GENPRES_PROD=1 but GENPRES_PASSWORD is shorter than {minProductionPasswordLength} characters. \
                 Refusing to start in production with a weak admin password. \
                 Generate a stronger one with `openssl rand -base64 32`. \
                 See DEVELOPMENT.md → Password policy."
        | Some _ -> ()


validateProductionPassword ()


let provider =
    let logger =
        Logging.loggingLevel
        |> Option.map (fun level ->
            Logging.getLogger level Logging.ResourcesLogger
            |> (fun logger ->
                logger |> Logging.setComponentName (Some "Provider") |> Async.RunSynchronously
                logger
            )
        )
        |> Option.map _.Logger
        |> Option.defaultValue Informedica.GenOrder.Lib.Logging.noOp

    // Treat empty / whitespace-only as unset (the Dockerfile declares
    // `ENV GENPRES_URL_ID=` with an empty default so it is visible to
    // container management UIs; operators inject the real value at runtime).
    // Without this filter, the empty string would silently flow into
    // `getCachedProviderWithDataUrlId` and surface much later as a confusing
    // "cannot find column" error from GenForm.
    tryGetEnv "GENPRES_URL_ID"
    |> Option.filter (System.String.IsNullOrWhiteSpace >> not)
    |> Option.defaultWith (fun () -> failwith "No GENPRES_URL_ID (or value is empty)")
    |> Informedica.GenForm.Lib.Api.getCachedProviderWithDataUrlId logger


let logClientIP: HttpHandler =
    fun (next: HttpFunc) (ctx: HttpContext) ->
        match Logging.loggingLevel with
        | None -> next ctx
        | Some level ->
            let clientIP = getClientIP ctx
            let path = ctx.Request.Path.ToString()
            let method = ctx.Request.Method
            let logger = Logging.getLogger level Logging.RequestLogger

            async {
                do! logger |> Logging.setComponentName (Some "Client_Request")

                Logging.ServerLogging.logRequest logger method path clientIP
                return ()
            }
            |> Async.Start

            // Continue with the next handler
            next ctx


let webApi =
    Remoting.createApi ()
    |> Remoting.fromValue (createServerApi provider)
    |> Remoting.withRouteBuilder routerPaths
    |> Remoting.buildHttpHandler


let webApp =
    choose
        [
            logClientIP >=> webApi
            GET >=> text "GenInteractions App. Use localhost: 8080 for the GUI"
        ]


type LoggerShutdown() =
    interface IHostedService with
        member _.StartAsync _ = Task.CompletedTask

        member _.StopAsync _ =
            lock
                Logging.loggerLock
                (fun () ->
                    [|
                        for kv in Logging.loggers do
                            let logger = kv.Value

                            writeInfoMessage $"Trying to Stop {kv.Key}"

                            try
                                logger.StopAsync()
                            with ex ->
                                writeDebugMessage $"Logger shutdown failed: {ex.Message}"
                                async { return () }
                    |]
                    |> Async.Parallel
                    |> Async.StartAsTask
                    :> Task
                )


let application =
    application {
        url ("http://*:" + port.ToString() + "/")
        use_mime_types [ ".svg", "image/svg+xml"; ".png", "image/png" ]
        use_static "public" //publicPath
        use_router webApp

        service_config (fun services ->
            services.AddHostedService<LoggerShutdown>() |> ignore
            services
        )

        memory_cache
        use_gzip
    }

run application
