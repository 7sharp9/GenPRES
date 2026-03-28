# Changelog

All notable changes to GenPRES will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- **Scripts (GenSOLVER)**: Add `LRUSolverIntegration.fsx` — W2 final integration step for the LRU cache: variable-name remapping on cache hit, `SessionSolver` injectable module with `warmUp`, 6 correctness tests, capacity tuning benchmark across 8 capacity values, and a documented migration path to `Solver.fs` integration (PR #237/238)
- **Client (UI)**: Localize hardcoded UI strings — replace hardcoded Dutch strings across all client views with localized terms using the existing localization system; add new `Terms` DU cases for shared, patient, nutrition, and interaction labels (PR #239)
- **Client (UI)**: Improve overall layout — responsive table rendering, sidebar initialisation, conditional totals bar, and layout design documentation update (PR #235)
- **Scripts (GenSOLVER)**: Add `CanonKeyInvariant.fsx` — 8 unit tests + 6 FsCheck property tests for `CanonKey`; validates key normalisation, round-trip symmetry, and hash-stability invariants (PR #233)
- **Scripts (GenSOLVER)**: Add `LRUCache.fsx` — session-level LRU cache prototype for the constraint solver; implements `LRUCache<'K,'V>` (thread-safe, O(1) get/put/evict, configurable capacity), `Solver.solveAllLRU` with canonical keys for cross-variable-name sharing, and Expecto correctness tests plus a 10-patient dosing benchmark (PR #220/221)
- **Tests (GenSOLVER)**: Add `LRUCacheProps.fsx` — FsCheck property-based tests for the `LRUCache` module; validates cache invariants (capacity, eviction, thread-safety), get/put correctness, and stress-test properties (PR #230)
- **Scripts (FHIR)**: Add `ImplementationPlan.fsx` — comprehensive FHIR R4 integration prototype: defines `FhirScenario` and `FhirMedicationRequest` types, implements bidirectional translation (`toFhirMedicationRequest` / `fromFhirMedicationRequest`), maps scenarios 6.1–6.6 from the interface specification, and documents the path to full `Hl7.Fhir.R4` integration (PR #215)
- **Client (UI)**: Improve interactions feature — deduplicate drug-interaction requests, fix `retryDrugNames` `InProgress` state handling, resolve unbounded concurrency (PR #216)
- **Scripts (FHIR)**: Add `FhirExpectoTests.fsx` — Expecto test scaffolding for the six FHIR translation scenarios; covers `toFhirMedicationRequest` output shape, `fromFhirMedicationRequest` round-trip, and Dutch G-Standard coding system constants
- **Server**: Graceful shutdown support — server now cleanly terminates active agents and connections on SIGTERM/SIGINT
- **Server**: Switch to bounded domain modular architecture — server modules reorganised as independent bounded contexts for improved cohesion and testability; legacy code removed
- **Server (Agents)**: Add `Agent.createReplyAsync` — new variant that accepts `'Request -> Async<'Reply>` to avoid blocking thread-pool threads in async agent workflows
- **Client (UI)**: Update to latest Fable releases (Feliz.Router patched for compatibility)
- **Tests (GenFORM)**: Migrate test scaffolding into formal CI test suite — 218 lines of new Expecto tests
- **Tests (GenORDER)**: Migrate test scaffolding into formal CI test suite — 120 lines of new Expecto tests
- **Build**: Add Fantomas pre-commit hook — F# source files are now auto-formatted on every commit; `.fantomasignore` updated to exclude client UI code

### Changed

- **Client**: Refactor environment variable access to use `AppEnv` module — adds `asEnv` Fable-compatible helper (`unbox<'T>`), eliminating scattered inline environment reads; fixes `appendScenarioToTreatmentPlan` naming to remove shadowing of outer `updateTreatmentPlan`; prevents concurrent duplicate `UpdateOrderPlan` requests by checking `InProgress`/`Recalculating` state (PR #223)
- **Docs**: Update `clean-safe-architecture.md` to reflect implemented safe-and-clean architecture state and add code-verified implementation notes (PR #227)

### Fixed

- **Client (UI)**: Fix layout overflow — replace `React.Fragment` with `Box` for correct overflow containment in universal layout; conditional totals bar rendering and duplicate padding corrections (PR #229, PR #235)
- **Client (UI)**: Remove hardcoded year from app title — '2023' removed from `GenPres.fs`; title now reads 'GenPRES \<page\>' without a stale year (PR #237)
- **Client (UI)**: Replace confusing language flag emoji with short language code in title bar language selector (PR #239)
- **Client (UI)**: Improve interactions management — InProgress guard prevents redundant server calls when a request is already in flight; unused `drugs` variable removed (PR #224)
- **Scripts (FHIR)**: Fix `ImplementationPlan.fsx` — major rework with correct FHIR property mappings and a full end-to-end round trip from a calculated `GenOrder.Order` to `FhirMedicationRequest` and back (PR #222)
- **Client (UI)**: Fix "Select All" in treatment plan table — rows now correctly toggle; unfiltered row search replaced with filtered-row lookup for O(n²) → O(n) improvement (PR #217)
- **Build**: Exclude `.fsx` scripts from Fantomas automatic formatting (PR #218)
- **Build**: Fix Fantomas glob pattern — scripts directory path corrected so `.fsx` files are properly ignored (PR #219)
- **Build**: Bump `yaml` dependency in client project (PR #225)
- **Build**: Bump `picomatch` from 4.0.3 to 4.0.4 in client project

---

## [0.1.2-alpha] - 2026-03-23

> ⚠️ **Alpha release** — Early development stage. Major features are incomplete. **Not for clinical use.**

### Added

- **Architecture**: Modular agent bounded contexts — safe and clean architecture with Command/Response pattern for ZForm, ZIndex, GenForm, and GenOrder agents (PR #204)
- **Server**: Graceful shutdown support with proper resource cleanup (PR #207)
- **Build**: Enforce Fantomas code formatting on all projects at commit time (PR #208, #209)
- **Tests**: Add formal Expecto tests for GenForm, GenOrder, and NKF libraries (PR #206)
- **Tests (Agents)**: Convert `Agents.Lib` tests to Expecto.Flip reversed-argument style (Issue #197)
- **Docs**: Additional F# documentation and implementation proposals (PR #201)
- **Scripts**: `GenCORECalculationsScaffolding.fsx` for W3 GenCORE test scaffolding — catalogues pure functions in Calculations and Measures modules and generates Expecto test scaffolding
- **Scripts**: `GenCOREPatientScaffolding.fsx` for W3 GenCORE patient scaffolding — identifies coverage gaps and provides concrete test scaffolding for Patient modules
- **Scripts**: `GenFORMTestScaffolding.fsx` for W3 GenFORM test scaffolding — catalogues pure functions and generates scaffolded Expecto tests for the GenFORM library
- **Scripts**: `GenOrderTestScaffolding.fsx` for W3 GenORDER test scaffolding — identifies coverage gaps and provides concrete test scaffolding for Patient, Medication, and OrderVariable modules
- **Server/GenForm**: Improve error handling when resources (Google Sheets / CSV) cannot be loaded
- **Scripts**: `NKFTestAnalysis.fsx` for W3 NKF test coverage — catalogues 14 pure testable functions and prints ready-to-use Expecto test cases
- **Tests (ZForm)**: Migrate 11 pure ZForm tests from `ZFormCITests.fsx` script into `tests/Informedica.ZForm.Tests/Tests.fs` formal test suite (W3)
- **Scripts (NKF)**: Add `NKFCITests.fsx` — 19 pure NKF tests ready for CI migration (W3)
- **Scripts (NKF)**: Add `NKFTestAnalysis.fsx` — W3 test coverage analysis for NKF library
- **Scripts (ZForm)**: Add `ZFormTestMigration.fsx` — analysis script for ZForm test migration
- **Scripts**: Add `TestMigrationStatus.fsx` — W3 test migration status across all libraries
- **Scripts (ZForm)**: Add `ZFormCITests.fsx` — 11 pure ZForm tests ready for CI migration (W3)

### Changed

- **Build**: Apply Fantomas formatting to all F# source files (formatting-only change, no logic changes)
- **Docs**: Update F# code formatting instructions to reflect Fantomas configuration

### Removed

- **Scripts**: Remove W3 analysis scripts (`GenCORECalculationsScaffolding.fsx`, `GenCOREPatientScaffolding.fsx`, `GenFORMTestScaffolding.fsx`, `GenOrderTestScaffolding.fsx`, `NKFCITests.fsx`, `NKFTestAnalysis.fsx`, `TestMigrationStatus.fsx`) — content has been migrated to formal CI test suites

### Fixed

- **Server (Agents)**: Convert `processOrderPlanCommand` and `processNutritionCommand` to use `let!` for async calls instead of `Async.RunSynchronously`, preventing thread-pool starvation
- **Dependencies**: Update Fable to latest version (PR #205)
- **Build**: Apply Fantomas formatting to client UI code (PR #209)
- **Build**: Apply Fantomas formatting to all F# source files (formatting-only change, no logic changes)
- **Docs**: Update F# code formatting instructions to reflect Fantomas configuration

### Removed

- **Scripts**: Remove W3 analysis scripts (`GenCORECalculationsScaffolding.fsx`, `GenCOREPatientScaffolding.fsx`, `GenFORMTestScaffolding.fsx`, `GenOrderTestScaffolding.fsx`, `NKFCITests.fsx`, `NKFTestAnalysis.fsx`, `TestMigrationStatus.fsx`) — content has been migrated to formal CI test suites

### Fixed

- **Server/GenForm**: Improve error handling when resources (Google Sheets / CSV) cannot be loaded
- **Server (Agents)**: Convert `processOrderPlanCommand` and `processNutritionCommand` to use `let!` for async calls instead of `Async.RunSynchronously`, preventing thread-pool starvation
- **Tests**: Fix errors in test scaffolding scripts (PR #203)
- **GenOrder**: Fix incompatible substance concentrations causing incorrect product filtering
- **GenOrder**: Add warning when filtering out products with incompatible units
- **Client (UI)**: Fix date formatting — zero-padded day/month display (e.g., `01 - 03 - 2026`)
- **Docs**: Fix 3 code-snippet bugs in `docs/mdr/design-history/agent-architecture.md`

---

## [0.1.1-alpha] - 2026-03-16

> ⚠️ **Alpha release** — Early development stage. Major features are incomplete. **Not for clinical use.**

### Added

- **Client (UI)**: Move resource-reload action to the Settings page for better UX organisation
- **Client (UI)**: Improved loading and calculation busy-state indicators
- **Scripts (GenSolver)**: Add `Benchmark.fsx` — baseline performance measurements for constraint solver (Roadmap W2)
- **Scripts (GenSolver)**: Add `Profile.fsx` — profiling script for W2 review
- **Scripts (GenSolver)**: Add `Memo.fsx` — prototype memoization layer for `Equation.solve` (Roadmap W2)
- **Scripts (GenSolver)**: Add `CoverageAnalysis.fsx` — W3 test-coverage analysis
- **Scripts (GenForm)**: Add `LocalProducts.fsx` — prototype for type-safe local product support (`ProductId = ZIndex | Local`)
- **Docs**: Expanded user guide with examples table and deployment URLs
- **Tests (ZIndex)**: Port ZIndex test script to the formal test suite

### Fixed

- **Client (UI)**: Fix proper loading mask when reloading resources
- **Client (UI)**: Remove duplicate is-loading logic; clear error banner when server returns online
- **Server**: Improve error handling and propagation when resources cannot be loaded
- **Server**: Fix errors in profile and benchmark scripts
- **ZIndex Tests**: Prevent data loss of pre-existing BST files in fixture teardown
- **Docs**: Fix Markdown lint warnings in user guide

---

## [0.1.0-alpha] - 2026-03-11

> ⚠️ **Alpha release** — Early development stage. Major features are incomplete. **Not for clinical use.**

### Added

- **Client**: Add total dose adjust and rate schedule time display
- **Client**: Add navigation to orderable dose quantity
- **Client**: Even distribution of totals in bottom view
- **Server**: Allow multiple nutrition scenarios
- **Server**: Implement multiple order context filter settings for nutrition
- **Client (TPN)**: First working version of rendering a nutrition (TPN) order in the UI
- **Client (TPN)**: Render min/max values when there is a navigation option for a variable
- **Client**: Counting button with support for repeated clicks or holding the button
- **Server**: Implement navigation and processing of TPN orders
- **Server**: Improved variable value rendering — now prints min/max cases alongside main value
- **GenOrder**: Pick nearest higher (else lower) component quantity when component orderable quantity is set
- **GitHub**: PR sub-template for documentation and non-code changes
- **GenForm**: New formulary product type defined (implementation pending)
- **GenForm**: Better support for different types of formulary products and additional substances
- **Server**: New command type for nutrition

### Changed

- **Client**: Set UI to 80% zoom level for better screen fit
- **Client**: Use theme to size UI for desktop and mobile
- **Client**: Rename "intake" to "totals" in nutrition view
- **Client**: Move zoom level to document index
- **Client**: Give bottom view a light background color
- **Docs**: Prototype TypesSplit.fsx for ValueUnit type reorganisation (GenUnits)
- **Client**: Centralize shared models — remove and consolidate duplicated code into shared models
- **Client**: Rename message cases and simplify duplicated UI logic for cleaner code
- **Client**: Shared patient business logic centralised between client and server
- **GenOrder**: Improved printing of component quantity
- **GenOrder**: Print dose adjust only when it has defined constraints; otherwise show dose per time (or dose adjust per time)
- **AGENTS.md / CLAUDE.md / CONTRIBUTING.md**: Stricter rules for AI/LLM use — script-only policy clarified and expanded
- **AGENTS.md**: Clarified module shadowing pattern documentation for FSI script-based development
- **GenForm**: Pretty print invalid dose rule data to console for improved debugging
- **Utils**: Improved web download with `Result` type for explicit error handling and propagation
- **Dependencies**: Bump `immutable` npm package to 5.1.5
- **Dependencies**: Update NuGet transitive dependencies; pin `Microsoft.Net.Test.Sdk` to 18.3.0
- **Docs**: Improve FSI MCP server usage instructions, including auto-load Claude file guidance

### Removed

- **Client**: Remove non-functioning clear buttons
- Outdated FSI script files updated to match latest source code signatures
- Unused `.fs` source files removed from repository

### Fixed

- **Client**: Fix missing autocomplete label rendering due to Fable string interpolation issue
- **GenOrder**: Fix silent swallowing of error in fetch equations
- **Client (TPN)**: Fix Substance key mismatch — phosphate and vitamin D data was never rendered
- **Client**: Add type annotation to prevent compiler warning
- Fix npm build warnings caused by conflicting glob package versions (Mocha compatibility)
- Update all FSI script files to latest source code signatures
- Proper error handling and propagation for Google Sheet data retrieval
- Remove all hard-coded Google Sheet URL ID references from source files
- **GenForm**: Prevent comparing incompatible value units in product filtering
- **GenForm**: Fix race condition using a non-concurrent collection in an async context

---

## About This Changelog

### For Contributors

When contributing, please ensure:

- All tests pass
- Documentation is updated
- CHANGELOG.md is updated (add to [Unreleased] section)
- Follow [conventional commit messages](.github/instructions/commit-message.instructions.md)
- Consider MDR impact for safety-related changes

### Versioning

This project follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html):

- **Major (x.0.0)**: Breaking changes, major features, architectural changes
- **Minor (2.x.0)**: New features, non-breaking enhancements
- **Patch (2.0.x)**: Bug fixes, security patches, minor improvements

### Release Types

- **Alpha**: Early development, major features incomplete, not for clinical use
- **Beta**: Feature-complete, undergoing validation, limited clinical testing
- **Release Candidate (RC)**: Validation complete, final testing before release
- **Stable**: Production-ready, clinically validated, regulatory compliance

### Design History File

This CHANGELOG.md is the user-facing release notes. For developer-focused design changes, see:

- [Design History Change Log](docs/mdr/design-history/change-log.md)

The design history file tracks internal design decisions and technical changes, while this CHANGELOG focuses on user-visible changes and release information.

