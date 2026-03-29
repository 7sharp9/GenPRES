---
description: "Create a new FSI script for prototyping a feature or fix in a GenPRES library"
---

Create a new `.fsx` script in the appropriate `Scripts/` directory for the library you're working on.

Follow these steps:

1. Identify which library the feature belongs to (e.g., `src/Informedica.GenORDER.Lib/Scripts/`).
2. Create a new `.fsx` file with a descriptive name (e.g., `MyFeature.fsx`).
3. Start the script with:
   ```fsharp
   #I __SOURCE_DIRECTORY__
   Environment.CurrentDirectory <- __SOURCE_DIRECTORY__
   #load "load.fsx"
   ```
4. Open the relevant namespaces.
5. Prototype the feature or fix using the module shadowing pattern if extending existing modules.
6. Write inline Expecto tests to verify correctness:
   ```fsharp
   #r "nuget: Expecto"
   open Expecto
   open Expecto.Flip

   let tests =
       testList "feature tests" [
           test "describe what is expected" {
               actual
               |> Expect.equal "message" expected
           }
       ]

   runTestsWithCLIArgs [] [||] tests
   ```
7. Do **not** modify any `.fs` source files — leave migration to the user.

**Remember:** All new logic must stay in `.fsx` scripts. The user reviews and migrates verified code to `.fs` source files.
