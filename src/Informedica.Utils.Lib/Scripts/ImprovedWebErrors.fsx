/// Script prototype for improved error messages in Web.GoogleSheets
/// Addresses issue #52: https://github.com/informedica/GenPRES/issues/52
///
/// This script shadows the Web.GoogleSheets module with an improved version that:
/// 1. Checks HTTP response status codes
/// 2. Includes the URL ID and sheet name in error messages
/// 3. Provides actionable hints when the URL ID may be outdated
///
/// To migrate to source: update `src/Informedica.Utils.Lib/Web.fs`
/// - Replace `download` with `downloadWithCheck`
/// - Replace `getDataFromSheet` with the improved version below

System.Environment.CurrentDirectory <- __SOURCE_DIRECTORY__

#load "load.fsx"

open System
open System.IO
open System.Net.Http

open Informedica.Utils.Lib


/// Improved version of Web.GoogleSheets with descriptive error messages.
module ImprovedGoogleSheets =

    let private client = new HttpClient()


    /// Create a url to download a sheet from a Google spreadsheet
    let createUrl sheet id =
        $"https://docs.google.com/spreadsheets/d/%s{id}/gviz/tq?tqx=out:csv&sheet=%s{sheet}"


    /// Download a sheet from a Google spreadsheet, returning an error string
    /// if the HTTP response is not successful (e.g. 404, 403, or redirect to
    /// Google sign-in when the spreadsheet is private or the ID is stale).
    let downloadResult url : Async<Result<string, string>> =
        async {
            try
                use! resp = client.GetAsync(Uri(url)) |> Async.AwaitTask

                if not resp.IsSuccessStatusCode then
                    let status = int resp.StatusCode
                    return
                        Error
                            $"HTTP {status} ({resp.StatusCode}) downloading sheet.\n\
                              URL: {url}\n\
                              Hint: if this is a Google Sheets URL, check that:\n\
                              - The spreadsheet is shared publicly (Anyone with the link can view)\n\
                              - The GENPRES_URL_ID environment variable is set correctly\n\
                              - The URL ID has not expired or been rotated"
                else
                    use! stream = resp.Content.ReadAsStreamAsync() |> Async.AwaitTask
                    use reader = new StreamReader(stream)
                    return Ok(reader.ReadToEnd())
            with ex ->
                return
                    Error
                        $"Network error downloading sheet from {url}.\n\
                          Exception: {ex.GetType().Name}: {ex.Message}"
        }


    /// Load a sheet and parse it, returning a descriptive error when loading fails.
    let getDataFromSheetResult parser dataUrlId sheet : Async<Result<'T, string>> =
        async {
            let url = createUrl sheet dataUrlId

            let! result = downloadResult url

            return
                result
                |> Result.mapError (fun err ->
                    $"Failed to load sheet '{sheet}' using URL ID '{dataUrlId}'.\n{err}"
                )
                |> Result.bind (fun data ->
                    try
                        Ok(parser data)
                    with ex ->
                        Error
                            $"Sheet '{sheet}' (URL ID: '{dataUrlId}') downloaded successfully \
                              but could not be parsed.\n\
                              Exception: {ex.GetType().Name}: {ex.Message}\n\
                              Hint: verify that the sheet name is correct and the spreadsheet \
                              has the expected column structure."
                )
        }


    // ── Quick smoke test ──────────────────────────────────────────────────────

    let private dummyUrlId = "INVALID_ID_FOR_DEMO"
    let private dummySheet = "TestSheet"

    let runSmokeTest () =
        async {
            let! result = getDataFromSheetResult id dummyUrlId dummySheet

            match result with
            | Ok data ->
                printfn "Unexpected success (test with real ID?): %d bytes" data.Length
            | Error msg ->
                printfn "Got expected error (demonstrates improved message):\n%s" msg
        }
        |> Async.RunSynchronously


// Uncomment to run the smoke test interactively in FSI:
// ImprovedGoogleSheets.runSmokeTest ()
