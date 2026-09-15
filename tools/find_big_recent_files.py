import os
import time
from datetime import datetime

cutoff = datetime(2026, 9, 14, 17, 0, 0).timestamp()

print(f"Scanning C:\\Users\\derek for ANY files > 1 MB modified after 17:00...")

found = []
for root, dirs, files in os.walk(r"C:\Users\derek"):
    # skip .gemini brain/logs and .git
    if ".gemini" in root or ".git" in root or "node_modules" in root:
        continue
    for f in files:
        path = os.path.join(root, f)
        try:
            stat = os.stat(path)
            if stat.st_mtime >= cutoff and stat.st_size >= 1024 * 1024:
                size_mb = stat.st_size / (1024 * 1024)
                found.append((stat.st_mtime, size_mb, path))
        except Exception:
            pass

found.sort(key=lambda x: x[0], reverse=True)
print(f"Found {len(found)} file(s) > 1 MB modified after 17:00:")
for mtime, size_mb, path in found:
    print(f"  {time.ctime(mtime)} | {size_mb:7.2f} MB | {path}")
