/// Localization Improvement — Demonstration Script
///
/// This script demonstrates a more robust, idiomatic F# approach to the
/// localization system that is more compliant with standard .NET/web methods.
///
/// CURRENT PROBLEMS with the existing `getTerm` implementation:
///   1. `string[][]` is fragile: column positions are hardcoded (English=1,
///      Dutch=2, …). Reordering columns in the Google Sheet silently breaks
///      all translations at runtime.
///   2. `fromString` throws an exception for unknown language names instead
///      of returning an `Option` or `Result`.
///   3. No offline / fallback capability: translations are always loaded from
///      Google Sheets at startup; a network failure leaves the UI untranslated.
///   4. The `Deferred<string[][]>` representation is opaque and leaks CSV
///      structure all the way up into view code.
///
/// PROPOSED IMPROVEMENTS demonstrated below:
///   A. Parse CSV by column *headers* (language display names) instead of
///      fixed column indices → robust to spreadsheet column reordering.
///   B. Store translations as `TranslationMap` (Map<string, Map<Locales, string>>)
///      → typed, queryable, no magic numbers.
///   C. Safe `tryFromString` returning `Option<Locales>` instead of throwing.
///   D. Hardcoded static fallback translations embedded in the binary → the
///      app still works (with English/Dutch at minimum) when the sheet is
///      unavailable.
///   E. A `getTermOrFallback` helper that merges the remote map over the static
///      fallback so partial sheet loads always degrade gracefully.

#load "../Types.fs"
#load "../Localization.fs"

open System
open Shared


// ──────────────────────────────────────────────────────────────────────────────
// A. Type alias
// ──────────────────────────────────────────────────────────────────────────────

/// A typed translation map: term-key → locale → translated string.
/// Replaces the current opaque `string[][]` representation.
type TranslationMap = Map<string, Map<Localization.Locales, string>>


// ──────────────────────────────────────────────────────────────────────────────
// B. Safe locale parsing
// ──────────────────────────────────────────────────────────────────────────────

/// Converts a display name string to a `Locales` value, returning `None`
/// for unknown names instead of throwing an exception (unlike `fromString`).
let tryLocaleFromString (s: string) : Localization.Locales option =
    let s = s.Trim().ToLower()

    match s with
    | "english" -> Some Localization.English
    | "nederlands" -> Some Localization.Dutch
    | "français" -> Some Localization.French
    | "español" -> Some Localization.Spanish
    | "deutsch" -> Some Localization.German
    | "italiano" -> Some Localization.Italian
    // | "中文" -> Some Localization.Chinees
    | _ -> None


// ──────────────────────────────────────────────────────────────────────────────
// C. Column-header-based CSV parser
// ──────────────────────────────────────────────────────────────────────────────

/// Parses a `string[][]` produced by `Csv.parseCSV` into a `TranslationMap`.
///
/// The first row is expected to contain column headers. Any column whose
/// header matches a known locale display name (via `tryLocaleFromString`) is
/// treated as a translation column. The first column always holds the term key.
///
/// This approach is **robust to column reordering** in the source spreadsheet,
/// unlike the current implementation that uses hardcoded column indices.
let parseLocalizationCSV (csv: string[][]) : TranslationMap =
    if csv.Length < 2 then
        Map.empty
    else
        let headers = csv.[0]

        // Identify which column indices correspond to which locale
        let localeColumns: (int * Localization.Locales) array =
            headers
            |> Array.mapi (fun i header -> i, tryLocaleFromString header)
            |> Array.choose (fun (i, opt) -> opt |> Option.map (fun l -> i, l))

        csv
        |> Array.skip 1 // skip header row
        |> Array.choose (fun row ->
            if row.Length > 0 && not (String.IsNullOrWhiteSpace row.[0]) then
                let termKey = row.[0].Trim()

                let translations =
                    localeColumns
                    |> Array.choose (fun (colIdx, locale) ->
                        if colIdx < row.Length && not (String.IsNullOrWhiteSpace row.[colIdx]) then
                            Some(locale, row.[colIdx].Trim())
                        else
                            None
                    )
                    |> Map.ofArray

                Some(termKey, translations)
            else
                None
        )
        |> Map.ofArray


// ──────────────────────────────────────────────────────────────────────────────
// D. Type-safe term lookup
// ──────────────────────────────────────────────────────────────────────────────

/// Looks up a translated string in a `TranslationMap`.
/// Returns `None` when the term or locale is absent (no silent mis-indexing).
let getTerm (translations: TranslationMap) (locale: Localization.Locales) (term: Terms) : string option =
    let key = $"{term}".Trim()
    translations |> Map.tryFind key |> Option.bind (Map.tryFind locale)


// ──────────────────────────────────────────────────────────────────────────────
// E. Static fallback translations
// ──────────────────────────────────────────────────────────────────────────────

/// Hard-coded translations that are always available without a network call.
/// These serve as the baseline so the UI remains functional when the Google
/// Sheet cannot be reached, and as a reference for the expected shape of data.
///
/// Extend this list as new `Terms` cases are added to keep offline support
/// complete.
let staticTranslations: (Terms * (Localization.Locales * string) list) list =
    let en = Localization.English
    let nl = Localization.Dutch
    let fr = Localization.French
    let de = Localization.German
    let es = Localization.Spanish
    let it = Localization.Italian

    [
        Terms.``Patient enter patient data``,
        [
            en, "Enter patient data ..."
            nl, "Voer patient gegevens in ..."
            fr, "Saisir les données du patient..."
            de, "Geben Sie Patientendaten ein..."
            es, "Ingrese los datos del paciente..."
            it, "Inserisci i dati del paziente..."
        ]
        Terms.``Patient Age``,
        [
            en, "Age"
            nl, "Leeftijd"
            fr, "Âge"
            de, "Alter"
            es, "Edad"
            it, "Età"
        ]
        Terms.``Patient GA Age``,
        [
            en, "Gest. Age"
            nl, "Zwangerschapstermijn"
            fr, "Âge gestationnel"
            de, "Schwangerschaftsalter"
            es, "Edad gestacional"
            it, "Età gestazionale"
        ]
        Terms.``Patient Age year``,
        [
            en, "year"
            nl, "jaar"
            fr, "an"
            de, "Jahr"
            es, "año"
            it, "anno"
        ]
        Terms.``Patient Age years``,
        [
            en, "years"
            nl, "jaren"
            fr, "ans"
            de, "Jahre"
            es, "años"
            it, "anni"
        ]
        Terms.``Patient Age month``,
        [
            en, "month"
            nl, "maand"
            fr, "mois"
            de, "Monat"
            es, "mes"
            it, "mese"
        ]
        Terms.``Patient Age months``,
        [
            en, "months"
            nl, "maanden"
            fr, "mois"
            de, "Monate"
            es, "meses"
            it, "mesi"
        ]
        Terms.``Patient Age week``,
        [
            en, "week"
            nl, "week"
            fr, "semaine"
            de, "Woche"
            es, "semana"
            it, "settimana"
        ]
        Terms.``Patient Age weeks``,
        [
            en, "weeks"
            nl, "weken"
            fr, "semaines"
            de, "Wochen"
            es, "semanas"
            it, "settimane"
        ]
        Terms.``Patient Age day``,
        [
            en, "day"
            nl, "dag"
            fr, "jour"
            de, "Tag"
            es, "día"
            it, "giorno"
        ]
        Terms.``Patient Age days``,
        [
            en, "days"
            nl, "dagen"
            fr, "jours"
            de, "Tage"
            es, "días"
            it, "giorni"
        ]
        Terms.``Patient Estimated``,
        [
            en, "Estimated"
            nl, "Geschat"
            fr, "Estimé"
            de, "Geschätzt"
            es, "Estimado"
            it, "Stimato"
        ]
        Terms.``Patient Years``,
        [
            en, "Years"
            nl, "Jaren"
            fr, "Années"
            de, "Jahre"
            es, "Años"
            it, "Anni"
        ]
        Terms.``Patient Months``,
        [
            en, "Months"
            nl, "Maanden"
            fr, "Mois"
            de, "Monate"
            es, "Meses"
            it, "Mesi"
        ]
        Terms.``Patient Weeks``,
        [
            en, "Weeks"
            nl, "Weken"
            fr, "Semaines"
            de, "Wochen"
            es, "Semanas"
            it, "Settimane"
        ]
        Terms.``Patient Days``,
        [
            en, "Days"
            nl, "Dagen"
            fr, "Jours"
            de, "Tage"
            es, "Días"
            it, "Giorni"
        ]
        Terms.``Patient Weight``,
        [
            en, "Weight"
            nl, "Gewicht"
            fr, "Poids"
            de, "Gewicht"
            es, "Peso"
            it, "Peso"
        ]
        Terms.``Patient Length``,
        [
            en, "Length"
            nl, "Lengte"
            fr, "Taille"
            de, "Größe"
            es, "Talla"
            it, "Altezza"
        ]
        Terms.``Patient remove patient data``,
        [
            en, "Remove patient data"
            nl, "Verwijder patient gegevens"
            fr, "Supprimer les données du patient"
            de, "Patientendaten entfernen"
            es, "Eliminar datos del paciente"
            it, "Rimuovi i dati del paziente"
        ]
        Terms.``Emergency List``,
        [
            en, "Emergency List"
            nl, "Noodlijst"
            fr, "Liste d'urgence"
            de, "Notfallliste"
            es, "Lista de emergencia"
            it, "Lista di emergenza"
        ]
        Terms.``Emergency List show when patient data``,
        [
            en, "Show when patient data is available"
            nl, "Toon wanneer patiëntgegevens beschikbaar zijn"
            fr, "Afficher lorsque les données du patient sont disponibles"
            de, "Anzeigen, wenn Patientendaten verfügbar sind"
            es, "Mostrar cuando los datos del paciente estén disponibles"
            it, "Mostra quando i dati del paziente sono disponibili"
        ]
        Terms.``Emergency List Catagory``,
        [
            en, "Category"
            nl, "Categorie"
            fr, "Catégorie"
            de, "Kategorie"
            es, "Categoría"
            it, "Categoria"
        ]
        Terms.``Emergency List Intervention``,
        [
            en, "Intervention"
            nl, "Interventie"
            fr, "Intervention"
            de, "Intervention"
            es, "Intervención"
            it, "Intervento"
        ]
        Terms.``Emergency List Calculated``,
        [
            en, "Calculated"
            nl, "Berekend"
            fr, "Calculé"
            de, "Berechnet"
            es, "Calculado"
            it, "Calcolato"
        ]
        Terms.``Emergency List Preparation``,
        [
            en, "Preparation"
            nl, "Bereiding"
            fr, "Préparation"
            de, "Zubereitung"
            es, "Preparación"
            it, "Preparazione"
        ]
        Terms.``Emergency List Advice``,
        [
            en, "Advice"
            nl, "Advies"
            fr, "Conseil"
            de, "Hinweis"
            es, "Consejo"
            it, "Consiglio"
        ]
        Terms.``Continuous Medication List``,
        [
            en, "Continuous Medication"
            nl, "Continue Medicatie"
            fr, "Médicaments continus"
            de, "Dauertherapie"
            es, "Medicación continua"
            it, "Farmaci continui"
        ]
        Terms.``Continuous Medication List show when patient data``,
        [
            en, "Show when patient data is available"
            nl, "Toon wanneer patiëntgegevens beschikbaar zijn"
            fr, "Afficher lorsque les données du patient sont disponibles"
            de, "Anzeigen, wenn Patientendaten verfügbar sind"
            es, "Mostrar cuando los datos del paciente estén disponibles"
            it, "Mostra quando i dati del paziente sono disponibili"
        ]
        Terms.``Continuous Medication Catagory``,
        [
            en, "Category"
            nl, "Categorie"
            fr, "Catégorie"
            de, "Kategorie"
            es, "Categoría"
            it, "Categoria"
        ]
        Terms.``Continuous Medication Medication``,
        [
            en, "Medication"
            nl, "Medicatie"
            fr, "Médicament"
            de, "Medikament"
            es, "Medicación"
            it, "Farmaco"
        ]
        Terms.``Continuous Medication Quantity``,
        [
            en, "Quantity"
            nl, "Hoeveelheid"
            fr, "Quantité"
            de, "Menge"
            es, "Cantidad"
            it, "Quantità"
        ]
        Terms.``Continuous Medication Solution``,
        [
            en, "Solution"
            nl, "Oplossing"
            fr, "Solution"
            de, "Lösung"
            es, "Solución"
            it, "Soluzione"
        ]
        Terms.``Continuous Medication Dose``,
        [
            en, "Dose"
            nl, "Dosis"
            fr, "Dose"
            de, "Dosis"
            es, "Dosis"
            it, "Dose"
        ]
        Terms.``Continuous Medication Advice``,
        [
            en, "Advice"
            nl, "Advies"
            fr, "Conseil"
            de, "Hinweis"
            es, "Consejo"
            it, "Consiglio"
        ]
        Terms.``Prescribe``,
        [
            en, "Prescribe"
            nl, "Voorschrijven"
            fr, "Prescrire"
            de, "Verschreiben"
            es, "Prescribir"
            it, "Prescrivere"
        ]
        Terms.``Prescribe Scenarios``,
        [
            en, "Scenarios"
            nl, "Scenario's"
            fr, "Scénarios"
            de, "Szenarien"
            es, "Escenarios"
            it, "Scenari"
        ]
        Terms.``Prescribe Indications``,
        [
            en, "Indications"
            nl, "Indicaties"
            fr, "Indications"
            de, "Indikationen"
            es, "Indicaciones"
            it, "Indicazioni"
        ]
        Terms.``Prescribe Medications``,
        [
            en, "Medications"
            nl, "Medicaties"
            fr, "Médicaments"
            de, "Medikamente"
            es, "Medicamentos"
            it, "Farmaci"
        ]
        Terms.``Prescribe Routes``,
        [
            en, "Routes"
            nl, "Toedieningswegen"
            fr, "Voies d'administration"
            de, "Applikationswege"
            es, "Vías de administración"
            it, "Vie di somministrazione"
        ]
        Terms.``Prescribe Prescription``,
        [
            en, "Prescription"
            nl, "Voorschrift"
            fr, "Prescription"
            de, "Verschreibung"
            es, "Prescripción"
            it, "Prescrizione"
        ]
        Terms.``Prescribe Preparation``,
        [
            en, "Preparation"
            nl, "Bereiding"
            fr, "Préparation"
            de, "Zubereitung"
            es, "Preparación"
            it, "Preparazione"
        ]
        Terms.``Prescribe Administration``,
        [
            en, "Administration"
            nl, "Toediening"
            fr, "Administration"
            de, "Verabreichung"
            es, "Administración"
            it, "Somministrazione"
        ]
        Terms.``Order``,
        [
            en, "Order"
            nl, "Order"
            fr, "Ordre"
            de, "Auftrag"
            es, "Orden"
            it, "Ordine"
        ]
        Terms.``Order Frequency``,
        [
            en, "Frequency"
            nl, "Frequentie"
            fr, "Fréquence"
            de, "Häufigkeit"
            es, "Frecuencia"
            it, "Frequenza"
        ]
        Terms.``Order Dose``,
        [
            en, "Dose"
            nl, "Dosis"
            fr, "Dose"
            de, "Dosis"
            es, "Dosis"
            it, "Dose"
        ]
        Terms.``Order Adjusted dose``,
        [
            en, "Adjusted dose"
            nl, "Aangepaste dosis"
            fr, "Dose ajustée"
            de, "Angepasste Dosis"
            es, "Dosis ajustada"
            it, "Dose adattata"
        ]
        Terms.``Order Quantity``,
        [
            en, "Quantity"
            nl, "Hoeveelheid"
            fr, "Quantité"
            de, "Menge"
            es, "Cantidad"
            it, "Quantità"
        ]
        Terms.``Order Concentration``,
        [
            en, "Concentration"
            nl, "Concentratie"
            fr, "Concentration"
            de, "Konzentration"
            es, "Concentración"
            it, "Concentrazione"
        ]
        Terms.``Order Drip rate``,
        [
            en, "Drip rate"
            nl, "Druppelsnelheid"
            fr, "Débit de perfusion"
            de, "Tropfgeschwindigkeit"
            es, "Velocidad de goteo"
            it, "Velocità di infusione"
        ]
        Terms.``Order Administration time``,
        [
            en, "Administration time"
            nl, "Toedieningstijd"
            fr, "Durée d'administration"
            de, "Verabreichungszeit"
            es, "Tiempo de administración"
            it, "Tempo di somministrazione"
        ]
        Terms.``Nutrition``,
        [
            en, "Nutrition"
            nl, "Voeding"
            fr, "Nutrition"
            de, "Ernährung"
            es, "Nutrición"
            it, "Nutrizione"
        ]
        Terms.``Treatment Plan``,
        [
            en, "Treatment Plan"
            nl, "Behandelplan"
            fr, "Plan de traitement"
            de, "Behandlungsplan"
            es, "Plan de tratamiento"
            it, "Piano di trattamento"
        ]
        Terms.``Formulary``,
        [
            en, "Formulary"
            nl, "Formularium"
            fr, "Formulaire"
            de, "Arzneimittelverzeichnis"
            es, "Formulario"
            it, "Formulario"
        ]
        Terms.``Formulary Medications``,
        [
            en, "Medications"
            nl, "Medicaties"
            fr, "Médicaments"
            de, "Medikamente"
            es, "Medicamentos"
            it, "Farmaci"
        ]
        Terms.``Formulary Indications``,
        [
            en, "Indications"
            nl, "Indicaties"
            fr, "Indications"
            de, "Indikationen"
            es, "Indicaciones"
            it, "Indicazioni"
        ]
        Terms.``Formulary Routes``,
        [
            en, "Routes"
            nl, "Toedieningswegen"
            fr, "Voies d'administration"
            de, "Applikationswege"
            es, "Vías de administración"
            it, "Vie di somministrazione"
        ]
        Terms.``Formulary Patients``,
        [
            en, "Patients"
            nl, "Patiënten"
            fr, "Patients"
            de, "Patienten"
            es, "Pacientes"
            it, "Pazienti"
        ]
        Terms.``Parenteralia``,
        [
            en, "Parenteralia"
            nl, "Parenteralia"
            fr, "Parentéraux"
            de, "Parenteralia"
            es, "Parenterales"
            it, "Preparazioni parenterali"
        ]
        Terms.``Delete``,
        [
            en, "Delete"
            nl, "Verwijder"
            fr, "Supprimer"
            de, "Löschen"
            es, "Eliminar"
            it, "Elimina"
        ]
        Terms.``Edit``,
        [
            en, "Edit"
            nl, "Bewerk"
            fr, "Modifier"
            de, "Bearbeiten"
            es, "Editar"
            it, "Modifica"
        ]
        Terms.``Ok ``,
        [
            en, "Ok"
            nl, "Ok"
            fr, "Ok"
            de, "Ok"
            es, "Ok"
            it, "Ok"
        ]
        Terms.``Sort By``,
        [
            en, "Sort by"
            nl, "Sorteer op"
            fr, "Trier par"
            de, "Sortieren nach"
            es, "Ordenar por"
            it, "Ordina per"
        ]
        Terms.``Disclaimer``,
        [
            en, "Disclaimer"
            nl, "Disclaimer"
            fr, "Avertissement"
            de, "Haftungsausschluss"
            es, "Aviso legal"
            it, "Avvertenza"
        ]
        Terms.``Disclaimer text``,
        [
            en, "This application is for informational purposes only..."
            nl, "Deze applicatie is uitsluitend bedoeld voor informatieve doeleinden..."
            fr, "Cette application est uniquement à titre informatif..."
            de, "Diese Anwendung dient nur zu Informationszwecken..."
            es, "Esta aplicación es solo para fines informativos..."
            it, "Questa applicazione è solo a scopo informativo..."
        ]
        Terms.``Disclaimer accept``,
        [
            en, "I accept"
            nl, "Ik accepteer"
            fr, "J'accepte"
            de, "Ich akzeptiere"
            es, "Acepto"
            it, "Accetto"
        ]
        Terms.Settings,
        [
            en, "Settings"
            nl, "Instellingen"
            fr, "Paramètres"
            de, "Einstellungen"
            es, "Configuración"
            it, "Impostazioni"
        ]
        Terms.``Reload resources``,
        [
            en, "Reload resources"
            nl, "Herlaad bronnen"
            fr, "Recharger les ressources"
            de, "Ressourcen neu laden"
            es, "Recargar recursos"
            it, "Ricarica risorse"
        ]
        Terms.``Enter password``,
        [
            en, "Enter password"
            nl, "Voer wachtwoord in"
            fr, "Entrer le mot de passe"
            de, "Passwort eingeben"
            es, "Introducir contraseña"
            it, "Inserisci la password"
        ]
        Terms.Password,
        [
            en, "Password"
            nl, "Wachtwoord"
            fr, "Mot de passe"
            de, "Passwort"
            es, "Contraseña"
            it, "Password"
        ]
        Terms.``Invalid password``,
        [
            en, "Invalid password"
            nl, "Ongeldig wachtwoord"
            fr, "Mot de passe invalide"
            de, "Ungültiges Passwort"
            es, "Contraseña no válida"
            it, "Password non valida"
        ]
        Terms.Cancel,
        [
            en, "Cancel"
            nl, "Annuleer"
            fr, "Annuler"
            de, "Abbrechen"
            es, "Cancelar"
            it, "Annulla"
        ]
        Terms.Confirm,
        [
            en, "Confirm"
            nl, "Bevestig"
            fr, "Confirmer"
            de, "Bestätigen"
            es, "Confirmar"
            it, "Conferma"
        ]
    ]


/// Builds a `TranslationMap` from the static fallback list.
let staticTranslationMap: TranslationMap =
    staticTranslations
    |> List.map (fun (term, pairs) -> $"{term}".Trim(), pairs |> Map.ofList)
    |> Map.ofList


// ──────────────────────────────────────────────────────────────────────────────
// F. Merge helper: overlay remote sheet over static fallback
// ──────────────────────────────────────────────────────────────────────────────

/// Merges `remote` onto `fallback`: for each term that exists in `remote`,
/// per-locale entries in `remote` take precedence over `fallback`.
/// Terms missing from `remote` are kept from `fallback` unchanged.
let mergeTranslations (fallback: TranslationMap) (remote: TranslationMap) : TranslationMap =
    remote
    |> Map.fold
        (fun acc termKey remoteLocales ->
            let merged =
                match Map.tryFind termKey acc with
                | None -> remoteLocales
                | Some fallbackLocales ->
                    Map.fold (fun m locale txt -> Map.add locale txt m) fallbackLocales remoteLocales

            Map.add termKey merged acc
        )
        fallback


/// Looks up a term, falling back to `defVal` when absent.
let getTermOrFallback
    (translations: TranslationMap)
    (locale: Localization.Locales)
    (defVal: string)
    (term: Terms)
    : string
    =
    getTerm translations locale term |> Option.defaultValue defVal


// ──────────────────────────────────────────────────────────────────────────────
// Smoke tests
// ──────────────────────────────────────────────────────────────────────────────

#r "nuget: Expecto"

open Expecto
open Expecto.Flip


let localizationTests =
    testList
        "Localization improvements"
        [

            test "tryLocaleFromString returns Some for known locales" {
                tryLocaleFromString "English"
                |> Expect.equal "english" (Some Localization.English)

                tryLocaleFromString "Nederlands"
                |> Expect.equal "dutch" (Some Localization.Dutch)

                tryLocaleFromString "Français"
                |> Expect.equal "french" (Some Localization.French)

                tryLocaleFromString "Deutsch"
                |> Expect.equal "german" (Some Localization.German)

                tryLocaleFromString "Español"
                |> Expect.equal "spanish" (Some Localization.Spanish)

                tryLocaleFromString "Italiano"
                |> Expect.equal "italian" (Some Localization.Italian)
            }

            test "tryLocaleFromString returns None for unknown strings" {
                tryLocaleFromString "Klingon" |> Expect.equal "unknown" None
                tryLocaleFromString "" |> Expect.equal "empty" None
            }

            test "staticTranslationMap contains all Terms cases" {
                let allTerms =
                    Microsoft.FSharp.Reflection.FSharpType.GetUnionCases(typeof<Terms>)
                    |> Array.map (fun c -> c.Name.Trim())

                for term in allTerms do
                    staticTranslationMap
                    |> Map.containsKey term
                    |> Expect.isTrue $"static map should contain term '{term}'"
            }

            test "getTerm returns correct translation from static map" {
                getTerm staticTranslationMap Localization.English Terms.``Patient Age``
                |> Expect.equal "English patient age" (Some "Age")

                getTerm staticTranslationMap Localization.Dutch Terms.``Patient Age``
                |> Expect.equal "Dutch patient age" (Some "Leeftijd")

                getTerm staticTranslationMap Localization.French Terms.``Patient Age``
                |> Expect.equal "French patient age" (Some "Âge")
            }

            test "parseLocalizationCSV parses header row to identify locale columns" {
                // Simulate a CSV with columns in a different order than expected
                let csv =
                    [|
                        // Header row: term key, then languages in unexpected order
                        [| "Term"; "Deutsch"; "English"; "Nederlands" |]
                        [| "Patient Age"; "Alter"; "Age"; "Leeftijd" |]
                        [| "Patient Weight"; "Gewicht"; "Weight"; "Gewicht" |]
                    |]

                let map = parseLocalizationCSV csv

                // English is in column 2 (not the hardcoded 1 of the old system)
                getTerm map Localization.English Terms.``Patient Age``
                |> Expect.equal "English age from reordered CSV" (Some "Age")

                getTerm map Localization.Dutch Terms.``Patient Age``
                |> Expect.equal "Dutch age from reordered CSV" (Some "Leeftijd")

                getTerm map Localization.German Terms.``Patient Age``
                |> Expect.equal "German age from reordered CSV" (Some "Alter")
            }

            test "mergeTranslations overlays remote over fallback" {
                let fallback =
                    [
                        "Patient Age",
                        [
                            Localization.English, "Age (fallback)"
                            Localization.Dutch, "Leeftijd"
                        ]
                        |> Map.ofList
                    ]
                    |> Map.ofList

                let remote =
                    [
                        "Patient Age", [ Localization.English, "Age (remote)" ] |> Map.ofList
                    ]
                    |> Map.ofList

                let merged = mergeTranslations fallback remote

                // Remote value wins for English
                getTerm merged Localization.English Terms.``Patient Age``
                |> Expect.equal "remote wins" (Some "Age (remote)")

                // Fallback value retained for Dutch (not present in remote)
                getTerm merged Localization.Dutch Terms.``Patient Age``
                |> Expect.equal "fallback retained" (Some "Leeftijd")
            }

            test "getTermOrFallback returns defVal when term is absent" {
                let emptyMap: TranslationMap = Map.empty

                getTermOrFallback emptyMap Localization.English "default" Terms.``Patient Age``
                |> Expect.equal "default value used" "default"
            }

        ]


runTestsWithCLIArgs [] [||] localizationTests |> ignore
