#!/bin/bash
SELECTED_DIR="$NAUTILUS_SCRIPT_SELECTED_FILE_PATHS"
if [ -z "$SELECTED_DIR" ]; then
    SELECTED_DIR="$NAUTILUS_SCRIPT_CURRENT_URI"
    SELECTED_DIR="${SELECTED_DIR#file://}"
fi
SELECTED_DIR=$(printf '%b' "${SELECTED_DIR//%/\\x}" | head -n1 | tr -d '\n')

if [ ! -d "$SELECTED_DIR" ]; then
    zenity --error --title="CodeZip" --text="Please select a directory."
    exit 1
fi

OUTPUT=$(codeship zip "$SELECTED_DIR" 2>&1)
if [ $? -eq 0 ]; then
    ZIP_PATH=$(echo "$OUTPUT" | grep -oP '[^\s]+\.zip' | tail -1)
    if command -v xclip &> /dev/null; then echo -n "$ZIP_PATH" | xclip -selection clipboard
    elif command -v wl-copy &> /dev/null; then echo -n "$ZIP_PATH" | wl-copy; fi
    zenity --info --title="CodeZip" --text="Created: $ZIP_PATH\n\nPath copied to clipboard."
else
    zenity --error --title="CodeZip Error" --text="$OUTPUT"
fi
