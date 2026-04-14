# Changelog

All notable changes to GenPRES will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- **Shared (Calculations)**: Implement `Calculations.fs` — port all clinical calculation modules to the Shared library (Fable-compatible); `BSA` module with Mosteller, Du Bois, Haycock, Gehan & George, and Fujimoto formulas; `Age` module with post-menstrual age, adjusted (corrected) age, and chronological age in days; renal module with CKD-EPI Creatinine 2021, CKD-EPI 2009, MDRD 4-variable, and Bedside Schwartz eGFR formulas plus KDIGO 2012 GFR classification; all public APIs use F# units of measure (`int<gram>`, `int<cm>`, `float<bsa>`, `float<mL/minute/normalM2>`) for compile-time type safety with zero runtime overhead in JavaScript (PR #301)
- **Docs (MDR)**: Add GenSOLVER stability analysis and risk management documentation — `docs/domain/gensolver-stability-analysis.md` maps the three problems identified by Đelić (2022) onto the current implementation (efficiency fixed, incorrect-increment arithmetic fixed, cycle stability partially mitigated via `MAX_CALC_COUNT` cap); `docs/mdr/risk-analysis/` adds structured hazard analysis, risk control table, and risk management plan/report; `docs/discrepancies-analysis.md` documents known solver limitations and open stability questions (PR #303)
- **Client (UI)**: Add login/logout icons to title bar — `Login` icon displayed when logged out, `Logout` icon displayed when authenticated; button left margin added for improved spacing (PR #292)
- **Client (UI)**: Add admin authentication and log analysis — login/logout flow with password-based token auth; Settings page gated behind authentication; list and analyze server log files; `GENPRES_RELOAD_PASSWORD` env var renamed to `GENPRES_PASSWORD`; `SideMenu` items support disabled state for future access control (PR #288)
- **Scripts (Shared)**: Add `RenalCalculations.fsx` — Fable-compatible eGFR prototype for the Shared library; CKD-EPI Creatinine 2021 (no race coefficient), CKD-EPI 2009, MDRD 4-variable, and Bedside Schwartz (paediatric) formulas; creatinine (mg/dL ↔ µmol/L) and urea/BUN (mg/dL ↔ mmol/L) unit conversions; KDIGO 2012 classification (`Normal` → `KidneyFailure`); 31 Expecto tests (PR #284)
- **Scripts (GenSOLVER)**: Add `LoopDetect.fsx` — state-fingerprint-based cycle detection and bound-width convergence monitoring; wraps the solver inner loop with `StateFingerprint` (FNV-1a hash), `CycleDetector`, `ConvergenceTracker`, and `DetectingLoop.solve` returning a typed `TerminationReason` (`HardLimit` / `CycleDetected` / `PotentialStall`); 9 Expecto tests included (PR #249)
- **Server (MCP)**: Add `Informedica.MCP.Server` — new executable project wiring GenFORM and GenORDER APIs into a Model Context Protocol (stdio) server; implements `McpTools.GenForm.fs` handler, registers tools via `McpServerBuilder`, and adds `ModelContextProtocol` + `Microsoft.Extensions.Hosting` NuGet dependencies (PR #250)
- **Client (UI)**: Add remember filter functionality — introduces `EmergencyListFilter` and `ContinuousMedsFilter` state fields with controlled `selectedFilter`/`onFilterChange` props on `ResponsiveTable`; filter state persists across re-renders in Emergency List and Continuous Medications views (PR #251)
- **Client (UI)**: Add templating fields for emergency list — facilitates switching to prescribe mode for an emergency medication; adds template fields (`TemplateGeneric`, `TemplateRoute`, `TemplateDoseType`, `TemplateIndication`) to shared models and wires through server API and client app (PR #243)
- **GenFORM**: Resolve `min adj to max` constraints for patient — `PrescriptionRule.fs` now handles the adjustment of minimum doses relative to maximum constraints; 131 new test cases added to `Informedica.GenFORM.Tests` (PR #243)
- **Docs**: Add ADR for template-based prescribing — new design decision document describing the emergency medication prescribing template approach (PR #245)
- **Build/CI**: Add comprehensive Copilot instructions and prompt files — `.github/copilot-instructions.md` updated with full project guidance; four reusable prompt files added for `add-dose-rule`, `fix-failing-test`, `new-fsx-script`, and `review-pr` workflows (PR #247)
- **Client (UI)**: Localise hardcoded UI strings — replaces hardcoded Dutch strings across client views with localised terms; adds new `Terms` DU cases for shared, patient, nutrition, and interaction labels; language selector now shows short language code instead of flag emoji (PR #239)
- **Scripts (GenSOLVER)**: Add `LRUSolverIntegration.fsx` — W2 final step; session-level `SessionSolver` that integrates the LRU cache into the constraint solver with canonical name-remapping, 6 correctness tests, and a 50-patient capacity benchmark (PR #238)
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
- **Docs**: Add ADR-0015 security baseline — new design-history document defining the server-side and client-side security threat model, remediation status for the demo deployment, and deferred items; updated with XFF bypass and Content-Security-Policy details (PR #298)

### Changed

- **Build (deps)**: Bump `FsToolkit.ErrorHandling` from 5.1.0 to 5.2.0 (PR #306)
- **Codebase**: F# 8 syntax modernisation and cleanup — adopt shorthand lambdas (`_.Property`) and modern indexer syntax (`items[0]`); remove unused `open` statements and NuGet package references across multiple modules; refactor `createComponents` in `Medication.fs` to drop unused `solutionRule` parameter; update string-interpolation formatting to use explicit format specifiers throughout (PR #305)
- **Docs**: Update security documentation — security review document revised, X-Powered-By disclosure deferred, unused NuGet package removed (PR #299)
- **Client (UI)**: Migrate MUI from v7 to v9 — replace deprecated props (`color`, `display`, `alignItems`, `PaperProps`) with `sx` and `slotProps` APIs across all client components; chips rendered inline with `Chip` component; centralise style definitions in `Totals` and `Typography` modules (PR #289)
- **Client (UI)**: Extract JSX inline anonymous records — refactor Fable JSX `sx` props from inline anonymous records to named `let` bindings per F# coding guidelines; fix Markdown linting errors in docs (PR #290)
- **Client (UI)**: Extract inlined JSX event handler lambdas — `onChange` and `onClick` handlers in `Patient.fs`, `Prescribe.fs`, and `Settings.fs` extracted to named `let` bindings per F# coding guidelines; guidelines updated to explicitly require extraction of non-trivial event handlers from JSX (PR #293)
- **Docs**: Update shell script documentation — headings revised and sections expanded to cover helper scripts, pre-commit formatting, Docker usage, and local development tool invocations (PRs #295, #296)
- **GenSOLVER**: Prevent value explosion — cap cartesian-product size at `MAX_CALC_COUNT` (500) in `ValueRange.calc`; solver falls back to min/max bounds instead of full product when threshold exceeded, eliminating `ValueSetOverflow` errors; document all solver/pipeline safeguards in ADR-0014 (PR #285)
- **GenORDER / GenSOLVER**: Staged value expansion for timed orders — two-phase expansion via `skipRate` parameter: dose-quantity variables expand first, rate variables expand only after dose quantity is pinned, preventing combinatorial explosion in `OnceTimed`/`Timed` orders (PRs #283, #285, #286)
- **GenFORM / GenORDER (Order)**: Improve component dose display — all `wrap` calls now include both base dose fields and their adjustment counterparts (`QuantityAdjust`, `RateAdjust`, `PerTimeAdjust`) in alert/caution outputs; `DoseRule.useAdjust` now checks substance, component, and form levels; single-component medications use the component dose as the orderable dose when no orderable-level dose is set (PR #253)
- **Docs**: Restructure `docs/mdr/design-history/` as numbered ADRs with consistent naming — all design-history documents renamed with `0001`–`0013` prefixes for discoverability and traceability (PR #245)
- **Docs (MCP)**: Update MCP server architecture document — fix file structure listing and pin `ModelContextProtocol` to version `1.2.0` (PR #241)
- **Client**: Refactor environment variable access to use `AppEnv` module — adds `asEnv` Fable-compatible helper (`unbox<'T>`), eliminating scattered inline environment reads; fixes `appendScenarioToTreatmentPlan` naming to remove shadowing of outer `updateTreatmentPlan`; prevents concurrent duplicate `UpdateOrderPlan` requests by checking `InProgress`/`Recalculating` state (PR #223)
- **Docs**: Update `0007-clean-safe-architecture.md` to reflect implemented safe-and-clean architecture state and add code-verified implementation notes (PR #227)

### Fixed

- **Client (UI)**: Fix token-based auth replacing plaintext password storage — logout now properly clears token and authentication state; concurrent admin requests guarded by `InProgress` state (PR #288)
- **GenFORM**: Fix `OnceTimed` dose rule validation — updated to accept `MaxRate` or `MaxRateAdj` as valid conditions alongside `MaxTime`/`TimeUnit`; missing-field check now requires at least one of these three options (PR #255)
- **GenORDER**: Fix empty `ValueUnit` collection — `toValueUnit` returns `None` when the result is empty, preventing creation of invalid value units (PR #255)
- **GenFORM / Build**: Fix build failure caused by incompatible message error types (PR #243)
- **Client (UI)**: Remove hardcoded year ("2023") from application title bar (PR #238)
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

### Security

- **Server**: Fix XFF bypass on rate limiter — `X-Forwarded-For` header spoofing closed; server now correctly identifies client IP through all proxy hops when determining rate-limit buckets (PR #298)
- **Server / Client (UI)**: Fix security headers — Content-Security-Policy, HSTS, `X-Content-Type-Options`, and `Referrer-Policy` headers tightened; demo deployment gaps L1/L2/B2/A2/D2 closed (PR #298)
- **Server (Json)**: Disable Newtonsoft.Json `TypeNameHandling.Auto` — flip the default in `Informedica.Utils.Lib/Json.fs` to `TypeNameHandling.None` to eliminate the latent gadget-chain RCE foot-gun if any future caller passes attacker-controlled JSON to `deSerialize`. The setting is currently unreachable from the network (Fable.Remoting uses its own binary serialization) but the dangerous default is invisible to grep and would silently weaponise the next contributor who calls `Json.deSerialize` on untrusted input. Three Expecto regression tests added under a new `JsonSecurity` sub-module guard the default — they fail loudly if the setting is ever reverted. Verified by the full server test suite (5408 passed). Resolves §7.1 C1 of the [2026-04-10 security review](docs/security/2026-04-10-security-review.md)
- **Server (auth)**: Constant-time password comparison on `ReloadResources` — replace plain `<>` string equality in `ServerApi.Services.fs` with `CryptographicOperations.FixedTimeEquals` on UTF-8 bytes. The fail-closed default (no `GENPRES_PASSWORD` env var = always reject) is preserved. The deeper structural fix — migrating `ReloadResources` onto the HMAC token system used by `LogAnalyzerCmd` so the raw password no longer travels on the wire — is tracked by an inline `TODO(D4 follow-up)` comment. Resolves §7.1 D4
- **Server (startup)**: Production password policy enforcement — new `validateProductionPassword` in `Server.fs` runs before any HTTP listener is bound and refuses to start when `GENPRES_PROD=1` and `GENPRES_PASSWORD` is missing, empty, whitespace-only, or shorter than 16 characters. Demo mode (`GENPRES_PROD≠1`) is unaffected. The policy is documented in `.env.example` and in a new "Password policy" subsection of `DEVELOPMENT.md`. Resolves §7.1 E2
- **Build (Docker)**: Stop baking `GENPRES_URL_ID` into the published image — remove `ARG GENPRES_URL_ARG` / `ENV GENPRES_URL_ID=$GENPRES_URL_ARG` from `Dockerfile` so the proprietary Google Sheet ID is no longer visible to anyone with `docker inspect` access. Replaced with empty `ENV GENPRES_URL_ID=` / `ENV GENPRES_PASSWORD=` defaults so the variables remain discoverable by container management UIs (Plesk, Portainer, Rancher, Kubernetes manifests) while operators inject the real values at container runtime via `docker run -e`, Docker secret, or Kubernetes secret. All five docs that referenced the old `--build-arg GENPRES_URL_ARG` pattern updated (`README.md`, `AGENTS.md`, `.github/copilot-instructions.md`, `docs/mdr/design-history/0001-system-architecture.md`, `DEVELOPMENT.md`). Sheet ID rotation completed 2026-04-10 (operator-confirmed). Resolves §7.1 E3
- **Server (logging)**: Redact `GENPRES_URL_ID` in startup banner — previously printed the full Sheet ID, leaking the proprietary value into anywhere logs were shipped, screenshotted, or pasted into a bug report. The banner now masks the ID to its last-5 characters prefixed with `***` (e.g. `***j8SS8`) — enough fingerprint for an operator to confirm the right ID is loaded, without exposing the secret. Existing `***`/`NOT SET` masking for `GENPRES_PASSWORD` preserved
- **Server (config)**: Treat empty / whitespace-only env vars as unset — the new empty `ENV` defaults in `Dockerfile` would otherwise come back from `Env.getItem` as `Some ""` (because `Env.getItem` only treats `null` as `None`), bypassing the existing `failwith "No GENPRES_URL_ID"` and the `validateProductionPassword` `None` branch. Four call sites in `Server.fs` (`provider`, `validateProductionPassword`, banner `password`, banner `urlId`) now pipe through `Option.filter (System.String.IsNullOrWhiteSpace >> not)` so an empty value is reported as not-set rather than misinterpreted as a real value
- **Docs (security)**: Add comprehensive security review at `docs/security/2026-04-10-security-review.md` — long-form (~6500-word) three-deployment-context analysis (dev / on-prem / SaaS) covering authentication, server hardening, deserialisation, client, secrets/supply chain, audit/MDR, and external integrations. 32 findings with file:line evidence, code excerpts, MDR / 21 CFR Part 11 mapping, three-bucket remediation roadmap, and a post-implementation status section tracking the §7.1 "Do now" items resolved in this changelog entry

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
- **Docs**: Fix 3 code-snippet bugs in `docs/mdr/design-history/0008-agent-architecture.md`

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

- [Design History Change Log](docs/mdr/design-history/0000-change-log.md)

The design history file tracks internal design decisions and technical changes, while this CHANGELOG focuses on user-visible changes and release information.

