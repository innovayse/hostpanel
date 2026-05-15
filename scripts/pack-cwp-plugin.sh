#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$SCRIPT_DIR/.."
SRC="$ROOT/backend/src/Innovayse.Providers.CWP"
OUT="$ROOT/dist/innovayse-cwp"
ZIP="$ROOT/dist/innovayse-cwp.zip"

echo "Building CWP plugin..."
dotnet publish "$SRC/Innovayse.Providers.CWP.csproj" \
  -c Release \
  -o "$OUT" \
  --no-self-contained \
  -f net8.0

echo "Copying plugin.json..."
cp "$SRC/plugin.json" "$OUT/plugin.json"

echo "Removing unnecessary files..."
rm -f "$OUT"/*.pdb
rm -f "$OUT"/*.xml
find "$OUT" -name "Microsoft.*.dll" -delete
find "$OUT" -name "System.*.dll" -delete

echo "Creating ZIP at $ZIP..."
rm -f "$ZIP"
python -c "
import zipfile, os, sys
out_dir = sys.argv[1]
zip_path = sys.argv[2]
with zipfile.ZipFile(zip_path, 'w', zipfile.ZIP_DEFLATED) as zf:
    for root, dirs, files in os.walk(out_dir):
        for file in files:
            abs_path = os.path.join(root, file)
            rel_path = os.path.relpath(abs_path, out_dir)
            zf.write(abs_path, rel_path)
" "$OUT" "$ZIP"

echo "Done: $ZIP"
