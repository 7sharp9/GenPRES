# ADR-0021: IsAdult Patient Category Facet

**Date**: 2026-05-18
**Status**: Accepted

## Context

The FTK dose-rule extraction pipeline (see
[`docs/data-extraction/doserule-extraction-flowchart.md`](../../data-extraction/doserule-extraction-flowchart.md))
must capture source rules phrased categorically as "adults" (Dutch
`volwassenen`, and `ouderen`/`bejaarden` for elderly) where the source gives
**no numeric age range**. The `core-domain.md` Patient Category model is
defined strictly in terms of numeric ranges (age, weight, BSA, gestational
age, post-menstrual age, gender), so there is no existing way to represent
"applies to adults, age asserted categorically."

## Decision

Introduce `IsAdult` as a **standalone, positive-only boolean facet** of
Patient Category:

- `"x"` means the rule applies to adults, asserted categorically; absence
  asserts **nothing** (it is never a negative — never "not an adult").
- When `IsAdult = "x"`, the rule's numeric `MinAge`/`MaxAge` are intentionally
  emptied: the categorical adult assertion *replaces* the numeric age range.
- Elderly (`ouderen`/`bejaarden`) is deliberately folded into Adult — dosing
  rarely differs and a separate elderly band is not warranted.
- It is fully system-resolved by the extraction pipeline (preliminary keyword
  at Pass 1, confirmed at Pass 3); it is not a human-edited field and not the
  first value of a general age-band axis.

## Considered Options

- **Numeric adult floor (`MinAge ≈ 6570 days`)** — rejected: loses the
  distinction between "rule explicitly bounded at 18 yr" and "rule applies to
  the adult category"; pollutes the numeric range with a synthetic value.
- **Categorical AgeCategory axis (Adult / Neonate / Child / Elderly)** —
  rejected: over-generalises; only Adult needs the categorical escape hatch,
  the other bands remain well-served by numeric ranges.

## Consequences

- **Blocking precondition (safety).** No `IsAdult = "x"` row may reach GenFORM
  ingest until (a) Patient Category in GenFORM `.fs` carries the facet **and**
  (b) patient-matching enforces "adult patients only" for such rules. Until
  both exist, an `IsAdult = "x"` row has empty age bounds and an unconsumed
  flag, so it would match every age — directly violating the
  `core-domain.md` safety-by-construction / completeness guarantees.
- **Hard to reverse.** `IsAdult` is hashed into the Pass-4 `GrpId`/`Id`, and
  the clearing guard irreversibly erases `MinAge`/`MaxAge` in extracted TSVs.
  Reversing the model means re-running extraction on affected generics.
- `core-domain.md` and the GenFORM Patient Category type must be updated to
  record this single, deliberate exception to the ranges-only model when the
  facet is implemented in domain code.
