# Context Map

GenPRES is a large multi-context project. Contexts are registered here as
their language is grilled and a `CONTEXT.md` is written — this map is **not**
an exhaustive taxonomy of the system.

## Contexts

- [Data Extraction](./docs/data-extraction/CONTEXT.md) — turns free-text
  formulary source (FTK, NKF, …) into the canonical `DoseRules` TSV.

> Other contexts (e.g. the GenFORM / GenORDER / GenSOLVER clinical domain) are
> not yet carved into their own `CONTEXT.md`. The canonical glossary for
> clinical-domain language remains the Core Definitions tables in
> [`docs/domain/`](./docs/domain/) until a context is explicitly grilled and
> registered here.

## Relationships

- **Data Extraction → Core Domain**: Data Extraction emits the canonical
  `data/sources/Rules/doserules.tsv`; the clinical domain (GenFORM) ingests it
  as Dose Rule OKRs. The TSV is the boundary — Data Extraction never reaches
  into GenFORM `.fs` types.
