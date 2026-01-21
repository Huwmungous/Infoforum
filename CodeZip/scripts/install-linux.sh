#!/bin/bash
set -e
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

echo "CodeZip Linux Installer"
echo "========================"

if ! command -v codeship &> /dev/null; then
    echo "Warning: codeship not found. Install with: dotnet tool install -g CodeZip.Cli"
fi

if command -v nautilus &> /dev/null; then
    mkdir -p ~/.local/share/nautilus/scripts
    cp "$SCRIPT_DIR/nautilus/CodeZip.sh" ~/.local/share/nautilus/scripts/CodeZip
    chmod +x ~/.local/share/nautilus/scripts/CodeZip
    echo "âœ“ Nautilus script installed"
fi

if command -v dolphin &> /dev/null; then
    mkdir -p ~/.local/share/kio/servicemenus
    cp "$SCRIPT_DIR/dolphin/codeship.desktop" ~/.local/share/kio/servicemenus/
    echo "âœ“ Dolphin service menu installed"
fi

if command -v nemo &> /dev/null; then
    mkdir -p ~/.local/share/nemo/actions
    cp "$SCRIPT_DIR/nemo/codeship.nemo_action" ~/.local/share/nemo/actions/
    echo "âœ“ Nemo action installed"
fi

echo ""
echo "Done! Restart your file manager for changes to take effect."
