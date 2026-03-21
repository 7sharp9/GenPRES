# Changelog

All notable changes to GenPRES will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- **Tests (ZForm)**: Migrate 11 pure ZForm tests from `ZFormCITests.fsx` script into `tests/Informedica.ZForm.Tests/Tests.fs` formal test suite (W3)
- **Scripts (NKF)**: Add `NKFCITests.fsx` — 19 pure NKF tests ready for CI migration (W3)
- **Scripts (NKF)**: Add `NKFTestAnalysis.fsx` — W3 test coverage analysis for NKF library
- **Scripts (ZForm)**: Add `ZFormTestMigration.fsx` — analysis script for ZForm test migration
- **Scripts**: Add `TestMigrationStatus.fsx` — W3 test migration status across all libraries
- **Scripts (ZForm)**: Add `ZFormCITests.fsx` — 11 pure ZForm tests ready for CI migration (W3)

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

