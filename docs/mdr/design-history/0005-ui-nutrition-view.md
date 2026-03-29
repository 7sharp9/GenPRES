# ADR-0005: Nutrition View Layout

**Date**: 2024-01-01
**Status**: Proposed

## Context

The nutrition (TPN/enteral) order view has a different layout from the standard prescribe view because it involves multiple components with individual quantities, a shared orderable quantity, and administration rate/time controls displayed side by side.

## Decision

Adopt a two-column table layout separating "Bereiding" (preparation) on the left from "Dosering" (dosing) on the right, with a shared "Toediening" (administration) row spanning both columns below.

## Consequences

- Clinicians can see preparation and dosing quantities for each component at a glance.
- The layout mirrors clinical workflow: prepare first, then set the dose.
- Implementation must map `Comp Orderable Quantity` and `Component Dose Quantity Adjust` fields to the correct order model variables.

---

# Nutrition View Design

## Layout

| Bereiding                    | Dosering                       |
|------------------------------|--------------------------------|
| Comp Orderable Quantity      | Component Dose Qauntity Adjust |
| Comp Orderable Quantity      | Component Dose Qauntity Adjust |
| Comp Orderable Quantity      | Component Dose Qauntity Adjust |
| Comp Orderable Quantity      | Component Dose Qauntity Adjust |
| Orderable Orderable Quantity |                                |
|                              |                                |
| Toediening                   |                                |
| Orderable Dose Quantity      | Orderable Dose Quantity Adjust |
| Orderable Dose Rate          | Schedule Time                  |
|                              |                                |
| Totals                       |                                |

