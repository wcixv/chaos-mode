// Chaos Mode - standalone mod. Press F2 for its menu.
// Only runs when YOU are the host of a Friends Only or Invite Only lobby.
using System;
using System.IO;
using Mirror;
using UnityEngine;

namespace ChaosMode
{
    public class ChaosPlugin : MonoBehaviour
    {
        private readonly Chaos chaos = new Chaos();
        private bool showMenu;
        private bool showTest;
        private ChaosGui gui;
        private readonly ModShared.ClickGui ui = new ModShared.ClickGui();
        private readonly VoteOverlay overlay = new VoteOverlay();
        private Rect window = new Rect(440, 40, 340, 0);
        private GUIStyle bannerStyle, voteStyle;
        private string channelInput = "";
        // gift rule editor (actions are queued and applied in Update so the menu layout never changes mid-draw)
        private int tab;                       // 0 = Chaos, 1 = Stream, 2 = TikTok gifts
        private readonly System.Collections.Generic.Queue<Action> deferred = new System.Collections.Generic.Queue<Action>();
        private void Later(Action a) { deferred.Enqueue(a); }  // menu clicks change state next frame, so the layout never changes mid-draw
        private string giftKey = "", giftMsg = "";
        private int giftNewEv = 1;                       // index in the list below; 0 = (off)
        private int giftActKind, giftActLine, giftActDir; // 1 = change event, 2 = remove, 3 = add, 4 = test
        private string giftActKey = "";
        private System.Collections.Generic.List<string> GiftChoices()
        {
            var l = new System.Collections.Generic.List<string> { "(off)" };
            foreach (var n in chaos.Gifts.EventNames()) l.Add(n);
            return l;
        }
        private static string CfgPath => Path.Combine(Path.GetDirectoryName(typeof(Loader).Assembly.Location), "ChaosMode.cfg");

        private void Awake()
        {
            gui = gameObject.AddComponent<ChaosGui>();
            gui.owner = this;
            gui.enabled = false;
            Loader.Log("Chaos Mode ready. Press F2 in game.");
            string legacy = "";
            try { if (File.Exists(CfgPath)) legacy = File.ReadAllText(CfgPath).Trim(); } catch { }
            channelInput = Settings.Load(chaos, legacy);
        }

        // hostLobbyMode: 0 = Public, 1 = Invite Only, 2 = Friends Only
        private static string LockReason()
        {
            if (!NetworkServer.active) return "You must be the HOST.";
            if (SteamCreateLobbyForServer.hostLobbyMode == 0) return "Public lobby - Chaos Mode disabled.";
            if (ScammerGameManager.Instance == null) return "Start a game first.";
            return null;
        }

        // Creator Mode (streamer mode) needs the ChatBridge app installed next to the game: <game>\ChatBridge\.
        private float bridgeCheckedAt = -100f;
        private bool bridgeInstalled;
        private bool ChatBridgeInstalled()
        {
            if (Time.unscaledTime - bridgeCheckedAt < 2f) return bridgeInstalled;
            bridgeCheckedAt = Time.unscaledTime;
            try
            {
                string dir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "ChatBridge");
                bridgeInstalled = File.Exists(Path.Combine(dir, "ChatBridge.exe")) && File.Exists(Path.Combine(dir, "chatbridge-core.exe"));
            }
            catch { bridgeInstalled = false; }
            return bridgeInstalled;
        }

        private void Update()
        {
            try
            {
                while (deferred.Count > 0) deferred.Dequeue()();
                Settings.SaveIfChanged(chaos, channelInput);
                ApplyGiftAction();
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb != null && kb.f2Key.wasPressedThisFrame) showMenu = !showMenu;
                ui.Poll(showMenu);

                var mc = Metater.MetaCursor.Instance;
                if (mc != null) { if (showMenu) mc.CursorUsers.Add(this); else mc.CursorUsers.Remove(this); }
                InputBlock.Set(this, showMenu);

                bool wantGui = showMenu || Time.unscaledTime < chaos.BannerUntil || (chaos.CreatorMode && chaos.ShowInGame);
                if (gui.enabled != wantGui) gui.enabled = wantGui;

                if ((chaos.CreatorMode || chaos.GiftsOn) && !ChatBridgeInstalled()) { chaos.CreatorMode = false; chaos.GiftsOn = false; }
                chaos.Tick(LockReason() == null);
            }
            catch (Exception e) { Loader.Log("Update error: " + e); }
        }

        private void ApplyGiftAction()
        {
            if (giftActKind == 0) return;
            int kind = giftActKind; giftActKind = 0;
            var choices = GiftChoices();
            if (kind == 1)
            {
                var rule = chaos.Gifts.RuleList.Find(r => r.Line == giftActLine);
                int i = rule.Event == null ? 0 : Mathf.Max(0, choices.IndexOf(rule.Event));
                i = ((i + giftActDir) % choices.Count + choices.Count) % choices.Count;
                chaos.Gifts.SetRuleAt(giftActLine, i == 0 ? null : choices[i]);
            }
            else if (kind == 2) chaos.Gifts.RemoveRuleAt(giftActLine);
            else if (kind == 4) { if (LockReason() == null) chaos.TestGift(giftActKey); else giftMsg = "Can't test right now: " + LockReason(); }
            else if (kind == 3)
            {
                string ev = giftNewEv <= 0 || giftNewEv >= choices.Count ? null : choices[giftNewEv];
                if (chaos.Gifts.AddRule(giftKey, ev)) { giftMsg = "Saved: " + GiftMap.NormalizeKey(giftKey) + " = " + (ev ?? "off"); giftKey = ""; }
                else giftMsg = chaos.Gifts.LastError;
            }
            if (kind != 3 && chaos.Gifts.LastError != "") giftMsg = chaos.Gifts.LastError;
        }

        private void DrawGiftEditor()
        {
            var choices = GiftChoices();
            Say("Gift  ->  what happens", Dim);
            if (chaos.Gifts.RuleList.Count == 0) Say("  No rules yet. Add one below.", Dim);
            foreach (var r in chaos.Gifts.RuleList)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(r.Key, GUILayout.Width(95));
                if (ui.Button("<", GUILayout.Width(26))) { giftActKind = 1; giftActLine = r.Line; giftActDir = -1; }
                GUILayout.Label(Nice(r.Event), GUILayout.Width(105));
                if (ui.Button(">", GUILayout.Width(26))) { giftActKind = 1; giftActLine = r.Line; giftActDir = 1; }
                if (ui.Button("Test", GUILayout.Width(44))) { giftActKind = 4; giftActKey = r.Key; }
                if (ui.Button("Delete", GUILayout.Width(56))) { giftActKind = 2; giftActLine = r.Line; }
                GUILayout.EndHorizontal();
            }
            GUILayout.Space(8);
            Say("<b>Add a new gift rule</b>");
            Say("1. Type the gift's name (like Rose or Galaxy). Or type a number, like 500, to mean any gift worth that many diamonds or more.", Dim);
            giftKey = ui.TextField("giftkey", giftKey, 40);
            Say("2. Pick what should happen:", Dim);
            GUILayout.BeginHorizontal();
            if (ui.Button("<", GUILayout.Width(26))) giftNewEv = (giftNewEv - 1 + choices.Count) % choices.Count;
            GUILayout.Label(Nice(giftNewEv >= 0 && giftNewEv < choices.Count ? (giftNewEv == 0 ? null : choices[giftNewEv]) : null), GUILayout.Width(190));
            if (ui.Button(">", GUILayout.Width(26))) giftNewEv = (giftNewEv + 1) % choices.Count;
            GUILayout.EndHorizontal();
            Say("3. Save it:", Dim);
            if (ui.Button("Add this rule")) giftActKind = 3;
            if (giftMsg != "") Say(giftMsg, giftMsg.StartsWith("Saved") ? Good : Bad);
        }

        private static string Nice(string ev) => ev == null ? "do nothing" : ev;

        private static readonly Color Good = new Color(0.45f, 0.9f, 0.55f), Bad = new Color(1f, 0.55f, 0.5f), Dim = new Color(1f, 1f, 1f, 0.65f);
        private static void Say(string text, Color? color = null)
        {
            var old = GUI.color;
            if (color.HasValue) GUI.color = color.Value;
            GUILayout.Label(text);
            GUI.color = old;
        }

        private void Tabs()
        {
            string[] names = { "Chaos", "Stream", "TikTok Gifts" };
            GUILayout.BeginHorizontal();
            for (int i = 0; i < names.Length; i++)
            {
                int idx = i;
                if (ui.Button((tab == i ? "[ " + names[i] + " ]" : names[i]))) Later(() => tab = idx);
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(4);
        }

        private static string Secs(float s) => s >= 60f ? (s / 60f) + " min" : s + " sec";

        private void DrawChaosTab(string locked)
        {
            if (locked != null)
            {
                Say("Chaos Mode is locked right now.", Bad);
                Say(locked, Bad);
                Say("To use it, host a Friends Only or Invite Only lobby and start a game.", Dim);
                return;
            }
            Say("Random events hit everyone in your lobby, on a timer.", Dim);
            chaos.Enabled = ui.Toggle(chaos.Enabled, "Random events");
            GUILayout.BeginHorizontal();
            GUILayout.Label("One every:");
            foreach (var sec in new[] { 30f, 60f, 120f })
                if (ui.Button((chaos.Interval == sec ? "[ " + Secs(sec) + " ]" : Secs(sec)))) chaos.Interval = sec;
            GUILayout.EndHorizontal();
            if (chaos.Enabled) Say("Next event in " + Mathf.CeilToInt(chaos.NextEventIn) + " seconds.", Good);
            if (chaos.LastEvent != "") Say("Last event: " + chaos.LastEvent, Dim);
            GUILayout.Space(6);
            if (ui.Button("Trigger a random event now")) chaos.TriggerRandom();
            bool open = showTest;
            if (ui.Button(open ? "Hide the event list" : "Pick a specific event...")) Later(() => showTest = !showTest);
            if (open)
            {
                int col = 0;
                GUILayout.BeginHorizontal();
                foreach (var name in chaos.EventNames)
                {
                    if (col == 3) { GUILayout.EndHorizontal(); GUILayout.BeginHorizontal(); col = 0; }
                    if (ui.Button(name, GUILayout.Width(108))) chaos.TriggerByName(name);
                    col++;
                }
                GUILayout.EndHorizontal();
            }
        }

        private void DrawStreamTab(string locked)
        {
            Say("Let your chat decide what happens next. Viewers type 1, 2 or 3 to vote.", Dim);
            if (!ChatBridgeInstalled())
            {
                chaos.CreatorMode = false; chaos.GiftsOn = false;
                Say("Chat votes are locked: ChatBridge isn't installed.", Bad);
                Say("Extract the ChatBridge download into your game folder. You should end up with a ChatBridge folder next to the game, with ChatBridge.exe inside it.", Dim);
                return;
            }
            if (locked != null) { Say("Locked right now: " + locked, Bad); return; }

            chaos.CreatorMode = ui.Toggle(chaos.CreatorMode, "Chat votes");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Each vote lasts:");
            foreach (var sec in new[] { 15f, 25f, 40f })
                if (ui.Button((chaos.VoteSeconds == sec ? "[ " + Secs(sec) + " ]" : Secs(sec)))) chaos.VoteSeconds = sec;
            GUILayout.EndHorizontal();
            chaos.ShowInGame = ui.Toggle(chaos.ShowInGame, "Show the vote box in the game");
            Say("Turn this off if you use the OBS overlay, so viewers don't see it twice.", Dim);
            if (chaos.CreatorMode && !chaos.Vote.Active && ui.Button("Start the next vote now")) chaos.StartVote();

            GUILayout.Space(8);
            Say("<b>Where your chat comes from</b>");
            Say("Twitch (built in): type your channel name", Dim);
            channelInput = ui.TextField("channel", channelInput, 40);
            GUILayout.BeginHorizontal();
            if (ui.Button(chaos.Twitch.Connected ? "Reconnect" : "Connect"))
            {
                chaos.Twitch.Connect(channelInput);
                try { File.WriteAllText(CfgPath, channelInput ?? ""); } catch { }
            }
            if (ui.Button("Disconnect")) chaos.Twitch.Disconnect();
            GUILayout.EndHorizontal();
            Say(chaos.Twitch.Status, chaos.Twitch.Connected ? Good : Dim);
            GUILayout.Space(4);
            Say("YouTube, TikTok and Kick need the ChatBridge app. To open it: go to your game folder (Steam: right-click the game > Manage > Browse local files), open the ChatBridge folder and run ChatBridge.exe.", Dim);
            bool bridgeWanted = chaos.CreatorMode || chaos.GiftsOn;
            Say(bridgeWanted ? chaos.Bridge.Status : "Turn on Chat votes (or TikTok gifts) to connect to ChatBridge.", bridgeWanted && chaos.Bridge.Connected ? Good : Dim);
        }

        private void DrawGiftsTab(string locked)
        {
            Say("When a viewer sends a TikTok gift, make something happen in the game.", Dim);
            if (!ChatBridgeInstalled())
            {
                Say("Needs ChatBridge. Extract the ChatBridge download into your game folder first. Its ChatBridge folder holds ChatBridge.exe.", Bad);
                return;
            }
            if (locked != null) { Say("Locked right now: " + locked, Bad); }
            else
            {
                if (ui.Button((chaos.GiftsOn ? "[ON]  " : "[off]  ") + "TikTok gifts")) Later(() => chaos.GiftsOn = !chaos.GiftsOn);
                Say("This is separate from Chat votes. You can use gifts only, votes only, or both.", Dim);
            }
            if (!chaos.GiftsOn) Say("Turn TikTok gifts on to let gifts trigger the rules below.", Dim);
            else if (!chaos.Bridge.Connected) Say("ChatBridge isn't running yet. Go to your game folder, open the ChatBridge folder and run ChatBridge.exe. Then connect TikTok through TikFinity.", Bad);
            else if (chaos.Bridge.Version == "") Say("Your ChatBridge is out of date and can't send gifts. Download the newest ChatBridge zip from the releases page and extract it into the game folder (replace the files), then restart ChatBridge.", Bad);
            else Say("Ready. ChatBridge " + chaos.Bridge.Version + " is connected. Gifts will trigger the rules below.", Good);
            Say("Gifts only do something while a workday is running.", Dim);
            Say("Gifts received this session: " + chaos.GiftsReceived, Dim);
            if (chaos.LastGift != "") Say("Last gift: " + chaos.LastGift, chaos.LastGiftFired ? Good : Bad);
            Say("Press Test next to a rule to try it without waiting for a gift.", Dim);
            GUILayout.Space(6);
            DrawGiftEditor();
        }

        private void OnDestroy() { chaos.Twitch.Disconnect(); chaos.Bridge.KeepAlive(false); }

        private void OnDisable() { var mc = Metater.MetaCursor.Instance; if (mc != null) mc.CursorUsers.Remove(this); InputBlock.Set(this, false); }

        internal void DrawGui()
        {
            DrawBanner();
            overlay.Draw(chaos);
            if (showMenu) { DrawWindow(); ui.EndFrame(); }
        }

        private void DrawBanner()
        {
            if (Time.unscaledTime > chaos.BannerUntil || string.IsNullOrEmpty(chaos.Banner)) return;
            if (bannerStyle == null)
            {
                bannerStyle = new GUIStyle(GUI.skin.box) { fontSize = 22, alignment = TextAnchor.MiddleCenter, wordWrap = true };
                bannerStyle.normal.textColor = new Color(1f, 0.35f, 0.25f);
            }
            float w = Mathf.Min(900f, Screen.width - 40f);
            GUI.Box(new Rect((Screen.width - w) / 2f, 30f, w, 70f), new GUIContent(chaos.Banner), bannerStyle);
        }

        private void DrawVote()
        {
            if (!chaos.CreatorMode) return;
            var v = chaos.Vote;
            if (voteStyle == null)
            {
                voteStyle = new GUIStyle(GUI.skin.box) { fontSize = 20, alignment = TextAnchor.UpperLeft, wordWrap = true };
                voteStyle.normal.textColor = Color.white;
            }
            string txt; int lines;
            if (v.Active)
            {
                txt = "CHAT VOTE - type 1, 2 or 3!   " + (chaos.WaitingForDay ? "(paused until the workday)" : Mathf.CeilToInt(v.SecondsLeft) + "s") + "\n";
                for (int i = 0; i < v.Options.Length; i++)
                {
                    int pct = v.TotalVotes > 0 ? Mathf.RoundToInt(100f * v.Counts[i] / v.TotalVotes) : 0;
                    txt += "\n" + (i + 1) + ".  " + v.Options[i] + "   -   " + v.Counts[i] + " (" + pct + "%)";
                }
                lines = v.Options.Length + 2;
            }
            else
            {
                txt = chaos.WaitingForDay ? "CHAT VOTE - starts when the workday begins" : "CHAT VOTE - next vote in " + Mathf.CeilToInt(chaos.NextVoteIn) + "s";
                if (chaos.LastResult != "") txt += "\n\nLast: " + chaos.LastResult;
                lines = chaos.LastResult != "" ? 3 : 1;
            }
            float w = 460f, h = 30f + 30f * lines;
            GUI.Box(new Rect(Screen.width - w - 20f, 120f, w, h), new GUIContent(txt), voteStyle);
        }

        private void DrawWindow()
        {
            var area = new Rect(440f, 40f, 400f, Screen.height - 60f);
            GUILayout.BeginArea(area);
            GUILayout.BeginVertical(GUI.skin.box);
            ui.BeginWindow(area);
            GUILayout.Label("<b>Chaos Mode</b>   (press F2 to close)");
            string locked = LockReason();
            Tabs();
            if (tab == 0) DrawChaosTab(locked);
            else if (tab == 1) DrawStreamTab(locked);
            else DrawGiftsTab(locked);
            GUILayout.Space(6);
            Say("24 events: 16 punishments, 8 rewards. Your friends don't need the mod.", Dim);
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }
    }

    // OnGUI only exists while the menu or a banner is showing, so it can't block the in-game computer.
    public class ChaosGui : MonoBehaviour
    {
        public ChaosPlugin owner;
        private void OnGUI() { if (owner != null) owner.DrawGui(); }
    }

    public static class ModMain
    {
        public static void OnGameReady()
        {
            var go = new GameObject("ChaosMode");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            go.AddComponent<ChaosPlugin>();
        }
    }

    public static class Loader
    {
        public static void Log(string msg)
        {
            try
            {
                string path = Path.Combine(Path.GetDirectoryName(typeof(Loader).Assembly.Location), "ChaosMode.log");
                File.AppendAllText(path, DateTime.Now.ToString("HH:mm:ss") + "  " + msg + Environment.NewLine);
            }
            catch { }
        }
    }
}

namespace ChaosMode
{
    // Stops the player moving/looking while a mod menu is open (same blockers the in-game computer uses).
    public static class InputBlock
    {
        public static void Set(MonoBehaviour who, bool on)
        {
            var pc = Metater.MetaPlayerController.Instance;
            if (pc == null || pc.blocker == null) return;
            var b = pc.blocker;
            if (on) { b.InputBlockers.Add(who); b.MoveBlockers.Add(who); b.JumpBlockers.Add(who); b.StrafeCameraBlockers.Add(who); }
            else { b.InputBlockers.Remove(who); b.MoveBlockers.Remove(who); b.JumpBlockers.Remove(who); b.StrafeCameraBlockers.Remove(who); }
        }
    }
}
