import os
import time
from datetime import datetime

cutoff = datetime(2026, 9, 14, 16, 0, 0).timestamp()

user_root = r"C:\Users"
extensions = {".mp4", ".mkv", ".mov", ".webm", ".avi", ".ts"}

skip_dirs = {
    ".gemini", ".git", ".vscode", "node_modules", "site-packages",
    "cache", "caches", "package cache", "assembly", "system volume information",
    "microsoft", "google", "brave", "edge"
}

found = []

print(f"Scanning {user_root} for video files modified after 16:00 today...")

for root, dirs, files in os.walk(user_root):
    # filter dirs
    dirs[:] = [d for d in dirs if d.lower() not in skip_dirs and not d.startswith(".")]
    
    for f in files:
        ext = os.path.splitext(f)[1].lower()
        if ext in extensions:
            path = os.path.join(root, f)
            try:
                stat = os.stat(path)
                if stat.st_mtime >= cutoff:
                    size_mb = stat.st_size / (1024 * 1024)
                    found.append((stat.st_mtime, size_mb, path))
            except Exception:
                pass

found.sort(key=lambda x: x[0], reverse=True)

print(f"Done! Found {len(found)} video(s):")
for mtime, size_mb, path in found:
    print(f"  {time.ctime(mtime)} | {size_mb:6.2f} MB | {path}")

if not found:
    print("\nNo videos with timestamp >= 16:00 found. Checking for ANY video files modified today (since 00:00)...")
    today_start = datetime(2026, 9, 14, 0, 0, 0).timestamp()
    found_today = []
    for root, dirs, files in os.walk(user_root):
        dirs[:] = [d for d in dirs if d.lower() not in skip_dirs and not d.startswith(".")]
        for f in files:
            ext = os.path.splitext(f)[1].lower()
            if ext in extensions:
                path = os.path.join(root, f)
                try:
                    stat = os.stat(path)
                    if stat.st_mtime >= today_start:
                        size_mb = stat.st_size / (1024 * 1024)
                        found_today.append((stat.st_mtime, size_mb, path))
                except Exception:
                    pass
    found_today.sort(key=lambda x: x[0], reverse=True)
    print(f"Found {len(found_today)} video(s) from today:")
    for mtime, size_mb, path in found_today:
        print(f"  {time.ctime(mtime)} | {size_mb:6.2f} MB | {path}")
