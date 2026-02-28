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

The script-only code policy in [AGENTS.md](AGENTS.md) applies. As a reminder:

1. **NEVER write new code in `.fs` source files** — Only work in `.fsx` script files
2. **Use `Scripts/` directories** — Each library has a `Scripts/` folder for development
3. **Leave migration to the user** — They will review and move verified code to source files

For the full workflow, module shadowing pattern, and testing in scripts, see the **Script-Based Development Workflow** section in [AGENTS.md](AGENTS.md).
