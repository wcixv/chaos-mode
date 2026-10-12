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
        private bool showGifts;
        private string giftKey = "", giftMsg = "";
        private int giftNewEv = 1;                       // index in the list below; 0 = (off)
        private int giftActKind, giftActLine, giftActDir; // 1 = change event, 2 = remove, 3 = add
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
            try { if (File.Exists(CfgPath)) channelInput = File.ReadAllText(CfgPath).Trim(); } catch { }
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
                ApplyGiftAction();
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb != null && kb.f2Key.wasPressedThisFrame) showMenu = !showMenu;
                ui.Poll(showMenu);

                var mc = Metater.MetaCursor.Instance;
                if (mc != null) { if (showMenu) mc.CursorUsers.Add(this); else mc.CursorUsers.Remove(this); }
                InputBlock.Set(this, showMenu);

                bool wantGui = showMenu || Time.unscaledTime < chaos.BannerUntil || (chaos.CreatorMode && chaos.ShowInGame);
                if (gui.enabled != wantGui) gui.enabled = wantGui;

                if (chaos.CreatorMode && !ChatBridgeInstalled()) chaos.CreatorMode = false;
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
            GUILayout.Label("Gift  ->  event   (use < > to change it, X to delete)");
            if (chaos.Gifts.RuleList.Count == 0) GUILayout.Label("  No rules yet. Add one below.");
            foreach (var r in chaos.Gifts.RuleList)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(r.Key, GUILayout.Width(110));
                if (ui.Button("<", GUILayout.Width(26))) { giftActKind = 1; giftActLine = r.Line; giftActDir = -1; }
                GUILayout.Label(r.Event ?? "(off)", GUILayout.Width(130));
                if (ui.Button(">", GUILayout.Width(26))) { giftActKind = 1; giftActLine = r.Line; giftActDir = 1; }
                if (ui.Button("X", GUILayout.Width(26))) { giftActKind = 2; giftActLine = r.Line; }
                GUILayout.EndHorizontal();
            }
            GUILayout.Space(4);
            GUILayout.Label("Add a rule: type the gift's name (Rose, Galaxy...), or a diamond amount like 500 for \"this much or more\".");
            giftKey = ui.TextField("giftkey", giftKey, 40);
            GUILayout.BeginHorizontal();
            if (ui.Button("<", GUILayout.Width(26))) giftNewEv = (giftNewEv - 1 + choices.Count) % choices.Count;
            GUILayout.Label(giftNewEv >= 0 && giftNewEv < choices.Count ? choices[giftNewEv] : "(off)", GUILayout.Width(150));
            if (ui.Button(">", GUILayout.Width(26))) giftNewEv = (giftNewEv + 1) % choices.Count;
            if (ui.Button("Add rule")) giftActKind = 3;
            GUILayout.EndHorizontal();
            if (giftMsg != "") GUILayout.Label("  " + giftMsg);
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
            var area = new Rect(440f, 40f, 360f, Screen.height - 60f);
            GUILayout.BeginArea(area);
            GUILayout.BeginVertical(GUI.skin.box);
            ui.BeginWindow(area);
            GUILayout.Label("<b>Chaos Mode  [F2]</b>");
            string locked = LockReason();
            if (locked != null)
            {
                GUI.color = new Color(1f, 0.55f, 0.5f);
                GUILayout.Label("LOCKED: " + locked);
                GUILayout.Label("Host a Friends Only or Invite Only game to use this.");
                GUI.color = Color.white;
            }
            else
            {
                chaos.Enabled = ui.Toggle(chaos.Enabled, "Chaos Mode ON");
                GUILayout.BeginHorizontal();
                GUILayout.Label("Every:");
                foreach (var sec in new[] { 30f, 60f, 120f })
                    if (ui.Button((chaos.Interval == sec ? "> " : "") + sec + "s")) chaos.Interval = sec;
                GUILayout.EndHorizontal();
                if (chaos.Enabled) GUILayout.Label("Next event in " + Mathf.CeilToInt(chaos.NextEventIn) + "s" + (chaos.LastEvent != "" ? "   (last: " + chaos.LastEvent + ")" : ""));
                if (ui.Button("Trigger random event now")) chaos.TriggerRandom();
                showTest = ui.Toggle(showTest, "Test a specific event");
                if (showTest)
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

                GUILayout.Space(6);
                GUILayout.Label("<b>Creator Mode</b> (chat votes on events, back-to-back)");
                if (ChatBridgeInstalled())
                    chaos.CreatorMode = ui.Toggle(chaos.CreatorMode, "Creator Mode (chat votes)");
                else
                {
                    chaos.CreatorMode = false;
                    GUI.color = new Color(1f, 0.55f, 0.5f);
                    GUILayout.Label("Creator Mode is locked: ChatBridge isn't installed.");
                    GUILayout.Label("Copy the ChatBridge folder from the download into the game folder.");
                    GUI.color = Color.white;
                }
                chaos.ShowInGame = ui.Toggle(chaos.ShowInGame, "Show vote box in game (turn off if using the OBS overlay)");
                GUILayout.BeginHorizontal();
                GUILayout.Label("Vote length:");
                foreach (var sec in new[] { 15f, 25f, 40f })
                    if (ui.Button((chaos.VoteSeconds == sec ? "> " : "") + sec + "s")) chaos.VoteSeconds = sec;
                GUILayout.EndHorizontal();
                GUILayout.Label("Twitch (built in):");
                GUILayout.BeginHorizontal();
                GUILayout.Label("Channel:", GUILayout.Width(60));
                channelInput = ui.TextField("channel", channelInput, 40);
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                if (ui.Button(chaos.Twitch.Connected ? "Reconnect" : "Connect"))
                {
                    chaos.Twitch.Connect(channelInput);
                    try { File.WriteAllText(CfgPath, channelInput ?? ""); } catch { }
                }
                if (ui.Button("Disconnect")) chaos.Twitch.Disconnect();
                GUILayout.EndHorizontal();
                GUILayout.Label("  " + chaos.Twitch.Status);
                GUILayout.Label("YouTube / TikTok / Kick:");
                GUILayout.Label("  " + (chaos.CreatorMode ? chaos.Bridge.Status : "(turn on Creator Mode)"));
                GUILayout.Label("TikTok gifts: " + (chaos.CreatorMode ? chaos.Gifts.Summary : "(turn on Creator Mode)"));
                if (ChatBridgeInstalled()) showGifts = ui.Toggle(showGifts, "Edit gift rules");
                if (showGifts && ChatBridgeInstalled()) DrawGiftEditor();
                if (chaos.CreatorMode && !chaos.Vote.Active && ui.Button("Start next vote now")) chaos.StartVote();
            }
            GUILayout.Label("<size=10>16 punishments, 8 rewards. Friends don't need the mod.</size>");
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
