**Chaos Mode v1.3** makes TikTok gifts easier to live with: the switch is remembered, spam can be limited, and the ChatBridge window has a button for TikTok LIVE Studio.

- **Your TikTok gifts switch is remembered.** Turn it on once and it stays on the next time you start the game. Nothing fires until you're hosting a Friends Only or Invite Only lobby and ChatBridge is installed, as before.
- **Gift cooldowns.** On the **TikTok Gifts** tab, set how long the same gift has to wait before it can fire again, and how long the same viewer has to wait. Off, 10 sec, 30 sec, 1 min or 5 min. Your choices are saved. The **Test** buttons ignore cooldowns.
- **Copy for TikTok button.** ChatBridge now has **Copy for OBS** and **Copy for TikTok** next to **Preview**. TikTok LIVE Studio refuses `localhost` links, so the TikTok button copies `http://localtest.me:47801/overlay` instead. Paste it as a link source in LIVE Studio.

**Updating:** replace `ChaosMode.dll` in `BepInEx\plugins` and the files in your `ChatBridge` folder with the ones in this release (`ChaosMode-v1.3.zip` and `ChatBridge-v1.3.zip`), then restart the game and ChatBridge. Your saved settings and gift rules are kept. Launcher users get both through the post-install step.

**Install:**
1. Set up BepInEx once with the community [Setup tool](https://github.com/swyf-modding/Setup). The README has the exact command.
2. Download `ChaosMode-v1.3.zip` and `ChatBridge-v1.3.zip`. Put `ChaosMode.dll` in `BepInEx\plugins` in the game folder, and extract the ChatBridge zip into the game folder. Chat votes and gifts stay locked until ChatBridge is installed.
3. Host a Friends Only or Invite Only lobby and press **F2**.

`ChaosMode-v1.3.zip` is the plugin plus `setup-chatbridge.ps1`, which mod launchers offer as an optional step to install ChatBridge for you.

If Tased, Launch or Knockout do nothing, turn off **Ragdoll Immunity** in the game settings.
