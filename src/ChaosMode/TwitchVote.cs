// Creator Mode: Twitch chat votes on the next Chaos event.
// Reads chat anonymously over Twitch IRC (no login or token needed), so it only needs your channel name.
using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using System.Threading;

namespace ChaosMode
{
    public class TwitchChat
    {
        public string Channel { get; private set; } = "";
        public string Status { get; private set; } = "Not connected";
        public bool Connected { get; private set; }

        private TcpClient client;
        private Thread thread;
        private volatile bool stop;
        private readonly Queue<KeyValuePair<string, string>> inbox = new Queue<KeyValuePair<string, string>>();
        private readonly object gate = new object();

        public void Connect(string channel)
        {
            Disconnect();
            Channel = (channel ?? "").Trim().TrimStart('#').ToLowerInvariant();
            if (Channel.Length == 0) { Status = "Enter your Twitch channel name"; return; }
            stop = false;
            Status = "Connecting to #" + Channel + "...";
            thread = new Thread(Run) { IsBackground = true };
            thread.Start();
        }

        public void Disconnect()
        {
            stop = true;
            try { client?.Close(); } catch { }
            client = null;
            Connected = false;
            Status = "Not connected";
        }

        // Main thread: pull (user, message) pairs received since last call.
        public List<KeyValuePair<string, string>> Drain()
        {
            lock (gate)
            {
                var list = new List<KeyValuePair<string, string>>(inbox);
                inbox.Clear();
                return list;
            }
        }

        private void HandleLine(string line, Action<string> send)
        {
            if (line.StartsWith("PING")) { send("PONG" + line.Substring(4)); return; }
            // :user!user@user.tmi.twitch.tv PRIVMSG #channel :message
            int priv = line.IndexOf(" PRIVMSG ", StringComparison.Ordinal);
            if (priv < 0 || !line.StartsWith(":")) return;
            int bang = line.IndexOf('!');
            int msgStart = line.IndexOf(" :", priv, StringComparison.Ordinal);
            if (bang < 0 || msgStart < 0) return;
            string user = line.Substring(1, bang - 1);
            string msg = line.Substring(msgStart + 2);
            lock (gate)
            {
                if (inbox.Count < 5000) inbox.Enqueue(new KeyValuePair<string, string>("twitch:" + user.ToLowerInvariant(), msg));
            }
        }

        private void Run()
        {
            int backoff = 2000;
            while (!stop)
            {
                try
                {
                    client = new TcpClient();
                    client.Connect("irc.chat.twitch.tv", 6667);
                    var stream = client.GetStream();
                    Action<string> send = l => { var bytes = Encoding.UTF8.GetBytes(l + "\r\n"); stream.Write(bytes, 0, bytes.Length); };
                    send("NICK justinfan" + new Random().Next(10000, 99999));
                    send("JOIN #" + Channel);
                    Connected = true;
                    Status = "Connected to #" + Channel;
                    backoff = 2000;
                    var buf = new byte[8192];
                    var pending = new StringBuilder();
                    while (!stop)
                    {
                        int n = stream.Read(buf, 0, buf.Length);
                        if (n <= 0) break;
                        pending.Append(Encoding.UTF8.GetString(buf, 0, n));
                        string all = pending.ToString();
                        int nl;
                        while ((nl = all.IndexOf('\n')) >= 0)
                        {
                            string line = all.Substring(0, nl).TrimEnd('\r');
                            all = all.Substring(nl + 1);
                            HandleLine(line, send);
                        }
                        pending.Length = 0;
                        pending.Append(all);
                    }
                }
                catch (Exception e)
                {
                    if (!stop) { Status = "Connection lost, retrying..."; Loader.Log("Twitch: " + e.Message); }
                }
                Connected = false;
                try { client?.Close(); } catch { }
                if (!stop) { Thread.Sleep(backoff); backoff = Math.Min(backoff * 2, 30000); }
            }
        }
    }

    // A chat vote between 3 random events. Viewers type 1, 2 or 3 (one vote each, last vote counts).
    public class ChatVote
    {
        public bool Active;
        public string[] Options = new string[0];
        public int[] Counts = new int[3];
        public float EndsAt;
        private readonly Dictionary<string, int> votes = new Dictionary<string, int>();

        public void Start(string[] options, float seconds)
        {
            Options = options;
            Counts = new int[options.Length];
            votes.Clear();
            EndsAt = UnityEngine.Time.unscaledTime + seconds;
            Active = true;
        }

        public void Feed(List<KeyValuePair<string, string>> msgs)
        {
            if (!Active) return;
            foreach (var kv in msgs)
            {
                string m = kv.Value.Trim();
                if (m.Length == 0 || m.Length > 3) continue;
                if (!int.TryParse(m.TrimStart('#'), out int n) || n < 1 || n > Options.Length) continue;
                votes[kv.Key] = n - 1;
            }
            for (int i = 0; i < Counts.Length; i++) Counts[i] = 0;
            foreach (var v in votes.Values) Counts[v]++;
        }

        public float SecondsLeft => Math.Max(0f, EndsAt - UnityEngine.Time.unscaledTime);
        public int TotalVotes => votes.Count;

        // Index of the winner (random among ties).
        public int Winner(Random rng)
        {
            int best = -1; var ties = new List<int>();
            for (int i = 0; i < Counts.Length; i++)
            {
                if (Counts[i] > best) { best = Counts[i]; ties.Clear(); ties.Add(i); }
                else if (Counts[i] == best) ties.Add(i);
            }
            return ties[rng.Next(ties.Count)];
        }
    }
}

namespace ChaosMode
{
    // Reads chat from the ChatBridge companion app (YouTube, TikTok, Kick, Twitch...) over localhost.
    // Line format from the bridge:  platform<TAB>user<TAB>message   or   #status<TAB>text
    public class BridgeChat
    {
        public const int Port = 47800;
        public bool Connected { get; private set; }
        public string Version { get; private set; } = "";   // ChatBridge version (v1.1 and older never send one)
        public string Status { get; private set; } = "ChatBridge not running";

        private Thread thread;
        private volatile bool want;
        private readonly Queue<KeyValuePair<string, string>> inbox = new Queue<KeyValuePair<string, string>>();
        private readonly object gate = new object();
        private readonly Queue<GiftMsg> giftInbox = new Queue<GiftMsg>();
        private NetworkStream current;
        private readonly object sendGate = new object();

        // Send one line to the bridge (used to publish the vote state for the OBS overlay).
        public void SendLine(string line)
        {
            var s = current;
            if (s == null) return;
            try
            {
                var bytes = Encoding.UTF8.GetBytes(line + "\n");
                lock (sendGate) s.Write(bytes, 0, bytes.Length);
            }
            catch { }
        }

        // Keep trying to connect while Creator Mode is on.
        public void KeepAlive(bool on)
        {
            want = on;
            if (on && (thread == null || !thread.IsAlive))
            {
                thread = new Thread(Run) { IsBackground = true };
                thread.Start();
            }
        }

        public List<KeyValuePair<string, string>> Drain()
        {
            lock (gate)
            {
                var list = new List<KeyValuePair<string, string>>(inbox);
                inbox.Clear();
                return list;
            }
        }

        public List<GiftMsg> DrainGifts()
        {
            lock (gate)
            {
                var list = new List<GiftMsg>(giftInbox);
                giftInbox.Clear();
                return list;
            }
        }

        private void Run()
        {
            while (want)
            {
                TcpClient client = null;
                try
                {
                    client = new TcpClient();
                    client.Connect("127.0.0.1", Port);
                    var stream = client.GetStream();
                    current = stream;
                    Connected = true;
                    Status = "ChatBridge connected";
                    var buf = new byte[8192];
                    var pending = new StringBuilder();
                    while (want)
                    {
                        int n = stream.Read(buf, 0, buf.Length);
                        if (n <= 0) break;
                        pending.Append(Encoding.UTF8.GetString(buf, 0, n));
                        string all = pending.ToString();
                        int nl;
                        while ((nl = all.IndexOf('\n')) >= 0)
                        {
                            string line = all.Substring(0, nl).TrimEnd('\r');
                            all = all.Substring(nl + 1);
                            var parts = line.Split('\t');
                            if (parts.Length >= 2 && parts[0] == "#status") { Status = "ChatBridge: " + parts[1]; continue; }
                            if (parts.Length >= 2 && parts[0] == "#version") { Version = parts[1]; continue; }
                            if (parts[0] == "#gift" && parts.Length >= 6)
                            {
                                int dia, cnt; int.TryParse(parts[4], out dia); int.TryParse(parts[5], out cnt);
                                lock (gate)
                                {
                                    if (giftInbox.Count < 200)
                                        giftInbox.Enqueue(new GiftMsg { Platform = parts[1], User = parts[2], Name = parts[3], Diamonds = dia, Count = cnt < 1 ? 1 : cnt });
                                }
                                continue;
                            }
                            if (parts.Length < 3) continue;
                            lock (gate)
                            {
                                if (inbox.Count < 5000)
                                    inbox.Enqueue(new KeyValuePair<string, string>(parts[0].ToLowerInvariant() + ":" + parts[1].ToLowerInvariant(), parts[2]));
                            }
                        }
                        pending.Length = 0;
                        pending.Append(all);
                    }
                }
                catch { }
                current = null;
                Version = "";
                try { client?.Close(); } catch { }
                if (Connected) Loader.Log("ChatBridge disconnected.");
                Connected = false;
                Status = "ChatBridge not running (start ChatBridge.exe)";
                if (want) Thread.Sleep(3000);
            }
        }
    }
}
