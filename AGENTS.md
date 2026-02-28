# AGENTS.md

Instructions for AI coding agents working on the GenPRES repository. Make edits small, test-driven, and follow existing repository patterns.

## Project Overview

GenPRES is a Clinical Decision Support System (CDSS) for medication prescribing, built entirely in F# using the SAFE Stack (Saturn, Azure, Fable, Elmish). It provides safe and efficient medication order entry, calculation, and validation for medical settings.

## Quick Start — Build, Run & Test

### Prerequisites

- **.NET SDK**, **Node.js**, and **npm**

For the canonical list of supported versions, see the **Toolchain Requirements** section in [`DEVELOPMENT.md`](DEVELOPMENT.md#toolchain-requirements). For environment variables, see [`DEVELOPMENT.md`](DEVELOPMENT.md#environment-configuration).

### Build and Run

**IMPORTANT:** This repository contains multiple projects. Always specify the solution file:

```bash
# CORRECT - specify the solution
dotnet build GenPRES.sln
dotnet test GenPRES.sln

# INCORRECT - will fail with "more than one project" error
dotnet build
dotnet test
```

- `dotnet run` - Start full application (server + client with hot reload)
- `dotnet run list` - Show all available build targets
- `dotnet run Build` - Build the solution
- `dotnet run Bundle` - Create production bundle
- `dotnet run Clean` - Clean build artifacts
- Access the application at `http://localhost:5173`

### Testing

- `dotnet run ServerTests` - Run all F# unit tests using Expecto
- `dotnet run TestHeadless` - Run tests in headless mode
- `dotnet run WatchTests` - Run tests in watch mode
- `dotnet test GenPRES.sln` - Alternative way to run all tests

Individual library tests:

```bash
dotnet test tests/Informedica.GenSOLVER.Tests/
dotnet test tests/Informedica.GenORDER.Tests/
dotnet test tests/Informedica.GenUNITS.Tests/
# ... etc for other test projects
```

### Code Quality

- `dotnet run Format` - Format F# code using Fantomas

### Docker

- `docker build --build-arg GENPRES_URL_ARG="your_secret_url_id" -t halcwb/genpres .`
- `docker run -it -p 8080:8085 halcwb/genpres`
- `dotnet run DockerRun` - Run pre-built Docker image

## Key Code Locations

- F# libraries under `src/`
- Tests: `tests/` (Expecto + FsCheck). Look for BigRational and ValueUnit tests.
- Resource loading and tests: `src/Informedica.GenForm.Lib/Api.fs` and `tests/`
- Sheet parsers: `Mapping.fs`, `Product.fs`, `DoseRule.fs`, `SolutionRule.fs`, `RenalRule.fs`
- Unit and BigRational helpers: `src/Informedica.GenUnits.Lib/ValueUnit.fs`
- Sheet documentation: `docs/mdr/design-history/genpres_resource_requirements.md`

**Important:** an opt-in strategy is used in the `.gitignore` file — you have to specifically define what should be included instead of the other way around!

## Configuration Architecture

- All medication rules and constraints stored in Google Spreadsheets
- Downloaded as CSV and parsed dynamically
- `GENPRES_URL_ID` environment variable controls which spreadsheet to use
- Local cache files provide offline medication data access

## Communication Pattern

- Client-server communication via Fable.Remoting (type-safe RPC)
- API contracts defined in `src/Informedica.GenPRES.Shared/Api.fs`
- Server processes medication calculations and returns validated results

## Resource Loading Pattern

- Docs with sheet specs: `docs/mdr/design-history/genpres_resource_requirements.md`.
- Check `genpres_resource_requirements.md` for expected sheet and column names.
- Resources are loaded from Google Sheets via `Web.getDataFromSheet dataUrlId "SheetName"`.
- Mapping helper functions use `Csv.getStringColumn` / `Csv.getFloatOptionColumn` and call getString/getFloat-style delegates.
- The central `ResourceConfig` (in `Api.fs`) expects functions returning `GenFormResult<'T>` (alias for `Result<'T, Message list>`). Use the `*Result` variants where present (e.g., `Mapping.getRouteMapping` or `Mapping.getRouteMappingResult`) and wrap with `delay` when the signature expects a `unit -> GenFormResult<_>`.
- To add/modify sheet mappings: adjust the mapper in the corresponding module (e.g., `Product.Reconstitution.get`, `DoseRule.get`) and update `genpres_resource_requirements.md` to reflect column names.
- Update the mapper to read columns by name using the `get` delegate (e.g., `let get = getColumn row in get "Generic"`), parse with `BigRational.toBrs` / `getFloat` as appropriate.
- If adding optional numeric columns, use `getFloatOptionColumn` and `Option.bind BigRational.fromFloat`.

## Result and Error Handling

- IO and parsing functions should return `GenFormResult<'T>` (i.e., Result). Use `FsToolkit.ErrorHandling.ResultCE` computation expression for readability (`result { let! x = ... }`).
- When editing `ResourceConfig` or callers, make sure to handle `Result` values consistently; use `Result.bind`, CE, or `delay` for unit-returning getters.

## BigRational & ValueUnit Semantics

- BigRational operations are used broadly for dosing math. Respect existing helpers in `Informedica.GenUnits.Lib`.
- `removeBigRationalMultiples` semantics: it keeps the smallest positive BigRational representatives and removes later values that are integer multiples of a previously kept value. Example: [1/3; 1/2; 1] → keep 1/2 and 1/3 (both non-multiples of each other), but if 1/2 and 1 are present, keep 1/2 and remove 1 (1 is multiple of 1/2).
- Use `BigRational.isMultiple` when reasoning about integer multiples.
- Prefer using existing helpers like `ValueUnit.singleWithUnit`, `ValueUnit.withUnit`, etc., when manipulating units.
- Use **BigRational** for all medication calculations (absolute precision).
- Use `[<RequireQualifiedAccess>]` on DUs and modules.

## Testing Patterns

All tests use Expecto with Expecto.Flip for fluent assertions:

```fsharp
open Expecto
open Expecto.Flip

test "example test" {
    actual
    |> Expect.equal "should match expected" expected
}
```

### Test Scenarios

Test scenarios are defined in `tests/Informedica.GenORDER.Tests/Scenarios.fs` and include:
- `pcmSupp` - Paracetamol suppository
- `amfo` - Amphotericin B liposomal IV
- `morfCont` - Morphine continuous infusion
- `pcmDrink` - Paracetamol oral liquid
- `cotrim` - Cotrimoxazole
- `tpn` / `tpnComplete` - Total parenteral nutrition
- `fullMedication` - Fully populated medication (all fields set)

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

## Data Dependencies

- Production requires proprietary medication cache files (not in repository)
- Demo version uses sample medication data included in repository
- Google Spreadsheets contain live configuration — changes affect running systems

## Safety, MDR and Documentation

- This project targets clinical medication workflows. Any change that affects dosing, rules, parsing, or resource mapping must include: unit tests, changelog entry, and an update to `docs/mdr/design-history/genpres_resource_requirements.md` if spreadsheet columns or semantics changed.
- Add notes to CONTRIBUTING.md if the change introduces a new external dependency or changes deployment behavior.

## Checklist for Automated Edits

- [ ] Small, focused change with < 300 LOC modified when possible.
- [ ] Add or update unit tests covering the change.
- [ ] Ensure `dotnet run servertests` passes locally for affected projects.
- [ ] Update `genpres_resource_requirements.md` if spreadsheet column names or semantics change.
- [ ] Use conventional commit message with scope and short description.

## Related Documentation

- Coding standards: [F# Coding Instructions](.github/instructions/fsharp-coding.instructions.md)
- Code formatting: [F# Code Formatting](.github/instructions/fsharp-code-formatting.md)
- Commit conventions: [Commit Message Instructions](.github/instructions/commit-message.instructions.md)
- Architecture: [ARCHITECTURE.md](ARCHITECTURE.md)
- Development setup: [DEVELOPMENT.md](DEVELOPMENT.md)
- Contributing: [CONTRIBUTING.md](CONTRIBUTING.md)
- Domain model: `docs/domain/core-domain.md`
