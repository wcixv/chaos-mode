// Stream-friendly chat vote overlay: dark card, countdown bar, animated vote bars, winner reveal.
using System.Collections.Generic;
using UnityEngine;

namespace ChaosMode
{
    public class VoteOverlay
    {
        public static readonly Dictionary<string, string> Desc = new Dictionary<string, string>
        {
            { "DRUNK", "Everyone is wasted for 30s" },
            { "KNOCKOUT", "Everyone faceplants" },
            { "LAUNCH", "Everyone gets yeeted skyward" },
            { "PHONE LINES CUT", "Every call hangs up" },
            { "BANKRUPTCY", "Team loses HALF its cash" },
            { "TAX MAN", "Team loses 20% of its cash" },
            { "PICKPOCKET", "Someone loses all their shopping money" },
            { "BOSS IS FURIOUS", "Quota +50%" },
            { "BOSS IS ANGRY", "Quota +20%" },
            { "TIME CRUNCH", "Half the clock disappears" },
            { "EVACUATION", "Everyone back to the entrance" },
            { "YOU'RE FIRED (jk)", "A very real-looking firing" },
            { "TASED", "Everyone gets tased" },
            { "TASER PARTY", "Tased every 6s for 25s" },
            { "PAYDAY", "Everyone gets $400 Scamazon money" },
            { "QUOTA CUT", "Quota -25%" },
            { "DOUBLE CASH", "All scam money x2 for 60s" },
            { "BOSS ON BREAK", "Clock frozen for 30s" },
            { "DOUBLE OR NOTHING", "50/50: team cash doubles or vanishes" },
            { "HUDDLE UP", "Everyone teleports to one player" },
            { "SWAP!", "Two players swap places" },
            { "MONEY RAIN", "Everyone gets $150" },
            { "JACKPOT", "One lucky player wins $750" },
            { "OVERTIME", "+60 seconds on the clock" },
            { "END DAY", "The workday ends right now" },
        };
        public static readonly HashSet<string> Good = new HashSet<string> { "MONEY RAIN", "JACKPOT", "OVERTIME", "PAYDAY", "QUOTA CUT", "DOUBLE CASH", "BOSS ON BREAK" };

        // palette
        private static readonly Color Bg = new Color(0.06f, 0.06f, 0.09f, 0.92f);
        private static readonly Color Card = new Color(1f, 1f, 1f, 0.06f);
        private static readonly Color Accent = new Color(1f, 0.30f, 0.38f);     // chaos pink-red
        private static readonly Color GoodCol = new Color(0.30f, 0.90f, 0.55f);
        private static readonly Color Gold = new Color(1f, 0.82f, 0.25f);
        private static readonly Color[] OptCols = { new Color(0.36f, 0.62f, 1f), new Color(0.75f, 0.45f, 1f), new Color(1f, 0.62f, 0.25f) };

        private readonly float[] shown = new float[3];   // animated bar fill
        private GUIStyle title, big, small, num, center;
        private float scale = 1f;
        private string lastFirst;

        private void Styles()
        {
            float s = Mathf.Clamp(Screen.height / 1080f, 0.75f, 2f);
            if (title != null && Mathf.Approximately(s, scale)) return;
            scale = s;
            title = new GUIStyle(GUI.skin.label) { fontSize = Px(15), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            big = new GUIStyle(GUI.skin.label) { fontSize = Px(22), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            small = new GUIStyle(GUI.skin.label) { fontSize = Px(13), alignment = TextAnchor.MiddleLeft };
            num = new GUIStyle(GUI.skin.label) { fontSize = Px(24), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            center = new GUIStyle(GUI.skin.label) { fontSize = Px(30), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            foreach (var st in new[] { title, big, small, num, center }) { st.normal.textColor = Color.white; st.wordWrap = false; }
        }

        private int Px(float v) => Mathf.RoundToInt(v * scale);
        private float S(float v) => v * scale;

        private static void Fill(Rect r, Color c)
        {
            var old = GUI.color; GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        private static void Text(Rect r, string t, GUIStyle st, Color c)
        {
            var old = GUI.color;
            var gc = new GUIContent(t);
            // drop shadow for readability on stream
            GUI.color = new Color(0, 0, 0, 0.6f * c.a);
            GUI.Label(new Rect(r.x + 1, r.y + 2, r.width, r.height), gc, st);
            GUI.color = c;
            GUI.Label(r, gc, st);
            GUI.color = old;
        }

        public void Draw(Chaos chaos)
        {
            if (!chaos.CreatorMode || !chaos.ShowInGame) return;
            Styles();
            float t = Time.unscaledTime;
            float w = S(440), x = Screen.width - w - S(24), y = S(110);
            var v = chaos.Vote;
            if (v.Active && v.Options.Length > 0 && v.Options[0] != lastFirst) { lastFirst = v.Options[0]; Reset(); }

            // ---------- winner reveal (first 4 s after a vote) ----------
            float since = t - chaos.LastWinnerAt;
            if (!v.Active && since < 4f && chaos.LastWinner != "")
            {
                float pop = Mathf.Clamp01(since / 0.25f);
                float h = S(150) * (0.85f + 0.15f * pop);
                var r = new Rect(x, y, w, h);
                bool good = Good.Contains(chaos.LastWinner);
                Color c = good ? GoodCol : Accent;
                Fill(r, Bg);
                Fill(new Rect(x, y, w, S(5)), c);
                if (since < 0.6f && ((int)(since * 10) % 2 == 0)) Fill(r, new Color(c.r, c.g, c.b, 0.18f)); // flash
                Text(new Rect(x, y + S(14), w, S(22)), "CHAT HAS SPOKEN", new GUIStyle(title) { alignment = TextAnchor.MiddleCenter }, Gold);
                Text(new Rect(x, y + S(44), w, S(44)), chaos.LastWinner, center, c);
                Text(new Rect(x, y + S(92), w, S(22)), Describe(chaos.LastWinner), new GUIStyle(small) { alignment = TextAnchor.MiddleCenter }, new Color(1, 1, 1, 0.85f));
                Text(new Rect(x, y + S(116), w, S(22)), chaos.LastWinnerVotes + " of " + chaos.LastWinnerTotal + " votes", new GUIStyle(small) { alignment = TextAnchor.MiddleCenter }, new Color(1, 1, 1, 0.55f));
                return;
            }

            // ---------- idle: waiting / next vote ----------
            if (!v.Active)
            {
                var r = new Rect(x, y, w, S(64));
                Fill(r, Bg);
                Fill(new Rect(x, y, S(5), r.height), Accent);
                Text(new Rect(x + S(18), y + S(8), w, S(24)), "CHAT DECIDES", title, Accent);
                string msg = chaos.WaitingForDay ? "Voting starts when the workday begins" : "Next vote in " + Mathf.CeilToInt(chaos.NextVoteIn) + "s";
                Text(new Rect(x + S(18), y + S(32), w, S(24)), msg, small, new Color(1, 1, 1, 0.8f));
                return;
            }

            // ---------- live vote ----------
            int n = v.Options.Length;
            float rowH = S(66), headH = S(64), footH = S(30);
            float H = headH + n * (rowH + S(8)) + footH;
            Fill(new Rect(x, y, w, H), Bg);

            // header
            Fill(new Rect(x, y, w, S(4)), Accent);
            Text(new Rect(x + S(18), y + S(12), w, S(24)), "CHAT DECIDES", title, Accent);
            float left = v.SecondsLeft;
            bool urgent = left <= 5f && !chaos.WaitingForDay;
            string timer = chaos.WaitingForDay ? "PAUSED" : Mathf.CeilToInt(left) + "s";
            Color timerCol = urgent ? (((int)(t * 4) % 2 == 0) ? Accent : Color.white) : Color.white;
            Text(new Rect(x, y + S(10), w - S(18), S(30)), timer, new GUIStyle(big) { alignment = TextAnchor.MiddleRight }, timerCol);
            // countdown bar
            float frac = chaos.VoteSeconds > 0 ? Mathf.Clamp01(left / chaos.VoteSeconds) : 0f;
            Fill(new Rect(x + S(18), y + S(46), w - S(36), S(6)), new Color(1, 1, 1, 0.1f));
            Fill(new Rect(x + S(18), y + S(46), (w - S(36)) * frac, S(6)), urgent ? Accent : Gold);

            // options
            int total = v.TotalVotes;
            int lead = -1, best = 0;
            for (int i = 0; i < n; i++) if (v.Counts[i] > best) { best = v.Counts[i]; lead = i; }
            for (int i = 0; i < n; i++)
            {
                float ry = y + headH + i * (rowH + S(8));
                var row = new Rect(x + S(12), ry, w - S(24), rowH);
                Color col = OptCols[i % OptCols.Length];
                float target = total > 0 ? (float)v.Counts[i] / total : 0f;
                shown[i] = Mathf.Lerp(shown[i], target, 1f - Mathf.Exp(-8f * Time.unscaledDeltaTime));

                Fill(row, Card);
                Fill(new Rect(row.x, row.y, row.width * shown[i], row.height), new Color(col.r, col.g, col.b, 0.28f));
                if (i == lead) Fill(new Rect(row.x, row.y, S(4), row.height), Gold);

                // number badge
                var badge = new Rect(row.x + S(10), row.y + S(11), S(44), S(44));
                Fill(badge, col);
                Text(badge, (i + 1).ToString(), num, Color.white);

                bool good = Good.Contains(v.Options[i]);
                Text(new Rect(badge.xMax + S(14), row.y + S(8), row.width - S(150), S(28)), v.Options[i], big, good ? GoodCol : Color.white);
                Text(new Rect(badge.xMax + S(14), row.y + S(36), row.width - S(150), S(22)), Describe(v.Options[i]), small, new Color(1, 1, 1, 0.65f));

                int pct = total > 0 ? Mathf.RoundToInt(100f * v.Counts[i] / total) : 0;
                Text(new Rect(row.x, row.y + S(8), row.width - S(12), S(28)), pct + "%", new GUIStyle(big) { alignment = TextAnchor.MiddleRight }, Color.white);
                Text(new Rect(row.x, row.y + S(36), row.width - S(12), S(22)), v.Counts[i] + (v.Counts[i] == 1 ? " vote" : " votes"), new GUIStyle(small) { alignment = TextAnchor.MiddleRight }, new Color(1, 1, 1, 0.6f));
            }

            // footer
            float fy = y + H - footH;
            Text(new Rect(x + S(18), fy, w, footH), "Type 1, 2 or 3 in chat", small, new Color(1, 1, 1, 0.75f));
            Text(new Rect(x, fy, w - S(18), footH), total + (total == 1 ? " vote" : " votes"), new GUIStyle(small) { alignment = TextAnchor.MiddleRight }, new Color(1, 1, 1, 0.5f));
        }

        public static string Describe(string ev) => Desc.TryGetValue(ev, out var d) ? d : "";

        public void Reset() { for (int i = 0; i < shown.Length; i++) shown[i] = 0f; }
    }
}
