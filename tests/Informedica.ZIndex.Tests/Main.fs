open Expecto

open System


[<EntryPoint>]
let main argv =
    printfn $"{Environment.CurrentDirectory}"

    // FixtureSetup module (in FixtureTests.fs) handles BST*.test -> BST* copying
    // at module initialization time, before any test accesses ZIndex modules.

    let mutable result = 1
    try
        result <-
            Informedica.ZIndex.Tests.Tests.tests
            |> runTestsWithCLIArgs [] argv
    finally
        Informedica.ZIndex.Tests.ZIndexFixture.teardownAfterTest
            Informedica.ZIndex.Tests.FixtureSetup.fixtureCreatedFiles

    result
