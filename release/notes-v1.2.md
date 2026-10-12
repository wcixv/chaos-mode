**Chaos Mode v1.2** adds TikTok gifts.

- **Gifts trigger events.** With Creator Mode on and TikTok connected through TikFinity, you choose what each gift does in the F2 menu (**Edit gift rules**: add, change or delete rules without leaving the game) or in `ChatBridge\gifts.txt`. Example: `Galaxy = END DAY` ends the workday on the spot, `Rose = MONEY RAIN` makes it rain. Rules by gift name or by diamond value (`>= 500 = JACKPOT`). The file is created for you the first time, reloads while the game runs, and the menu keeps your comments.
- **New gift-only event: END DAY.** Clocks everyone out right now.
- A gift combo fires once when it ends, gifts between days do nothing, and a gift storm is spaced out to one event every 2 seconds.
- ChatBridge forwards TikTok gifts to the game (both through TikFinity and the direct connection).

**Updating from v1.1:** replace `ChaosMode.dll` in `BepInEx\plugins` with the one in `ChaosMode-v1.2.zip`, and replace the `ChatBridge` folder contents with `ChatBridge-v1.2.zip` (your `chatbridge.json` settings are kept). Launcher users get both through the post-install step.

**Install:**
1. Set up BepInEx once with the community [Setup tool](https://github.com/swyf-modding/Setup). The README has the exact command.
2. Download `ChaosMode-v1.2.zip` and `ChatBridge-v1.2.zip`. Put `ChaosMode.dll` in `BepInEx\plugins` in the game folder, and extract the ChatBridge zip into the game folder. Creator Mode stays locked until ChatBridge is installed.
3. Host a Friends Only or Invite Only lobby and press **F2**.

`ChaosMode-v1.2.zip` is the plugin plus `setup-chatbridge.ps1`, which mod launchers offer as an optional step to install ChatBridge for you.

- 24 chaos events (16 punishments, 8 rewards) plus END DAY for gifts
- Creator Mode: chat votes on Twitch, YouTube, TikTok (via TikFinity) and Kick
- ChatBridge app and an animated OBS overlay
- Friends don't need to install anything

If Tased, Launch or Knockout do nothing, turn off **Ragdoll Immunity** in the game settings.
