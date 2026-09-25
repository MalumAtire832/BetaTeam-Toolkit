#!/usr/bin/env bash
# Sets up the local Research/ workspace used for reverse engineering the game's formats:
# links the game install, unpacks its archives, collects every binary for Ghidra, and
# registers the pyghidra MCP server with Claude Code for this repository.
#
# Usage: scripts/setup-research.sh <game-directory> [ghidra-install-directory]
#
# Safe to run again: existing extracted files and the Ghidra project are kept.

set -euo pipefail
shopt -s nullglob nocaseglob

if [[ $# -lt 1 ]]; then
    echo "Usage: $0 <game-directory> [ghidra-install-directory]" >&2
    exit 1
fi

REPO=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
RESEARCH="$REPO/Research"
GAME=$(realpath "$1")
GHIDRA=${2:-${GHIDRA_INSTALL_DIR:-}}

# Default to the newest Ghidra under /opt/ghidra.
if [[ -z "$GHIDRA" ]]; then
    GHIDRA=$(ls -d /opt/ghidra/ghidra_*_PUBLIC 2>/dev/null | sort -V | tail -n 1 || true)
fi

archives=("$GAME"/*.pac)
if [[ ${#archives[@]} -eq 0 ]]; then
    echo "No .pac archives found in $GAME, is this the game's install directory?" >&2
    exit 1
fi

echo "==> Creating $RESEARCH"
mkdir -p "$RESEARCH"/{extracted,binaries,ghidra,notes,patterns}
ln -sfn "$GAME" "$RESEARCH/game"

if [[ -z "$(ls -A "$RESEARCH/extracted")" ]]; then
    echo "==> Unpacking archives"
    dotnet run --project "$REPO/Malumware.BetaTeam.Cli" -- unpack "$RESEARCH/game" "$RESEARCH/extracted"
else
    echo "==> Archives already unpacked, skipping"
fi

echo "==> Linking binaries"
binaries=("$GAME"/*.dll "$GAME"/*.exe "$RESEARCH/extracted/Bhvr"/*.dll)
for binary in "${binaries[@]}"; do
    ln -sfn "$(realpath "$binary")" "$RESEARCH/binaries/$(basename "$binary")"
done
echo "    ${#binaries[@]} binaries"

if ! command -v uvx >/dev/null; then
    echo "==> Skipping Ghidra: uv (uvx) is not installed, see https://docs.astral.sh/uv/"
elif [[ ! -x "$GHIDRA/support/analyzeHeadless" ]]; then
    echo "==> Skipping Ghidra: no installation found, pass it as the second argument or set GHIDRA_INSTALL_DIR"
else
    pyghidra_args=(pyghidra-mcp --project-path "$RESEARCH/ghidra" --project-name alphateam "$RESEARCH/binaries")

    # A new project imports every binary before the MCP server answers, which takes longer than Claude Code
    # waits for a server to start. Analysing up front makes every later start take seconds.
    if [[ ! -f "$RESEARCH/ghidra/alphateam.gpr" ]]; then
        echo "==> Analysing binaries with Ghidra (takes around half an hour)"
        log="$RESEARCH/ghidra/initial-analysis.log"
        GHIDRA_INSTALL_DIR="$GHIDRA" setsid uvx "${pyghidra_args[@]}" \
            --transport streamable-http --port 8765 --wait-for-analysis > "$log" 2>&1 &
        server=$!

        # The server only starts listening once the analysis is complete.
        until grep -q "Uvicorn running" "$log"; do
            if ! kill -0 "$server" 2>/dev/null; then
                echo "    Analysis failed, see $log" >&2
                exit 1
            fi
            sleep 10
        done
        kill -- "-$server"
        wait "$server" 2>/dev/null || true
        echo "    Done, log written to $log"
    else
        echo "==> Ghidra project already exists, skipping analysis"
    fi

    echo "==> Registering pyghidra MCP server"
    if command -v claude >/dev/null; then
        (
            cd "$REPO"
            claude mcp remove --scope local pyghidra >/dev/null 2>&1 || true
            GHIDRA_INSTALL_DIR="$GHIDRA" claude mcp add --scope local pyghidra -e GHIDRA_INSTALL_DIR="$GHIDRA" -- \
                uvx "${pyghidra_args[@]}" >/dev/null
        )
        echo "    Registered with $GHIDRA"
    else
        echo "    Skipped: Claude Code (claude) is not installed"
    fi
fi

echo "==> Done"
