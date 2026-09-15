import os
import sys
import time
from datetime import datetime

today_after_5pm = datetime(2026, 9, 14, 17, 0, 0).timestamp()

search_dirs = [
    r"C:\Users\derek",
    r"E:",
    r"S:",
    r"W:",
    r"Y:",
]

print(f"Scanning drives for videos created after 17:00 today ({time.ctime(today_after_5pm)})...")

found = []
for base in search_dirs:
    if not os.path.exists(base):
        continue
    print(f"Checking {base}...")
    for root, dirs, files in os.walk(base):
        # Skip huge non-video directories
        dirs_lower = [d.lower() for d in dirs]
        # modify dirs in-place to skip system or build folders
        dirs[:] = [d for d in dirs if d.lower() not in (
            "windows", "program files", "program files (x86)", "$recycle.bin", 
            "system volume information", "node_modules", ".git", "appdata"
        )]
        for f in files:
            ext = os.path.splitext(f)[1].lower()
            if ext in (".mp4", ".mkv", ".mov", ".webm", ".avi", ".ts"):
                path = os.path.join(root, f)
                try:
                    mtime = os.path.getmtime(path)
                    if mtime >= today_after_5pm:
                        size_mb = os.path.getsize(path) / (1024 * 1024)
                        found.append((mtime, size_mb, path))
                except Exception:
                    pass

found.sort(key=lambda x: x[0], reverse=True)
print(f"\n--- Found {len(found)} video(s) created after 17:00 ---")
for mtime, size_mb, path in found:
    print(f"{time.ctime(mtime)} | {size_mb:.2f} MB | {path}")
