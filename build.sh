#!/usr/bin/env bash
# Serial build and test — each step must complete and succeed before the next runs.
# Exit code 0 = all steps passed. Non-zero = which step failed is shown above the error.

set -euo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")"

step() {
    echo ""
    echo "================================================================"
    echo "  $*"
    echo "================================================================"
}

step "RESTORE"
dotnet restore NEXUS-STRATUM.slnx

step "BUILD"
dotnet build NEXUS-STRATUM.slnx --no-restore

step "TEST"
dotnet run --project tests/NEXUS-STRATUM.Tests/NEXUS-STRATUM.Tests.fsproj --no-build

step "ALL STEPS PASSED"
