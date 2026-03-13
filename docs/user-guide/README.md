# GenPRES User Guide

> ⚠️ **Clinical Disclaimer**: GenPRES is a Clinical Decision Support System (CDSS). It is **not** intended for direct clinical use without appropriate validation, regulatory approval, and institutional governance. Always apply independent clinical judgment. See [SUPPORT.md](../../SUPPORT.md#medical-advice-disclaimer).

---

## Overview

GenPRES supports medication prescribing, preparation, and administration in pediatric (and adult) critical care. It performs rule lookup, dose calculations, and constraint validation based on patient-specific parameters.

This guide is aimed at:

- **New developers** onboarding to the project
- **QA testers** verifying application behaviour
- **Clinical informatics staff** evaluating the system

---

## External User Guides

Full functional user guides are maintained at the GenPRES project site. These include annotated screenshots and animated walkthroughs *(language banner available — figures and animations remain in Dutch)*:

| Guide | Description |
|-------|-------------|
| [Emergency List & Standard Infusion Pumps](https://picuwkz.nl/de-genpres-noodlijst/) | Emergency medication list and continuous infusion pump workflows |
| [Prescribing & Drug Dosing](https://picuwkz.nl/genpres-medicatie-controle/) | Step-by-step prescribing and dose-checking workflow |

---

## Documentation in this Guide

| Document | Description |
|----------|-------------|
| [Getting Started](getting-started.md) | Running the application, accessing it without patient data, entering patient data via the UI or URL parameters |
| [Testing Workflows](testing-workflows.md) | Reproducible testing procedures for QA: no-patient-context testing, unit conversion testing, basic prescribing workflow |

---

## MDR Compliance

GenPRES is developed in accordance with the European Medical Device Regulation (MDR 2017/745). Relevant regulatory documentation is maintained under [`docs/mdr/`](../mdr/README.md):

- [User Requirements](../mdr/requirements/user-requirements.md)
- [User Profile](../mdr/usability/user-profile.md)
- [Critical Tasks](../mdr/usability/critical-tasks.md)
- [Risk Analysis](../mdr/risk-analysis/)

---

## Getting Help

- **GitHub Discussions** – preferred for questions, ideas, and clinical use cases
- **GitHub Issues** – bug reports and feature requests
- **Slack** – [genpresworkspace.slack.com](https://genpresworkspace.slack.com)
- **SUPPORT.md** – [Support policy and contact information](../../SUPPORT.md)
