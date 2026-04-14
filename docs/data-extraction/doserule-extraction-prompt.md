# DoseRules Extraction Prompt

## 1. Purpose

Extract Dose Rules from free text (formularies, guidelines, protocols) into tab-delimited rows matching `data/sources/Rules/doserules.tsv` (consumed by `Informedica.GenFORM.Lib`).

One row = one Substance-level rule, keyed by: `Source + Generic + Route + Indication + PatientCategory + DoseType (+ Component + Substance)`.

## 2. Domain reference

Authoritative sources (keep open while extracting):

- `docs/domain/genform-free-text-to-operational-rules.md` §3, §5, §6.1, §6.2, Appendix C.2
- `docs/domain/core-domain.md` (OKRs, Rule Hierarchy)
- Sample rows: `data/sources/Rules/doserules.tsv`

Every column is a Selection Constraint (which rule applies) or Calculation Constraint (feeds GenSOLVER).

- Selection: `Source`, `Generic`, `Form`, `Brand`, `GPKs`, `Indication`, `Route`, `Dep`, PatientCategory (`Gender`, `Min/MaxAge`, `Min/MaxWeight`, `Min/MaxBSA`, `Min/MaxGestAge`, `Min/MaxPMAge`), `DoseType`, `Component`, `Substance`.
- Calculation: Schedule (`Freqs`, `FreqUnit`, `Min/MaxTime`, `TimeUnit`, `Min/MaxInt`, `IntUnit`, `Min/MaxDur`, `DurUnit`); DoseLimits (`DoseUnit`, `AdjustUnit`, `RateUnit`, `Min/MaxQty`, `Min/MaxQtyAdj`, `Min/MaxPerTime`, `Min/MaxPerTimeAdj`, `Min/MaxRate`, `Min/MaxRateAdj`).

## 3. Output contract

- Tab-delimited rows only. No markdown, fences, prose, or header (unless explicitly requested).
- Column order (51 columns, 50 tabs/row) — identical to line 1 of `doserules.tsv`:

  ```text
  SortNo	Source	Generic	Form	Brand	Route	GPKs	Indication	ScheduleText	Dep	Gender	MinAge	MaxAge	MinWeight	MaxWeight	MinBSA	MaxBSA	MinGestAge	MaxGestAge	MinPMAge	MaxPMAge	DoseType	DoseText	Component	Substance	Freqs	DoseUnit	AdjustUnit	FreqUnit	RateUnit	MinTime	MaxTime	TimeUnit	MinInt	MaxInt	IntUnit	MinDur	MaxDur	DurUnit	MinQty	MaxQty	MinQtyAdj	MaxQtyAdj	MinPerTime	MaxPerTime	MinPerTimeAdj	MaxPerTimeAdj	MinRate	MaxRate	MinRateAdj	MaxRateAdj
  ```

- Missing → empty field (two tabs). Never `null`/`N/A`/`-`/`0`.
- Decimal `.` (convert `7,5` → `7.5`).
- Lists (`GPKs`, `Freqs`): semicolon, no spaces (`3;4`).
- `Indication`, `ScheduleText`, `DoseText` — verbatim, source language. Replace literal tab with space; collapse newlines to single space.

## 4. Column rules

### Identification

- **SortNo** — integer, source order. Siblings from one paragraph may share or be consecutive.
- **Source** *(required)* — `NKF`, `FK`, `SWAB`, protocol id.
- **Generic** *(required)* — lower-case unless source capitalises.
- **Form** — `tablet`, `suspensie`, `injectievloeistof`, `zetpil`, etc. Empty if form-agnostic. (Was `Shape` — obsolete.)
- **Brand** — only if rule is brand-specific.
- **GPKs** — semicolon list.
- **Route** *(required)* — as in source (`ORAAL`, `INTRAVENEUS`, `RECTAAL`, `SUBCUTAAN`, …).
- **Indication** *(required)* — verbatim.
- **ScheduleText** *(required)* — full original dose-schedule paragraph, verbatim. Traceability field.

### Setting

- **Dep** — department/ward (`ICK`, `NICU`, `kinderafdeling`). Empty for general.

### Patient Category

Integers unless noted. Empty = unrestricted.

- **Gender** — `male`/`female`/empty.
- **Min/MaxAge** — days. weeks ×7; months ×30 (`6 maanden`→182); years ×365 (`1 jaar`→365, `18 jaar`→6574). Inclusive lower, exclusive upper (`1 maand tot 18 jaar` → MinAge=30, MaxAge=6574).
- **Min/MaxWeight** — grams (`2000 gr`→2000; `5 kg`→5000).
- **Min/MaxBSA** — m², float.
- **Min/MaxGestAge** — days (`34 weken`→238, `41 weken`→287).
- **Min/MaxPMAge** — days (`32 weken`→224, `44 weken`→308).

### DoseType *(exactly one)*

| Value | Meaning | Cue |
|-------|---------|-----|
| `once` | single, untimed | "éénmalig", "startdosering" |
| `onceTimed` | single over time | "oplaaddosis over 30 min" |
| `discontinuous` | repeated bolus | "… mg/kg/dag in N doses" |
| `timed` | repeated, each over time | "3 dd in 30 min" |
| `continuous` | continuous infusion | "… mg/kg/uur", "continu infuus" |

- **DoseText** — phase label (`startdosering`, `onderhoudsdosering`, `dag 3`). Empty if single phase.

### Component and Substance

- **Component** — often = Generic. For combinations (`amoxicilline/clavulaanzuur`) the whole combination is Component.
- **Substance** *(required)* — active substance for this row.
- Combinations → one row per Substance, shared Component, own limits.

### Schedule

- **Freqs** — integer list per FreqUnit (`in 3 doses`→`3`; `3-4 doses`→`3;4`). Empty for `once`/`onceTimed`/`continuous`.
- **DoseUnit** *(required)* — `mg`, `g`, `IE`, `microg`, `mL`, …
- **AdjustUnit** — `kg`/`m2` when adjusted; else empty.
- **FreqUnit** — `dag`, `uur`, `week`.
- **RateUnit** — `uur`/`min`; empty unless `continuous` or rate given.
- **Min/MaxTime**, **TimeUnit** — infusion/admin time (`in 15 min`→15/15/min).
- **Min/MaxInt**, **IntUnit** — interval between doses.
- **Min/MaxDur**, **DurUnit** — total treatment duration.

### Dose Limits

| Column | Unit |
|--------|------|
| Min/MaxQty | DoseUnit / dose |
| Min/MaxQtyAdj | DoseUnit / AdjustUnit / dose |
| Min/MaxPerTime | DoseUnit / FreqUnit |
| Min/MaxPerTimeAdj | DoseUnit / AdjustUnit / FreqUnit |
| Min/MaxRate | DoseUnit / RateUnit |
| Min/MaxRateAdj | DoseUnit / AdjustUnit / RateUnit |

- Never invent bounds. Single value (`50 mg/kg/dag`) → fill one side, partner empty.
- `max 4 g/dag` → Max*. `min X` → Min*.
- Range (`10-15 mg/kg/dosis`) → both Min* and Max*.
- Unlabelled single value (recommendation) → Min* only, Max* empty.

## 5. Row-splitting

Emit multiple rows when one paragraph encodes multiple rules:

- **Multi-phase (start+maintenance)** — one row/phase, differ by DoseType/DoseText. E.g. paracetamol IV premature → `once` (startdosering) + `discontinuous` (empty DoseText).
- **Combination products** — one row per Substance, shared Component.
- **Distinct PatientCategories in one paragraph** — one row each. E.g. `<1wk+<2000g`, `<1wk+≥2000g`, `1-4wk+<2000g`, `1-4wk+≥2000g` → 4 rows (×2 Substances = 8).
- **Distinct DoseTypes** — always separate.

Preserve same `ScheduleText` across all siblings from one paragraph.

No duplicates: rows duplicate if same (Source, Generic, Route, Indication, PatientCategory, DoseType, DoseText, Component, Substance).

Multi-phase cues (Dutch):

| Cues | Split |
|------|-------|
| `startdosering`/`onderhoudsdosering` | 2 rows, both often `discontinuous`, differ by DoseText/limits |
| `oplaaddosis`/`onderhoud` or `continu` | load `onceTimed`/`once`; maintenance `continuous`/`timed` |
| `dag 1`/`dag 2-7` | one row per day-range; DoseText = day label |
| `initieel`/`vervolgens` | initial + follow-up rows |

Missing a load/maintenance row is a bug — emit both when stated.

## 6. Example

Source (NKF, amoxicilline/clavulaanzuur, IV, Ernstige bacteriele infecties):

> `< 1 week en geboortegewicht < 2000 gr Amoxicilline/clavulaanzuur 10:1 : 50 / 5 mg/kg/dag in 2 doses`

Two rows (one per Substance). Same PatientCategory (MaxAge=7, MaxWeight=2000), `discontinuous`, Freqs=2, ScheduleText. Per-substance value → `MinPerTimeAdj`; MaxPerTimeAdj empty.

```text
211	NKF	amoxicilline/clavulaanzuur			INTRAVENEUS		Ernstige bacteriele infecties	< 1 week en geboortegewicht < 2000 gr Amoxicilline/clavulaanzuur 10:1 : 50 / 5 mg/kg/dag in 2 doses					7		2000								discontinuous		amoxicilline/clavulaanzuur	amoxicilline	2	mg	kg	dag																		50					
212	NKF	amoxicilline/clavulaanzuur			INTRAVENEUS		Ernstige bacteriele infecties	< 1 week en geboortegewicht < 2000 gr Amoxicilline/clavulaanzuur 10:1 : 50 / 5 mg/kg/dag in 2 doses					7		2000								discontinuous		amoxicilline/clavulaanzuur	clavulaanzuur	2	mg	kg	dag																		5					
```

More examples across DoseTypes: see `data/sources/Rules/doserules.tsv`.

## 7. Instructions

1. Emit only tab-delimited rows in §3 column order. No header unless requested. No prose/fences/commentary.
2. Keep `Indication`, `ScheduleText`, `DoseText` verbatim; collapse tabs/newlines to single space.
3. Apply §4 conversions (days/grams/m²; decimal `.`; semicolon lists).
4. Apply §5 splitting — one row per (PatientCategory × DoseType × Substance).
5. Unknown → empty. Never `0`/`-`/`N/A`/`null`.
6. Don't invent bounds; follow §4 Min/Max conventions.
