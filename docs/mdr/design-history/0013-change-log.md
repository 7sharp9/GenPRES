# ADR-0013: Design History Change Log

**Date**: 2024-01-01
**Status**: Accepted

## Context

MDR (Medical Device Regulation) requires a Design History File (DHF) that records significant design and development decisions over the lifetime of the product. A running change log within the design-history folder supplements the individual ADRs by providing a chronological index of changes.

## Decision

Maintain this document as a reverse-chronological log of significant design changes, linking to the relevant ADR or CHANGELOG entry for details.

## Consequences

- Auditors and reviewers can quickly navigate the design history.
- Each entry should reference the ADR number and a brief description of the change.

---

## Log

| Date | ADR | Summary |
|------|-----|---------|
| 2026-03-28 | [ADR-0009](0009-mcp-server-architecture.md) | MCP server architecture proposed |
| 2026-03-25 | [ADR-0007](0007-clean-safe-architecture.md) | Clean SAFE architecture accepted (all 4 phases complete) |
| 2025-12-21 | [ADR-0012](0012-resource-verification.md) | Resource requirements verified against GenFORM implementation |
| 2026-03-01 | [ADR-0008](0008-agent-architecture.md) | Agent architecture proposed |
| 2024-01-01 | [ADR-0011](0011-universal-layout-overflow.md) | Universal layout overflow design accepted |
| 2024-01-01 | [ADR-0010](0010-analysis-solve-order-triggers.md) | Analysis of SolveOrder trigger paths |
| 2024-01-01 | [ADR-0006](0006-ui-order-view.md) | Quantitative order constraint navigation proposed |
| 2024-01-01 | [ADR-0005](0005-ui-nutrition-view.md) | Nutrition view layout proposed |
| 2024-01-01 | [ADR-0004](0004-ui-wireframes.md) | UI wireframes accepted |
| 2024-01-01 | [ADR-0003](0003-resource-requirements.md) | Resource requirements specification accepted |
| 2024-01-01 | [ADR-0001](0001-system-architecture.md) | System architecture accepted |
| 2021-12-02 | [ADR-0002](0002-state-of-affairs.md) | State of affairs documented |
