// ChatBridge - reads live chat from YouTube, TikTok, Kick and Twitch and forwards it to the
// Chaos Mode game mod on this PC (localhost:47800). Run it while you stream; leave the window open.
"use strict";
process.noDeprecation = true;
const fs = require("fs");
const path = require("path");
const net = require("net");
const readline = require("readline");

const VERSION = "1.2.2";
const PORT = 47800;
const OVERLAY_PORT = 47801;
const http = require("http");
const OVERLAY_HTML = require("./overlay.html");
const baseDir = process.pkg ? path.dirname(process.execPath) : __dirname;
const cfgPath = path.join(baseDir, "chatbridge.json");

const defaults = {
  youtube: "",          // @handle, channel id (UC...), or a live video id
  useTikFinity: true,   // TikTok chat comes from the TikFinity desktop app (recommended)
  tikfinityUrl: "ws://localhost:21213/",
  tiktok: "",           // only used if useTikFinity is false: TikTok username for a direct connection
  tiktokSignApiKey: "", // only used if useTikFinity is false
  kick: "",             // Kick channel name
  kickChatroomId: "",   // optional: only needed if Kick lookup is blocked
  twitch: ""            // Twitch channel (optional - the mod also has Twitch built in)
};

function log(platform, msg) {
  const t = new Date().toLocaleTimeString();
  console.log(`[${t}] ${platform.padEnd(8)} ${msg}`);
}

// ---------------- game connection (the mod connects to us) ----------------
const clients = new Set();
const status = {};
function statusLine() {
  const parts = Object.entries(status).map(([k, v]) => `${k}: ${v}`);
  return parts.length ? parts.join(" | ") : "no platforms set";
}
function sendToGame(line) {
  for (const c of clients) { try { c.write(line + "\n"); } catch (_) {} }
}
function setStatus(platform, s) {
  status[platform] = s;
  log(platform, s);
  sendToGame("#status\t" + statusLine());
}
function chat(platform, user, message) {
  if (!user || !message) return;
  const clean = (x) => String(x).replace(/[\t\r\n]+/g, " ").trim();
  sendToGame(`${platform}\t${clean(user)}\t${clean(message)}`);
  if (/^#?[1-9]$/.test(message.trim())) log(platform, `${user} voted ${message.trim()}`);
}

// Gifts (TikTok): forwarded to the mod, which maps them to events using BepInEx\config\ChaosMode\gifts.txt.
function gift(platform, user, name, diamonds, count) {
  const clean = (x) => String(x == null ? "" : x).replace(/[\t\r\n]+/g, " ").trim();
  if (!name) { log(platform, "got a gift but couldn't read its name (see the payload line above)"); return; }
  count = Math.max(1, Number(count) || 1);
  diamonds = Math.max(0, Math.round(Number(diamonds) || 0));
  log(platform, `${clean(user) || "someone"} sent ${clean(name)} x${count} (${diamonds} diamonds)`);
  sendToGame(`#gift\t${platform}\t${clean(user) || "someone"}\t${clean(name)}\t${diamonds}\t${count}`);
}

let gameCount = 0;
function sendGameState() { sendToGame("#game\t" + gameCount); }
const server = net.createServer((sock) => {
  clients.add(sock);
  sock.isGui = false;
  let decided = false;
  // The GUI identifies itself with "#gui" right away; anything else is the game mod.
  const decide = (isGui) => {
    if (decided) return; decided = true;
    sock.isGui = isGui;
    if (!isGui) { gameCount++; log("game", "Chaos Mode connected"); }
    sock.write("#status\t" + statusLine() + "\n");
    if (!isGui) sock.write("#version\t" + VERSION + "\n");
    sendGameState();
  };
  let buf = "";
  sock.on("data", (d) => {
    const text = d.toString();
    if (!decided) decide(text.startsWith("#gui"));
    if (sock.isGui) return;
    buf += text;
    let nl;
    while ((nl = buf.indexOf("\n")) >= 0) {
      const line = buf.slice(0, nl).replace(/\r$/, ""); buf = buf.slice(nl + 1);
      if (line.startsWith("#vote\t")) publishVote(line.slice(6));
    }
    if (buf.length > 100000) buf = "";
  });
  setTimeout(() => decide(false), 400);
  sock.on("close", () => {
    clients.delete(sock);
    if (decided && !sock.isGui) { gameCount = Math.max(0, gameCount - 1); log("game", "Chaos Mode disconnected"); sendGameState(); }
  });
  sock.on("error", () => clients.delete(sock));
});
server.on("error", (e) => {
  console.error(e.code === "EADDRINUSE" ? "ChatBridge is already running in another window." : e.message);
  setTimeout(() => process.exit(1), 5000);
});
server.listen(PORT, "127.0.0.1");

// ---------------- OBS overlay (browser source) ----------------
const sseClients = new Set();
let lastVote = null;
function publishVote(json) {
  lastVote = json;
  for (const res of sseClients) { try { res.write("data: " + json + "\n\n"); } catch (_) {} }
}
const overlayServer = http.createServer((req, res) => {
  const url = req.url.split("?")[0];
  if (url === "/events") {
    res.writeHead(200, { "Content-Type": "text/event-stream", "Cache-Control": "no-cache", Connection: "keep-alive", "Access-Control-Allow-Origin": "*" });
    res.write("retry: 1500\n\n");
    if (lastVote && gameCount > 0) res.write("data: " + lastVote + "\n\n");
    sseClients.add(res);
    req.on("close", () => sseClients.delete(res));
    return;
  }
  if (url === "/" || url === "/overlay") {
    res.writeHead(200, { "Content-Type": "text/html; charset=utf-8", "Cache-Control": "no-cache" });
    res.end(OVERLAY_HTML);
    return;
  }
  res.writeHead(404); res.end();
});
overlayServer.on("error", (e) => log("overlay", "couldn't start overlay server: " + e.message));
overlayServer.listen(OVERLAY_PORT, "127.0.0.1", () => { log("overlay", `OBS overlay: http://localhost:${OVERLAY_PORT}/overlay`); log("overlay", `TikTok LIVE Studio (it rejects "localhost"): http://localtest.me:${OVERLAY_PORT}/overlay`); });

// ---------------- YouTube ----------------
async function startYouTube(id) {
  const { LiveChat } = require("youtube-chat");
  id = id.trim();
  let opts;
  if (id.startsWith("@")) opts = { handle: id };
  else if (/^UC[\w-]{22}$/.test(id)) opts = { channelId: id };
  else opts = { liveId: id.replace(/^.*[?&]v=/, "").replace(/^.*youtu\.be\//, "").replace(/[?&].*$/, "") };
  const connect = async () => {
    const lc = new LiveChat(opts);
    lc.on("start", () => setStatus("YouTube", "connected"));
    lc.on("end", () => { setStatus("YouTube", "stream ended, retrying..."); setTimeout(connect, 30000); });
    lc.on("error", (e) => log("YouTube", "error: " + (e && e.message ? e.message : e)));
    lc.on("chat", (item) => {
      const text = (item.message || []).map((m) => m.text || m.emojiText || "").join("");
      chat("youtube", item.author && (item.author.channelId || item.author.name), text);
    });
    setStatus("YouTube", "connecting...");
    const ok = await lc.start().catch(() => false);
    if (!ok) { setStatus("YouTube", "not live yet, retrying in 30s"); setTimeout(connect, 30000); }
  };
  connect();
}

// ---------------- TikTok ----------------
function startTikTok(user, apiKey) {
  const { TikTokLiveConnection, WebcastEvent, ControlEvent } = require("tiktok-live-connector");
  user = user.trim().replace(/^@/, "");
  const connect = () => {
    const conn = new TikTokLiveConnection(user, apiKey ? { signApiKey: apiKey } : {});
    conn.on(WebcastEvent.CHAT, (d) => chat("tiktok", d.user && (d.user.uniqueId || d.user.userId), d.comment));
    conn.on(WebcastEvent.GIFT, (d) => {
      const g = readGift(d);
      if (g.streak && g.repeatEnd === false) return; // combo still running
      gift("tiktok", g.user, g.name, g.diamonds * g.count, g.count);
    });
    conn.on(ControlEvent.DISCONNECTED, () => { setStatus("TikTok", "disconnected, retrying in 20s"); setTimeout(connect, 20000); });
    setStatus("TikTok", "connecting...");
    conn.connect()
      .then(() => setStatus("TikTok", "connected"))
      .catch((e) => {
        const m = (e && e.message) || String(e);
        setStatus("TikTok", (/offline|not live/i.test(m) ? "not live yet" : "failed: " + m.slice(0, 80)) + ", retrying in 30s");
        setTimeout(connect, 30000);
      });
  };
  connect();
}

// ---- reading gifts from TikFinity / TikTok payloads (field names vary, so look at several spellings, nested too) ----
const norm = (k) => String(k).toLowerCase().replace(/[^a-z0-9]/g, "");
function dig(obj, keys, depth) {
  // breadth-first: the shallowest matching, non-empty primitive wins
  const want = new Set(keys.map(norm));
  let level = [obj];
  for (let d = 0; d <= (depth == null ? 3 : depth) && level.length; d++) {
    const next = [];
    for (const o of level) {
      if (!o || typeof o !== "object") continue;
      for (const [k, v] of Object.entries(o)) {
        if (want.has(norm(k)) && v != null && typeof v !== "object" && v !== "") return v;
        if (v && typeof v === "object") next.push(v);
      }
    }
    level = next;
  }
  return undefined;
}
function looksLikeGift(d) { return dig(d, ["giftName", "gift_name", "giftId", "gift_id"], 2) !== undefined; }
function readGift(d) {
  const name = dig(d, ["giftName", "gift_name", "name_of_gift", "extendedGiftInfo.name"], 3)
    || (() => { const g = d.giftDetails || d.gift || d.extendedGiftInfo; return g && typeof g === "object" ? (g.giftName || g.name) : undefined; })()
    || (dig(d, ["giftId", "gift_id"], 3) !== undefined ? "Gift " + dig(d, ["giftId", "gift_id"], 3) : undefined);
  const each = Number(dig(d, ["diamondCount", "diamond_count", "diamonds", "giftValue", "coins", "value"], 3)) || 0;
  const count = Number(dig(d, ["repeatCount", "repeat_count", "comboCount", "combo", "count", "amount"], 3)) || 1;
  const end = dig(d, ["repeatEnd", "repeat_end"], 3);
  const type = dig(d, ["giftType", "gift_type"], 3);
  const user = dig(d, ["uniqueId", "unique_id", "userName", "username", "nickname", "userId", "user_id"], 3);
  return { name, diamonds: each, count, user, streak: Number(type) === 1, repeatEnd: end === undefined ? undefined : (end === true || end === "true" || end === 1) };
}

// ---------------- TikTok via TikFinity ----------------
// TikFinity Desktop exposes a local Event API websocket (default ws://localhost:21213/).
function startTikFinity(url) {
  const WebSocket = require("ws");
  let announced = false, giftLogs = 0;
  const seen = new Set();
  const connect = () => {
    const ws = new WebSocket(url);
    ws.on("open", () => setStatus("TikTok", "connected via TikFinity"));
    ws.on("message", (raw) => {
      let m; try { m = JSON.parse(raw.toString()); } catch (_) { return; }
      const ev = String(m.event || m.type || m.eventType || "");
      const d = m.data || m.payload || m;
      if (!seen.has(ev) && seen.size < 30) { seen.add(ev); log("TikTok", "TikFinity event type: " + (ev || "(none)")); }
      const u = d.user || {};
      if (/gift/i.test(ev) || looksLikeGift(d)) {
        if (giftLogs < 5) { giftLogs++; log("TikTok", "gift payload: " + JSON.stringify(m).slice(0, 400)); }
        const g = readGift(d);
        // a gift streak (combo) is still running: wait for its final message so one combo fires once
        if (g.streak && g.repeatEnd === false) return;
        gift("tiktok", g.user, g.name, g.diamonds * g.count, g.count);
        return;
      }
      if (!/chat|comment/i.test(ev)) return;
      const user = d.uniqueId || u.uniqueId || d.userId || u.userId || d.nickname || u.nickname;
      const text = d.comment != null ? d.comment : (d.text != null ? d.text : d.message);
      if (text == null) return;
      chat("tiktok", user, String(text));
    });
    ws.on("close", () => {
      setStatus("TikTok", announced ? "TikFinity closed, retrying..." : "waiting for TikFinity (open it and enable the Event API)");
      announced = true;
      setTimeout(connect, 5000);
    });
    ws.on("error", () => {});
  };
  connect();
}

// ---------------- Kick ----------------
async function kickChatroomId(slug, override) {
  if (override) return String(override).trim();
  const res = await fetch(`https://kick.com/api/v2/channels/${encodeURIComponent(slug)}`, {
    headers: { "User-Agent": "Mozilla/5.0", Accept: "application/json" }
  });
  if (!res.ok) throw new Error("Kick lookup blocked (HTTP " + res.status + "). Put kickChatroomId in chatbridge.json.");
  const j = await res.json();
  if (!j.chatroom || !j.chatroom.id) throw new Error("channel not found");
  return String(j.chatroom.id);
}

function startKick(slug, override) {
  const WebSocket = require("ws");
  slug = slug.trim().toLowerCase();
  const connect = async () => {
    let roomId;
    try { roomId = await kickChatroomId(slug, override); }
    catch (e) { setStatus("Kick", e.message + " - retrying in 60s"); setTimeout(connect, 60000); return; }
    const ws = new WebSocket("wss://ws-us2.pusher.com/app/32cbd69e4b950bf97679?protocol=7&client=js&version=8.4.0&flash=false");
    ws.on("open", () => {
      ws.send(JSON.stringify({ event: "pusher:subscribe", data: { auth: "", channel: `chatrooms.${roomId}.v2` } }));
      setStatus("Kick", "connected");
    });
    ws.on("message", (raw) => {
      let m; try { m = JSON.parse(raw.toString()); } catch (_) { return; }
      if (m.event === "pusher:ping") { ws.send(JSON.stringify({ event: "pusher:pong", data: {} })); return; }
      if (!/ChatMessageEvent/.test(m.event || "")) return;
      let d; try { d = typeof m.data === "string" ? JSON.parse(m.data) : m.data; } catch (_) { return; }
      chat("kick", d.sender && (d.sender.username || d.sender.id), d.content);
    });
    ws.on("close", () => { setStatus("Kick", "disconnected, retrying in 10s"); setTimeout(connect, 10000); });
    ws.on("error", (e) => log("Kick", "error: " + e.message));
  };
  connect();
}

// ---------------- Twitch (optional; the mod has Twitch built in) ----------------
function startTwitch(channel) {
  const tmi = require("tmi.js");
  const client = new tmi.Client({ channels: [channel.trim().replace(/^#/, "").toLowerCase()], connection: { reconnect: true, secure: true } });
  client.on("connected", () => setStatus("Twitch", "connected"));
  client.on("disconnected", () => setStatus("Twitch", "disconnected"));
  client.on("message", (_ch, tags, msg) => chat("twitch", tags.username, msg));
  setStatus("Twitch", "connecting...");
  client.connect().catch((e) => setStatus("Twitch", "failed: " + e));
}

// ---------------- setup ----------------
function ask(rl, q) { return new Promise((r) => rl.question(q, (a) => r(a.trim()))); }

async function main() {
  console.log("==============================================");
  console.log("  ChatBridge for Chaos Mode");
  console.log("  Sends your stream chat into the game for votes.");
  console.log("==============================================\n");

  let cfg = { ...defaults };
  if (fs.existsSync(cfgPath)) {
    try { cfg = { ...defaults, ...JSON.parse(fs.readFileSync(cfgPath, "utf8")) }; } catch (e) { console.log("chatbridge.json is broken, starting setup again."); }
  }
  const nothingSet = !fs.existsSync(cfgPath) && !process.argv.includes("--headless");
  if (nothingSet) {
    try {
      const rl = readline.createInterface({ input: process.stdin, output: process.stdout });
      console.log("First-time setup. Press Enter to skip a platform.\n");
      cfg.youtube = await ask(rl, "YouTube (@handle, channel ID, or live video link): ");
      const tf = await ask(rl, "Use TikFinity for TikTok chat? (Y/n): ");
      cfg.useTikFinity = !/^n/i.test(tf);
      if (!cfg.useTikFinity) cfg.tiktok = await ask(rl, "TikTok username (direct connection): ");
      cfg.kick = await ask(rl, "Kick channel name: ");
      cfg.twitch = await ask(rl, "Twitch channel (optional, the mod already has Twitch): ");
      rl.close();
    } catch (e) {
      console.log("Couldn't ask questions here (" + e.message + ").");
    }
    fs.writeFileSync(cfgPath, JSON.stringify(cfg, null, 2));
    console.log(`\nSaved to ${cfgPath} (edit it any time).\n`);
    if (!cfg.youtube && !cfg.useTikFinity && !cfg.tiktok && !cfg.kick && !cfg.twitch) {
      console.log("No platforms entered. Opening chatbridge.json in Notepad - fill it in, save, then restart ChatBridge.");
      try { require("child_process").spawn("notepad.exe", [cfgPath], { detached: true, stdio: "ignore" }).unref(); } catch (_) {}
    }
  } else {
    console.log(`Using settings from ${cfgPath}\n`);
  }

  if (cfg.youtube) startYouTube(cfg.youtube);
  if (cfg.useTikFinity) startTikFinity(cfg.tikfinityUrl || "ws://localhost:21213/");
  else if (cfg.tiktok) startTikTok(cfg.tiktok, cfg.tiktokSignApiKey);
  if (cfg.kick) startKick(cfg.kick, cfg.kickChatroomId);
  if (cfg.twitch) startTwitch(cfg.twitch);
  if (!cfg.youtube && !cfg.useTikFinity && !cfg.tiktok && !cfg.kick && !cfg.twitch) console.log("No platforms set. Edit chatbridge.json and restart.");

  log("game", `Waiting for the game on port ${PORT}... (turn on Creator Mode with F2)`);
}

process.on("uncaughtException", (e) => log("error", e && e.stack ? e.stack.split("\n")[0] : String(e)));
process.on("unhandledRejection", (e) => log("error", e && e.message ? e.message : String(e)));
if (process.argv.includes("--selftest")) {
  const tests = { "youtube-chat": () => require("youtube-chat"), "tiktok-live-connector": () => require("tiktok-live-connector"), ws: () => require("ws"), "tmi.js": () => require("tmi.js") };
  for (const [n, f] of Object.entries(tests)) { try { f(); console.log("ok", n); } catch (e) { console.log("FAIL", n, e.message.split("\n")[0]); } }
  process.exit(0);
}
main();
