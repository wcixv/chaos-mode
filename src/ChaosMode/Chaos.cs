// Chaos Mode: every so often a random server-side event hits the whole lobby.
// Everything here runs on the host and syncs to friends through the game's own networking,
// so friends don't need the mod installed.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Mirror;
using UnityEngine;

namespace ChaosMode
{
    public class Chaos
    {
        public bool Enabled;
        public float Interval = 60f;          // seconds between events
        public string Banner = "";
        public float BannerUntil;
        public string LastEvent = "";
        public float NextEventIn => Mathf.Max(0f, nextAt - Time.unscaledTime);

        private float nextAt = -1f;
        private float lowGravUntil = -1f;
        private float fireLightUntil = -1f;
        private readonly System.Random rng = new System.Random();

        private const BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        private static readonly MethodInfo mFireLight = typeof(ScammerGameManager).GetMethod("ServerSetFireLightActive", Any);
        private static readonly MethodInfo mFiredFx = typeof(ScammerGameManager).GetMethod("RpcPlayFiredEffects", Any);

        private struct Ev { public string name; public Func<string> run; public Func<bool> ok; public bool hidden; }  // hidden = gift-only, never in votes/random/menu
        private List<Ev> events;

        public Chaos()
        {
            events = new List<Ev>
            {
                // ---- punishments ----
                E("DRUNK", Drunk),
                E("KNOCKOUT", () => Ragdoll(4f, Vector3.zero, "Everyone hits the floor!")),
                E("TASED", () => Tase(), () => true),
                E("TASER PARTY", TaserParty),
                E("LAUNCH", Launch),
                E("PHONE LINES CUT", HangUpAll, () => ActiveCalls().Count > 0),
                E("BANKRUPTCY", () => Tax(0.5f, "The team lost HALF its money"), () => Gm.MoneyPoolLong >= 100),
                E("TAX MAN", () => Tax(0.2f, "The tax man took 20%"), () => Gm.MoneyPoolLong >= 100),
                E("PICKPOCKET", Pickpocket, () => KPlayers().Any(k => k.SpendableMoney > 0)),
                E("BOSS IS FURIOUS", () => ScaleQuota(1.5f, "Quota up 50%!"), () => !Gm.IsSandboxMode),
                E("BOSS IS ANGRY", () => ScaleQuota(1.2f, "Quota up 20%!"), () => !Gm.IsSandboxMode),
                E("TIME CRUNCH", TimeCrunch, () => Gm.DayTimeRemaining > 60f),
                E("EVACUATION", Evacuate),
                E("YOU'RE FIRED (jk)", FakeFiring, () => mFiredFx != null),
                E("HUDDLE UP", Huddle, () => Players().Count >= 2),
                E("SWAP!", Swap, () => Players().Count >= 2),
                // ---- rewards ----
                E("MONEY RAIN", MoneyRain),
                E("JACKPOT", Jackpot),
                E("OVERTIME", () => AddTime(60f, "+60 seconds on the clock!")),
                E("PAYDAY", Payday),
                E("QUOTA CUT", () => ScaleQuota(0.75f, "Boss cut the quota by 25%!"), () => !Gm.IsSandboxMode),
                E("DOUBLE CASH", DoubleCash, () => doubleCashUntil < Time.unscaledTime),
                E("BOSS ON BREAK", BossOnBreak, () => freezeClockUntil < Time.unscaledTime),
                E("DOUBLE OR NOTHING", DoubleOrNothing, () => Gm.MoneyPoolLong >= 100),
                // ---- gift-only (TikTok gifts via gifts.txt) ----
                H("END DAY", EndDay, () => DayRunning() && Gm.CurrentDay > 0 && (Gm.IsSandboxMode || Gm.CurrentDay < 16)),
            };
            Gifts.EventNames = () => events.Select(e => e.name);
            Gifts.Describe = VoteOverlay.Describe;
            try { Gifts.Path = Settings.GiftsFile; Gifts.OldPath = Settings.OldGiftsFile; } catch { }
        }

        private static Ev E(string n, Func<string> r, Func<bool> ok = null) => new Ev { name = n, run = r, ok = ok ?? (() => true) };
        private static Ev H(string n, Func<string> r, Func<bool> ok = null) => new Ev { name = n, run = r, ok = ok ?? (() => true), hidden = true };
        private static ScammerGameManager Gm => ScammerGameManager.Instance;

        private static bool DayRunning()
        {
            var gm = Gm;
            return gm != null && gm.ScammerGameStarted && !gm.IsReviewPhase && !gm.DayZeroTutorialActive && gm.DayTimeRemaining > 5f;
        }

        // Called every frame by the trainer. allowed = host + private lobby.
        // Creator Mode (Twitch chat votes)
        public bool CreatorMode;
        public bool GiftsOn;                      // TikTok gifts trigger events (works with or without chat votes)
        public bool ShowInGame = true;
        private float nextPublish;
        public float VoteSeconds = 25f;
        public float BreakSeconds = 5f;          // pause between votes (shows the result)
        public readonly TwitchChat Twitch = new TwitchChat();
        public readonly BridgeChat Bridge = new BridgeChat();
        public readonly ChatVote Vote = new ChatVote();
        public readonly GiftMap Gifts = new GiftMap();
        private readonly Queue<GiftMsg> giftQueue = new Queue<GiftMsg>();
        public int GiftsReceived;                 // gifts ChatBridge has passed to the game this session
        public string LastGift = "";              // what happened with the last one, shown in the menu
        public bool LastGiftFired;
        // The last gift that fired an event, sent to the OBS overlay so it can pop up a "thanks" card.
        public int GiftSeq; public string GiftUser = "", GiftName = "", GiftEvent = ""; public int GiftCount = 1;
        private float nextGiftAt;
        public string LastResult = "";
        public string LastWinner = ""; public int LastWinnerVotes, LastWinnerTotal; public float LastWinnerAt = -100f;
        private float nextVoteAt = -1f;
        public float NextVoteIn => Mathf.Max(0f, nextVoteAt - Time.unscaledTime);
        public bool WaitingForDay { get; private set; }

        // ---- timed effects ----
        private float doubleCashUntil = -1f, freezeClockUntil = -1f, taserUntil = -1f, nextTase;
        private long lastPool = -1;
        private float frozenClock;
        private readonly List<KeyValuePair<float, PlayerRagdollManager>> pendingLaunch = new List<KeyValuePair<float, PlayerRagdollManager>>();
        public string ActiveEffect
        {
            get
            {
                float t = Time.unscaledTime;
                if (doubleCashUntil > t) return "DOUBLE CASH " + Mathf.CeilToInt(doubleCashUntil - t) + "s";
                if (freezeClockUntil > t) return "BOSS ON BREAK " + Mathf.CeilToInt(freezeClockUntil - t) + "s";
                if (taserUntil > t) return "TASER PARTY " + Mathf.CeilToInt(taserUntil - t) + "s";
                return "";
            }
        }

        private void TickEffects(bool allowed)
        {
            float t = Time.unscaledTime;
            if (!allowed) { doubleCashUntil = freezeClockUntil = taserUntil = -1f; pendingLaunch.Clear(); return; }
            var gm = Gm;
            if (gm == null) return;

            // Launch: push ragdolls up a few frames after they go limp (ragdoll needs a moment to switch on)
            for (int i = pendingLaunch.Count - 1; i >= 0; i--)
            {
                if (t < pendingLaunch[i].Key) continue;
                var r = pendingLaunch[i].Value;
                if (r != null) r.ServerVelocityDelta(new Vector3(UnityEngine.Random.Range(-1.5f, 1.5f), 4.5f, UnityEngine.Random.Range(-1.5f, 1.5f)));
                pendingLaunch.RemoveAt(i);
            }

            // Double cash: match every dollar the team earns
            if (doubleCashUntil > t)
            {
                long pool = gm.MoneyPoolLong;
                if (lastPool >= 0 && pool > lastPool) { gm.ServerCreditTeamMoney(pool - lastPool); pool = gm.MoneyPoolLong; }
                lastPool = pool;
            }
            else lastPool = -1;

            // Boss on break: hold the clock
            if (freezeClockUntil > t && gm.DayTimeRemaining > 0f && gm.DayTimeRemaining < frozenClock) gm.NetworkdayTimeRemaining = frozenClock;

            // Taser party: tase everyone every 6 seconds
            if (taserUntil > t && t >= nextTase) { nextTase = t + 6f; Tase(); }
        }

        public void Tick(bool allowed)
        {
            try { TickEffects(allowed); } catch (Exception e) { Loader.Log("Effect error: " + e.Message); }
            // timed effects always clean up, even if chaos gets switched off
            if (lowGravUntil > 0f && (Time.unscaledTime > lowGravUntil || !allowed)) { SetLowGrav(false); lowGravUntil = -1f; }
            if (fireLightUntil > 0f && (Time.unscaledTime > fireLightUntil || !allowed)) { SetFireLight(false); fireLightUntil = -1f; }

            var chat = Twitch.Drain();
            chat.AddRange(Bridge.Drain());
            var gifts = Bridge.DrainGifts();
            Gifts.Refresh(Time.unscaledTime);
            Bridge.KeepAlive(CreatorMode || GiftsOn);
            if (Bridge.Connected && Time.unscaledTime >= nextPublish)
            {
                nextPublish = Time.unscaledTime + 0.2f;
                try { Bridge.SendLine("#vote\t" + StateJson()); } catch (Exception e) { Loader.Log("Publish failed: " + e.Message); }
            }

            if (!allowed) { Enabled = false; CreatorMode = false; GiftsOn = false; Vote.Active = false; nextAt = -1f; giftQueue.Clear(); return; }

            // TikTok gifts fire their event (one every couple of seconds so a gift storm doesn't stack up)
            if (GiftsOn)
            {
                foreach (var g in gifts) { GiftsReceived++; if (giftQueue.Count < 30) giftQueue.Enqueue(g); }
                if (giftQueue.Count > 0 && Time.unscaledTime >= nextGiftAt) RunGift(giftQueue.Dequeue());
            }
            else
            {
                giftQueue.Clear();
                foreach (var g in gifts) { GiftsReceived++; LastGift = g.User + " sent " + g.Name + " - ignored: TikTok gifts are turned off"; LastGiftFired = false; }
            }

            // ----- Creator Mode: chat votes back-to-back, replaces the normal timer -----
            if (CreatorMode)
            {
                WaitingForDay = !DayRunning();
                if (Vote.Active)
                {
                    if (WaitingForDay) { Vote.EndsAt += Time.unscaledDeltaTime; return; } // freeze the vote between days
                    Vote.Feed(chat);
                    if (Vote.SecondsLeft <= 0f)
                    {
                        Vote.Active = false;
                        int w = Vote.Winner(rng);
                        LastResult = Vote.Options[w] + " won with " + Vote.Counts[w] + "/" + Vote.TotalVotes + " votes";
                        LastWinner = Vote.Options[w]; LastWinnerVotes = Vote.Counts[w]; LastWinnerTotal = Vote.TotalVotes; LastWinnerAt = Time.unscaledTime;
                        Trigger(Vote.Options[w], " (chat voted " + Vote.Counts[w] + "/" + Vote.TotalVotes + ")");
                        nextVoteAt = Time.unscaledTime + BreakSeconds;
                    }
                }
                else if (!WaitingForDay && Time.unscaledTime >= nextVoteAt)
                {
                    StartVote();
                }
                return;
            }
            Vote.Active = false;
            nextVoteAt = -1f;

            // ----- Normal Chaos Mode: random event on a timer -----
            if (!Enabled) { nextAt = -1f; return; }
            if (nextAt < 0f) nextAt = Time.unscaledTime + Interval;
            if (!DayRunning()) return; // pause between days / during review
            if (Time.unscaledTime >= nextAt)
            {
                TriggerRandom();
                nextAt = Time.unscaledTime + Interval;
            }
        }

        // Vote state for the OBS browser overlay (sent to ChatBridge).
        private string StateJson()
        {
            var sb = new System.Text.StringBuilder();
            string state;
            if (!CreatorMode) state = "off";
            else if (!Vote.Active && Time.unscaledTime - LastWinnerAt < 4f && LastWinner != "") state = "result";
            else if (Vote.Active) state = WaitingForDay ? "paused" : "vote";
            else state = WaitingForDay ? "waiting" : "idle";
            sb.Append("{\"state\":").Append(J(state));
            sb.Append(",\"secondsLeft\":").Append(Mathf.CeilToInt(Vote.SecondsLeft));
            sb.Append(",\"duration\":").Append(Mathf.RoundToInt(VoteSeconds));
            sb.Append(",\"remaining\":").Append(Vote.SecondsLeft.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
            sb.Append(",\"nextIn\":").Append(Mathf.CeilToInt(NextVoteIn));
            sb.Append(",\"total\":").Append(Vote.TotalVotes);
            sb.Append(",\"options\":[");
            for (int i = 0; i < Vote.Options.Length; i++)
            {
                if (i > 0) sb.Append(',');
                string n = Vote.Options[i];
                sb.Append("{\"name\":").Append(J(n)).Append(",\"desc\":").Append(J(VoteOverlay.Describe(n)))
                  .Append(",\"good\":").Append(VoteOverlay.Good.Contains(n) ? "true" : "false")
                  .Append(",\"count\":").Append(i < Vote.Counts.Length ? Vote.Counts[i] : 0).Append('}');
            }
            sb.Append("],\"winner\":{\"name\":").Append(J(LastWinner)).Append(",\"desc\":").Append(J(VoteOverlay.Describe(LastWinner)))
              .Append(",\"good\":").Append(VoteOverlay.Good.Contains(LastWinner) ? "true" : "false")
              .Append(",\"votes\":").Append(LastWinnerVotes).Append(",\"total\":").Append(LastWinnerTotal).Append("}");
            sb.Append(",\"gift\":{\"seq\":").Append(GiftSeq).Append(",\"user\":").Append(J(GiftUser)).Append(",\"name\":").Append(J(GiftName))
              .Append(",\"count\":").Append(GiftCount).Append(",\"event\":").Append(J(GiftEvent)).Append(",\"desc\":").Append(J(VoteOverlay.Describe(GiftEvent)))
              .Append(",\"good\":").Append(VoteOverlay.Good.Contains(GiftEvent) ? "true" : "false").Append("}}");
            return sb.ToString();
        }

        private static string J(string s)
        {
            if (s == null) return "\"\"";
            var sb = new System.Text.StringBuilder("\"");
            foreach (char c in s)
            {
                if (c == '"' || c == '\\') sb.Append('\\').Append(c);
                else if (c < ' ') sb.Append(' ');
                else sb.Append(c);
            }
            return sb.Append('"').ToString();
        }

        private void RunGift(GiftMsg g)
        {
            string ev = Gifts.Match(g.Name, g.Diamonds);
            Loader.Log("Gift: " + g.User + " sent " + g.Name + " x" + g.Count + " (" + g.Diamonds + " diamonds) -> " + (ev ?? "no rule"));
            string head = g.User + " sent " + g.Name + " (" + g.Diamonds + " diamonds)";
            LastGiftFired = false;
            if (ev == null) { LastGift = head + " - no rule for this gift. Add one in the list below."; return; }
            if (!DayRunning()) { LastGift = head + " - ignored: no workday is running"; Show("Gift from " + g.User + " arrived between days - no effect."); return; }
            nextGiftAt = Time.unscaledTime + 2f;
            LastGift = head + " -> " + ev;
            LastGiftFired = true;
            GiftUser = g.User; GiftName = g.Name; GiftEvent = ev; GiftCount = g.Count < 1 ? 1 : g.Count; GiftSeq++;
            Trigger(ev, " (" + g.User + " sent " + g.Name + ")");
        }

        // Menu "Test" button: act as if a viewer had sent this gift (works without TikTok connected).
        public void TestGift(string key)
        {
            string k = GiftMap.NormalizeKey(key);
            int dia = 1; string name = k;
            if (k.StartsWith(">=")) { int.TryParse(k.Substring(2).Trim(), out dia); name = "Test gift"; }
            RunGift(new GiftMsg { Platform = "test", User = "Test", Name = name, Diamonds = dia, Count = 1 });
        }

        public void StartVote()
        {
            var pool = Available().OrderBy(_ => rng.Next()).Take(3).Select(e => e.name).ToArray();
            if (pool.Length == 0) return;
            Vote.Start(pool, VoteSeconds);
            Loader.Log("Vote started: " + string.Join(" / ", pool));
        }

        public IEnumerable<string> EventNames => events.Where(e => !e.hidden).Select(e => e.name);
        public void TriggerByName(string name) { if (DayRunning()) Trigger(name, " (manual)"); else Show("Start the workday first."); }

        private List<Ev> Available() => events.Where(e => { try { return !e.hidden && e.ok(); } catch { return false; } }).ToList();

        private void Trigger(string name, string suffix)
        {
            var ev = events.FirstOrDefault(e => e.name == name);
            if (ev.run == null) return;
            bool ok; try { ok = ev.ok(); } catch { ok = false; }
            if (!ok) { Announce("CHAOS: " + name + " fizzled (can't happen right now)" + suffix); return; }
            string detail;
            try { detail = ev.run(); }
            catch (Exception e) { Loader.Log("Chaos event " + ev.name + " failed: " + e); return; }
            LastEvent = ev.name;
            Announce("CHAOS: " + ev.name + " - " + detail + suffix);
        }

        public void TriggerRandom()
        {
            if (!DayRunning()) { Show("Chaos waits for the workday to start..."); return; }
            var pool = events.Where(e => { try { return !e.hidden && e.ok(); } catch { return false; } }).ToList();
            if (pool.Count == 0) return;
            var ev = pool[rng.Next(pool.Count)];
            string detail;
            try { detail = ev.run(); }
            catch (Exception e) { Loader.Log("Chaos event " + ev.name + " failed: " + e); return; }
            LastEvent = ev.name;
            Announce("CHAOS: " + ev.name + " - " + detail);
        }

        // ---------- events ----------
        private string LowGravity()
        {
            SetLowGrav(true);
            lowGravUntil = Time.unscaledTime + 45f;
            return "Everyone floats for 45 seconds!";
        }

        private string MoneyRain()
        {
            int n = 0;
            foreach (var kp in KPlayers()) { kp.ServerCreditScamMoney(150); n++; }
            return "Everyone gets $150! (+" + (150 * n) + " to the pool)";
        }

        private string Jackpot()
        {
            var ps = Players();
            var lucky = ps[rng.Next(ps.Count)];
            var kp = lucky.GetComponent<KolkataOsPlayer>();
            if (kp != null) kp.ServerCreditScamMoney(750);
            return lucky.playerName + " won $750!";
        }

        private string AddTime(float secs, string msg)
        {
            var gm = Gm;
            gm.NetworkdayTimeRemaining = Mathf.Max(30f, gm.DayTimeRemaining + secs);
            return msg;
        }

        private string ScaleQuota(float f, string msg)
        {
            var gm = Gm;
            gm.ServerSetQuota(Mathf.Max(1, Mathf.RoundToInt(gm.Quota * f)));
            return msg + " New quota: $" + gm.Quota.ToString("N0");
        }

        private string TaxMan()
        {
            var gm = Gm;
            long tax = Math.Max(50L, gm.MoneyPoolLong / 10);
            gm.ServerTrySpendTeamMoney(0UL, "Chaos tax", tax, out _);
            return "The tax man took $" + tax.ToString("N0") + " from the team!";
        }

        private string FireDrill()
        {
            SetFireLight(true);
            fireLightUntil = Time.unscaledTime + 15f;
            return "Fire alarm! (it's just a drill)";
        }

        private string FakeFiring()
        {
            var ids = Players().Select(p => p.netId).ToArray();
            mFiredFx.Invoke(Gm, new object[] { ids, new Vector3[0], 5f });
            return "...just kidding. Get back to work.";
        }

        private string Huddle()
        {
            var ps = Players();
            var host = ps[rng.Next(ps.Count)];
            Vector3 c = host.transform.position;
            foreach (var p in ps)
            {
                if (p == host) continue;
                var off = UnityEngine.Random.insideUnitCircle * 1.5f;
                Teleport(p, c + new Vector3(off.x, 0.2f, off.y));
            }
            return "Everyone teleported to " + host.playerName + "!";
        }

        private string Swap()
        {
            var ps = Players().OrderBy(_ => rng.Next()).ToList();
            var a = ps[0]; var b = ps[1];
            Vector3 pa = a.transform.position, pb = b.transform.position;
            Teleport(a, pb + Vector3.up * 0.2f);
            Teleport(b, pa + Vector3.up * 0.2f);
            return a.playerName + " and " + b.playerName + " swapped places!";
        }

        private string Launch()
        {
            float t = Time.unscaledTime;
            foreach (var r in Ragdolls())
            {
                if (!TryRagdoll(r, 6f, false)) continue;
                // several upward kicks right after the ragdoll switches on = a big launch
                for (int k = 0; k < 3; k++) pendingLaunch.Add(new KeyValuePair<float, PlayerRagdollManager>(t + 0.08f + k * 0.06f, r));
            }
            return "Everyone gets YEETED into the air!";
        }

        private string TaserParty()
        {
            taserUntil = Time.unscaledTime + 25f;
            nextTase = Time.unscaledTime;
            return "Everyone gets tased every 6 seconds for 25 seconds!";
        }

        private string Payday()
        {
            int n = 0;
            foreach (var kp in KPlayers()) { Gm.ServerCreditSpendableMoney(kp, kp.RunSaveSteamId, 400); n++; }
            return "Everyone gets $400 Scamazon money!";
        }

        private string DoubleCash()
        {
            doubleCashUntil = Time.unscaledTime + 60f;
            lastPool = Gm.MoneyPoolLong;
            return "Every dollar scammed in the next 60s counts DOUBLE!";
        }

        private string BossOnBreak()
        {
            freezeClockUntil = Time.unscaledTime + 30f;
            frozenClock = Gm.DayTimeRemaining;
            return "The clock is frozen for 30 seconds!";
        }

        private string DoubleOrNothing()
        {
            var gm = Gm;
            long pool = gm.MoneyPoolLong;
            if (rng.Next(2) == 0)
            {
                gm.ServerCreditTeamMoney(pool);
                return "WON! The team money doubled to $" + gm.MoneyPoolLong.ToString("N0") + "!";
            }
            gm.ServerTrySpendTeamMoney(0UL, "Double or nothing", pool, out _);
            return "LOST! The team money is gone!";
        }

        private string Tase()
        {
            foreach (var r in Ragdolls()) TryRagdoll(r, 3f, true);
            return "Everyone got tased!";
        }

        private string EndDay()
        {
            Gm.ServerEndCurrentDayEarly();
            return "The day is over - clocking out early!";
        }

        private string Drunk()
        {
            foreach (var r in Ragdolls()) r.ServerSetDrunkForTime(30f);
            return "Everyone is drunk for 30 seconds!";
        }

        private string Ragdoll(float secs, Vector3 vel, string msg)
        {
            foreach (var r in Ragdolls()) TryRagdoll(r, secs, false);
            return msg;
        }

        private static List<VictimCall> ActiveCalls() =>
            UnityEngine.Object.FindObjectsByType<VictimCall>(FindObjectsSortMode.None)
                .Where(c => c != null && c.callState == NetworkedCall.CallState.Talking).ToList();

        private string HangUpAll()
        {
            var calls = ActiveCalls();
            foreach (var c in calls) c.ServerHangUp();
            return calls.Count + " call(s) got disconnected!";
        }

        private string Tax(float frac, string msg)
        {
            var gm = Gm;
            long tax = Math.Max(50L, (long)(gm.MoneyPoolLong * frac));
            gm.ServerTrySpendTeamMoney(0UL, "Chaos: " + msg, tax, out _);
            return msg + " (-$" + tax.ToString("N0") + ")";
        }

        private string Pickpocket()
        {
            var victims = KPlayers().Where(k => k.SpendableMoney > 0).ToList();
            var v = victims[rng.Next(victims.Count)];
            long amt = v.SpendableMoney;
            Gm.ServerTrySpendPersonalMoney(v, v.RunSaveSteamId, "Pickpocketed!", amt, out _);
            var pn = v.GetComponent<PlayerName>();
            return (pn != null ? pn.playerName : "Someone") + " got pickpocketed and lost $" + amt.ToString("N0") + " shopping money!";
        }

        private string TimeCrunch()
        {
            var gm = Gm;
            gm.NetworkdayTimeRemaining = Mathf.Max(30f, gm.DayTimeRemaining * 0.5f);
            return "Half the day's time is gone!";
        }

        private string Evacuate()
        {
            var pt = Gm.GetDayStartPoint(Gm.CurrentDay);
            if (pt == null) return "Nowhere to run.";
            foreach (var p in Players())
            {
                var off = UnityEngine.Random.insideUnitCircle * 2f;
                Teleport(p, pt.position + new Vector3(off.x, 0.2f, off.y));
            }
            return "Everyone got sent back to the entrance!";
        }

        // Find every player's ragdoll component wherever it lives on the player object, and log what we find.
        private static List<PlayerRagdollManager> Ragdolls()
        {
            var set = new List<PlayerRagdollManager>();
            foreach (var p in Players())
            {
                var r = p.GetComponent<PlayerRagdollManager>() ?? p.GetComponentInChildren<PlayerRagdollManager>(true) ?? p.GetComponentInParent<PlayerRagdollManager>();
                if (r != null && !set.Contains(r)) set.Add(r);
            }
            if (set.Count == 0)
                foreach (var r in UnityEngine.Object.FindObjectsByType<PlayerRagdollManager>(FindObjectsSortMode.None))
                    if (r != null && r.GetComponent<MainMenuSecurityNpc>() == null && !set.Contains(r)) set.Add(r);
            var imm = typeof(PlayerRagdollManager).GetField("ragdollImmunity", BindingFlags.Instance | BindingFlags.NonPublic);
            Loader.Log("Ragdoll targets: " + set.Count + " (players " + Players().Count + ")" +
                string.Concat(set.Select(r => " [" + r.name + " immune=" + (imm != null ? imm.GetValue(r) : "?") + " handler=" + SafeHandler(r) + "]")));
            return set;
        }

        private static string SafeHandler(PlayerRagdollManager r)
        {
            try { return r.GetCurrentRagdollHandler() != null ? "yes" : "no"; } catch (Exception e) { return "err:" + e.GetType().Name; }
        }

        private static bool TryRagdoll(PlayerRagdollManager r, float secs, bool tase)
        {
            try
            {
                bool ok = r.ServerBeginRagdoll(Vector3.zero, secs, Vector3.zero, instantStun: true, isTase: tase, dropHeldItems: true);
                if (!ok) Loader.Log("  ragdoll refused for " + r.name);
                return ok;
            }
            catch (Exception e) { Loader.Log("  ragdoll error for " + r.name + ": " + e.GetType().Name + " " + e.Message); return false; }
        }

        // ---------- helpers ----------
        private static List<PlayerName> Players() =>
            PlayerName.Instances.Where(p => p != null && p.gameObject.activeInHierarchy).ToList();

        private static IEnumerable<KolkataOsPlayer> KPlayers() =>
            Players().Select(p => p.GetComponent<KolkataOsPlayer>()).Where(k => k != null);

        private static void Teleport(PlayerName p, Vector3 pos)
        {
            var t = p.GetComponent<PlayerTeleporter>();
            if (t != null) t.ServerTeleportToPosition(pos, "CHAOS_MODE", "ChaosMode");
        }

        private static void SetLowGrav(bool on)
        {
            if (GameManager.Instance != null) GameManager.Instance.SetLowGravityEnabled(on);
        }

        private static void SetFireLight(bool on)
        {
            if (Gm != null && mFireLight != null) mFireLight.Invoke(Gm, new object[] { on });
        }

        private void Show(string msg) { Banner = msg; BannerUntil = Time.unscaledTime + 5f; }

        private void Announce(string msg)
        {
            Show(msg);
            Loader.Log(msg);
            // Also post it in the game's event feed so friends see what happened (if this mode has one).
            try { if (LogManager.Instance != null) LogManager.Instance.RpcAppendSimple(msg); } catch { }
        }
    }
}
