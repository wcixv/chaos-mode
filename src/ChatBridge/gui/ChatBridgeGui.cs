// ChatBridge - window app. Edits chatbridge.json, runs chatbridge-core.exe in the background
// (no console window) and shows live status for each platform plus incoming votes.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

[assembly: System.Reflection.AssemblyTitle("ChatBridge")]
[assembly: System.Reflection.AssemblyProduct("ChatBridge for Chaos Mode")]
[assembly: System.Reflection.AssemblyDescription("Sends stream chat votes to the Chaos Mode game mod")]
[assembly: System.Reflection.AssemblyVersion("1.1.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("1.1.0.0")]

namespace ChatBridge
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    class Theme
    {
        public static readonly Color Bg = Color.FromArgb(16, 16, 22);
        public static readonly Color Panel = Color.FromArgb(26, 26, 35);
        public static readonly Color Field = Color.FromArgb(36, 36, 48);
        public static readonly Color Text = Color.FromArgb(235, 235, 240);
        public static readonly Color Dim = Color.FromArgb(150, 150, 165);
        public static readonly Color Accent = Color.FromArgb(255, 77, 97);
        public static readonly Color Good = Color.FromArgb(77, 230, 140);
        public static readonly Color Warn = Color.FromArgb(255, 200, 70);
        public static readonly Color Bad = Color.FromArgb(255, 90, 90);
        public static readonly Color Off = Color.FromArgb(90, 90, 105);
    }

    class Dot : Control
    {
        private Color col = Theme.Off;
        public Color DotColor { get { return col; } set { col = value; Invalidate(); } }
        public Dot() { SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true); Size = new Size(12, 12); BackColor = Color.Transparent; }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (var b = new SolidBrush(col)) e.Graphics.FillEllipse(b, 0, 0, Width - 1, Height - 1);
        }
    }

    class Row
    {
        public string Name;
        public Dot Dot;
        public Label Status;
        public TextBox Input;
        public CheckBox Check;
    }

    class MainForm : Form
    {
        readonly string dir = AppDomain.CurrentDomain.BaseDirectory;
        string CfgPath { get { return Path.Combine(dir, "chatbridge.json"); } }
        string CorePath { get { return Path.Combine(dir, "chatbridge-core.exe"); } }

        readonly Dictionary<string, Row> rows = new Dictionary<string, Row>();
        Row gameRow;
        Button startBtn;
        ListBox logBox;
        Process core;
        Thread reader;
        volatile bool running;
        Dictionary<string, object> cfg = new Dictionary<string, object>();
        System.Windows.Forms.Timer uiTimer;
        readonly Queue<string> pendingLog = new Queue<string>();
        readonly Dictionary<string, string> pendingStatus = new Dictionary<string, string>();
        int pendingGame = -1;
        readonly object gate = new object();
        int votes;
        Label voteCounter;

        public MainForm()
        {
            Text = "ChatBridge";
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = new Font("Segoe UI", 10f);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            ClientSize = new Size(520, 700);
            StartPosition = FormStartPosition.CenterScreen;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            var title = new Label { Text = "ChatBridge", Font = new Font("Segoe UI", 20f, FontStyle.Bold), ForeColor = Theme.Text, AutoSize = true, Location = new Point(24, 18) };
            var sub = new Label { Text = "Sends your stream chat into Chaos Mode votes", ForeColor = Theme.Dim, AutoSize = true, Location = new Point(27, 58) };
            var bar = new Panel { BackColor = Theme.Accent, Location = new Point(0, 0), Size = new Size(520, 4) };
            Controls.AddRange(new Control[] { bar, title, sub });

            var card = new Panel { BackColor = Theme.Panel, Location = new Point(20, 92), Size = new Size(480, 290) };
            Controls.Add(card);
            int y = 14;
            rows["YouTube"] = MakeRow(card, "YouTube", "@handle, channel ID or live link", ref y, false);
            rows["TikTok"] = MakeRow(card, "TikTok", null, ref y, true);
            rows["Kick"] = MakeRow(card, "Kick", "channel name", ref y, false);
            rows["Twitch"] = MakeRow(card, "Twitch", "channel (optional, mod has it built in)", ref y, false);
            var sep = new Panel { BackColor = Theme.Field, Location = new Point(14, y + 2), Size = new Size(452, 1) };
            card.Controls.Add(sep);
            y += 14;
            gameRow = MakeStatusOnly(card, "Game", ref y);
            SetStatus(gameRow, "waiting for Chaos Mode (F2 > Creator Mode)", Theme.Off);

            startBtn = new Button
            {
                Text = "START", Font = new Font("Segoe UI", 12f, FontStyle.Bold), ForeColor = Color.White, BackColor = Theme.Accent,
                FlatStyle = FlatStyle.Flat, Location = new Point(20, 396), Size = new Size(480, 46), Cursor = Cursors.Hand
            };
            startBtn.FlatAppearance.BorderSize = 0;
            startBtn.Click += (s, e) => { if (running) StopCore(); else StartCore(); };
            Controls.Add(startBtn);

            // OBS overlay row
            var obs = new Panel { BackColor = Theme.Panel, Location = new Point(20, 452), Size = new Size(480, 40) };
            var obsLbl = new Label { Text = "OBS overlay", Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = Theme.Text, AutoSize = false, Size = new Size(96, 40), Location = new Point(10, 0), TextAlign = ContentAlignment.MiddleLeft };
            var obsUrl = new Label { Text = OverlayUrl, ForeColor = Theme.Dim, AutoSize = false, Size = new Size(210, 40), Location = new Point(104, 0), TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Consolas", 9f) };
            var copy = SmallButton("Copy", 318);
            copy.Click += (s, e) => { try { Clipboard.SetText(OverlayUrl); AddLog("Copied " + OverlayUrl + "  (OBS: Sources > + > Browser, size 480 x 420)"); } catch { } };
            var prev = SmallButton("Preview", 396);
            prev.Click += (s, e) =>
            {
                if (!running) { AddLog("Press START first, then Preview shows a demo vote."); return; }
                try { Process.Start(OverlayUrl + "?demo"); } catch (Exception ex) { AddLog("Couldn't open browser: " + ex.Message); }
            };
            obs.Controls.AddRange(new Control[] { obsLbl, obsUrl, copy, prev });
            Controls.Add(obs);

            var logTitle = new Label { Text = "LIVE VOTES", Font = new Font("Segoe UI", 9f, FontStyle.Bold), ForeColor = Theme.Dim, AutoSize = true, Location = new Point(22, 506) };
            voteCounter = new Label { Text = "0 votes", ForeColor = Theme.Dim, AutoSize = false, TextAlign = ContentAlignment.MiddleRight, Location = new Point(300, 504), Size = new Size(200, 20) };
            logBox = new ListBox
            {
                BackColor = Theme.Panel, ForeColor = Theme.Text, BorderStyle = BorderStyle.None, Font = new Font("Consolas", 9.5f),
                Location = new Point(20, 528), Size = new Size(480, 152), IntegralHeight = false
            };
            Controls.AddRange(new Control[] { logTitle, voteCounter, logBox });

            LoadConfig();
            uiTimer = new System.Windows.Forms.Timer { Interval = 150 };
            uiTimer.Tick += (s, e) => FlushUi();
            uiTimer.Start();
            FormClosing += (s, e) => StopCore();
            AddLog("Fill in the platforms you stream on, then press START.");
            Shown += (s, e) => ActiveControl = startBtn;
        }

        const string OverlayUrl = "http://localhost:47801/overlay";

        Button SmallButton(string text, int x)
        {
            var b = new Button { Text = text, ForeColor = Theme.Text, BackColor = Theme.Field, FlatStyle = FlatStyle.Flat, Location = new Point(x, 6), Size = new Size(74, 28), Cursor = Cursors.Hand, Font = new Font("Segoe UI", 9f) };
            b.FlatAppearance.BorderSize = 0;
            return b;
        }

        Row MakeRow(Panel card, string name, string hint, ref int y, bool tiktok)
        {
            var r = new Row { Name = name };
            var lbl = new Label { Text = name, Font = new Font("Segoe UI", 10.5f, FontStyle.Bold), ForeColor = Theme.Text, AutoSize = false, Size = new Size(80, 26), Location = new Point(14, y + 2), TextAlign = ContentAlignment.MiddleLeft };
            card.Controls.Add(lbl);
            if (tiktok)
            {
                r.Check = new CheckBox { Text = "via TikFinity (keep TikFinity open)", ForeColor = Theme.Text, AutoSize = true, Location = new Point(96, y + 4), Checked = true, FlatStyle = FlatStyle.Flat };
                card.Controls.Add(r.Check);
            }
            else
            {
                r.Input = new TextBox { BackColor = Theme.Field, ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle, Location = new Point(96, y + 2), Size = new Size(368, 26) };
                SetHint(r.Input, hint);
                card.Controls.Add(r.Input);
            }
            r.Dot = new Dot { Location = new Point(98, y + 36) };
            r.Status = new Label { Text = "off", ForeColor = Theme.Dim, AutoSize = false, Size = new Size(350, 18), Location = new Point(114, y + 32), Font = new Font("Segoe UI", 8.5f) };
            card.Controls.Add(r.Dot); card.Controls.Add(r.Status);
            y += 56;
            return r;
        }

        Row MakeStatusOnly(Panel card, string name, ref int y)
        {
            var r = new Row { Name = name };
            var lbl = new Label { Text = name, Font = new Font("Segoe UI", 10.5f, FontStyle.Bold), ForeColor = Theme.Text, AutoSize = false, Size = new Size(80, 22), Location = new Point(14, y), TextAlign = ContentAlignment.MiddleLeft };
            r.Dot = new Dot { Location = new Point(98, y + 5) };
            r.Status = new Label { ForeColor = Theme.Dim, AutoSize = false, Size = new Size(350, 22), Location = new Point(114, y), TextAlign = ContentAlignment.MiddleLeft };
            card.Controls.Add(lbl); card.Controls.Add(r.Dot); card.Controls.Add(r.Status);
            y += 28;
            return r;
        }

        // simple placeholder text for TextBox
        void SetHint(TextBox tb, string hint)
        {
            tb.Tag = hint;
            Action show = () => { if (tb.Text.Length == 0) { tb.Text = hint; tb.ForeColor = Theme.Dim; } };
            tb.GotFocus += (s, e) => { if (tb.ForeColor == Theme.Dim) { tb.Text = ""; tb.ForeColor = Theme.Text; } };
            tb.LostFocus += (s, e) => show();
            show();
        }

        string Val(Row r)
        {
            if (r.Input == null) return "";
            return r.Input.ForeColor == Theme.Dim ? "" : r.Input.Text.Trim();
        }

        void SetVal(Row r, string v)
        {
            if (r.Input == null || string.IsNullOrEmpty(v)) return;
            r.Input.Text = v; r.Input.ForeColor = Theme.Text;
        }

        void LoadConfig()
        {
            try
            {
                if (!File.Exists(CfgPath)) return;
                cfg = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(File.ReadAllText(CfgPath)) ?? new Dictionary<string, object>();
                SetVal(rows["YouTube"], Str("youtube"));
                SetVal(rows["Kick"], Str("kick"));
                SetVal(rows["Twitch"], Str("twitch"));
                object tf;
                if (cfg.TryGetValue("useTikFinity", out tf) && tf is bool) rows["TikTok"].Check.Checked = (bool)tf;
            }
            catch (Exception e) { AddLog("Couldn't read chatbridge.json: " + e.Message); }
        }

        string Str(string k) { object v; return cfg.TryGetValue(k, out v) && v != null ? v.ToString() : ""; }

        void SaveConfig()
        {
            cfg["youtube"] = Val(rows["YouTube"]);
            cfg["kick"] = Val(rows["Kick"]);
            cfg["twitch"] = Val(rows["Twitch"]);
            cfg["useTikFinity"] = rows["TikTok"].Check.Checked;
            if (!cfg.ContainsKey("tikfinityUrl")) cfg["tikfinityUrl"] = "ws://localhost:21213/";
            var json = new JavaScriptSerializer().Serialize(cfg).Replace(",\"", ",\n  \"").Replace("{\"", "{\n  \"").Replace("}", "\n}");
            File.WriteAllText(CfgPath, json);
        }

        void SetInputsEnabled(bool on)
        {
            foreach (var r in rows.Values) { if (r.Input != null) r.Input.Enabled = on; if (r.Check != null) r.Check.Enabled = on; }
        }

        void StartCore()
        {
            if (!File.Exists(CorePath)) { MessageBox.Show(this, "chatbridge-core.exe is missing. Put it next to ChatBridge.exe.", "ChatBridge"); return; }
            try { SaveConfig(); } catch (Exception e) { AddLog("Couldn't save settings: " + e.Message); return; }
            foreach (var p in Process.GetProcessesByName("chatbridge-core")) { try { p.Kill(); } catch { } }

            foreach (var r in rows.Values)
            {
                bool used = r.Name == "TikTok" ? r.Check.Checked : Val(r).Length > 0;
                SetStatus(r, used ? "starting..." : "off", used ? Theme.Warn : Theme.Off);
            }
            var psi = new ProcessStartInfo(CorePath, "--headless")
            {
                UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = dir,
                RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
            };
            try
            {
                core = new Process { StartInfo = psi, EnableRaisingEvents = true };
                core.OutputDataReceived += (s, e) => { if (e.Data != null && !e.Data.Contains(" voted ")) QueueLog(Clean(e.Data)); };
                core.ErrorDataReceived += (s, e) => { if (e.Data != null) QueueLog(Clean(e.Data)); };
                core.Exited += (s, e) => { if (running) QueueLog("Bridge stopped unexpectedly."); };
                core.Start();
                core.BeginOutputReadLine();
                core.BeginErrorReadLine();
            }
            catch (Exception e) { AddLog("Couldn't start: " + e.Message); return; }

            running = true;
            reader = new Thread(ReadBridge) { IsBackground = true };
            reader.Start();
            startBtn.Text = "STOP";
            startBtn.BackColor = Theme.Field;
            SetInputsEnabled(false);
            AddLog("Started. Open the game and turn on Creator Mode (F2).");
        }

        void StopCore()
        {
            running = false;
            try { if (core != null && !core.HasExited) core.Kill(); } catch { }
            core = null;
            if (startBtn == null || IsDisposed) return;
            startBtn.Text = "START";
            startBtn.BackColor = Theme.Accent;
            SetInputsEnabled(true);
            foreach (var r in rows.Values) SetStatus(r, "off", Theme.Off);
            SetStatus(gameRow, "waiting for Chaos Mode (F2 > Creator Mode)", Theme.Off);
        }

        static string Clean(string s)
        {
            // strip "[time] platform" prefix spacing noise from the core log
            return s.Replace("\t", " ").Trim();
        }

        // Talks to the core over localhost to get status + chat for the window.
        void ReadBridge()
        {
            while (running)
            {
                try
                {
                    using (var c = new TcpClient())
                    {
                        c.Connect("127.0.0.1", 47800);
                        var st = c.GetStream();
                        var hello = Encoding.UTF8.GetBytes("#gui\n");
                        st.Write(hello, 0, hello.Length);
                        var buf = new byte[8192];
                        var sb = new StringBuilder();
                        while (running)
                        {
                            int n = st.Read(buf, 0, buf.Length);
                            if (n <= 0) break;
                            sb.Append(Encoding.UTF8.GetString(buf, 0, n));
                            string all = sb.ToString(); int nl;
                            while ((nl = all.IndexOf('\n')) >= 0) { HandleLine(all.Substring(0, nl).TrimEnd('\r')); all = all.Substring(nl + 1); }
                            sb.Length = 0; sb.Append(all);
                        }
                    }
                }
                catch { }
                if (running) Thread.Sleep(700);
            }
        }

        void HandleLine(string line)
        {
            var parts = line.Split('\t');
            lock (gate)
            {
                if (parts[0] == "#status" && parts.Length > 1)
                {
                    foreach (var seg in parts[1].Split(new[] { " | " }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        int c = seg.IndexOf(": ");
                        if (c > 0) pendingStatus[seg.Substring(0, c)] = seg.Substring(c + 2);
                    }
                }
                else if (parts[0] == "#game" && parts.Length > 1) { int g; if (int.TryParse(parts[1], out g)) pendingGame = g; }
                else if (parts.Length >= 3)
                {
                    string msg = parts[2].Trim().TrimStart('#');
                    int v;
                    if (msg.Length == 1 && int.TryParse(msg, out v) && v >= 1 && v <= 9)
                    {
                        votes++;
                        pendingLog.Enqueue("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + Platform(parts[0]).PadRight(8) + parts[1] + "  ->  " + v);
                    }
                }
            }
        }

        static string Platform(string p)
        {
            switch (p.ToLowerInvariant())
            {
                case "tiktok": return "TikTok";
                case "youtube": return "YouTube";
                case "kick": return "Kick";
                case "twitch": return "Twitch";
                default: return Cap(p);
            }
        }

        static string Cap(string s) { return s.Length == 0 ? s : char.ToUpper(s[0]) + s.Substring(1); }

        void QueueLog(string s) { lock (gate) pendingLog.Enqueue(s); }

        void FlushUi()
        {
            lock (gate)
            {
                while (pendingLog.Count > 0) AddLog(pendingLog.Dequeue());
                foreach (var kv in pendingStatus)
                {
                    Row r;
                    if (rows.TryGetValue(kv.Key, out r) && running) SetStatus(r, kv.Value, ColorFor(kv.Value));
                }
                pendingStatus.Clear();
                if (pendingGame >= 0 && running)
                {
                    if (pendingGame > 0) SetStatus(gameRow, "Chaos Mode connected", Theme.Good);
                    else SetStatus(gameRow, "waiting for Chaos Mode (F2 > Creator Mode)", Theme.Warn);
                    pendingGame = -1;
                }
                voteCounter.Text = votes + (votes == 1 ? " vote" : " votes");
            }
        }

        static Color ColorFor(string s)
        {
            string l = s.ToLowerInvariant();
            if (l.Contains("failed") || l.Contains("blocked") || l.Contains("error")) return Theme.Bad;
            if (l.StartsWith("connected") || l.Contains(" connected") && !l.Contains("dis")) return Theme.Good;
            return Theme.Warn;
        }

        void SetStatus(Row r, string text, Color c)
        {
            r.Status.Text = text;
            r.Status.ForeColor = c == Theme.Off ? Theme.Dim : c;
            r.Dot.DotColor = c;
        }

        void AddLog(string s)
        {
            logBox.Items.Add(s);
            while (logBox.Items.Count > 300) logBox.Items.RemoveAt(0);
            logBox.TopIndex = Math.Max(0, logBox.Items.Count - 1);
        }
    }
}
