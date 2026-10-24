"""
Thunderstore Vanity Tracker for djcdevelopment (Valheim)
Features:
- Live 5-minute polling cycle with West Coast (Pacific Time) timestamps
- Dynamic "Celebration Window" alert highlighting new installs per 5m pulse
- Cumulative leaderboard podium with medal ranks
- Dynamic milestone progress bar (e.g. road to 1,000 downloads)
- Comprehensive velocity breakdown (5m, 1h, 3h, 6h, 12h, Cumulative)
- Fast real-time package-metrics querying (<300ms)
- Automatic background HTTP server on port 5050 (/api/stats + web widget)
- Visual terminal padding with full UTF-8 box drawing and ANSI colors
- Clean X11 terminal glyph fallback for Linux displays
"""

import os
import sys
import time
import json
import re
import urllib.request
import http.server
import socketserver
import threading
from datetime import datetime, timedelta

os.system('')
if hasattr(sys.stdout, 'reconfigure'):
    sys.stdout.reconfigure(encoding='utf-8', errors='replace')

# Pacific Time (West Coast) Zone
try:
    from zoneinfo import ZoneInfo
    PT_ZONE = ZoneInfo("America/Los_Angeles")
except Exception:
    from datetime import timezone
    PT_ZONE = timezone(timedelta(hours=-7), name="PDT")

COMMUNITY = "valheim"
OWNER = "djcdevelopment"
DATA_DIR = os.path.dirname(os.path.abspath(__file__))
HISTORY_FILE = os.path.join(DATA_DIR, "history.json")
DEFAULT_INTERVAL_MINS = 5
PORT = 5050

KNOWN_MODS = ["IsModded", "Unfaded", "Unswayed", "SelfieStick", "TotemSentinel"]

WINDOWS = [
    ("5m", 5 * 60),
    ("1h", 1 * 3600),
    ("3h", 3 * 3600),
    ("6h", 6 * 3600),
    ("12h", 12 * 3600)
]

TOLERANCES = {
    "5m": 600,       # allow up to 10m
    "1h": 1200,      # within 20m of 1h ago
    "3h": 2700,      # within 45m of 3h ago
    "6h": 5400,      # within 90m of 6h ago
    "12h": 10800     # within 3h of 12h ago
}

# ANSI color codes
C_RESET = "\033[0m"
C_BOLD = "\033[1m"
C_DIM = "\033[2m"
C_GREEN = "\033[92m"
C_YELLOW = "\033[93m"
C_CYAN = "\033[96m"
C_MAGENTA = "\033[95m"
C_WHITE = "\033[97m"

def is_x11_mode():
    return "--x11" in sys.argv or os.environ.get("AM4_DISPLAY") == "1" or (sys.platform.startswith("linux") and os.environ.get("DISPLAY"))

def strip_ansi(s):
    return re.sub(r'\033\[[0-9;]*m', '', s)

def visual_len(s):
    clean = strip_ansi(s)
    wide_chars = set('🚀✨🎉⚡🥇🥈🥉🎖🔥🏆🎯')
    w = 0
    for ch in clean:
        w += 2 if ch in wide_chars else 1
    return w

def pad_to(s, width, align='left'):
    cur_w = visual_len(s)
    padding = max(0, width - cur_w)
    if align == 'right':
        return (' ' * padding) + s
    elif align == 'center':
        left = padding // 2
        right = padding - left
        return (' ' * left) + s + (' ' * right)
    else:
        return s + (' ' * padding)

def get_pt_time():
    return datetime.now(PT_ZONE)

def format_pt_timestamp(dt):
    return dt.strftime("%Y-%m-%d %I:%M:%S %p %Z")

def fetch_mod_metric(mod_name, last_known=0):
    """Fetch real-time metrics from Thunderstore package-metrics endpoint."""
    url = f"https://thunderstore.io/api/v1/package-metrics/{OWNER}/{mod_name}/"
    req = urllib.request.Request(url, headers={"User-Agent": "ThunderstoreVanity/2.0"})
    try:
        with urllib.request.urlopen(req, timeout=8) as resp:
            data = json.loads(resp.read().decode("utf-8"))
            dls = data.get("downloads", last_known)
            return {
                "name": mod_name,
                "downloads": max(dls, last_known),
                "rating_score": data.get("rating_score", 0),
                "latest_version": data.get("latest_version", "1.0.0"),
                "url": f"https://thunderstore.io/c/{COMMUNITY}/p/{OWNER}/{mod_name}/"
            }
    except Exception as e:
        return {
            "name": mod_name,
            "downloads": last_known,
            "rating_score": 0,
            "latest_version": "?",
            "url": f"https://thunderstore.io/c/{COMMUNITY}/p/{OWNER}/{mod_name}/",
            "error": str(e)
        }

def get_live_metrics(history=None):
    if history is None:
        history = load_history()
    last_counts = history[-1].get("counts", {}) if history else {}
    return [fetch_mod_metric(m, last_known=last_counts.get(m, 0)) for m in KNOWN_MODS]

def load_history():
    if os.path.exists(HISTORY_FILE):
        try:
            with open(HISTORY_FILE, "r", encoding="utf-8") as f:
                return json.load(f)
        except Exception:
            return []
    return []

def save_history(history):
    cutoff = time.time() - (14 * 86400)
    trimmed = [pt for pt in history if pt.get("timestamp", 0) >= cutoff]
    with open(HISTORY_FILE, "w", encoding="utf-8") as f:
        json.dump(trimmed, f, indent=2)

def record_snapshot(mods):
    history = load_history()
    now = time.time()
    now_pt = get_pt_time()
    last_counts = history[-1].get("counts", {}) if history else {}

    clean_counts = {}
    for m in mods:
        name = m["name"]
        curr = m.get("downloads", 0)
        prev = last_counts.get(name, 0)
        clean = max(curr, prev)
        clean_counts[name] = clean
        m["downloads"] = clean

    snapshot = {
        "timestamp": now,
        "pt_time": now_pt.strftime("%Y-%m-%d %I:%M:%S %p %Z"),
        "counts": clean_counts
    }
    history.append(snapshot)
    save_history(history)
    return history

def compute_velocities(mods, history):
    now = time.time()
    enriched = []

    window_snaps = {}
    for key, secs in WINDOWS:
        target_t = now - secs
        tol = TOLERANCES.get(key, secs * 0.3)
        best = None
        best_diff = float("inf")
        for pt in history:
            diff = abs(pt.get("timestamp", 0) - target_t)
            if diff < best_diff:
                best_diff = diff
                best = pt

        if best and best_diff <= tol:
            window_snaps[key] = best
        else:
            window_snaps[key] = None

    if len(history) >= 2:
        prior = history[-2]
        if (now - prior.get("timestamp", 0)) <= 900:
            window_snaps["5m"] = prior

    for m in mods:
        name = m["name"]
        curr = m["downloads"]
        item = dict(m)
        item["cumulative"] = curr

        for key, _ in WINDOWS:
            snap = window_snaps.get(key)
            if snap is not None:
                prev = snap.get("counts", {}).get(name, curr)
                item[f"last_{key}"] = max(0, curr - prev)
            else:
                item[f"last_{key}"] = None

        enriched.append(item)
    return enriched

def render_dashboard(enriched, last_checked_dt, next_update_dt, interval_mins):
    total_cum = sum(m["cumulative"] for m in enriched)
    totals = {}
    for key, _ in WINDOWS:
        vals = [m[f"last_{key}"] for m in enriched if m.get(f"last_{key}") is not None]
        totals[key] = sum(vals) if len(vals) == len(enriched) else None

    sorted_mods = sorted(enriched, key=lambda x: x["cumulative"], reverse=True)

    use_x11 = is_x11_mode()
    W = 106

    # 1. Header Box
    print("\n" + f"{C_CYAN}┌{'─' * (W - 2)}┐{C_RESET}")
    bolt = ">>" if use_x11 else "⚡"
    title = f"{bolt} DJCDEVELOPMENT • VALHEIM MODS VANITY RADAR ({interval_mins}m Pulse)"
    print(f"{C_CYAN}│{C_RESET} {C_BOLD}{C_YELLOW}{pad_to(title, W - 4)}{C_RESET} {C_CYAN}│{C_RESET}")
    ts_line = f"Last Checked: {format_pt_timestamp(last_checked_dt)}    Next: {next_update_dt.strftime('%I:%M:%S %p %Z')} (every {interval_mins}m)"
    print(f"{C_CYAN}│{C_RESET} {C_DIM}{pad_to(ts_line, W - 4)}{C_RESET} {C_CYAN}│{C_RESET}")
    print(f"{C_CYAN}└{'─' * (W - 2)}┘{C_RESET}")

    # 2. Celebration Window
    total_5m = totals.get("5m") or 0
    if total_5m > 0:
        active = [f"{m['name']} (+{m['last_5m']})" for m in sorted(enriched, key=lambda x: (x.get('last_5m') or 0), reverse=True) if (m.get('last_5m') or 0) > 0]
        active_str = " • ".join(active)
        print(f"{C_MAGENTA}╔{'═' * (W - 2)}╗{C_RESET}")
        party = "***" if use_x11 else "🎉"
        rocket = "***" if use_x11 else "🚀"
        banner_title = f"{party} LIVE {interval_mins}-MIN PULSE: +{total_5m} NEW INSTALLS DETECTED! {rocket}"
        print(f"{C_MAGENTA}║{C_RESET} {C_BOLD}{C_YELLOW}{pad_to(banner_title, W - 4)}{C_RESET} {C_MAGENTA}║{C_RESET}")
        fire = ">>" if use_x11 else "🔥"
        sub = f"{fire} Active Movers: {active_str}"
        if visual_len(sub) > W - 4:
            sub = sub[:W - 7] + "..."
        print(f"{C_MAGENTA}║{C_RESET} {C_GREEN}{pad_to(sub, W - 4)}{C_RESET} {C_MAGENTA}║{C_RESET}")
        print(f"{C_MAGENTA}╚{'═' * (W - 2)}╝{C_RESET}")
    else:
        print(f"{C_DIM}┌{'─' * (W - 2)}┐{C_RESET}")
        radar = "[-]" if use_x11 else "📡"
        msg = f"{radar} Live {interval_mins}-Min Pulse: Steady — radar listening for incoming downloads..."
        print(f"{C_DIM}│{C_RESET} {C_DIM}{pad_to(msg, W - 4)}{C_RESET} {C_DIM}│{C_RESET}")
        print(f"{C_DIM}└{'─' * (W - 2)}┘{C_RESET}")

    # 3. Leaderboard Podium
    if use_x11:
        medals = ["[#1]", "[#2]", "[#3]", "[#4]", "[#5]"]
        trophy = ">>"
    else:
        medals = ["🥇", "🥈", "🥉", "🎖️", "🎖️"]
        trophy = "🏆"

    podium_parts = [f"{medals[i]} {C_BOLD}{m['name']}{C_RESET} ({m['cumulative']:,})" for i, m in enumerate(sorted_mods[:5])]
    top3 = "   •   ".join(podium_parts[:3])
    rest = "   •   ".join(podium_parts[3:])
    prefix = f"  {trophy} {C_BOLD}LEADERBOARD:{C_RESET} "
    print(f"\n{prefix}{top3}")
    if rest:
        indent = " " * visual_len(f"  {trophy} LEADERBOARD: ")
        print(f"{indent}{rest}")

    # Milestone Calculation
    if total_cum < 1000:
        target = 1000 if total_cum >= 700 else ((total_cum // 100) + 1) * 100
    elif total_cum < 2500:
        target = ((total_cum // 250) + 1) * 250
    else:
        target = ((total_cum // 500) + 1) * 500

    pct = (total_cum / target) * 100
    bar_w = 28
    filled = int(round(min(1.0, total_cum / target) * bar_w))
    bar = "█" * filled + "░" * (bar_w - filled)
    remaining = max(0, target - total_cum)
    target_ico = ">>" if use_x11 else "🎯"
    print(f"  {target_ico} {C_BOLD}ROAD TO {target:,}:{C_RESET} [{C_CYAN}{bar}{C_RESET}] {C_YELLOW}{total_cum:,}/{target:,} ({pct:.1f}% • {remaining:,} to go!){C_RESET}\n")

    # 4. Mod Velocity Table (Fits inside W=100)
    cols = [
        ("Mod Name", 15, 'left'),
        ("Version", 8, 'left'),
        (f"Last {interval_mins}m", 10, 'left'),
        ("Last 1h", 9, 'left'),
        ("Last 3h", 9, 'left'),
        ("Last 6h", 9, 'left'),
        ("Last 12h", 10, 'left'),
        ("Cumulative", 11, 'right')
    ]

    top_border = "┌" + "┬".join("─" * (w + 2) for _, w, _ in cols) + "┐"
    mid_border = "├" + "┼".join("─" * (w + 2) for _, w, _ in cols) + "┤"
    bot_border = "└" + "┴".join("─" * (w + 2) for _, w, _ in cols) + "┘"

    def format_row(cells):
        out = []
        for (val, (_, w, align)) in zip(cells, cols):
            out.append(f" {pad_to(str(val), w, align)} ")
        return "│" + "│".join(out) + "│"

    print(top_border)
    header_cells = [f"{C_BOLD}{name}{C_RESET}" for name, _, _ in cols]
    print(format_row(header_cells))
    print(mid_border)

    for m in sorted_mods:
        v5 = m.get("last_5m")
        if v5 is not None:
            if use_x11:
                spark = " ^^" if v5 >= 5 else (" *" if v5 > 0 else "")
            else:
                spark = " 🚀" if v5 >= 5 else (" ✨" if v5 > 0 else "")
            f5 = f"{C_GREEN}+{v5}{spark}{C_RESET}" if v5 > 0 else f"{C_DIM}+0{C_RESET}"
        else:
            f5 = f"{C_DIM}--{C_RESET}"

        f1h = f"+{m['last_1h']}" if m.get('last_1h') is not None else f"{C_DIM}--{C_RESET}"
        f3h = f"+{m['last_3h']}" if m.get('last_3h') is not None else f"{C_DIM}--{C_RESET}"
        f6h = f"+{m['last_6h']}" if m.get('last_6h') is not None else f"{C_DIM}--{C_RESET}"
        f12h = f"+{m['last_12h']}" if m.get('last_12h') is not None else f"{C_DIM}--{C_RESET}"

        row = [
            f"{C_CYAN}{m['name']}{C_RESET}",
            f"{C_DIM}v{m['latest_version']}{C_RESET}",
            f5,
            f1h,
            f3h,
            f6h,
            f12h,
            f"{C_BOLD}{C_WHITE}{m['cumulative']:,}{C_RESET}"
        ]
        print(format_row(row))

    print(mid_border)
    if total_5m > 0:
        tot_spark = " **" if use_x11 else " 🎉"
        tot_5m_str = f"{C_BOLD}{C_GREEN}+{total_5m}{tot_spark}{C_RESET}"
    elif totals.get("5m") is not None:
        tot_5m_str = f"{C_DIM}+0{C_RESET}"
    else:
        tot_5m_str = f"{C_DIM}--{C_RESET}"

    total_row = [
        f"{C_BOLD}{C_YELLOW}GRAND TOTAL{C_RESET}",
        f"{C_DIM}-{C_RESET}",
        tot_5m_str,
        f"{C_DIM}--{C_RESET}" if totals['1h'] is None else f"+{totals['1h']}",
        f"{C_DIM}--{C_RESET}" if totals['3h'] is None else f"+{totals['3h']}",
        f"{C_DIM}--{C_RESET}" if totals['6h'] is None else f"+{totals['6h']}",
        f"{C_DIM}--{C_RESET}" if totals['12h'] is None else f"+{totals['12h']}",
        f"{C_BOLD}{C_YELLOW}{total_cum:,}{C_RESET}"
    ]
    print(format_row(total_row))
    print(bot_border)

class WidgetHandler(http.server.SimpleHTTPRequestHandler):
    def __init__(self, *args, **kwargs):
        super().__init__(*args, directory=DATA_DIR, **kwargs)

    def do_GET(self):
        if self.path == "/api/stats":
            now_pt = get_pt_time()
            next_pt = now_pt + timedelta(minutes=DEFAULT_INTERVAL_MINS)
            mods = get_live_metrics()
            history = record_snapshot(mods)
            enriched = compute_velocities(mods, history)
            resp_totals = {"cumulative": sum(m["cumulative"] for m in enriched)}
            for k, _ in WINDOWS:
                k_vals = [m[f"last_{k}"] for m in enriched if m.get(f"last_{k}") is not None]
                resp_totals[f"last_{k}"] = sum(k_vals) if len(k_vals) == len(enriched) else None

            resp_data = {
                "owner": OWNER,
                "community": COMMUNITY,
                "last_checked": format_pt_timestamp(now_pt),
                "next_update": format_pt_timestamp(next_pt),
                "mods": enriched,
                "totals": resp_totals
            }
            body = json.dumps(resp_data).encode("utf-8")
            self.send_response(200)
            self.send_header("Content-Type", "application/json")
            self.send_header("Access-Control-Allow-Origin", "*")
            self.send_header("Content-Length", str(len(body)))
            self.end_headers()
            self.wfile.write(body)
        else:
            super().do_GET()

    def log_message(self, format, *args):
        pass

def start_background_server():
    try:
        socketserver.TCPServer.allow_reuse_address = True
        httpd = socketserver.TCPServer(("", PORT), WidgetHandler)
        t = threading.Thread(target=httpd.serve_forever, daemon=True)
        t.start()
        return True
    except OSError:
        return False

def perform_check(interval_mins):
    last_checked_dt = get_pt_time()
    next_update_dt = last_checked_dt + timedelta(minutes=interval_mins)
    mods = get_live_metrics()
    history = record_snapshot(mods)
    enriched = compute_velocities(mods, history)
    render_dashboard(enriched, last_checked_dt, next_update_dt, interval_mins)
    return next_update_dt

def main():
    once_mode = "--once" in sys.argv or "-1" in sys.argv
    interval_mins = DEFAULT_INTERVAL_MINS

    for i, arg in enumerate(sys.argv):
        if arg in ("--interval", "-i") and i + 1 < len(sys.argv):
            try:
                interval_mins = max(1, int(sys.argv[i + 1]))
            except ValueError:
                pass

    server_running = start_background_server()

    if once_mode:
        perform_check(interval_mins)
        return

    try:
        while True:
            os.system('cls' if os.name == 'nt' else 'clear')
            next_update_dt = perform_check(interval_mins)

            target_ts = time.time() + (interval_mins * 60)
            while True:
                remaining = int(target_ts - time.time())
                if remaining <= 0:
                    break
                mins, secs = divmod(remaining, 60)
                srv_note = f" • Web API: http://localhost:{PORT}" if server_running else ""
                sys.stdout.write(
                    f"\r [Next pulse in {mins:02d}m {secs:02d}s (at {next_update_dt.strftime('%I:%M:%S %p %Z')}){srv_note} • Ctrl+C to stop] "
                )
                sys.stdout.flush()
                time.sleep(1)
            sys.stdout.write("\r" + " " * 95 + "\r")
            sys.stdout.flush()

    except KeyboardInterrupt:
        print("\n\nTracker stopped.")

if __name__ == "__main__":
    main()
