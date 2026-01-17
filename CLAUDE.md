# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/claude-code) when working with code in this repository.

## Session Startup

**At the start of each session**, automatically check the FSI MCP server status:

```
mcp__fsi-mcp__get_fsi_status
```

This establishes whether the F# Interactive MCP server is available for the session. If running, prefer using the MCP tools for all F# interactive work (see "Running FSI Scripts" below).

## Quick Reference

### Build Commands

**IMPORTANT:** This repository contains multiple projects. Always specify the solution file:

```bash
# CORRECT - specify the solution
dotnet build GenPRES.sln
dotnet test GenPRES.sln

# INCORRECT - will fail with "more than one project" error
dotnet build
dotnet test
```

### Running Tests

```bash
# Run all server tests (from project root)
dotnet run servertests

# Run tests for a specific library
dotnet test tests/Informedica.GenORDER.Tests/
dotnet test tests/Informedica.GenSOLVER.Tests/

# Alternative using solution
dotnet test GenPRES.sln
```

### Running FSI Scripts

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

### Full Application

```bash
# Start full application (server + client with hot reload)
dotnet run

# List all available build targets
dotnet run list
```

## Related Documentation

For detailed guidance, refer to these files:

| File | Purpose |
|------|---------|
| [WARP.md](WARP.md) | Project overview, architecture, and common commands |
| [DEVELOPMENT.md](DEVELOPMENT.md) | Development environment setup and toolchain requirements |
| [ARCHITECTURE.md](ARCHITECTURE.md) | System architecture and design decisions |
| [.github/instructions/fsharp-coding.instructions.md](.github/instructions/fsharp-coding.instructions.md) | F# coding guidelines and patterns |
| [.github/instructions/fsharp-code-formatting.md](.github/instructions/fsharp-code-formatting.md) | Code formatting conventions |
| [.github/instructions/commit-message.instructions.md](.github/instructions/commit-message.instructions.md) | Commit message format and examples |

## Project Structure

- **src/Informedica.*.Lib/** - Domain-specific F# libraries
- **tests/Informedica.*.Tests/** - Test projects (one per library)
- **src/Informedica.GenPRES.Server/** - Server application
- **src/Informedica.GenPRES.Client/** - Client application (Fable/React)
- **src/Informedica.GenPRES.Shared/** - Shared types and API contracts

### Key Libraries

| Library | Purpose |
|---------|---------|
| GenORDER.Lib | Core medication order modeling and calculation |
| GenSOLVER.Lib | Constraint solving engine with BigRational precision |
| GenUNITS.Lib | Units of measure system |
| GenFORM.Lib | Pharmaceutical forms and preparations |
| ZIndex.Lib | Dutch medication database integration |

## Testing Patterns

### Expecto Framework

All tests use Expecto with Expecto.Flip for fluent assertions:

```fsharp
open Expecto
open Expecto.Flip

test "example test" {
    actual
    |> Expect.equal "should match expected" expected
}
```

### FSI Test Scripts

Scripts in `Scripts/` directories can run tests interactively:

- `Tests.fsx` - General test runner template with FsCheck generators
- `Medication.fsx` - Medication-specific scenarios and tests
- `load.fsx` - Loads all dependencies (referenced by other scripts)

The `load.fsx` script loads `Scenarios.fs` from the test project, making test scenarios available in FSI.

### Test Scenarios

Test scenarios are defined in `tests/Informedica.GenORDER.Tests/Scenarios.fs` and include:
- `pcmSupp` - Paracetamol suppository
- `amfo` - Amphotericin B liposomal IV
- `morfCont` - Morphine continuous infusion
- `pcmDrink` - Paracetamol oral liquid
- `cotrim` - Cotrimoxazole
- `tpn` / `tpnComplete` - Total parenteral nutrition
- `fullMedication` - Fully populated medication (all fields set)

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

### Benefits

- **Safe** - Source files are never modified by Claude
- **Verifiable** - User can review and validate before migrating
- **Iterative** - Rapid FSI feedback without compilation cycles
- **Traceable** - All changes are contained in script files

## Common Errors and Solutions

### "Specify which project or solution file to use"

```
MSBUILD : error MSB1011: Specify which project or solution file to use
because this folder contains more than one project or solution file.
```

**Solution:** Always specify `GenPRES.sln`:
```bash
dotnet build GenPRES.sln
dotnet test GenPRES.sln
```

### FSI Script Path Errors

If FSI scripts fail to load dependencies, ensure you're running from the script's directory:

```bash
cd src/Informedica.GenORDER.Lib/Scripts
dotnet fsi Tests.fsx
```

### DLL Not Found

If FSI scripts fail because DLLs are not found, rebuild the solution first:

```bash
dotnet build GenPRES.sln
```

## Code Style Highlights

- **4 spaces** for indentation (no tabs)
- **PascalCase** for types, modules, public members
- **camelCase** for local bindings and private members
- Use **BigRational** for all medication calculations (absolute precision)
- Use **discriminated unions** for domain modeling
- Prefer **modules and functions** over classes
- Use `[<RequireQualifiedAccess>]` on DUs and modules

## Commit Messages

Use conventional commits format:

```
<type>(<scope>): <description>
```

Common scopes: `genorder`, `gensolver`, `genunits`, `zindex`, `client`, `server`, `api`

Examples:
```
feat(genorder): add pediatric dosage calculation
fix(gensolver): resolve infinite loop in constraint propagation
test(genorder): add property tests for dose calculations
```

## Medical Safety Note

This system handles medication dosing calculations. Precision and safety are critical:
- All mathematical operations use **BigRational** to prevent rounding errors
- Extensive validation prevents dangerous medication combinations or doses
- Changes to calculation logic require thorough testing
