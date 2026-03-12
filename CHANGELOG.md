# Changelog

All notable changes to GenPRES will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

---

## [0.1.0-alpha] - 2026-03-11

> ⚠️ **Alpha release** — Early development stage. Major features are incomplete. **Not for clinical use.**

### Added

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

- Outdated FSI script files updated to match latest source code signatures
- Unused `.fs` source files removed from repository

### Fixed

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

