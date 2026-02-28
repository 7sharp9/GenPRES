# WARP.md

This file provides guidance to WARP (warp.dev) when working with code in this repository.

## Project Overview

GenPRES is a Clinical Decision Support System (CDSS) for medication prescribing, built entirely in F# using the SAFE Stack (Saturn, Azure, Fable, Elmish). It provides safe and efficient medication order entry, calculation, and validation for medical settings.

## Development Environment

### Prerequisites

- **.NET SDK**, **Node.js**, and **npm**

For the canonical list of supported versions, see the
**Toolchain Requirements** section in [`DEVELOPMENT.md`](DEVELOPMENT.md#toolchain-requirements).

### Required Environment Variables

For demo and development environment variables, see `DEVELOPMENT.md#environment-configuration`.

## Common Development Commands

### Build and Run

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
- `dotnet test GenPres.sln` - Alternative way to run all tests

### Individual Library Testing

Run tests for specific libraries:

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

## Architecture Overview

GenPRES follows a client-server web application architecture built on the [SAFE Stack](https://safe-stack.github.io/). For the complete architecture documentation, see [ARCHITECTURE.md](ARCHITECTURE.md) and `docs/mdr/design-history/architecture.md`. For the core domain model and transformation pipeline, see `docs/domain/core-domain.md`.

For the complete library list and dependency order, see the [Core Libraries](DEVELOPMENT.md#core-libraries) section in DEVELOPMENT.md.

### Configuration Architecture

- All medication rules and constraints stored in Google Spreadsheets
- Downloaded as CSV and parsed dynamically
- `GENPRES_URL_ID` environment variable controls which spreadsheet to use
- Local cache files provide offline medication data access

### Communication Pattern

- Client-server communication via Fable.Remoting (type-safe RPC)
- API contracts defined in `src/Informedica.GenPRES.Shared/Api.fs`
- Server processes medication calculations and returns validated results

## Key Development Patterns

For coding standards, testing patterns, and F# development guidelines, see the [F# Coding Instructions](.github/instructions/fsharp-coding.instructions.md).

For medical safety considerations, see the [Domain-Specific Guidelines](DEVELOPMENT.md#domain-specific-guidelines) section in DEVELOPMENT.md.

## Commit Message Conventions

Follow the [Commit Message Instructions](.github/instructions/commit-message.instructions.md) for conventional commit format, types, scopes, and examples.

## Important Notes

### Data Dependencies

- Production requires proprietary medication cache files (not in repository)
- Demo version uses sample medication data included in repository
- Google Spreadsheets contain live configuration - changes affect running systems
