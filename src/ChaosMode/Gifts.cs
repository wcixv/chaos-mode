// TikTok gifts -> Chaos events. Creators edit <game>\ChatBridge\gifts.txt; changes are picked up while the game runs.
//
// Line format (one rule per line, # starts a comment):
//     Galaxy = END DAY          a gift by name (not case sensitive)
//     >= 500 = JACKPOT          any gift worth at least 500 diamonds (the biggest matching rule wins)
//     Rose = none               turn a gift off
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ChaosMode
{
    public struct GiftMsg
    {
        public string Platform, User, Name;
        public int Diamonds, Count;
    }

    public class GiftMap
    {
        private readonly Dictionary<string, string> byName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly List<KeyValuePair<int, string>> byValue = new List<KeyValuePair<int, string>>();
        private DateTime loadedStamp = DateTime.MinValue;
        private float nextCheck;
        private readonly HashSet<string> warned = new HashSet<string>();

        public string Path = "";
        public int Rules => byName.Count + byValue.Count;

        // Every event name, so rules can be checked and given their proper spelling.
        public Func<IEnumerable<string>> EventNames = () => new string[0];
        public Func<string, string> Describe = n => "";

        public string Summary
        {
            get
            {
                if (Path == "" || !File.Exists(Path)) return "no gifts.txt yet";
                return Rules + " gift rule" + (Rules == 1 ? "" : "s") + " loaded (ChatBridge\\gifts.txt)";
            }
        }

        // Called often; only touches the disk every few seconds.
        public void Refresh(float now)
        {
            if (now < nextCheck) return;
            nextCheck = now + 3f;
            try
            {
                if (Path == "") return;
                if (!File.Exists(Path))
                {
                    if (!Directory.Exists(System.IO.Path.GetDirectoryName(Path))) return;
                    File.WriteAllText(Path, DefaultFile());
                }
                var stamp = File.GetLastWriteTimeUtc(Path);
                if (stamp == loadedStamp) return;
                loadedStamp = stamp;
                Parse(File.ReadAllText(Path).Replace("\r", "").Split(new[] { (char)10 }));
                Loader.Log("Gift rules loaded: " + Rules);
            }
            catch (Exception e) { Loader.Log("gifts.txt: " + e.Message); }
        }

        private string Canonical(string ev)
        {
            foreach (var n in EventNames()) if (string.Equals(n, ev, StringComparison.OrdinalIgnoreCase)) return n;
            return null;
        }

        private void Parse(string[] lines)
        {
            byName.Clear(); byValue.Clear(); warned.Clear();
            int lineNo = 0;
            foreach (var raw in lines)
            {
                lineNo++;
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                int eq = line.IndexOf('=');
                // ">= 500 = X": the first '=' belongs to ">=", so look for the one after it
                if (line.StartsWith(">=")) eq = line.IndexOf('=', 2);
                if (eq <= 0) { Warn(lineNo, "needs  gift = EVENT"); continue; }
                string key = line.Substring(0, eq).Trim();
                string val = line.Substring(eq + 1).Trim();
                bool off = val.Length == 0 || val.Equals("none", StringComparison.OrdinalIgnoreCase) || val.Equals("off", StringComparison.OrdinalIgnoreCase);
                string ev = off ? null : Canonical(val);
                if (!off && ev == null) { Warn(lineNo, "unknown event \"" + val + "\""); continue; }

                if (key.StartsWith(">="))
                {
                    string num = key.Substring(2).Trim().ToLowerInvariant().Replace("diamonds", "").Replace("diamond", "").Replace("d", "").Trim();
                    int min;
                    if (!int.TryParse(num, out min) || min < 1) { Warn(lineNo, "bad diamond amount \"" + key + "\""); continue; }
                    if (ev != null) byValue.Add(new KeyValuePair<int, string>(min, ev));
                }
                else if (ev != null) byName[key] = ev;
                else byName.Remove(key);
            }
            byValue.Sort((a, b) => b.Key.CompareTo(a.Key));
        }

        private void Warn(int line, string msg)
        {
            string m = "gifts.txt line " + line + ": " + msg;
            if (warned.Add(m)) Loader.Log(m);
        }

        // Event for this gift, or null if no rule matches.
        public string Match(string giftName, int diamonds)
        {
            string ev;
            if (giftName != null && byName.TryGetValue(giftName.Trim(), out ev)) return ev;
            foreach (var kv in byValue) if (diamonds >= kv.Key) return kv.Value;
            return null;
        }

        private string DefaultFile()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Chaos Mode - TikTok gift rules. Edit and save any time, no restart needed.");
            sb.AppendLine("# Needs Creator Mode on and ChatBridge connected to TikTok (via TikFinity).");
            sb.AppendLine("#");
            sb.AppendLine("#   Gift name = EVENT          e.g.  Rose = MONEY RAIN");
            sb.AppendLine("#   >= 500 = EVENT             any gift worth 500+ diamonds (biggest matching rule wins)");
            sb.AppendLine("#   Gift name = none           turn a gift off");
            sb.AppendLine("#");
            sb.AppendLine("# Gift names are what TikTok calls them (Rose, Finger Heart, Doughnut, Galaxy...), not case sensitive.");
            sb.AppendLine("# A gift only fires while a workday is running.");
            sb.AppendLine("#");
            sb.AppendLine("# Events you can use:");
            foreach (var n in EventNames()) sb.AppendLine("#   " + n + (Describe(n).Length > 0 ? "  -  " + Describe(n) : ""));
            sb.AppendLine();
            sb.AppendLine("Rose = MONEY RAIN");
            sb.AppendLine("Finger Heart = DRUNK");
            sb.AppendLine("Doughnut = TASED");
            sb.AppendLine("Galaxy = END DAY");
            return sb.ToString();
        }
    }
}
