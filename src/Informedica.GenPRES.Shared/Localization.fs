/// Localization support for the GenPRES application.
///
/// The current implementation fetches a "Localization" sheet from Google Sheets
/// at startup and stores translations as a `string[][]` matrix.  The column
/// indices that map to each language are hardcoded in `getTerm`, which means
/// **reordering columns in the spreadsheet silently breaks all translations**.
///
/// An improved, more idiomatic approach is demonstrated in
/// `Scripts/Localization.fsx`.  Key improvements proposed there:
///   - Parse the CSV by column *headers* (language display names) so the
///     implementation is robust to spreadsheet column reordering.
///   - Store translations as `TranslationMap` (`Map<string, Map<Locales, string>>`)
///     instead of `string[][]` — no magic column indices.
///   - `tryLocaleFromString` returns `Option` instead of throwing.
///   - Static fallback translations embedded in the binary so the UI works
///     without a network connection.
///   - `mergeTranslations` overlays remote sheet data over the static fallback
///     so a partial sheet load degrades gracefully.
namespace Shared


/// Compile-time-safe enumeration of all localizable UI strings.
/// Add a new case here whenever a new UI label is introduced, and update the
/// Localization sheet (and `Scripts/Localization.fsx` static fallback) accordingly.
type Terms =
    | ``Patient enter patient data``
    | ``Patient Age``
    | ``Patient GA Age``
    | ``Patient Age year``
    | ``Patient Age years``
    | ``Patient Age month``
    | ``Patient Age months``
    | ``Patient Age week``
    | ``Patient Age weeks``
    | ``Patient Age day``
    | ``Patient Age days``
    | ``Patient Estimated``
    | ``Patient Years``
    | ``Patient Months``
    | ``Patient Weeks``
    | ``Patient Days``
    | ``Patient Weight``
    | ``Patient Length``
    | ``Patient remove patient data``
    | ``Emergency List``
    | ``Emergency List show when patient data``
    | ``Emergency List Catagory``
    | ``Emergency List Intervention``
    | ``Emergency List Calculated``
    | ``Emergency List Preparation``
    | ``Emergency List Advice``
    | ``Continuous Medication List``
    | ``Continuous Medication List show when patient data``
    | ``Continuous Medication Catagory``
    | ``Continuous Medication Medication``
    | ``Continuous Medication Quantity``
    | ``Continuous Medication Solution``
    | ``Continuous Medication Dose``
    | ``Continuous Medication Advice``
    | ``Prescribe``
    | ``Prescribe Scenarios``
    | ``Prescribe Indications``
    | ``Prescribe Medications``
    | ``Prescribe Routes``
    | ``Prescribe Prescription``
    | ``Prescribe Preparation``
    | ``Prescribe Administration``
    | ``Order``
    | ``Order Frequency``
    | ``Order Dose``
    | ``Order Adjusted dose``
    | ``Order Quantity``
    | ``Order Concentration``
    | ``Order Drip rate``
    | ``Order Administration time``
    | ``Nutrition``
    | ``Treatment Plan``
    | ``Formulary``
    | ``Formulary Medications``
    | ``Formulary Indications``
    | ``Formulary Routes``
    | ``Formulary Patients``
    | ``Parenteralia``
    | ``Delete``
    | ``Edit``
    | ``Ok ``
    | ``Sort By``
    | ``Disclaimer``
    | ``Disclaimer text``
    | ``Disclaimer accept``
    | Settings
    | ``Reload resources``
    | ``Enter password``
    | Password
    | ``Invalid password``
    | Cancel
    | Confirm


module Localization =


    /// Supported UI languages.  Add a case here when a new language is
    /// introduced, then update `toString`, `fromString`, `languages`, the
    /// Localization spreadsheet, and the static fallback in
    /// `Scripts/Localization.fsx`.
    type Locales =
        | English
        | Dutch
        | French
        | German
        | Spanish
        | Italian
    //        | Chinees


    /// Converts a `Locales` value to its human-readable display name as it
    /// appears in the "Localization" spreadsheet header row.
    let toString =
        function
        | English -> "English"
        | Dutch -> "Nederlands"
        | French -> "Français"
        | Spanish -> "Español"
        | German -> "Deutsch"
        | Italian -> "Italiano"
    //        | Chinees -> "中文"


    /// Converts a display name string to a `Locales` value.
    ///
    /// ⚠️  This function **throws** for unknown input.  Consider using the
    /// `tryLocaleFromString` function from `Scripts/Localization.fsx` which
    /// returns `Option<Locales>` instead.
    let fromString (s: string) =
        let s = s.Trim().ToLower()

        match s with
        | _ when s = "english" -> English
        | _ when s = "nederlands" -> Dutch
        | _ when s = "français" -> French
        | _ when s = "español" -> Spanish
        | _ when s = "deutsch" -> German
        | _ when s = "italiano" -> Italian
        //        | _ when s = "中文" -> Chinees
        | _ -> failwith $"{s} is not a known language"


    let languages = [| English; Dutch; French; German; Spanish; Italian |]


    /// Looks up a translated string for `term` in `locale` from a `string[][]`
    /// matrix produced by `Csv.parseCSV` on the "Localization" Google Sheet.
    ///
    /// ⚠️  Column positions are **hardcoded** (English = 1, Dutch = 2, …).
    /// Reordering columns in the spreadsheet will silently return wrong
    /// translations.  See `Scripts/Localization.fsx` for a header-based parser
    /// (`parseLocalizationCSV`) that is robust to column reordering and uses a
    /// typed `TranslationMap` instead of `string[][]`.
    let getTerm (terms: string[][]) locale term =
        let term = $"{term}".Trim()

        let indx =
            match locale with
            | English -> 1
            | Dutch -> 2
            | French -> 3
            | German -> 4
            | Spanish -> 5
            | Italian -> 6
        //            | Chinees -> 7

        terms
        |> Array.tryFind (fun r -> r[0] = term)
        |> Option.map (fun r -> r[indx])
        |> fun r ->
            if r.IsNone then
                printfn $"cannot find term: {term}"

            r