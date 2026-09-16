"""
Thunderstore Vanity Tracker for djcdevelopment (Valheim)
Tracks live mod download metrics with 1h, 3h, 6h, 12h, and cumulative velocity.
Polls every 15 minutes with Pacific Time (West Coast) timestamps and live countdown.
"""

import os
import sys
import time
import json
import urllib.request
import http.server
import socketserver
import threading
from datetime import datetime, timedelta

# West Coast (Pacific Time) zone setup
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
DEFAULT_INTERVAL_MINS = 15
PORT = 5050

KNOWN_MODS = ["Unfaded", "IsModded", "Unswayed", "SelfieStick", "TotemSentinel"]

WINDOWS = [
    ("1h", 1 * 3600),
    ("3h", 3 * 3600),
    ("6h", 6 * 3600),
    ("12h", 12 * 3600)
]

def get_pt_time():
    """Get current datetime in Pacific Time."""
    return datetime.now(PT_ZONE)

def fetch_mod_metric(mod_name):
    """Fetch quick metrics from Thunderstore package-metrics endpoint."""
    url = f"https://thunderstore.io/api/v1/package-metrics/{OWNER}/{mod_name}/"
    req = urllib.request.Request(url, headers={"User-Agent": "ThunderstoreVanityTracker/1.0"})
    try:
        with urllib.request.urlopen(req, timeout=8) as resp:
            data = json.loads(resp.read().decode("utf-8"))
            return {
                "name": mod_name,
                "downloads": data.get("downloads", 0),
                "rating_score": data.get("rating_score", 0),
                "latest_version": data.get("latest_version", "1.0.0"),
                "url": f"https://thunderstore.io/c/{COMMUNITY}/p/{OWNER}/{mod_name}/"
            }
    except Exception as e:
        return {
            "name": mod_name,
            "downloads": 0,
            "rating_score": 0,
            "latest_version": "?",
            "url": f"https://thunderstore.io/c/{COMMUNITY}/p/{OWNER}/{mod_name}/",
            "error": str(e)
        }

def discover_all_mods():
    """Discover all packages published by the owner from community index."""
    url = f"https://thunderstore.io/c/{COMMUNITY}/api/v1/package/"
    req = urllib.request.Request(url, headers={"User-Agent": "ThunderstoreVanityTracker/1.0"})
    try:
        with urllib.request.urlopen(req, timeout=15) as resp:
            packages = json.loads(resp.read().decode("utf-8"))
            matched = []
            for p in packages:
                if p.get("owner") == OWNER:
                    latest = p.get("versions", [{}])[0] if p.get("versions") else {}
                    total_dl = sum(v.get("downloads", 0) for v in p.get("versions", []))
                    matched.append({
                        "name": p.get("name"),
                        "downloads": total_dl,
                        "latest_version": latest.get("version_number", "1.0.0"),
                        "icon": latest.get("icon", ""),
                        "url": p.get("package_url", f"https://thunderstore.io/c/{COMMUNITY}/p/{OWNER}/{p.get('name')}/")
                    })
            return matched
    except Exception:
        return [fetch_mod_metric(m) for m in KNOWN_MODS]

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
    snapshot = {
        "timestamp": now,
        "pt_time": now_pt.strftime("%Y-%m-%d %I:%M:%S %p %Z"),
        "counts": {m["name"]: m["downloads"] for m in mods}
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
        matched = None
        for pt in reversed(history):
            if pt.get("timestamp", 0) <= target_t:
                matched = pt
                break
        if matched is None and history:
            matched = history[0]
        window_snaps[key] = matched

    for m in mods:
        name = m["name"]
        curr = m["downloads"]
        item = dict(m)
        item["cumulative"] = curr

        for key, _ in WINDOWS:
            snap = window_snaps.get(key)
            prev = snap.get("counts", {}).get(name, curr) if snap else curr
            item[f"last_{key}"] = max(0, curr - prev)

        enriched.append(item)
    return enriched

def format_pt_timestamp(dt):
    return dt.strftime("%Y-%m-%d %I:%M:%S %p %Z")

def print_cli_table(enriched, last_checked_dt, next_update_dt):
    total_cum = sum(m["cumulative"] for m in enriched)
    totals = {key: sum(m[f"last_{key}"] for m in enriched) for key, _ in WINDOWS}

    header = f" {'Mod Name':<18} | {'Version':<8} | {'Last 1h':<9} | {'Last 3h':<9} | {'Last 6h':<9} | {'Last 12h':<10} | {'Cumulative':<10}"
    sep = "-" * len(header)
    double_sep = "=" * len(header)

    print("\n" + double_sep)
    print(f"  THUNDERSTORE VANITY TRACKER: {OWNER} (Valheim)")
    print(f"  Last Checked: {format_pt_timestamp(last_checked_dt)}")
    print(f"  Next Update:  {format_pt_timestamp(next_update_dt)} (every 15 min)")
    print(double_sep)
    print(header)
    print(sep)
    for m in sorted(enriched, key=lambda x: x["cumulative"], reverse=True):
        print(f" {m['name']:<18} | {m.get('latest_version','?'):<8} | +{m['last_1h']:<8} | +{m['last_3h']:<8} | +{m['last_6h']:<8} | +{m['last_12h']:<9} | {m['cumulative']:<10}")
    print(sep)
    print(f" {'TOTALS':<18} | {'-':<8} | +{totals['1h']:<8} | +{totals['3h']:<8} | +{totals['6h']:<8} | +{totals['12h']:<9} | {total_cum:<10}")
    print(double_sep)

class WidgetHandler(http.server.SimpleHTTPRequestHandler):
    def __init__(self, *args, **kwargs):
        super().__init__(*args, directory=DATA_DIR, **kwargs)

    def do_GET(self):
        if self.path == "/api/stats":
            now_pt = get_pt_time()
            next_pt = now_pt + timedelta(minutes=DEFAULT_INTERVAL_MINS)
            mods = discover_all_mods()
            history = record_snapshot(mods)
            enriched = compute_velocities(mods, history)
            resp_data = {
                "owner": OWNER,
                "community": COMMUNITY,
                "last_checked": format_pt_timestamp(now_pt),
                "next_update": format_pt_timestamp(next_pt),
                "mods": enriched,
                "totals": {
                    "cumulative": sum(m["cumulative"] for m in enriched),
                    **{f"last_{k}": sum(m[f"last_{k}"] for m in enriched) for k, _ in WINDOWS}
                }
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

def run_server():
    socketserver.TCPServer.allow_reuse_address = True
    with socketserver.TCPServer(("", PORT), WidgetHandler) as httpd:
        print(f"[+] Vanity widget server running at http://localhost:{PORT}")
        print(f"[+] Serving API at http://localhost:{PORT}/api/stats")
        print("[+] Press Ctrl+C to stop.")
        httpd.serve_forever()

def perform_check(interval_mins):
    last_checked_dt = get_pt_time()
    next_update_dt = last_checked_dt + timedelta(minutes=interval_mins)
    mods = discover_all_mods()
    history = record_snapshot(mods)
    enriched = compute_velocities(mods, history)
    print_cli_table(enriched, last_checked_dt, next_update_dt)
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

    if "--serve" in sys.argv or "-s" in sys.argv:
        perform_check(interval_mins)
        def background_poller():
            while True:
                time.sleep(interval_mins * 60)
                try:
                    m = discover_all_mods()
                    record_snapshot(m)
                except Exception:
                    pass
        t = threading.Thread(target=background_poller, daemon=True)
        t.start()
        run_server()
        return

    # One-shot check
    if once_mode:
        print(f"[*] Querying Thunderstore for {OWNER}...")
        perform_check(interval_mins)
        return

    # Continuous 15-minute polling loop (Default)
    print(f"[*] Starting 15-minute polling tracker for {OWNER}...")
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
                sys.stdout.write(
                    f"\r [Next update in {mins:02d}m {secs:02d}s (at {next_update_dt.strftime('%I:%M:%S %p %Z')}) • Ctrl+C to stop] "
                )
                sys.stdout.flush()
                time.sleep(1)
            sys.stdout.write("\r" + " " * 85 + "\r")
            sys.stdout.flush()

    except KeyboardInterrupt:
        print("\n\nTracker stopped.")

if __name__ == "__main__":
    main()
