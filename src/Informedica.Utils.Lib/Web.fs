namespace Informedica.Utils.Lib


module Web =


    module GoogleSheets =

        open System
        open System.IO
        open System.Net.Http


        let private client = new HttpClient()


        /// Create a url to download a sheet from a Google spreadsheet
        /// The id is the unique id of the spreadsheet and sheet is the name of the sheet
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
                        let! content = resp.Content.ReadAsStringAsync() |> Async.AwaitTask
                        return Ok(content)
                with ex ->
                    return
                        Error
                            $"Network error downloading sheet from {url}.\n\
                              Exception: {ex.GetType().Name}: {ex.Message}"
            }


        /// Load a sheet and parse it, returning a descriptive error when loading fails.
        let getDataFromSheet parser dataUrlId sheet : Async<Result<'T, string>> =
            async {
                let url = createUrl sheet dataUrlId

                let! result = downloadResult url

                return
                    result
                    |> Result.mapError (fun err -> $"Failed to load sheet '{sheet}' using URL ID '{dataUrlId}'.\n{err}")
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


        let getCsvDataFromSheet = getDataFromSheet Csv.parseCSV


        let getCsvDataFromSheetSync dataUrlId sheet =
            getCsvDataFromSheet dataUrlId sheet
            |> Async.RunSynchronously
            |> function
                | Ok result -> result |> Ok
                | Error msg ->
                    ConsoleWriter.writeErrorMessage msg true false
                    msg |> Error
