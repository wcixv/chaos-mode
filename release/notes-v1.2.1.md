**Chaos Mode v1.2.1** makes TikTok gifts easier to set up and to fix when they don't fire.

- **Gift troubleshooting in the menu.** The **TikTok Gifts** tab now says exactly what's missing (Chat votes off, ChatBridge too old, ChatBridge not running), counts the gifts it has received, and shows what happened to the last one (fired, no rule for that gift, or no workday running).
- **Test button.** Press **Test** next to any gift rule to try its event right away, without waiting for a real gift.
- **ChatBridge reads more gift formats.** It now understands several spellings of TikTok gift data and logs the raw gift data it receives, so problems are easy to spot. **Use the new ChatBridge zip with this plugin.** ChatBridge v1.1 can't pass gifts to the game at all, and the menu now tells you when your ChatBridge is too old.

**Updating:** replace `ChaosMode.dll` in `BepInEx\plugins` and the files in your `ChatBridge` folder with the ones in this release (`ChaosMode-v1.2.1.zip` and `ChatBridge-v1.2.1.zip`), then restart the game and ChatBridge. Your saved settings and gift rules are kept. Launcher users get both through the post-install step.

**Install:**
1. Set up BepInEx once with the community [Setup tool](https://github.com/swyf-modding/Setup). The README has the exact command.
2. Download `ChaosMode-v1.2.1.zip` and `ChatBridge-v1.2.1.zip`. Put `ChaosMode.dll` in `BepInEx\plugins` in the game folder, and extract the ChatBridge zip into the game folder. Chat votes stay locked until ChatBridge is installed.
3. Host a Friends Only or Invite Only lobby and press **F2**.

`ChaosMode-v1.2.1.zip` is the plugin plus `setup-chatbridge.ps1`, which mod launchers offer as an optional step to install ChatBridge for you.

If Tased, Launch or Knockout do nothing, turn off **Ragdoll Immunity** in the game settings.
