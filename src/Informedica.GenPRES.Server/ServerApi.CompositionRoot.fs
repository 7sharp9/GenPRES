namespace ServerApi


module CompositionRoot =

    open Informedica.Utils.Lib.ConsoleWriter.NewLineNoTime
    open Shared.Api


    let compose (provider: Informedica.GenForm.Lib.Resources.IResourceProvider) : IServerApi =
        let env = Adapters.makeAppEnv provider

        {
            processCommand =
                fun cmd ->
                    async {
                        try
                            writeInfoMessage $"Processing command: {cmd |> Shared.Api.Command.toString}"
                            let! result = Command.processCmd env cmd
                            writeInfoMessage $"Finished processing command: {cmd |> Shared.Api.Command.toString}"
                            return result
                        with
                        | ex ->
                            writeErrorMessage $"Error processing command: {cmd |> Shared.Api.Command.toString}\n{ex.Message}"
                            return Error [| ex.Message |]
                    }

            testApi =
                fun () ->
                    async {
                        return "Hello world!"
                    }
        }
