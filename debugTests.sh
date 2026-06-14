#!/usr/bin/env bash

# debugTests - Run all test projects with debug mode
# Usage: ./debugTests.sh

# Load env vars from .env (GENPRES_URL_ID etc.)
set -a; source .env; set +a

echo "Running all test projects in debug mode..."
echo "=========================================="

# Alternative Expecto options you can use:
# --debug --summary --sequenced          # Current: Detailed output with summary, sequential execution
# --debug --summary                       # Detailed output with summary, parallel execution
# --summary                               # Normal output with summary only
# --filter "Agent Logging"                # Run only tests matching filter
# --list-tests                            # List all tests without running them
# --summary-location                      # Include source code locations in summary

# Array of test projects
test_projects=(
    "tests/Informedica.Utils.Tests/Informedica.Utils.Tests.fsproj"
    "tests/Informedica.Agents.Tests/Informedica.Agents.Tests.fsproj"
    "tests/Informedica.Logging.Tests/Informedica.Logging.Tests.fsproj"
    "tests/Informedica.GenUNITS.Tests/Informedica.GenUNITS.Tests.fsproj"
    "tests/Informedica.GenCORE.Tests/Informedica.GenCORE.Tests.fsproj"
    "tests/Informedica.GenSOLVER.Tests/Informedica.GenSOLVER.Tests.fsproj"
    "tests/Informedica.GenFORM.Tests/Informedica.GenFORM.Tests.fsproj"
    "tests/Informedica.GenORDER.Tests/Informedica.GenORDER.Tests.fsproj"
#    "tests/Informedica.ZIndex.Tests/Informedica.ZIndex.Tests.fsproj"
    "tests/Informedica.GenPRES.Server.Tests/Informedica.GenPRES.Server.Tests.fsproj"
)

# Run each test project
for project in "${test_projects[@]}"; do
    echo ""
    echo "Running: $project"
    echo "----------------------------------------"
    dotnet run --project "$project" -- --debug --summary --sequenced
    
    # Check if the command succeeded
    if [ $? -ne 0 ]; then
        echo "ERROR: Failed to run $project"
        exit 1
    fi
done

echo ""
echo "All test projects completed successfully!"
