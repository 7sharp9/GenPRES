namespace ServerApi


[<AutoOpen>]
module ApiImpl =

    open Informedica.Utils.Lib.ConsoleWriter.NewLineNoTime
    open Shared.Api


    /// An implementation of the Shared IServerApi protocol.
    let createServerApi provider : IServerApi =
        {
            processCommand =
                fun cmd ->
                    async {
                        try
                            writeInfoMessage $"Processing command: {cmd |> Command.toString}"
                            let! result = Command.processCmd provider cmd
                            writeInfoMessage $"Finished processing command: {cmd |> Command.toString}"
                            return result
                        with
                        | ex ->
                            writeErrorMessage $"Error processing command: {cmd |> Command.toString}\n{ex.Message}"
                            return Error [| ex.Message |]
                    }

            testApi =
                fun () ->
                    async {
                        return "Hello world!"
                    }
        }
