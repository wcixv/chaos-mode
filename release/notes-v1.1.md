**Chaos Mode v1.1** now runs on BepInEx.

**Install:**
1. Set up BepInEx once with the community [Setup tool](https://github.com/swyf-modding/Setup). It installs BepInEx and the fixed runtime files this game needs. The README has the exact command.
2. Download `ChaosMode-v1.1.zip` and `ChatBridge-v1.1.zip`. Put `ChaosMode.dll` in `BepInEx\plugins` in the game folder (Steam → Manage → Browse local files), and extract the ChatBridge zip into the game folder. Creator Mode (chat votes) stays locked until ChatBridge is installed.

`ChaosMode-v1.1.zip` is the plugin plus `setup-chatbridge.ps1`, so mod launchers can install it. ChatBridge is its own download because it's a separate app. The script downloads it, checks its checksum and installs it for you (launchers offer it as an optional step).
3. Host a Friends Only or Invite Only lobby and press **F2**.

**Coming from v1.0?** Delete `winhttp.dll`, `doorstop_config.ini`, the `ModLoader` folder and `Mods\ChaosMode.dll` first.

- 24 chaos events (16 punishments, 8 rewards)
- Creator Mode: chat votes on Twitch, YouTube, TikTok (via TikFinity) and Kick
- ChatBridge app and an animated OBS overlay
- Friends don't need to install anything

If Tased, Launch or Knockout do nothing, turn off **Ragdoll Immunity** in the game settings.
