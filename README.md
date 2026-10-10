<p align="center">
  <img src="docs/assets/banner.png" alt="Chaos Mode" width="100%">
</p>

<h1 align="center">Chaos Mode</h1>
<p align="center"><b>Let your chat wreck your call center.</b><br>
A party/streamer mod for <i>Scam With Your Friends</i>: random events, chat votes from Twitch, YouTube, TikTok and Kick, and an OBS overlay.</p>

<p align="center">
  <a href="../../releases/latest"><b>⬇ Download</b></a> ·
  <a href="#install">Install</a> ·
  <a href="#events">Events</a> ·
  <a href="#creator-mode-chat-votes">Creator Mode</a> ·
  <a href="#troubleshooting">Troubleshooting</a>
</p>

### ▶ [Watch the trailer](https://wcixv.github.io/chaos-mode/#trailer)

> Unofficial fan mod. Not affiliated with the game's developers. The game is in playtest, so updates can break mods.

---

## What it does

- **24 chaos events.** 16 punishments, 8 rewards, from getting tased to doubling every dollar you scam.
- **Chat decides.** Viewers type `1`, `2` or `3` to pick the next event. Votes run back-to-back.
- **Every platform.** Twitch is built in; YouTube, TikTok (via TikFinity) and Kick go through the ChatBridge app.
- **OBS overlay.** An animated vote widget you add as a Browser Source.
- **Friends don't need the mod.** Everything runs on the host and syncs through the game's own networking.
- **Private lobbies only.** Chaos Mode only works when you host a Friends Only or Invite Only lobby.

## Install

1. Download **`ChaosMode-v1.1.zip`** from [Releases](../../releases/latest).
2. In Steam: right-click the game → **Manage → Browse local files**.
3. Copy **everything** from the zip except the `BepInEx-version` folder into that folder (`winhttp.dll` sits next to the game's `.exe`).
4. Launch the game, host a **Friends Only** or **Invite Only** lobby and press **F2**.

**Uninstall:** delete `winhttp.dll` from the game folder.

### Using other mods? (BepInEx version, optional)

The v1.1 zip also has a **BepInEx version** in `BepInEx-version/` for people who run other BepInEx mods. It's the same mod, started by BepInEx instead of the built-in loader. Most people should use the normal install above.

BepInEx needs some fixed runtime files on this game, which the community [Setup tool](https://github.com/swyf-modding/Setup) installs for you. Read `BepInEx-version/README.txt` in the zip for the steps. Thanks to the swyf-modding folks for working this out.

## Events

| Punishments | | Rewards | |
|---|---|---|---|
| 🍺 **Drunk** | Everyone wasted for 30s | 💸 **Money Rain** | Everyone gets $150 |
| 💥 **Knockout** | Everyone hits the floor | 🎰 **Jackpot** | One player wins $750 |
| ⚡ **Tased** | Everyone gets tased | ⏰ **Overtime** | +60s on the clock |
| ⚡ **Taser Party** | Tased every 6s for 25s | 💼 **Payday** | $400 Scamazon money each |
| 🚀 **Launch** | Everyone gets yeeted | ✂️ **Quota Cut** | Quota −25% |
| 📵 **Phone Lines Cut** | Every call hangs up | 💰 **Double Cash** | All scam money ×2 for 60s |
| 📉 **Bankruptcy** | Team loses half its cash | ☕ **Boss On Break** | Clock frozen 30s |
| 🧾 **Tax Man** | Team loses 20% | 🎲 **Double or Nothing** | Team cash doubles… or vanishes |
| 👛 **Pickpocket** | Someone loses their shopping money | | |
| 😡 **Boss Is Furious** | Quota +50% | | |
| 😠 **Boss Is Angry** | Quota +20% | | |
| ⏳ **Time Crunch** | Half the clock disappears | | |
| 🚪 **Evacuation** | Everyone back to the entrance | | |
| 🔥 **You're Fired (jk)** | A very convincing fake firing | | |
| 🤝 **Huddle Up** | Everyone teleports to one player | | |
| 🔀 **Swap!** | Two players swap places | | |

Without Creator Mode, a random event fires every 30, 60 or 120 seconds. The F2 menu also lets you trigger any event yourself.

## Creator Mode (chat votes)

1. Press **F2** and turn on **Creator Mode**.
2. **Twitch:** type your channel in the F2 menu and press **Connect**.
3. **YouTube / TikTok / Kick:** open `ChatBridge\ChatBridge.exe`, fill in your channels and press **START**.
   - TikTok uses **[TikFinity](https://tikfinity.com/)**. Keep it open and turn on its Event API.
4. Chat types **1**, **2** or **3**. The winner fires, then the next vote starts.

### OBS overlay

In ChatBridge, click **Copy** next to *OBS overlay*. In OBS, add a **Browser** source with that URL at **480 × 420**. Click **Preview** in ChatBridge to see a demo vote while you position it. Turn off *Show vote box in game* in the F2 menu so viewers don't see the vote twice.

## Troubleshooting

| Problem | Fix |
|---|---|
| Tased / Launch / Knockout do nothing | Turn **off** *Ragdoll Immunity* in the game settings |
| F2 says LOCKED | You must be the **host** of a **Friends Only** or **Invite Only** lobby |
| Windows warns about ChatBridge | *More info → Run anyway* (it isn't code-signed) |
| TikTok won't connect | Open TikFinity, log in and enable its Event API (port 21213) |
| Kick says "lookup blocked" | Put your `kickChatroomId` in `ChatBridge\chatbridge.json` |
| Something broke after a game update | Check `ModLoader\ModLoader.log` and `Mods\ChaosMode.log`, then open an issue |

## How it works

- **ModLoader:** a tiny [UnityDoorstop](https://github.com/NeighTools/UnityDoorstop) entry point that loads every DLL in `Mods\`. BepInEx can't run on this game's stripped Unity 6 runtime.
- **ChaosMode.dll:** calls the game's own server functions (money, quota, clock, ragdolls, teleports), so every effect syncs to friends without them installing anything.
- **ChatBridge:** a small Windows app that reads stream chat and passes votes to the mod over `localhost:47800`. It also serves the OBS overlay on `localhost:47801`.

Build from source with `build.sh`. You'll need the .NET SDK, Mono and Node 22, plus `GAME_MANAGED` pointing at your game's `..._Data/Managed` folder. The game's DLLs aren't included in this repo.

## License

MIT for this project's code. The release zip also contains UnityDoorstop (LGPL-2.1).
