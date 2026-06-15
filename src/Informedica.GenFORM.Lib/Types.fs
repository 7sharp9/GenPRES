namespace Informedica.GenForm.Lib


[<AutoOpen>]
module Types =

    open MathNet.Numerics

    open Informedica.Logging.Lib
    open Informedica.GenUnits.Lib

    type MinMax = Informedica.GenCore.Lib.Ranges.MinMax


    /// Associate a Route and a Form
    /// setting default values for the other fields
    type FormRoute =
        {
            // The Route
            Route: string
            // The pharmaceutical form
            Form: string
            // The Unit of the form
            Unit: Unit
            // The Dose Unit to use for Dose Limits
            DoseUnit: Unit
            // The minimum Dose quantity
            MinDoseQty: ValueUnit option
            // The maximum Dose quantity
            MaxDoseQty: ValueUnit option
            // The minimum Adjusted Dose quantity
            MinDoseQtyPerKg: ValueUnit option
            // The maximum Adjusted Dose quantity
            MaxDoseQtyPerKg: ValueUnit option
            // The divisibility of a pharmaceutical form
            Divisibility: BigRational option
            // Whether a Dose runs over a Time
            Timed: bool
            // Whether the pharmaceutical form needs to be reconstituted
            Reconstitute: bool
            // Whether the pharmaceutical form is a solution
            IsSolution: bool
        }


    /// The types for Access.
    type AccessDevice =
        // Peripheral Venous Access
        | PVL
        // Central Venous Access
        | CVL
        // Any Venous Access
        | AnyAccess


    /// Possible Genders.
    type Gender =
        | Male
        | Female
        | AnyGender


    type RenalFunction =
        | EGFR of int option * int option
        | IntermittentHemodialysis
        | ContinuousHemodialysis
        | PeritonealDialysis


    /// Possible Dose Types.
    type DoseType =
        // A Once only Dose
        | Once of string
        // A Maintenance Dose
        | Discontinuous of string
        // A Continuous Dose
        | Continuous of string
        // A discontinuous per time
        | Timed of string
        // A once per time
        | OnceTimed of string
        | NoDoseType


    type Reconstitution =
        {
            // The GPK of the reconstitution
            GPK: string
            // The route for the reconstitution
            Route: string
            // The location for the reconstitution rule
            Location: string option
            // The department for the reconstitution
            Department: string option
            // The volume of the reconstitution
            DiluentVolume: ValueUnit
            // An optional expansion volume of the reconstitution
            ExpansionVolume: ValueUnit option
            // The Diluents for the reconstitution
            Diluents: string[]
        }


    /// A Substance type.
    type Substance =
        {
            // The name of the Substance
            Name: string
            // The Quantity of the Substance
            Concentration: ValueUnit option
            // The indivisible Quantity of the Substance
            MolarConcentration: ValueUnit option
        }

    type Brand = string
    type HPK = string

    type TradeProduct =
        {
            HPK: HPK
            Brand: Brand
            Substances: Substance list
        }

    type GPK = string
    type ATC = string
    type MainGroup = string
    type SubGroup = string

    type GenericName = string

    /// A Product type.
    type ProductComponent =
        {
            // The GPK id of the Generic Product
            GPK: GPK
            // The ATC code of the Product
            ATC: ATC
            // The ATC main group of the Product
            MainGroup: MainGroup
            // The ATC subgroup of the Product
            SubGroup: SubGroup
            // The Generic name of the Product
            Generic: GenericName
            // A tall-man representation of the Generic name of the Product
            TallMan: string
            // Synonyms for the Product
            Synonyms: string array
            // The full product name of the Product
            ProductLabels: string list
            // The label of the Product
            Label: string
            // The pharmaceutical form of the Product
            Form: string
            // The possible Routes of administration of the Product
            Routes: string[]
            // The possible quantities of the Pharmacological Form of the Product
            FormQuantities: ValueUnit
            // The uid of the pharmaceutical form of the Product
            FormUnit: Unit
            // Whether the pharmaceutical form of the Product requires reconstitution
            RequiresReconstitution: bool
            // The possible reconstitution rules for the Product
            Reconstitution: Reconstitution[]
            // The division factor of the Product
            Divisible: BigRational option
            // The Substances in the Product
            Substances: Substance array
            // Brand products
            TradeProducts: TradeProduct list
        }


    /// The Formulary Product that is
    /// available as a GenericProduct.
    type FormularyProduct =
        {
            /// The GPK code
            GPK: string
            /// The type of procuct
            ProductType: ProductType
            /// The department UMCU, ICC, NEO, ICK, etc...
            Departments: string list
            /// The generic name
            Generic: string
            /// The TallMan alternative name
            TallMan: string
            /// The Divisibility of the product
            Divisible: int option
            /// Use generic name
            UseGenName: bool
            /// Mmol
            Mmol: BigRational option
            /// Form
            Form: string option
            /// Brand
            Brand: string option
            /// Generic name
            GenName: string option
            /// GStand generic name
            GStandName: string option
            /// Unit
            Unit: string option
            /// Energy in kCal
            EnergyKCal: BigRational option
            /// Carbohydrates in g
            CarbG: BigRational option
            /// Protein in g
            ProtG: BigRational option
            /// Lipids in g
            LipG: BigRational option
            /// Sodium in mmol
            SodMmol: BigRational option
            /// Potassium in mmol
            PotMmol: BigRational option
            /// Calcium in mmol
            CalcMmol: BigRational option
            /// Phosphorus in mmol
            PosphMmol: BigRational option
            /// Magnesium
            MagnMmol: BigRational option
            /// Chloride in mmol
            ChlorMmol: BigRational option
            /// Iron in mmol
            IronMmol: BigRational option
            /// Vitamin D in IE
            VitDIE: BigRational option
            /// Is reconstituted
            IsReconste: bool
            /// Is diluted
            IsDilute: bool
            /// Is additive
            IsAdditive: bool
        }

    and ProductType =
        | MedicationProduct
        | ParenteralProduct
        | EnteralProduct
        | NoProduct


    type ComponentItem =
        {
            ComponentName: string
            ComponentQuantity: ValueUnit
            ItemName: string
            ItemConcentration: ValueUnit
        }


    type LimitTarget =
        | NoLimitTarget
        | OrderableLimitTarget
        | ComponentLimitTarget of string
        | SubstanceLimitTarget of string


    /// A DoseLimit for a pharmaceutical form or Substance.
    type DoseLimit =
        {
            DoseLimitTarget: LimitTarget
            // The unit to adjust dosing with
            AdjustUnit: Unit option
            // The unit to dose with
            DoseUnit: Unit
            // A MinMax Dose Quantity for the DoseLimit
            Quantity: MinMax
            // A MinMax Quantity Adjust for the DoseLimit
            QuantityAdjust: MinMax
            // An optional Dose Per Time for the DoseLimit
            PerTime: MinMax
            // A MinMax Per Time Adjust for the DoseLimit
            PerTimeAdjust: MinMax
            // A MinMax Rate for the DoseLimit
            Rate: MinMax
            // A MinMax Rate Adjust for the DoseLimit
            RateAdjust: MinMax
        }


    /// Either an absolute age is required or the
    /// patient just is "Adult"
    type Age =
        | AbsoluteAge of MinMax
        | IsAdult


    /// A PatientCategory to which a Rule applies.
    type PatientCategory =
        {
            Location: string option
            Department: string option
            Gender: Gender
            Age: Age
            Weight: MinMax
            BSA: MinMax
            GestAge: MinMax
            PMAge: MinMax
            Access: AccessDevice
        }


    /// A specific Patient to filter DoseRules.
    type Patient =
        {
            // The Location of the Patient
            Location: string option
            // The Department of the Patient
            Department: string option
            // A list of Diagnoses of the Patient
            Diagnoses: string[]
            // The Gender of the Patient
            Gender: Gender
            // The Age in days of the Patient
            Age: ValueUnit option
            // The Weight in grams of the Patient
            Weight: ValueUnit option
            // The Height in cm of the Patient
            Height: ValueUnit option
            // The Gestational Age in days of the Patient
            GestAge: ValueUnit option
            // The Post Menstrual Age in days of the Patient
            PMAge: ValueUnit option
            // The administration access devices of the Patient
            Access: AccessDevice list
            // The Renal Function of the Patient
            RenalFunction: RenalFunction option
        }

        static member Gender_ =
            (fun (p: Patient) -> p.Gender), (fun g (p: Patient) -> { p with Gender = g })

        static member Age_ = (fun (p: Patient) -> p.Age), (fun a (p: Patient) -> { p with Age = a })

        static member Weight_ =
            (fun (p: Patient) -> p.Weight), (fun w (p: Patient) -> { p with Weight = w })

        static member Height_ =
            (fun (p: Patient) -> p.Height), (fun b (p: Patient) -> { p with Height = b })

        static member GestAge_ =
            (fun (p: Patient) -> p.GestAge), (fun a (p: Patient) -> { p with GestAge = a })

        static member PMAge_ =
            (fun (p: Patient) -> p.PMAge), (fun a (p: Patient) -> { p with PMAge = a })

        static member Department_ =
            (fun (p: Patient) -> p.Department), (fun d (p: Patient) -> { p with Department = d })

        static member Access_ =
            (fun (p: Patient) -> p.Access), (fun a (p: Patient) -> { p with Access = a })

        static member RenalFunction_ =
            (fun (p: Patient) -> p.RenalFunction), (fun r (p: Patient) -> { p with RenalFunction = r })

        static member Location_ =
            (fun (p: Patient) -> p.Location), (fun l (p: Patient) -> { p with Location = l })


    type ProductId =
        | Gpk of string
        | Hpk of string


    type ComponentLimit =
        {
            Name: string
            // Specific GPKs
            ProductIds: ProductId array
            Limit: DoseLimit option
            Products: ProductComponent[]
            SubstanceLimits: DoseLimit[]
        }


    type Source =
        | Identified of string
        | Other of string

    type DoseRuleId = string
    type DataId = string
    type DoseRuleGroupId = string
    type SortNo = int


    /// Generic can be constructed by
    /// different pathways
    type GenericLabel =
        // Constructed by the list of active substances
        // concatenated by "/"
        | Canonical of string list
        // A short hand name
        | Shorthand of string
        // The canonical name appended with the form
        | GenericForm of gen: string * form: string
        // The canonical name appended with the brand
        | GenericBrand of gen: string * brand: string


    /// Distinguish between a solution
    /// and a solid pharmaceutical form
    type PharmaceuticalForm =
        | Solution of string
        | Solid of string


    type Generic =
        {
            Label: GenericLabel
            Form: PharmaceuticalForm
            Products: ProductId list
        }


    type Indication = string


    type Route = string


    /// The DoseRule type. A DoseRule is uniquely identified by the combination of
    /// Source, Generic, pharmaceutical form, Brand, Route, Indication, PatientCategory and DoseType
    /// (where DoseType also carries the original dose text as its case payload).
    type DoseRule =
        {
            // Unique identifier of this DoseRule. Treated as opaque by consumers.
            Id: DoseRuleId
            // Date id, the unique identifier of the orginal dose rule data
            DataId: DataId
            // Identifier shared by every DoseRule that belongs to the same rule group, i.e. the same
            // clinical context (Source, Generic, Form, Brand, Route, Indication, PatientCategory).
            // Rules in one group differ only in DoseType and dose schedule. Treated as opaque.
            GroupId: DoseRuleGroupId
            // Ordinal rank within a rule group, used for stable presentation order.
            SortNo: SortNo
            // The original source of the dose rule
            Source: Source
            // The original source text of the dose rule, before structured decomposition.
            SourceText: string
            // The Indication of the DoseRule
            Indication: Indication
            // The Generic of the DoseRule
            Generic: Generic
            // The Route of administration of the DoseRule
            Route: Route
            // The original text describing the patient category this rule applies to.
            PatientText: string
            // The PatientCategory the rule applies to. Whether the rule applies specifically to
            // adults is expressed by the Age case (AbsoluteAge | IsAdult).
            PatientCategory: PatientCategory
            // The original text describing the dose schedule.
            ScheduleText: string
            // The DoseType of the DoseRule. Each case (Once/OnceTimed/Discontinuous/Timed/Continuous)
            // carries the original dose text as its string payload, so DoseType captures both
            // the temporal category and the free-text description of the dose.
            DoseType: DoseType
            // The unit to adjust dosing with
            AdjustUnit: Unit option
            // The possible Frequencies of the DoseRule
            Frequencies: ValueUnit option
            // MinMax administration time. The time unit is part of the MinMax value.
            AdministrationTime: MinMax
            // MinMax interval between administrations. The time unit is part of the MinMax value.
            IntervalTime: MinMax
            // MinMax total duration of the order. The time unit is part of the MinMax value.
            Duration: MinMax
            // the limits based upon the pharmaceutical form and route
            FormLimit: DoseLimit option
            // the limits for the component and substances
            // in the component
            ComponentLimits: ComponentLimit[]
            // Reference to a renal-adjustment rule that applies on top of this DoseRule.
            // TODO: replace with a structured RenalRule type once the renal rule layer is finalised.
            RenalRuleSource: string option
            // Validation stamp
            Validated: string option
            // G-Standaard check
            Check: RuleCheck
        }

    and RuleCheck =
        {
            FreqCheck: string option
            DoseCheck: string option
        }


    /// A SolutionLimit for a Substance.
    type SolutionLimit =
        {
            // The Substance for the SolutionLimit
            SolutionLimitTarget: LimitTarget
            // The MinMax Quantity of the Substance for the SolutionLimit
            Quantity: MinMax
            // The MinMax Quantity Adjust of the Substance for the SolutionLimit
            QuantityAdj: MinMax
            // A list of possible Quantities of the Substance for the SolutionLimit
            Quantities: ValueUnit option
            // The Minmax Concentration of the Substance for the SolutionLimit
            Concentration: MinMax
            // The Products the SolutionRule applies to
            Products: ProductComponent[]
        }


    /// A SolutionRule for a specific Generic, pharmaceutical form, Route, DoseType, Department
    /// Venous Access Location, Age range, Weight range, Dose range and Generic Products.
    type SolutionRule =
        {
            // The Generic of the SolutionRule
            Generic: string
            // The pharmaceutical form of the SolutionRule
            Form: string option
            // The Route of the SolutionRule
            Route: string
            // The DoseType of the SolutionRule
            Indication: string option
            // The dose type of the SolutionRule
            DoseType: DoseType
            // The PatientCategory of the DoseRule
            PatientCategory: PatientCategory
            // The MinMax Dose range of the SolutionRule
            Dose: MinMax
            // The possible Solutions to use
            Diluents: ProductComponent[]
            // An optional dividability option
            Div: BigRational option
            // The possible Volumes to use
            Volumes: ValueUnit option
            // A MinMax Volume range to use
            Volume: MinMax
            // A MinMax adjusted Volume range to use
            VolumeAdjust: MinMax
            // A MinMax Drip Rate for the SolutionRule
            DripRate: MinMax
            // The percentage to be used as a DoseQuantity
            DosePerc: MinMax
            // The SolutionLimits for the SolutionRule
            SolutionLimits: SolutionLimit[]
        }


    /// A DoseLimit for a pharmaceutical form or Substance.
    type RenalLimit =
        {
            DoseLimitTarget: LimitTarget
            DoseReduction: DoseReduction
            Quantity: MinMax
            // An optional Dose Quantity Adjust for the DoseLimit.
            // Note: if this is specified a min and max QuantityAdjust
            // will be assumed to be 10% minus and plus the normal value
            NormQuantityAdjust: ValueUnit option
            // A MinMax Quantity Adjust for the DoseLimit
            QuantityAdjust: MinMax
            // An optional Dose Per Time for the DoseLimit
            PerTime: MinMax
            // An optional Per Time Adjust for the DoseLimit
            // Note: if this is specified a min and max NormPerTimeAdjust
            // will be assumed to be 10% minus and plus the normal value
            NormPerTimeAdjust: ValueUnit option
            // A MinMax Per Time Adjust for the DoseLimit
            PerTimeAdjust: MinMax
            // A MinMax Rate for the DoseLimit
            Rate: MinMax
            // A MinMax Rate Adjust for the DoseLimit
            RateAdjust: MinMax
        }

    and DoseReduction =
        | Absolute
        | Relative
        | NoReduction


    type RenalRule =
        {
            // The Generic of the RenalRule
            Generic: string
            // The Route of administration of the RenalRule
            Route: string
            Indication: string
            // The source of the RenalRule
            Source: string
            Age: MinMax
            RenalFunction: RenalFunction
            // The DoseType of the RenalRule
            DoseType: DoseType
            // The possible Frequencies of the RenalRule
            Frequencies: ValueUnit option
            // The MinMax Interval Time of the RenalRule
            IntervalTime: MinMax
            // The list of associated RenalLimits of the RenalRule.
            RenalLimits: RenalLimit array
        }


    type ProductFilter =
        {
            Generic: string
            Form: string option
            Route: string
            FormUnit: Unit option
        }


    /// A Filter to get the DoseRules for a specific Patient.
    type DoseFilter =
        {
            // the Indication to filter on
            Indication: string option
            // the Generic to filter on
            Generic: string option
            // the pharmaceutical form to filter on
            Form: string option
            // the Route to filter on
            Route: string option
            // the DoseType to filter on
            DoseType: DoseType option
            // the diluent to use
            Diluent: string option
            // the components to use
            Components: string list
            // the patient to filter on
            Patient: Patient
        }


    type SolutionFilter =
        {
            // The Generic of the SolutionRule
            Generic: string
            // The pharmaceutical form of the SolutionRule
            Form: string option
            // The Route of the SolutionRule
            Route: string option
            // The DoseType of the SolutionRule
            Indication: string option
            DoseType: DoseType option
            // the patient
            Patient: Patient
            // the diluent to dilute the component
            Diluent: string option
            // The MinMax Dose range of the SolutionRule
            Dose: ValueUnit option
        }

    /// A PrescriptionRule for a specific Patient
    /// with a DoseRule and a list of SolutionRules.
    type PrescriptionRule =
        {
            Patient: Patient
            DoseRule: DoseRule
            SolutionRules: SolutionRule[]
            RenalRules: RenalRule[]
        }


    type NormDose =
        | NormQuantityAdjust of LimitTarget * ValueUnit
        | NormPerTimeAdjust of LimitTarget * ValueUnit
        | NormRateAdjust of LimitTarget * ValueUnit


    type Message =
        | Info of string
        | Warning of string
        | ErrorMsg of (string * exn option)

        interface IMessage


    /// Data-transfer / raw-parse (IO) types. Loaded from sheets and mapped
    /// into the pure domain types above. Defined last so the domain layer
    /// cannot reference these DTOs (one-way dependency: Data -> Domain only).
    [<AutoOpen>]
    module Data =

        type UnitMapping =
            {
                Long: string
                Short: string
                MV: string
                Group: string
            }


        type RouteMapping =
            {
                Long: string
                Short: string
            }


        /// Reference intake-totals row: per patient age/weight category, the
        /// unit/time-unit and per-time min/max limits used to compute and annotate
        /// aggregated intake (volume, energy, etc.). Loaded from the "Totals" sheet.
        type TotalsData =
            {
                Name: string
                MinAge: BigRational option
                MaxAge: BigRational option
                MinWeight: BigRational option
                MaxWeight: BigRational option
                Unit: Unit option
                Adj: Unit option
                TimeUnit: Unit option
                MinPerTime: BigRational option
                MaxPerTime: BigRational option
                MinPerTimeAdj: BigRational option
                MaxPerTimeAdj: BigRational option
            }


        type HashId = string


        /// Raw dose rule data
        type DoseRuleData =
            {
                RowId: HashId
                RuleId: HashId
                GrpId: HashId
                SortNo: int
                Source: string
                SourceText: string
                Generic: GenericData
                Indication: string
                Route: string
                PatientText: string
                Patient: PatientCategoryData
                ScheduleText: string
                ScheduleData: ScheduleData
                Validated: string option
                FreqCheck: string option
                DoseCheck: string option
            }

        and GenericData =
            {
                Name: string
                Form: string
                Brand: string
                GPKs: string array
                HPKs: string array
            }

        and PatientCategoryData =
            {
                Location: string
                Dep: string
                IsAdult: bool
                Gender: Gender
                MinAge: BigRational option
                MaxAge: BigRational option
                MinWeight: BigRational option
                MaxWeight: BigRational option
                MinBSA: BigRational option
                MaxBSA: BigRational option
                MinGestAge: BigRational option
                MaxGestAge: BigRational option
                MinPMAge: BigRational option
                MaxPMAge: BigRational option
            }

        and ScheduleData =
            {
                DoseType: string
                DoseText: string
                Freqs: BigRational array
                AdjustUnit: string
                FreqUnit: string
                RateUnit: string
                MinTime: BigRational option
                MaxTime: BigRational option
                TimeUnit: string
                MinInt: BigRational option
                MaxInt: BigRational option
                IntUnit: string
                MinDur: BigRational option
                MaxDur: BigRational option
                DurUnit: string
                DoseLimitData: DoseLimitData
            }

        and DoseLimitData =
            {
                CmpBased: bool
                Component: string
                Substance: string
                DoseUnit: string
                MinQty: BigRational option
                MaxQty: BigRational option
                MinQtyAdj: BigRational option
                MaxQtyAdj: BigRational option
                MinPerTime: BigRational option
                MaxPerTime: BigRational option
                MinPerTimeAdj: BigRational option
                MaxPerTimeAdj: BigRational option
                MinRate: BigRational option
                MaxRate: BigRational option
                MinRateAdj: BigRational option
                MaxRateAdj: BigRational option
            }

        /// Grouping key for raw dose rule data: rows sharing this key belong to one
        /// dose rule group and differ only by dose type/text and component/substance.
        type DataGroupKey =
            {

                Source: string
                Indication: string
                Generic: GenericData
                Patient: PatientCategoryData
                Route: string
            }


        /// A group of raw DoseRuleData rows sharing a DataGroupKey, together with the
        /// resolved product forms/components and any warnings raised while grouping.
        type GroupedRuleData =
            {
                DataGroupKey: DataGroupKey
                DoseRuleData: DoseRuleData[]
                Forms: string[]
                Products: ProductComponent[]
                // warnings raised while grouping (e.g. RowId dedup conflicts);
                // carried out of the parallel group pass and merged in fromData
                Warnings: string list
            }

        /// Raw solution rule data
        type SolutionRuleData =
            {
                // solution rule section
                Generic: string
                Form: string
                Route: string
                Indication: string
                Location: string option
                Department: string option
                CVL: string
                PVL: string
                MinAge: BigRational option
                MaxAge: BigRational option
                MinWeight: BigRational option
                MaxWeight: BigRational option
                MinGestAge: BigRational option
                MaxGestAge: BigRational option
                MinDose: BigRational option
                MaxDose: BigRational option
                DoseType: string
                DoseText: string
                Solutions: string list
                Div: BigRational option
                Volumes: BigRational array
                MinVol: BigRational option
                MaxVol: BigRational option
                MinVolAdj: BigRational option
                MaxVolAdj: BigRational option
                MinPerc: BigRational option
                MaxPerc: BigRational option
                // solution limit section
                Component: string
                Substance: string
                Unit: string
                Quantities: BigRational array
                MinQty: BigRational option
                MaxQty: BigRational option
                MinQtyAdj: BigRational option
                MaxQtyAdj: BigRational option
                MinDrip: BigRational option
                MaxDrip: BigRational option
                MinConc: BigRational option
                MaxConc: BigRational option
            }


        /// Raw renal rule data
        type RenalRuleData =
            {
                Generic: string
                Route: string
                Indication: string
                Source: string
                MinAge: BigRational option
                MaxAge: BigRational option
                ContDial: string
                IntDial: string
                PerDial: string
                MinGFR: BigRational option
                MaxGFR: BigRational option
                DoseType: string
                DoseText: string
                Frequencies: BigRational array
                MinInterval: BigRational option
                MaxInterval: BigRational option
                IntervalUnit: string
                Substance: string
                DoseRed: string
                DoseUnit: string
                AdjustUnit: string
                FreqUnit: string
                RateUnit: string
                MinQty: BigRational option
                MaxQty: BigRational option
                NormQtyAdj: BigRational array
                MinQtyAdj: BigRational option
                MaxQtyAdj: BigRational option
                MinPerTime: BigRational option
                MaxPerTime: BigRational option
                NormPerTimeAdj: BigRational array
                MinPerTimeAdj: BigRational option
                MaxPerTimeAdj: BigRational option
                MinRate: BigRational option
                MaxRate: BigRational option
                MinRateAdj: BigRational option
                MaxRateAdj: BigRational option
            }
