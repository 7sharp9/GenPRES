# CLAUDE.md

Read and follow the instructions in [AGENTS.md](AGENTS.md).

This file contains additional guidance specific to Claude Code (claude.ai/claude-code).

## Session Startup

**At the start of each session**, automatically check the FSI MCP server status:

```
mcp__fsi-mcp__get_fsi_status
```

This establishes whether the F# Interactive MCP server is available for the session. If running, prefer using the MCP tools for all F# interactive work (see "Running FSI Scripts" below).

## Running FSI Scripts

**IMPORTANT:** When running F# code interactively, always check if the FSI MCP server is available first using `mcp__fsi-mcp__get_fsi_status`. If the server is running, prefer using the MCP tools over `dotnet fsi`:

- `mcp__fsi-mcp__send_fsharp_code` - Execute F# code directly (end statements with `;;`)
- `mcp__fsi-mcp__load_f_sharp_script` - Load and execute `.fsx` script files
- `mcp__fsi-mcp__get_recent_fsi_events` - View recent FSI output

The MCP server provides a persistent FSI session with real-time output, which is more convenient than running `dotnet fsi` via bash.

**Loading scripts via MCP:** Scripts use `#I __SOURCE_DIRECTORY__` for relative path resolution, but this only works when FSI's current directory matches the script's directory. Before loading a script via MCP, first set the working directory:

```fsharp
// Step 1: Set FSI's working directory to the script's directory
System.IO.Directory.SetCurrentDirectory("/Users/halcwb/Development/halcwb/apps/GenPRES/src/Informedica.GenORDER.Lib/Scripts");;

// Step 2: Then load the script via mcp__fsi-mcp__load_f_sharp_script
```

**Fallback:** If the FSI MCP server is not available, FSI scripts must be run from their directory because they use relative paths:

```bash
# CORRECT - change to script directory first
cd src/Informedica.GenORDER.Lib/Scripts
dotnet fsi Tests.fsx
dotnet fsi Medication.fsx

# INCORRECT - will fail with path errors
dotnet fsi src/Informedica.GenORDER.Lib/Scripts/Tests.fsx
```

## Script-Only Development

**CRITICAL:** Never modify source files (`.fs`) in the codebase. All new features, fixes, enhancements, and experiments must be implemented exclusively in FSI script files (`.fsx`). The user will manually review and migrate verified code to the codebase.

### Rules

1. **NEVER edit `.fs` source files** - Only work in `.fsx` script files
2. **Use `Scripts/` directories** - Each library has a `Scripts/` folder for development
3. **Load compiled code** via `#load "load.fsx"` to access existing library functions
4. **Extend or shadow modules** to add new functionality
5. **Write tests** in the same script to verify implementations
6. **Leave migration to the user** - They will move verified code to source files

### Extending Modules

Shadow an existing module and use `open` to include all its functions automatically:

```fsharp
#load "load.fsx"

open Informedica.GenOrder.Lib

// Shadow the Medication module to add new functions
module Medication =
    // Open the original module - all existing functions become available
    open Informedica.GenOrder.Lib.Medication

    // Add new function
    let fromString (s: string) : Result<Medication, string list> =
        // implementation...

    // Existing functions like toString, template, toOrderDto are now
    // automatically available as Medication.toString, etc.
```

This pattern allows calling both new and existing functions through the same module name:

```fsharp
let text = myMed |> Medication.toString       // original function
let parsed = text |> Medication.fromString    // new function
```

### Testing in Scripts

Write tests directly in the script file:

```fsharp
#r "nuget: expecto"

open Expecto
open Expecto.Flip

let tests =
    testList "feature tests" [
        test "roundtrip works" {
            let original = Scenarios.pcmSupp
            let text = original |> Medication.toString |> String.concat "\n"
            match text |> Medication.fromString with
            | Error errs -> failwith $"Parse failed: {errs}"
            | Ok parsed ->
                parsed.Id |> Expect.equal "Id matches" original.Id
        }
    ]

runTestsWithCLIArgs [] [||] tests
```

### FSI Test Scripts

Scripts in `Scripts/` directories can run tests interactively:

- `Tests.fsx` - General test runner template with FsCheck generators
- `Medication.fsx` - Medication-specific scenarios and tests
- `load.fsx` - Loads all dependencies (referenced by other scripts)

The `load.fsx` script loads `Scenarios.fs` from the test project, making test scenarios available in FSI.
