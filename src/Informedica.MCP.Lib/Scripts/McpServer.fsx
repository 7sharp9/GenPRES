/// <summary>
/// Prototype MCP server wiring for Informedica.MCP.Lib.
///
/// This script demonstrates how the GenFORM and GenORDER tool handlers
/// (prototyped in McpTools.fsx in each library's Scripts folder) would
/// be wired into a full MCP server using the ModelContextProtocol NuGet
/// package.
///
/// Reference implementation: https://github.com/jovaneyck/fsi-mcp-server
/// NuGet package: https://www.nuget.org/packages/ModelContextProtocol
///
/// ADR: docs/mdr/design-history/mcp-server-architecture.md
///
/// NOTE: This script is a DESIGN PROTOTYPE only.
/// The ModelContextProtocol package is not yet added to this project.
/// Before running, add it to the paket.dependencies and paket.references.
/// See the ADR for the dependency check procedure.
///
/// Usage (after adding the NuGet package):
///   cd src/Informedica.MCP.Lib/Scripts
///   dotnet fsi McpServer.fsx
/// </summary>

/// ── Pattern: How tool handlers map to MCP tools ────────────────────────────
///
/// The ModelContextProtocol .NET SDK (csharp-sdk) registers tools via
/// an attribute-based or builder-based API. Below is the builder-based
/// pattern in F# pseudocode showing how each GenFORM/GenORDER tool handler
/// maps to an MCP tool registration:
///
///   McpServerBuilder()
///     .AddTool(
///         name = "get_dose_rules",
///         description = "Return all dose rules from the GenFORM knowledge base",
///         inputSchema = JsonSchema.empty,
///         handler = fun _input -> GenFormTools.getDoseRules () |> Json.serialize
///     )
///     .AddTool(
///         name = "filter_dose_rules",
///         description = "Filter dose rules by generic drug name, route, form, indication, and patient demographics",
///         inputSchema = filterDoseRulesSchema,
///         handler = fun input ->
///             input
///             |> Json.deserialize<FilterDoseRulesInput>
///             |> GenFormTools.filterDoseRules
///             |> Json.serialize
///     )
///     // ... etc.
///     .UseStdioTransport()
///     .Build()
///     .RunAsync()
///
/// ── Tool registry ──────────────────────────────────────────────────────────
///
/// The following table summarises all Phase 1 tools and their source:
///
/// Tool name                     | Source script            | Status
/// ------------------------------|--------------------------|--------
/// get_resource_info             | GenFORM/McpTools.fsx    | ⬜ prototype
/// get_dose_rules                | GenFORM/McpTools.fsx    | ⬜ prototype
/// filter_dose_rules             | GenFORM/McpTools.fsx    | ⬜ prototype
/// get_solution_rules            | GenFORM/McpTools.fsx    | ⬜ prototype
/// get_renal_rules               | GenFORM/McpTools.fsx    | ⬜ prototype
/// get_prescription_rules        | GenFORM/McpTools.fsx    | ⬜ prototype
/// get_formulary                 | GenFORM/McpTools.fsx    | ⬜ prototype
/// get_parenteral_meds           | GenFORM/McpTools.fsx    | ⬜ prototype
/// get_order_context_filter_opts | GenORDER/McpTools.fsx   | ⬜ prototype
/// get_dose_rules_for_context    | GenORDER/McpTools.fsx   | ⬜ prototype
/// get_solution_rules_for_context| GenORDER/McpTools.fsx   | ⬜ prototype
/// create_order_context          | GenORDER/McpTools.fsx   | ⬜ prototype
/// get_order_scenarios           | GenORDER/McpTools.fsx   | ⬜ prototype
///
/// ── Claude Desktop configuration ──────────────────────────────────────────
///
/// After migration to source files and building the standalone executable,
/// add to claude_desktop_config.json:
///
/// {
///   "mcpServers": {
///     "genpres": {
///       "command": "dotnet",
///       "args": ["path/to/Informedica.MCP.Lib.dll"],
///       "env": {
///         "GENPRES_URL_ID": "<your-sheet-id>",
///         "GENPRES_PROD": "1"
///       }
///     }
///   }
/// }
///
/// ── Next steps ────────────────────────────────────────────────────────────
///
/// 1. Run McpTools.fsx in GenFORM.Lib/Scripts and verify output
/// 2. Run McpTools.fsx in GenORDER.Lib/Scripts and verify output
/// 3. Run this script after adding ModelContextProtocol NuGet package
/// 4. Submit for human review
/// 5. Migrate to McpTools.GenForm.fs, McpTools.GenOrder.fs, McpServer.fs

printfn "MCP server wiring prototype — see comments above for design details"
printfn "Run GenFORM/Scripts/McpTools.fsx and GenORDER/Scripts/McpTools.fsx first."
