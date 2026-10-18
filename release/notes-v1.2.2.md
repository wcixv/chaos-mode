**Chaos Mode v1.2.2** lets you run TikTok gifts on their own, and shows gifts on the stream overlay.

- **Gifts are separate from Chat votes.** The **TikTok Gifts** tab has its own **TikTok gifts** switch. Use gifts only, votes only, or both. You no longer have to turn on Chat votes to get gifts.
- **Gifts show on the overlay.** When a gift fires an event, a card slides in under the vote box with who sent it and what happened. It works with Chat votes off too. **Preview** in ChatBridge shows a demo gift card.
- **TikTok LIVE Studio works.** LIVE Studio refuses `localhost` links, so use `http://localtest.me:47801/overlay` there. ChatBridge now prints this link in its window when it starts.

**Updating:** replace `ChaosMode.dll` in `BepInEx\plugins` and the files in your `ChatBridge` folder with the ones in this release (`ChaosMode-v1.2.2.zip` and `ChatBridge-v1.2.2.zip`), then restart the game and ChatBridge. You need the new ChatBridge for the gift cards on the overlay. Your saved settings and gift rules are kept. Launcher users get both through the post-install step.

**Install:**
1. Set up BepInEx once with the community [Setup tool](https://github.com/swyf-modding/Setup). The README has the exact command.
2. Download `ChaosMode-v1.2.2.zip` and `ChatBridge-v1.2.2.zip`. Put `ChaosMode.dll` in `BepInEx\plugins` in the game folder, and extract the ChatBridge zip into the game folder. Chat votes and gifts stay locked until ChatBridge is installed.
3. Host a Friends Only or Invite Only lobby and press **F2**.

`ChaosMode-v1.2.2.zip` is the plugin plus `setup-chatbridge.ps1`, which mod launchers offer as an optional step to install ChatBridge for you.

If Tased, Launch or Knockout do nothing, turn off **Ragdoll Immunity** in the game settings.
