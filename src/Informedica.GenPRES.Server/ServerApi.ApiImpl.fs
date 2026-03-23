namespace ServerApi


[<AutoOpen>]
module ApiImpl =

    /// Creates the IServerApi implementation using the composition root.
    let createServerApi provider : Shared.Api.IServerApi = CompositionRoot.compose provider
