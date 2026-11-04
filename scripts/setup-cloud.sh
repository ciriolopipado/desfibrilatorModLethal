#!/usr/bin/env bash
set -euo pipefail
desfibrilator_repo_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
desfibrilator_sdk_dir=/workspace/.tools/dotnet
export DOTNET_CLI_HOME=/workspace/.tools/dotnet-home
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
if [ ! -x "$desfibrilator_sdk_dir/dotnet" ]; then
    mkdir -p /workspace/.tools
    desfibrilator_installer=$(mktemp /tmp/desfibrilator-dotnet.XXXXXX)
    trap 'rm -f "$desfibrilator_installer"' EXIT
    curl -fsSL https://dot.net/v1/dotnet-install.sh -o "$desfibrilator_installer"
    printf '%s  %s\n' '082f7685e156738a1b2e2ed8381a621870d4ce8e8c59278034556f05c186eb2e' "$desfibrilator_installer" | sha256sum --check --status
    bash "$desfibrilator_installer" --version 8.0.427 --install-dir "$desfibrilator_sdk_dir" --no-path
fi
export PATH="$desfibrilator_sdk_dir:$PATH"
cd "$desfibrilator_repo_dir"
python3 scripts/prepare-reference-snapshot.py
dotnet build src/Desfibrilator/Desfibrilator.csproj -c Release --nologo
dotnet run --project tests/Desfibrilator.Tests -c Release --no-launch-profile
python3 scripts/package.py
