import os
import time
from pathlib import Path

now = time.time()
two_hours_ago = now - 7200

candidate_roots = [
    Path("C:/Users/derek/Videos"),
    Path("C:/Users/derek/Desktop"),
    Path("C:/Users/derek/Documents"),
    Path("C:/Users/derek/Downloads"),
    Path("E:/BF6-Highlights"),
    Path("C:/Program Files (x86)/Steam/steamapps/common/Valheim"),
]

print(f"Scanning for video files modified in the last 2 hours (since {time.ctime(two_hours_ago)})...")

found = []
for root in candidate_roots:
    if not root.exists():
        continue
    for ext in ("*.mp4", "*.mkv", "*.mov", "*.webm", "*.avi"):
        try:
            for p in root.rglob(ext):
                try:
                    stat = p.stat()
                    if stat.st_mtime >= two_hours_ago:
                        found.append((stat.st_mtime, stat.st_size, p))
                except Exception:
                    pass
        except Exception:
            pass

found.sort(key=lambda x: x[0], reverse=True)

if not found:
    print("No recent video files found in candidate directories.")
    print("Listing top 5 newest videos in C:/Users/derek/Videos:")
    all_videos = []
    for ext in ("*.mp4", "*.mkv", "*.mov", "*.webm"):
        for p in Path("C:/Users/derek/Videos").rglob(ext):
            try:
                stat = p.stat()
                all_videos.append((stat.st_mtime, stat.st_size, p))
            except Exception:
                pass
    all_videos.sort(key=lambda x: x[0], reverse=True)
    for mtime, size, p in all_videos[:5]:
        print(f"  {time.ctime(mtime)} | {size / 1024 / 1024:.2f} MB | {p}")
else:
    for mtime, size, p in found:
        print(f"  {time.ctime(mtime)} | {size / 1024 / 1024:.2f} MB | {p}")
