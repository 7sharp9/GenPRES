# GenPRES Gebruikershandleiding (Nederlands)

> **⚠️ Medisch voorbehoud**  
> GenPRES is niet bedoeld voor direct klinisch gebruik zonder passende validatie en regelgevende goedkeuring.  
> Zie [SUPPORT.md](../../../SUPPORT.md) voor de volledige disclaimer.

---

## Inhoudsopgave

1. [Inleiding](#1-inleiding)
2. [De applicatie openen](#2-de-applicatie-openen)
3. [Basisnavigatie](#3-basisnavigatie)
4. [Medicatie voorschrijven](#4-medicatie-voorschrijven)
5. [Noodlijst en infuuspompen](#5-noodlijst-en-infuuspompen)
6. [Testen zonder patiëntgegevens](#6-testen-zonder-patiëntgegevens)
7. [Eenheidconversie testen](#7-eenheidconversie-testen)
8. [Veelvoorkomende gebruiksscenario's](#8-veelvoorkomende-gebruiksscenarios)
9. [Probleemoplossing](#9-probleemoplossing)

---

## 1. Inleiding

GenPRES (Generic Prescribing System) is een open-source Clinical Decision Support System (CDSS) dat klinisch personeel ondersteunt bij:

- Het opzoeken van evidence-based doseergrenzen en protocollen
- Het uitvoeren van veilige medicatieberekeningen
- Het verifiëren van de juiste toepassing van klinische richtlijnen

GenPRES richt zich op pediatrische en neonatale intensivecareafdeling, maar kan worden toegepast in elke medische omgeving.

Het live systeem draait op <http://genpres.nl>.

Aanvullende achtergrondinformatie is beschikbaar op <https://medicatieveiligensnel.nl>.

---

## 2. De applicatie openen

### Met patiëntgegevens (EPD-koppeling)

In een klinische omgeving wordt GenPRES doorgaans gestart vanuit een Elektronisch Patiënten Dossier (EPD) waarbij patiëntparameters vooraf zijn ingevuld in de URL, bijvoorbeeld:

```
https://genpres.nl/#patient?pg=pr&dc=n&la=du&ad=730&wt=12000
```

De URL gebruikt hash-based routing (`/#patient?...`). Ondersteunde queryparameters:

**Patiëntparameters:**

| Parameter | Omschrijving | Eenheid / Waarden |
|-----------|--------------|-------------------|
| `ad` | Leeftijd | Dagen (bijv. 730 ≈ 2 jaar) |
| `by` | Geboortejaar | JJJJ |
| `bm` | Geboortemaand | 1–12 |
| `bd` | Geboortedag | 1–31 |
| `wt` | Gewicht | Grammen (bijv. 12000 = 12 kg) |
| `ht` | Lengte | Centimeters |
| `gw` | Zwangerschapsduur weken | Weken |
| `gd` | Zwangerschapsduur dagen | Dagen |
| `cv` | Centraal veneuze lijn | `y` = ja |
| `dp` | Afdeling | Tekst |

> Gebruik `ad` (leeftijd in dagen) of `by`/`bm`/`bd` (geboortedatum), niet beide.

**Medicatieparameters:**

| Parameter | Omschrijving | Eenheid / Waarden |
|-----------|--------------|-------------------|
| `md` | Medicatie | Generieke naam |
| `rt` | Toedieningsweg | bijv. `oraal`, `intraveneus` |
| `in` | Indicatie | Tekst |
| `dt` | Doseertype | Tekst |
| `fr` | Vorm | Tekst |

**UI-parameters:**

| Parameter | Omschrijving | Eenheid / Waarden |
|-----------|--------------|-------------------|
| `pg` | Pagina | `pr`, `el`, `cm`, `fm`, `pe` |
| `la` | Taal | `en`, `du`, `fr`, `gr`, `sp`, `it` |
| `dc` | Disclaimer | `n` = verbergen |

### Zonder patiëntgegevens (demo / testen)

De applicatie kan worden gebruikt **zonder patiëntgegevens** in de querystring. Open de applicatie direct via:

```
http://localhost:5173
```

of op de productieserver:

```
http://genpres.nl
```

Wanneer er geen patiëntcontext is opgegeven, start de applicatie in demomodus. U kunt de patiëntgegevens dan handmatig invoeren in de interface voordat u een medicatie selecteert.

---

## 3. Basisnavigatie

Na het openen van de applicatie ziet u het hoofdscherm met de volgende functionele gebieden:

### Patiëntpaneel (bovenaan)

Toont de patiëntparameters (leeftijd, gewicht, geslacht, lengte). Als deze niet via de URL zijn meegegeven, kunt u ze hier handmatig invullen.

### Medicatiezoekbalk (hoofdgebied)

Gebruik het zoekveld om medicatie te zoeken op:
- Generieke naam (bijv. *paracetamol*, *morfine*)
- ATC-code

### Medicatielijst

Selecteer een medicatie uit de zoekresultaten om het doseerpaneel te openen.

### Doseerpaneel

Toont de berekende doseringsrange op basis van de patiëntparameters en het geselecteerde doseringsprotocol. Velden omvatten:

- **Dosis per kg** – gewichtsaangepaste dosis
- **Totale dosis** – berekende absolute dosis
- **Frequentie** – aantal doses per dag
- **Toedieningsweg** – oraal, IV, rectaal, etc.
- **Concentratie / Volume** – voor infuusbereidingen

---

## 4. Medicatie voorschrijven

### Stapsgewijze werkwijze

1. **Voer patiëntgegevens in** (leeftijd, gewicht, geslacht) in het patiëntpaneel.
2. **Zoek een medicatie** door de generieke naam of ATC-code in het zoekveld te typen.
3. **Selecteer de medicatie** uit de lijst met resultaten.
4. **Bekijk de doseringsrange** in het doseerpaneel. Het systeem markeert waarden die buiten de veilige grenzen vallen.
5. **Pas dosis of frequentie aan** indien klinisch geïndiceerd. Het systeem waarschuwt u als de ingevoerde waarde de maximale of minimale grens overschrijdt.
6. **Selecteer de toedieningsweg** (oraal, IV, rectaal, etc.).
7. **Bevestig het voorschrift** en draag de gegevens over naar het EPD of druk af/exporteer indien nodig.

### Veiligheidsmeldingen

GenPRES toont kleurgecodeerde meldingen:

| Kleur | Betekenis |
|-------|-----------|
| 🟢 Groen | Waarde binnen veilige range |
| 🟡 Geel | Waarde aan de grens van de veilige range – wees voorzichtig |
| 🔴 Rood | Waarde buiten veilige range – beoordeel voor verdergaan |

---

## 5. Noodlijst en infuuspompen

De noodlijst biedt snelle toegang tot standaardinstellingen van infuuspompen voor kritische medicatie (bijv. adrenaline, dopamine, noradrenaline). Deze is ontworpen voor gebruik in nood- en IC-situaties.

### De noodlijst openen

1. Open de applicatie.
2. Navigeer naar **Noodlijst** in het hoofdmenu.
3. Voer het gewicht van de patiënt in of bevestig dit.
4. Het systeem genereert de standaard infuusconcentraties en pompsnelheden voor elk medicament.

### Standaard infuuspompen

Elk item op de noodlijst toont:

- **Medicatienaam**
- **Aanbevolen concentratie** (bijv. 1 mg/mL)
- **Startdosis** (µg/kg/min of mL/h)
- **Doseringsrange** (minimum – maximum)

---

## 6. Testen zonder patiëntgegevens

U kunt een volledige end-to-end workflow uitvoeren zonder echte patiëntgegevens. Dit is nuttig voor:

- Onboarding van ontwikkelaars
- QA-testen
- Training en demonstraties

### Werkwijze

1. Start de applicatie lokaal:

   ```bash
   dotnet run
   ```

   Open <http://localhost:5173> in uw browser.

2. Laat de URL-querystring leeg (geen queryparameters).

3. Voer op het hoofdscherm **handmatig testpatiëntgegevens in**:
   - Leeftijd: bijv. `2` jaar
   - Gewicht: bijv. `12` kg
   - Geslacht: `Man`

4. Zoek een medicatie, bijv. `paracetamol`.

5. Bekijk de berekende doseringsinformatie.

6. Pas desgewenst dosiswaarden aan en observeer de veiligheidsmeldingen.

### Democache

De repository bevat een democachebestand met voorbeeldmedicatiegegevens. Dit is voldoende voor alle bovenstaande testworkflows. Er is geen liveverbinding met internet of eigendomsbestanden vereist.

---

## 7. Eenheidconversie testen

GenPRES gebruikt intern `BigRational`-rekenkunde voor exacte, eenheidveilige berekeningen via **Informedica.GenUNITS.Lib**. De volgende procedure stelt u in staat eenheidconversies in de gebruikersinterface te verifiëren.

### Dosisoenheden verifiëren

1. Selecteer een medicament met een bekende dosis (bijv. *paracetamol* oraal).
2. Bekijk het veld **dosis per kg** — dit moet de waarde in `mg/kg` tonen.
3. Wijzig het patiëntgewicht en bevestig dat het veld **totale dosis** dienovereenkomstig bijwerkt.

### Infuusconcentraties verifiëren

1. Selecteer een IV-medicament (bijv. *morfine*).
2. Bekijk het veld **concentratie** (mg/mL) en het veld **pompsnelheid** (mL/h).
3. Wijzig de gewenste dosis en bevestig dat de pompsnelheid correct herberekend wordt.

### Voorbeeld: Paracetamol oraal

| Patiëntgewicht | Dosis/kg | Verwachte totale dosis |
|---------------|---------|----------------------|
| 10 kg | 15 mg/kg | 150 mg |
| 20 kg | 15 mg/kg | 300 mg |
| 30 kg | 15 mg/kg | 450 mg |

---

## 8. Veelvoorkomende gebruiksscenario's

### Scenario 1: Oraal paracetamol voor een peuter

1. Voer in: leeftijd `2` jaar, gewicht `12` kg, geslacht `Man`.
2. Zoek: `paracetamol`.
3. Selecteer **Paracetamol – oraal**.
4. Bekijk de aanbevolen doseringsrange (doorgaans 10–15 mg/kg, 4–6 keer per dag).
5. Bevestig dat de maximale dagdosis niet wordt overschreden.

### Scenario 2: IV morfine-infuus voor een kind

1. Voer in: leeftijd `5` jaar, gewicht `20` kg, geslacht `Vrouw`.
2. Zoek: `morfine`.
3. Selecteer **Morfine – IV continu infuus**.
4. Bekijk de startdosis (bijv. 10–40 µg/kg/h) en de berekende pompsnelheid.
5. Pas de dosis aan; bevestig dat de snelheid bijwerkt.

### Scenario 3: TPN-berekening

1. Voer patiëntparameters in (gewicht, leeftijd).
2. Navigeer naar **TPN** in het hoofdmenu.
3. Bekijk de automatisch gegenereerde macro- en micronutriëntenformule.
4. Pas afzonderlijke componenten aan indien klinisch geïndiceerd.
5. Exporteer of druk de TPN-opdracht af voor de apotheek.

---

## 9. Probleemoplossing

### Applicatie start niet op

- Zorg ervoor dat de vereiste vereisten zijn geïnstalleerd (.NET SDK, Node.js, npm). Zie [DEVELOPMENT.md](../../../DEVELOPMENT.md#toolchain-requirements).
- Voer `dotnet run` uit vanuit de root van de repository.
- Controleer of poort `5173` niet bezet is door een ander proces.

### Geen medicatiegegevens zichtbaar

- De applicatie vereist een cachebestand. De democache (`*.demo`) in de repository is voldoende voor testen.
- Zorg ervoor dat de omgevingsvariabele `GENPRES_PROD` is ingesteld op `0` (demomodus). Zie [DEVELOPMENT.md](../../../DEVELOPMENT.md#environment-configuration).

### Dosiswaarden lijken onjuist

- Controleer of patiëntgewicht en leeftijd correct zijn ingevoerd.
- Controleer of de juiste toedieningsweg is geselecteerd.
- Bekijk de veiligheidskleurcodering — een rood signaal geeft een waarde buiten het toegestane bereik aan.

### Verdere hulp

- GitHub Issues: <https://github.com/informedica/GenPRES/issues>
- Slack-werkruimte: <https://genpresworkspace.slack.com>

---

*Versie: 1.0 — maart 2026*  
*Taal: Nederlands*  
*[🇬🇧 English version](../en/user-guide.md)*
