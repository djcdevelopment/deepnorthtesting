import subprocess

video = r"C:\Users\derek\Videos\Screen Recordings\Screen Recording 2026-09-14 182205.mp4"

# Let's extract 1 frame per second to find exact cut points
out_dir = r"C:\work\deepnorthtesting\tools\video_preview\per_sec"
import os
os.makedirs(out_dir, exist_ok=True)

cmd = [
    "ffmpeg", "-y",
    "-i", video,
    "-vf", "fps=1,scale=480:-1",
    "-loglevel", "error",
    os.path.join(out_dir, "sec_%02d.jpg")
]

subprocess.run(cmd)
frames = sorted(os.listdir(out_dir))
print(f"Extracted {len(frames)} 1-second frames.")
