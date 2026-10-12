// Remembers your Chaos Mode settings between games, in <game>\BepInEx\config\ChaosMode\ (settings.txt and gifts.txt).
// That folder is separate from the plugin and ChatBridge folders, so updating or reinstalling either one keeps your setup.
using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace ChaosMode
{
    public static class Settings
    {
        public static string Dir
        {
            get { return System.IO.Path.Combine(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath), "BepInEx"), System.IO.Path.Combine("config", "ChaosMode")); }
        }
        public static string File_ { get { return System.IO.Path.Combine(Dir, "settings.txt"); } }
        public static string GiftsFile { get { return System.IO.Path.Combine(Dir, "gifts.txt"); } }
        // Where v1.2 first kept the gift rules (still read once, so nobody loses their rules).
        public static string OldGiftsFile { get { return System.IO.Path.Combine(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath), "ChatBridge"), "gifts.txt"); } }

        private static string lastSaved = "";

        // Returns the Twitch channel (or "" if none saved). Sets the other values on chaos.
        public static string Load(Chaos chaos, string legacyChannel)
        {
            string channel = legacyChannel ?? "";
            try
            {
                if (File.Exists(File_))
                {
                    foreach (var raw in File.ReadAllText(File_).Replace("\r", "").Split(new[] { (char)10 }))
                    {
                        int eq = raw.IndexOf('=');
                        if (eq <= 0) continue;
                        string k = raw.Substring(0, eq).Trim().ToLowerInvariant(), v = raw.Substring(eq + 1).Trim();
                        float f; bool b;
                        if (k == "interval" && float.TryParse(v, out f) && f >= 5f) chaos.Interval = f;
                        else if (k == "voteseconds" && float.TryParse(v, out f) && f >= 5f) chaos.VoteSeconds = f;
                        else if (k == "showvoteboxingame" && bool.TryParse(v, out b)) chaos.ShowInGame = b;
                        else if (k == "twitchchannel") channel = v;
                    }
                }
            }
            catch (Exception e) { Loader.Log("settings.txt: " + e.Message); }
            lastSaved = Snapshot(chaos, channel);
            return channel;
        }

        private static string Snapshot(Chaos chaos, string channel)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Chaos Mode settings. The F2 menu saves these for you; you can also edit them here.");
            sb.AppendLine("interval=" + chaos.Interval);
            sb.AppendLine("voteSeconds=" + chaos.VoteSeconds);
            sb.AppendLine("showVoteBoxInGame=" + (chaos.ShowInGame ? "true" : "false"));
            sb.AppendLine("twitchChannel=" + (channel ?? "").Trim());
            return sb.ToString();
        }

        // Cheap to call every frame: only writes when something changed.
        public static void SaveIfChanged(Chaos chaos, string channel)
        {
            string now = Snapshot(chaos, channel);
            if (now == lastSaved) return;
            lastSaved = now;
            try { Directory.CreateDirectory(Dir); File.WriteAllText(File_, now); }
            catch (Exception e) { Loader.Log("Couldn't save settings: " + e.Message); }
        }
    }
}
