open Expecto

open System


[<EntryPoint>]
let main argv =
    printfn $"{Environment.CurrentDirectory}"

    // Force fixture setup BEFORE any test accesses ZIndex modules.
    // This must happen before runTestsWithCLIArgs, which triggers
    // BST001T module initialization (cached on first access).
    // Teardown is handled by a ProcessExit handler registered in FixtureSetup.
    let _fixtureResult = Informedica.ZIndex.Tests.FixtureSetup.fixtureCreatedFiles

    Informedica.ZIndex.Tests.Tests.tests
    |> runTestsWithCLIArgs [] argv
