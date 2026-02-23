# UI Order View

Shows an order for editing.

## UI Order View fields and their visibility conditions

| # | Group | Element | substIndx | comp > 1 | itms > 0 | itms > 1 | useAdjust | Continuous | Timed | OnceTimed | Once | Discont. | Additional data guard |
|---|-------|---------|:---------:|:--------:|:--------:|:--------:|:---------:|:----------:|:-----:|:---------:|:----:|:--------:|----------------------|
| 1 | | Component name | — | ✓ | — | — | — | ✓ | ✓ | ✓ | ✓ |    ✓     | — |
| 2 | | Substance name | — | not empty | — | ✓ | — | ✓ | ✓ | ✓ | ✓ |    ✓     | — |
| 3 | Prescription | Substance Dose Quantity | ✓ | — | ✓ | — | — | — | ✓ | ✓ | ✓ |    ✓     | has Vals |
| 4 | Prescription | Substance Dose Quantity Adjust | ✓ | — | ✓ | — | ✓ | — | — | ✓ | ✓ |    —     | has Vals |
| 5 | Prescription | Substance Dose PerTime | ✓ | — | ✓ | — | — | — | ✓ | ✓ | ✓ |    ✓     | PerTimeAdjust if useAdjust, else PerTime |
| 6 | Prescription | Substance Dose PerTime Adjust | ✓ | — | ✓ | — | ✓ | — | ✓ | ✓ | ✓ |    ✓     | PerTimeAdjust if useAdjust, else PerTime |
| 7 | Prescription | Substance Dose Rate | ✓ | — | ✓ | — | — | ✓ | — | — | — |    —     | RateAdjust if useAdjust, else Rate |
| 8 | Prescription | Substance Dose Rate Adjust | ✓ | — | ✓ | — | ✓ | ✓ | — | — | — |    —     | RateAdjust if useAdjust, else Rate |
| 9 | Preparation | Component Orderable Quantity | — | ✓ | — | — | — | ✓ | ✓ | ✓ | ✓ |    ✓     | — |
| 10 | Preparation | Substance Component Concentration | ✓ | — | — | — | — | ✓ | ✓ | ✓ | ✓ |    ✓     | DefinedConstraints.Vals.Length > 1 (≤1 → hidden) |
| 11 | Preparation | Substance Orderable Quantity | ✓ | ✓ | ✓ | — | — | ✓ | — | — | — |    —     | has Vals |
| 12 | Preparation | Substance Orderable Concentration | ✓ | ✓ | ✓ | — | — | — | ✓ | ✓ | ✓ |    ✓     | has Vals |
| 13 | Preparation | Orderable Quantity | — | ✓ | — | — | — | ✓ | ✓ | ✓ | ✓ |    ✓     | has Vals |
| 14 | Administration | Schedule Frequency | — | — | — | — | — | — | ✓ | — | — |    ✓     | has Vals |
| 15 | Administration | Orderable Dose Quantity | — | — | — | — | — | — | ✓ | ✓ | ✓ |    ✓     | has Vals |
| 16 | Administration | Orderable Dose Rate | — | — | — | — | — | ✓ | ✓ | ✓ | — |    —     | — |
| 17 | Administration | Schedule Time | — | — | — | — | — | ✓ | ✓ | ✓ | ✓ | ✓ | has Vals |

**Note** Rows 5 and 6 and rows 7 and 8 are mutually exclusive: if useAdjust is true, then the Adjust fields are shown, otherwise the non-Adjust fields are shown.


## UI Order View navigation options

| #  | Group          | Element                           | Label                 | Defined Increment | Selectable | Navigable | Stepable | Clearable |
|----|----------------|-----------------------------------|-----------------------|-------------------|------------|-----------|----------|-----------|
| 1  |                | Component name                    | componenten           | —                 | ✓          | —         | —        | —         |
| 2  |                | Substance name                    | stoffen               | —                 | ✓          | —         | —        | —         |
| 3  | Prescription   | Substance Dose Quantity           | keer dosis            | —                 | ✓          | —         | —        | ✓         |
| 4  | Prescription   | Substance Dose Quantity Adjust    | keer dosis            | —                 | ✓          | —         | —        | ✓         |
| 5  | Prescription   | Substance Dose PerTime            | dosering              | —                 | ✓          | —         | —        | ✓         |
| 6  | Prescription   | Substance Dose PerTime Adjust     | dosering              | —                 | ✓          | —         | —        | ✓         |
| 7  | Prescription   | Substance Dose Rate               | dosering              | —                 | ✓          | —         | —        | ✓         |
| 8  | Prescription   | Substance Dose Rate Adjust        | dosering              | —                 | ✓          | —         | —        | ✓         |
| 9  | Preparation    | Component Orderable Quantity      | bereiding hoeveelheid | ✓                 | —          | ✓         | ✓        | —         |
| 10 | Preparation    | Substance Component Concentration | product sterkte       | —                 | ✓          | —         | —        | —         |
| 11 | Preparation    | Substance Orderable Quantity      | {stof} hoeveelheid    | —                 | ✓          | —         | —        | —         |
| 12 | Preparation    | Substance Orderable Concentration | {stof} concentratie   | —                 | ✓          | —         | —        | —         |
| 13 | Preparation    | Orderable Quantity                | totale hoeveelheid    | ✓                 | ✓          | —         | —        | —         |
| 14 | Administration | Schedule Frequency                | frequentie            | ✓                 | ✓          | —         | —        | —         |
| 15 | Administration | Orderable Dose Quantity           | toedien hoeveelheid   | ✓                 | —          | ✓         | ✓        | —         |
| 16 | Administration | Orderable Dose Rate               | inloop snelheid       | ✓                 | —          | ✓         | ✓        | —         |
| 17 | Administration | Schedule Time                     | inloop tijd           | —                 | ✓          | —         | —        | —         |

