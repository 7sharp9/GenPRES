# Development on GenPRES

## Getting Started

### Toolchain Requirements

Before contributing, ensure you have the following installed (this section is the canonical source for toolchain versions):

- **.NET SDK**: 10.0.0 or later
- **Node.js**: 18.x, 22.x, or 23.x (LTS versions recommended)
- **npm**: 10.x or later

### Setting Up the Development Environment

1. Fork this repository
2. Clone your fork locally
3. Configure the demo environment variables as described in the
  [Environment Configuration](#environment-configuration) section below.

If you prefer, you can use `direnv`, as documented in the [Environment Configuration](#environment-configuration) section below.

### Start the application

```bash
dotnet run
```

Open your browser to `http://localhost:5173`

## Project Folder Structure

### Root Level

```text
GenPRES/
├── .github/                   # GitHub configuration and workflows
│   ├── ISSUE_TEMPLATE/        # Issue templates
│   ├── PULL_REQUEST_TEMPLATE/ # PR templates
│   ├── instructions/          # Development instructions
│   └── workflows/             # CI/CD workflows
├── .husky/                    # Git hooks
├── .idea/                     # JetBrains IDE configuration
├── .vscode/                   # VS Code configuration
├── benchmark/                 # Performance benchmarks
├── data/                      # Application data
│   ├── cache/                 # Cached data files
│   ├── config/                # Configuration files
│   ├── data/                  # JSON data files
│   └── zindex/                # Z-Index drug database files
├── deploy/                    # Deployment scripts and configurations
├── docs/                      # Documentation
│   ├── code-reviews/          # Code review documents
│   ├── data-extraction/       # Data extraction documentation
│   ├── domain/                # Domain documentation
│   ├── implementation-plans/  # Implementation plans
│   ├── literature/            # Research literature
│   ├── mdr/                   # Medical Device Regulation documentation
│   │   ├── design-history/    # Design history files
│   │   ├── interface/         # Interface specifications
│   │   ├── post-market/       # Post-market surveillance
│   │   ├── requirements/      # Requirements documentation
│   │   ├── risk-analysis/     # Risk management
│   │   ├── usability/         # Usability engineering
│   │   └── validation/        # Validation documentation
│   ├── roadmap/               # Project roadmap
│   └── scenarios/             # Clinical scenarios
├── scripts/                   # Utility scripts
└── src/                       # Source code
    ├── Informedica.Agents.Lib/           # Agent-based concurrency library
    ├── Informedica.DataPlatform.Lib/     # Data Platform integration
    ├── Informedica.FHIR.Lib/             # FHIR resource conversion
    ├── Informedica.FTK.Lib/              # Adult formulary parsing library
    ├── Informedica.GenCORE.Lib/          # Core domain library
    ├── Informedica.GenFORM.Lib/          # Formulary management library
    ├── Informedica.GenORDER.Lib/         # Order processing library
    ├── Informedica.GenPRES.Client/       # Frontend application
    │   ├── Components/        # UI components
    │   ├── Pages/             # Page components
    │   ├── Views/             # View components
    │   ├── output/            # Compiled JavaScript output
    │   └── public/            # Static assets
    ├── Informedica.GenPRES.Server/       # Backend application
    │   ├── Properties/        # Server properties
    │   ├── Scripts/           # Server scripts
    │   └── data/              # Server data directory
    ├── Informedica.GenPRES.Shared/       # Shared types and API protocol
    ├── Informedica.GenSOLVER.Lib/        # Constraint solver library
    ├── Informedica.GenUNITS.Lib/         # Units of measurement library
    ├── Informedica.HIXConnect.Lib/       # HIX Connect integration
    ├── Informedica.Logging.Lib/          # Logging utilities
    ├── Informedica.MCP.Lib/              # Model Context Protocol for LLM integration
    ├── Informedica.MetaVision.Lib/       # MetaVision integration
    ├── Informedica.NKF.Lib/              # Pediatric formulary parsing library
    ├── Informedica.NLP.Lib/              # Natural Language Processing for rule extraction
    ├── Informedica.OTS.Lib/              # Ontology Terminology Server integration
    ├── Informedica.Utils.Lib/            # Utility functions
    ├── Informedica.ZForm.Lib/            # Z-Index form library
    └── Informedica.ZIndex.Lib/           # Z-Index database library
```

### Key Configuration Files

- `Build.fs` / `Build.fsproj` - Build automation
- `GenPRES.sln` - Solution file
- `Dockerfile` - Docker containerization
- `paket.dependencies` - Package management
- `global.json` - .NET SDK version

### Documentation Files

- `README.md` - Project overview
- `CHANGELOG.md` - Version history
- `CONTRIBUTING.md` - Contribution guidelines
- `CODE_OF_CONDUCT.md` - Code of conduct
- `DEVELOPMENT.md` - Development guide (this file)
- `GOVERNANCE.md` - Project governance
- `MAINTAINERS.md` - Maintainer information
- `ROADMAP.md` - Project roadmap
- `SECURITY.md` - Security policy
- `SUPPORT.md` - Support information
- `WARP.md` - Warp AI agent documentation
- `docs/mdr/design-history/architecture.md` - Technical architecture
- `docs/domain/` - Domain model specifications
- `docs/user-guide/` - Multilingual user guide ([English](docs/user-guide/en/user-guide.md), [Nederlands](docs/user-guide/nl/gebruikershandleiding.md))

## Directory Descriptions

### Core Directories

- **`.github/`** - GitHub configurations (issue/PR templates, workflows, development instructions)
- **`benchmark/`** - Performance benchmarking suite
- **`data/`** - Application data (drug cache, configuration, clinical data, Z-Index database)
- **`docs/`** - Comprehensive documentation:
  - `docs/domain/` - Domain model specifications (Core Domain, GenFORM, GenORDER, GenSOLVER)
  - `docs/mdr/` - MDR compliance (design history, requirements, risk analysis, validation)
  - `docs/scenarios/` - Clinical scenarios
- **`src/`** - Source code (client, server, and F# libraries)

### Library Modules

Each `Informedica.*.Lib` directory contains:

- Core F# source files
- `Scripts/` - Interactive F# scripts for testing
- `Notebooks/` - Jupyter/Polyglot notebooks (where applicable)
- `paket.references` - Package dependencies
- `*.fsproj` - F# project file

## Project Architecture

For complete architectural documentation, see:

- **[Architecture Overview](docs/mdr/design-history/architecture.md)**: Technical stack, server/client structure, Docker hosting, and build configuration
- **[Core Domain Model](docs/domain/core-domain.md)**: Transformation pipeline, constraint-based architecture, and domain concepts
- **[GenFORM](docs/domain/genform-free-text-to-operational-rules.md)**: Free text to Operational Knowledge Rules (OKRs)
- **[GenORDER](docs/domain/genorder-operational-rules-to-orders.md)**: OKRs to Order Scenarios
- **[GenSOLVER](docs/domain/gensolver-from-orders-to-quantitative-solutions.md)**: Constraint solving engine

### Technology Stack

This project is built on the [SAFE Stack](https://safe-stack.github.io/):

- **Informedica.GenPRES.Server**: F# with [Saturn](https://saturnframework.org/)
- **Informedica.GenPRES.Client**: F# with [Fable](https://fable.io/docs/) and [Elmish](https://elmish.github.io/elmish/)
- **Testing**: Expecto with FsCheck for property-based testing
- **Build**: .NET 10.0

### Core Libraries

For complete library specifications including capabilities and dependencies, see [GenFORM Appendix B.3](docs/domain/genform-free-text-to-operational-rules.md#addendum-b3-genform-libraries).

Key libraries in dependency order:

- **Informedica.Utils.Lib**: Shared utilities, common functions  
- **Informedica.Agents.Lib**: Agent-based execution (MailboxProcessor)  
- **Informedica.Logging.Lib**: Concurrent logging  
- **Informedica.NLP.Lib**: Natural Language Processing for structured rule extraction
- **Informedica.OTS.Lib**: Google Sheets/CSV and Ontology Terminology Server integration
- **Informedica.GenUNITS.Lib**: Unit-safe calculations  
- **Informedica.GenSOLVER.Lib**: Quantitative constraint solving  
- **Informedica.GenCORE.Lib**: Core domain model  
- **Informedica.ZIndex.Lib**: Medication and product database  
- **Informedica.ZForm.Lib**: Z-Index dosing reference data  
- **Informedica.NKF.Lib**: Kinderformularium dose rule extraction
- **Informedica.FTK.Lib**: Farmacotherapeutisch Kompas dose rule extraction
- **Informedica.GenFORM.Lib**: Operational Knowledge Rules (OKRs)  
- **Informedica.GenORDER.Lib**: Clinical order scenarios and execution  
- **Informedica.MCP.Lib**: Model Context Protocol for LLM integration
- **Informedica.FHIR.Lib**: FHIR resource conversion
- **Informedica.DataPlatform.Lib**: Data Platform integration
- **Informedica.HIXConnect.Lib**: HIX Connect integration
- **Informedica.MetaVision.Lib**: MetaVision integration
- **Informedica.GenPRES.Shared**: Shared types and API protocol
- **Informedica.GenPRES.Server**: Server API and orchestration
- **Informedica.GenPRES.Client**: Web-based clinical UI

## Code Contribution Guidelines

### Repository Structure

**Important: an opt-in strategy is used** in the `.gitignore` file, i.e. you have to specifically define what should be included instead of the other way around!!

This project follows specific organizational patterns:

- **Library Structure**: Use the `Informedica.{Domain}.{Lib/Server/Client}` naming convention
- **Domain Libraries**: GenSOLVER, GenORDER, GenUNITS, GenCORE
- **Separate Test Projects**: Each library has its own test project
- **Opt-in .gitignore**: *You must explicitly define what should be included!!*

### Coding Standards

Follow the [F# Coding Instructions](.github/instructions/fsharp-coding.instructions.md) for code style, formatting, type design, error handling, testing, and documentation guidelines.

Follow the [Commit Message Instructions](.github/instructions/commit-message.instructions.md) for conventional commit format, types, scopes, and examples.

## Domain-Specific Guidelines

### Medical Safety Considerations

When contributing to medical functionality:

- **Patient Safety First**: All changes affecting dosage calculations, medication lookup, or clinical decision support must be thoroughly tested
- **Precision Matters**: Use appropriate units of measure and maintain calculation accuracy
- **Validation Required**: Implement comprehensive input validation for medical data
- **Error Handling**: Provide clear, actionable error messages for medical professionals
- **MDR Compliance**: Ensure all medical-related changes align with Medical Device Regulation requirements

For mathematical operations, units of measure, performance, and testing guidelines, see [F# Coding Instructions](.github/instructions/fsharp-coding.instructions.md).

## Development Workflow

### Git Workflow

1. **Fork** the repository
2. **Clone** your fork locally: `git clone https://github.com/your-username/GenPRES.git`
3. **Set up upstream remote**: `git remote add upstream https://github.com/informedica/GenPRES.git`
4. **Before starting work**, sync your fork:

   ```bash
   git checkout master
   git fetch upstream
   git merge upstream/master
   git push origin master
   ```

5. **Create a feature branch**: `git checkout -b feat/your-feature-name`
6. **Make changes** following our coding guidelines
7. **Commit** using conventional commit messages `git commit -m "feat(scope): description"`
8. **Check** that you are still in sync with upstream:

   ```bash
   git fetch upstream
   git merge upstream/master
   ```

9. **Push** to your fork `git push origin feat/your-feature-name`
10. **Create a pull request** to the main repository
11. **After PR is merged**, delete your feature branch locally and remotely:

    ```bash
    git checkout master
    git pull upstream master
    git push origin --delete feat/your-feature-name
    git branch -d feat/your-feature-name
    ```

12. **Repeat** for new features or fixes

### Opt-in .gitignore Strategy

This project uses an opt-in strategy for `.gitignore`:

- You must explicitly define what should be included
- When adding new files, ensure they're properly included in Git
- Proprietary medication cache files are excluded for licensing reasons

### Environment Configuration

This project uses a `.env` file at the project root as the single source of truth for environment variables. The `.env` file is excluded from git by the opt-in `.gitignore` strategy, so secrets are never committed.

#### Quick Setup

1. Copy the example file: `cp .env.example .env`
2. Edit `.env` and fill in the `GENPRES_URL_ID` value (ask a team member for the production URL ID)

The `.env` file uses standard `KEY=VALUE` format:

```bash
GENPRES_URL_ID=<your-url-id>   # Google Sheets data URL ID (required)
GENPRES_LOG=i                  # Logging level: 0=off, d=debug, i=info, w=warning, e=error
GENPRES_PROD=0                 # Production mode: 0=demo (safe default), 1=production data
GENPRES_DEBUG=1                # Debug mode: 0=off, 1=on
```

#### How It Works

Environment variables are resolved in this priority order (highest first):

1. **Already-set environment variable** (from shell, CI, Docker) — takes precedence
2. **`.env` file** — loaded by shell scripts or `Env.loadDotEnv()` in F#
3. **Hardcoded default in source code** — safe fallback (demo data)

This means you can always override `.env` values by setting an environment variable directly.

#### Loading in Different Contexts

- **Shell**: Source `.env` manually with `set -a; source .env; set +a` before running commands.
- **F# scripts (FSI)**: Scripts call `Informedica.Utils.Lib.Env.loadDotEnv()` which searches upward for `.env` from the current directory.
- **IDEs (Rider, VS Code)**: The `Env.loadDotEnv()` call in scripts ensures variables are available even when the IDE doesn't inherit shell environment.
- **Docker**: Source `.env` before `docker build` to pass `GENPRES_URL_ID` as a build argument.

#### Common Environment Variable Issues

**Missing GENPRES_URL_ID**: Will cause "cannot find column" errors when the application tries to load resources from Google Sheets. Make sure your `.env` file exists and contains a valid `GENPRES_URL_ID`.

**Incorrect GENPRES_PROD value**: Setting this to anything other than `0` in development may cause authentication or data access issues.

For background on this approach, see [Issue #44](https://github.com/informedica/GenPRES/issues/44).
