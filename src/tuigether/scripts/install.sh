#!/usr/bin/env bash
# Build tuigether (Release, single-file, framework-dependent) and install onto PATH.
# Usage: src/tuigether/scripts/install.sh [install-dir]    (default: ~/.local/bin)
# Override dir via arg or TUIGETHER_INSTALL_DIR. Windows: use install.ps1.
set -euo pipefail

# Script lives in src/tuigether/scripts, so the project is its parent directory.
project="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
install_dir="${1:-${TUIGETHER_INSTALL_DIR:-$HOME/.local/bin}}"

case "$(uname -s)" in
  Linux) os=linux ;;
  Darwin) os=osx ;;
  *)
    echo "Unsupported OS: $(uname -s). On Windows use install.ps1." >&2
    exit 1
    ;;
esac

case "$(uname -m)" in
  x86_64 | amd64) arch=x64 ;;
  arm64 | aarch64) arch=arm64 ;;
  *)
    echo "Unsupported arch: $(uname -m)" >&2
    exit 1
    ;;
esac

rid="$os-$arch"

echo "Publishing tuigether ($rid)..."
dotnet publish "$project" -c Release -r "$rid" --self-contained false -p:PublishSingleFile=true

binary="$project/bin/Release/net10.0/$rid/publish/Tuigether"
if [ ! -f "$binary" ]; then
  echo "Publish succeeded but binary not found at $binary" >&2
  exit 1
fi

mkdir -p "$install_dir"
target="$install_dir/tuigether"
# Unlink first so we can replace the binary even while a copy is running
# (overwriting in place fails with "Text file busy").
rm -f "$target"
cp "$binary" "$target"
chmod +x "$target"
echo "Installed: $target"

case ":$PATH:" in
  *":$install_dir:"*) ;;
  *)
    echo "Note: $install_dir is not on your PATH. Add it, e.g.:"
    echo "  export PATH=\"$install_dir:\$PATH\""
    ;;
esac
