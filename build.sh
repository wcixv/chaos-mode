#!/usr/bin/env bash
# Builds Chaos Mode, the mod loader and ChatBridge.
# Needs: .NET SDK (for csc), Mono (mcs, for the ChatBridge window), Node 22 + npm.
# GAME_MANAGED must point at "<game>/Scam With Your Friends_Data/Managed" (game DLLs are NOT included in this repo).
set -euo pipefail
: "${GAME_MANAGED:?Set GAME_MANAGED to the game's ..._Data/Managed folder}"
CSC=$(ls /usr/lib/dotnet/sdk/*/Roslyn/bincore/csc.dll /usr/share/dotnet/sdk/*/Roslyn/bincore/csc.dll 2>/dev/null | head -1)
M="$GAME_MANAGED"
OUT=out; mkdir -p "$OUT/ModLoader" "$OUT/Mods" "$OUT/ChatBridge"
REFS=()
for d in mscorlib System System.Core UnityEngine UnityEngine.CoreModule UnityEngine.IMGUIModule UnityEngine.TextRenderingModule \
         UnityEngine.PhysicsModule Unity.InputSystem Mirror ScriptsAssDef Assembly-CSharp com.rlabrecque.steamworks.net; do
  REFS+=("-r:$M/$d.dll")
done
# compile against the game's own (stripped) runtime so we only use APIs that actually exist in-game
dotnet "$CSC" -nologo -target:library -nostdlib -noconfig -langversion:9 -optimize -out:"$OUT/ModLoader/ModLoader.dll" \
  -r:"$M/mscorlib.dll" -r:"$M/System.dll" -r:"$M/UnityEngine.CoreModule.dll" src/ModLoader/ModLoader.cs
dotnet "$CSC" -nologo -target:library -nostdlib -noconfig -langversion:9 -optimize -nowarn:414,169 -out:"$OUT/Mods/ChaosMode.dll" \
  "${REFS[@]}" src/ChaosMode/*.cs src/Shared/ClickGui.cs

# BepInEx build (same code, plus a BepInEx entry point). Set BEPINEX_CORE to <game>/BepInEx/core to build it.
if [ -n "${BEPINEX_CORE:-}" ]; then
  mkdir -p "$OUT/BepInEx/plugins/ChaosMode"
  dotnet "$CSC" -nologo -target:library -nostdlib -noconfig -langversion:9 -optimize -nowarn:414,169 -define:BEPINEX \
    -out:"$OUT/BepInEx/plugins/ChaosMode/ChaosMode.dll" "${REFS[@]}" -r:"$BEPINEX_CORE/BepInEx.dll" \
    src/ChaosMode/*.cs src/Shared/ClickGui.cs
fi

# ChatBridge window (.NET Framework 4.x WinForms)
mcs -target:winexe -sdk:4.5 -optimize -win32icon:src/ChatBridge/gui/icon.ico -r:System.Windows.Forms.dll -r:System.Drawing.dll \
  -r:System.Web.Extensions.dll -out:"$OUT/ChatBridge/ChatBridge.exe" src/ChatBridge/gui/ChatBridgeGui.cs

# ChatBridge core (Node, bundled then packaged as a Windows exe)
( cd src/ChatBridge/core && npm install --no-audit --no-fund \
  && npx --yes esbuild@0.25.0 index.js --bundle --platform=node --target=node22 --format=cjs --loader:.html=text --outfile=dist/bundle.js \
  && echo '{"name":"chatbridge","version":"1.0.0","bin":"bundle.js"}' > dist/package.json \
  && cd dist && npx --yes @yao-pkg/pkg@6.23.0 . --targets node22-win-x64 --no-bytecode --public-packages "*" --public --output chatbridge-core.exe )
cp src/ChatBridge/core/dist/chatbridge-core.exe "$OUT/ChatBridge/"
echo "Built into $OUT/. Add UnityDoorstop 4.5.0 (winhttp.dll + doorstop_config.ini with target_assembly=ModLoader\\ModLoader.dll)."
echo "BepInEx build (if BEPINEX_CORE was set): needs BepInEx 5.4.23.5 + the unstripped corlib from https://github.com/swyf-modding/Setup"
