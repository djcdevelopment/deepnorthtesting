import subprocess
import os
from pathlib import Path

video_path = r"C:\Users\derek\Videos\Screen Recordings\Screen Recording 2026-09-14 182205.mp4"
out_dir = Path(r"C:\work\deepnorthtesting\tools\video_preview")
out_dir.mkdir(parents=True, exist_ok=True)

out_pattern = str(out_dir / "frame_%02d.jpg")

cmd = [
    "ffmpeg", "-y",
    "-i", video_path,
    "-vf", "fps=1/3,scale=960:-1",
    "-loglevel", "error",
    out_pattern
]

print("Extracting thumbnails...")
res = subprocess.run(cmd)
print("Return code:", res.returncode)

frames = sorted(list(out_dir.glob("*.jpg")))
print(f"Extracted {len(frames)} frames:")
for f in frames:
    print(f.name, f.stat().st_size, "bytes")
