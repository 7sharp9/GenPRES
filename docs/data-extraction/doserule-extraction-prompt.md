# DoseRules Extraction Prompt

## 1. Purpose

Extract **Dose Rules** from free text (formularies, guidelines, protocols) into tab-delimited rows matching the `doserules.tsv` schema consumed by `Informedica.GenFORM.Lib`.

A Dose Rule is one of the four Operational Knowledge Rule (OKR) types defined in GenFORM. Each extracted row represents **one Substance-level dose rule** inside one clinical scenario uniquely identified by:

`Source + Generic + Route + Indication + Patient Category + DoseType (+ Component + Substance)`

## 2. Domain reference

This extraction targets the Dose Rule model defined in the GenPRES domain docs. Keep these sections open while extracting:

- `docs/domain/genform-free-text-to-operational-rules.md`
  - §3 Sources and Types of Dose Rules
  - §5 Operational Structure of a Dose Rule
  - §6.1 Selection Constraints
  - §6.2 Calculation Constraints
  - Appendix C.2 Dose Rule Model Table (authoritative column semantics)
- `docs/domain/core-domain.md`
  - Operational Knowledge Rules, Rule Hierarchy

Authoritative sample rows: `data/sources/Rules/doserules.tsv`.

Every column below is either a **Selection Constraint** (decides which rule applies) or a **Calculation Constraint** (feeds GenSOLVER). Extracting them into the correct slot is what makes the rule operational.

- **Selection Constraints**: `Source`, `Generic`, `Form`, `Brand`, `GPKs`, `Indication`, `Route`, `Dep` (Setting), Patient Category (`Gender`, `MinAge`/`MaxAge`, `MinWeight`/`MaxWeight`, `MinBSA`/`MaxBSA`, `MinGestAge`/`MaxGestAge`, `MinPMAge`/`MaxPMAge`), `DoseType`, `Component`, `Substance`.
- **Calculation Constraints**:
  - Schedule: `Freqs`, `FreqUnit`, `MinTime`/`MaxTime`, `TimeUnit`, `MinInt`/`MaxInt`, `IntUnit`, `MinDur`/`MaxDur`, `DurUnit`.
  - Dose Limits: `DoseUnit`, `AdjustUnit`, `RateUnit`, `MinQty`/`MaxQty`, `MinQtyAdj`/`MaxQtyAdj`, `MinPerTime`/`MaxPerTime`, `MinPerTimeAdj`/`MaxPerTimeAdj`, `MinRate`/`MaxRate`, `MinRateAdj`/`MaxRateAdj`.

## 3. Output contract

- Output is **tab-delimited rows only**. No markdown, no code fences, no prose, no explanations. **Do not emit a header row** unless the user explicitly asks for one.
- Columns appear in this **exact order** — identical to line 1 of `data/sources/Rules/doserules.tsv`:

  ```text
  SortNo	Source	Generic	Form	Brand	Route	GPKs	Indication	ScheduleText	Dep	Gender	MinAge	MaxAge	MinWeight	MaxWeight	MinBSA	MaxBSA	MinGestAge	MaxGestAge	MinPMAge	MaxPMAge	DoseType	DoseText	Component	Substance	Freqs	DoseUnit	AdjustUnit	FreqUnit	RateUnit	MinTime	MaxTime	TimeUnit	MinInt	MaxInt	IntUnit	MinDur	MaxDur	DurUnit	MinQty	MaxQty	MinQtyAdj	MaxQtyAdj	MinPerTime	MaxPerTime	MinPerTimeAdj	MaxPerTimeAdj	MinRate	MaxRate	MinRateAdj	MaxRateAdj
  ```

  51 columns total; every row must have exactly 50 tab separators.

- Missing values: **empty field** (two consecutive tabs). Never `null`, `N/A`, `-`, or `0` as a placeholder.
- Decimal separator: `.` (convert any source `,` to `.`, e.g. `7,5` → `7.5`).
- Lists (`GPKs`, `Freqs`): semicolon-separated, no spaces (e.g. `3;4`).
- Preserve `Indication`, `ScheduleText`, `DoseText` **verbatim** in the source language — do not translate, summarize, paraphrase, or strip punctuation. Replace any literal tab character in source text with a single space so TSV parsing survives; collapse internal newlines into a single space.

## 4. Column-by-column extraction rules

### Identification

- **SortNo** — integer reflecting the order of the rule in the source schedule text. Sibling rows derived from the same paragraph (combination product Substances, multi-phase dosing) may share SortNo or be numbered consecutively depending on how the source enumerates them.
- **Source** — one of `NKF` (Kinderformularium), `FK` (Farmacotherapeutisch Kompas), `SWAB`, an oncology protocol identifier, or a local protocol identifier. **Required.**
- **Generic** — generic medication name, lower-case unless the source uses proper capitalisation. **Required.**
- **Form** — pharmaceutical form (e.g. `tablet`, `suspensie`, `injectievloeistof`, `zetpil`). Empty if the rule is form-agnostic. *Note: previously called `Shape` — that name is obsolete.*
- **Brand** — brand name if the rule is brand-specific. Empty otherwise.
- **GPKs** — semicolon-separated Generic Product Codes. Empty if unknown or not applicable.
- **Route** — administration route as used in the source (`ORAAL`, `INTRAVENEUS`, `RECTAAL`, `SUBCUTAAN`, …). **Required.**
- **Indication** — clinical indication label, **verbatim** from source. **Required.**
- **ScheduleText** — the full original dose-schedule paragraph, **verbatim**. **Required.** This is the traceability field.

### Setting

- **Dep** — department / ward (e.g. `ICK`, `NICU`, `kinderafdeling`). Empty for general rules.

### Patient Category

All range bounds are integers unless noted. Leave empty if the source does not restrict that dimension.

- **Gender** — `male` / `female` / empty.
- **MinAge** / **MaxAge** — in **days**. Conversions:
  - weeks → ×7 (`1 week` → `7`)
  - months → ×30 (`1 maand` → `30`, `6 maanden` → `182`)
  - years → ×365 (`18 jaar` → `6574`, `1 jaar` → `365`)
  - Ranges are inclusive-lower, exclusive-upper following the existing TSV convention (`1 maand tot 18 jaar` → `MinAge=30`, `MaxAge=6574`).
- **MinWeight** / **MaxWeight** — in **grams** (`2000 gr` → `2000`; `5 kg` → `5000`).
- **MinBSA** / **MaxBSA** — in **m²**, float.
- **MinGestAge** / **MaxGestAge** — gestational age in **days** (`34 weken` → `238`, `41 weken` → `287`).
- **MinPMAge** / **MaxPMAge** — post-menstrual age in **days** (`32 weken` → `224`, `44 weken` → `308`).

### DoseType

**DoseType** — exactly one of:

| Value | Meaning | Typical cue in source |
|-------|---------|------------------------|
| `once` | single administration, not timed | "éénmalig", "startdosering" |
| `onceTimed` | single administration given over a specified time | "oplaaddosis over 30 min" |
| `discontinuous` | repeated bolus / multiple daily doses | "… mg/kg/dag in N doses" |
| `timed` | repeated administrations each given over a specified time | "3 dd in 30 min" |
| `continuous` | continuous infusion (rate-based) | "… mg/kg/uur", "continu infuus" |

**DoseText** — label for the dosing phase (`startdosering`, `onderhoudsdosering`, `dag 3`, …). Empty when the scenario has only one phase.

### Component and Substance

- **Component** — name of the component (often equals `Generic`; for combination products such as `amoxicilline/clavulaanzuur` the whole combination is the Component).
- **Substance** — the active substance for this row. **Required.**

For combination products emit **one row per active Substance**, all sharing the same Component. Each Substance row carries its own dose limits.

### Schedule

- **Freqs** — integer frequencies per `FreqUnit` as a semicolon list (`in 3 doses` → `3`; `in 3 - 4 doses` → `3;4`). Empty for `once`, `onceTimed`, and `continuous`.
- **DoseUnit** — base dose unit (`mg`, `g`, `IE`, `microg`, `mL`, …). **Required.**
- **AdjustUnit** — `kg` or `m2` when the dose is patient-adjusted; empty otherwise.
- **FreqUnit** — unit of the frequency (`dag`, `uur`, `week`).
- **RateUnit** — unit for continuous rate (`uur`, `min`); empty unless DoseType = `continuous` (or a rate is otherwise given).
- **MinTime** / **MaxTime**, **TimeUnit** — infusion/administration time (e.g. "in 15 min" → `MinTime=15`, `MaxTime=15`, `TimeUnit=min`).
- **MinInt** / **MaxInt**, **IntUnit** — minimum/maximum interval between two doses.
- **MinDur** / **MaxDur**, **DurUnit** — total treatment duration.

### Dose Limits

All limit unit interpretations are fixed — keep them consistent within a row:

| Column | Unit interpretation |
|--------|---------------------|
| `MinQty` / `MaxQty` | `DoseUnit` per dose |
| `MinQtyAdj` / `MaxQtyAdj` | `DoseUnit` / `AdjustUnit` per dose |
| `MinPerTime` / `MaxPerTime` | `DoseUnit` per `FreqUnit` |
| `MinPerTimeAdj` / `MaxPerTimeAdj` | `DoseUnit` / `AdjustUnit` per `FreqUnit` |
| `MinRate` / `MaxRate` | `DoseUnit` per `RateUnit` |
| `MinRateAdj` / `MaxRateAdj` | `DoseUnit` / `AdjustUnit` per `RateUnit` |

Rules:

- **Do not invent bounds.** If the source states a single value (e.g. `50 mg/kg/dag`), fill only one side of the pair and leave the partner empty — mirror the pattern in `doserules.tsv` (`MinPerTimeAdj=50`, `MaxPerTimeAdj=` empty).
- A single value that the source labels as a maximum (`max 4 g/dag`) goes into the `Max*` column; a single value labelled as a minimum goes into `Min*`.
- A range (`10 - 15 mg/kg/dosis`) fills both `Min*` and `Max*`.
- A plain single value that is not labelled as min or max (a recommended value) by convention fills only `Min*` and leaves `Max*` empty.

## 5. Row-splitting rules

Emit multiple rows when a single source paragraph encodes multiple rules. Concrete splits (all visible in `doserules.tsv`):

- **Multi-phase dosing (start + maintenance)** — one row per phase. They usually differ by `DoseType` and `DoseText`. Example: paracetamol IV premature → one `once` row (`DoseText=startdosering`) plus one `discontinuous` row (`DoseText=` empty).
- **Combination products** — one row per active Substance sharing the same Component. Example: `amoxicilline/clavulaanzuur` → one row for `amoxicilline`, one row for `clavulaanzuur`.
- **Distinct Patient Categories in one paragraph** — one row per category. Example: `< 1 week + <2000 g`, `< 1 week + ≥2000 g`, `1–4 weken + <2000 g`, `1–4 weken + ≥2000 g` → four base rows (× 2 Substances = eight rows for a combination product).
- **Distinct DoseTypes** — always separate rows.

**Preserve the same `ScheduleText` across all sibling rows** derived from one paragraph so every row can be traced back to its source.

**No duplicate rows.** Two rows are duplicates when they share the same (`Source`, `Generic`, `Route`, `Indication`, Patient Category, `DoseType`, `DoseText`, `Component`, `Substance`) tuple. Each row must differ from every other by at least one of those fields. If you are tempted to repeat a row, stop — the correct output is already complete.

**Multi-phase dosing cues.** Dutch sources regularly stack a loading/starting dose and a maintenance dose in a single paragraph. Treat each phase as its own row and carry the phase label into `DoseText`:

| Cue pair | Phase split |
|----------|-------------|
| `startdosering` / `onderhoudsdosering` | two rows, both typically `discontinuous`, distinguished by `DoseText` and dose limits |
| `oplaaddosis` / `onderhoud` or `continu` | often `onceTimed` (or `once`) for the load, `continuous` / `timed` for the maintenance |
| `dag 1` / `dag 2-7` (or similar) | one row per day-range, `DoseText` holds the day label |
| `initieel` / `vervolgens` | initial + follow-up rows |

Missing the load- or maintenance-phase row is a bug — always emit both when the source states both.

## 6. Worked mini-example

Source paragraph (NKF, amoxicilline/clavulaanzuur, IV, Ernstige bacteriele infecties):

> `< 1 week en geboortegewicht < 2000 gr Amoxicilline/clavulaanzuur 10:1 : 50 / 5 mg/kg/dag in 2 doses`

Produces **two rows** (one per Substance). Same Patient Category (`MaxAge=7`, `MaxWeight=2000`), same `DoseType=discontinuous`, same `Freqs=2`, same `ScheduleText`. The per-substance value (`50` vs `5`) lands in `MinPerTimeAdj`; `MaxPerTimeAdj` stays empty.

```text
211	NKF	amoxicilline/clavulaanzuur			INTRAVENEUS		Ernstige bacteriele infecties	< 1 week en geboortegewicht < 2000 gr Amoxicilline/clavulaanzuur 10:1 : 50 / 5 mg/kg/dag in 2 doses					7		2000								discontinuous		amoxicilline/clavulaanzuur	amoxicilline	2	mg	kg	dag																		50					
212	NKF	amoxicilline/clavulaanzuur			INTRAVENEUS		Ernstige bacteriele infecties	< 1 week en geboortegewicht < 2000 gr Amoxicilline/clavulaanzuur 10:1 : 50 / 5 mg/kg/dag in 2 doses					7		2000								discontinuous		amoxicilline/clavulaanzuur	clavulaanzuur	2	mg	kg	dag																		5					
```

For additional examples across all DoseTypes (`once`, `onceTimed`, `discontinuous`, `timed`, `continuous`) consult `data/sources/Rules/doserules.tsv` directly — that file is the authoritative sample.

## 7. Instructions

When asked to extract dose rules from supplied source text:

1. Emit only tab-delimited rows in the exact column order of §3. No header unless explicitly requested. No prose, no code fences, no commentary.
2. Preserve `Indication`, `ScheduleText`, and `DoseText` **verbatim** in the source language; collapse internal tabs/newlines to single spaces only.
3. Apply the unit conversions in §4 (days / grams / m²; decimal `.`; semicolon lists).
4. Apply the row-splitting rules in §5 — one row per (Patient Category × DoseType × Substance).
5. Leave unknown fields empty; never substitute `0`, `-`, `N/A`, or `null`.
6. Do not invent dose bounds; follow the Min/Max conventions in §4 for single values, labelled max/min, and explicit ranges.
