import subprocess
import os
from pathlib import Path

raw_video = r"C:\Users\derek\Videos\Screen Recordings\Screen Recording 2026-09-14 182205.mp4"
out_dir = Path(r"C:\work\deepnorthtesting\plugins\Unfaded\export")
out_dir.mkdir(parents=True, exist_ok=True)

out_4k = out_dir / "Unfaded_Showcase_4K.mp4"
out_1080p = out_dir / "Unfaded_Showcase_1080p.mp4"

font_path = "C\\\\:/Windows/Fonts/segoeuib.ttf"

# Video timing:
# 00:00 - 00:06: Sprint to Troll
# 00:06 - 00:12: Lethal smash, bullet-time slow-mo ragdoll, Slain by Troll banner
# 00:12 - 00:17: Free-fly drone camera
# 00:17 - 00:24: Instant respawn & charge back
# 00:24 - 00:34: Killer Cam [K] tracking Troll
# 00:34 - 00:38: Instant respawn & end card

# Let's craft ffmpeg drawtext filter chain:
# Box with semi-transparent black background, bold yellow/cyan/white text
filters = [
    # Callout 1: 05s - 11s: ZERO BLACKOUT • BULLET-TIME SLOW-MO
    f"drawtext=fontfile='{font_path}':text='ZERO BLACK SCREEN VOID  •  BULLET-TIME SLOW-MO':fontcolor=white:fontsize=48:box=1:boxcolor=black@0.65:boxborderw=18:x=(w-text_w)/2:y=h-180:enable='between(t,5,11)'",
    
    # Callout 2: 12s - 17s: FREE-FLY DRONE SCOUTING [F]
    f"drawtext=fontfile='{font_path}':text='DRONE FREE-FLY SCOUTING [F]':fontcolor=#00FFAA:fontsize=48:box=1:boxcolor=black@0.65:boxborderw=18:x=(w-text_w)/2:y=h-180:enable='between(t,12,17)'",
    
    # Callout 3: 18s - 23s: INSTANT RESPAWN [SPACE]
    f"drawtext=fontfile='{font_path}':text='SKIP 10s BLACKOUT  •  INSTANT RESPAWN [SPACE]':fontcolor=white:fontsize=48:box=1:boxcolor=black@0.65:boxborderw=18:x=(w-text_w)/2:y=h-180:enable='between(t,18,23)'",
    
    # Callout 4: 26s - 34s: KILLER FOCUS CAM [K]
    f"drawtext=fontfile='{font_path}':text='KILLER FOCUS CAM [K]  •  SEE WHO GOT YOU':fontcolor=#FF6B6B:fontsize=48:box=1:boxcolor=black@0.65:boxborderw=18:x=(w-text_w)/2:y=h-180:enable='between(t,26,34)'",
    
    # End Card: 35s - 38s
    f"drawtext=fontfile='{font_path}':text='UNFADED  •  VALHEIM DEATH SPECTATOR':fontcolor=#00FFAA:fontsize=56:box=1:boxcolor=black@0.75:boxborderw=24:x=(w-text_w)/2:y=h/2-60:enable='between(t,35,38.5)'",
    f"drawtext=fontfile='{font_path}':text='FREE ON THUNDERSTORE  •  BY DJCDEVELOPMENT':fontcolor=white:fontsize=36:box=1:boxcolor=black@0.75:boxborderw=16:x=(w-text_w)/2:y=h/2+40:enable='between(t,35,38.5)'",
    
    # Fade in from black at 0s, fade out to black at 38s
    "fade=t=in:st=0:d=0.5",
    "fade=t=out:st=37.8:d=0.7"
]

vf_chain = ",".join(filters)

print("Rendering 1080p Web-Optimized Edition (< 20MB)...")
cmd_1080p = [
    "ffmpeg", "-y",
    "-ss", "00:00:00.80",
    "-to", "00:00:38.80",
    "-i", raw_video,
    "-vf", f"scale=1920:1080:force_original_aspect_ratio=decrease,pad=1920:1080:(ow-iw)/2:(oh-ih)/2,{vf_chain}",
    "-c:v", "libx264", "-preset", "slow", "-crf", "20",
    "-c:a", "aac", "-b:a", "192k",
    "-movflags", "+faststart",
    str(out_1080p)
]

res = subprocess.run(cmd_1080p)
if res.returncode == 0:
    size_mb = out_1080p.stat().st_size / (1024 * 1024)
    print(f"SUCCESS! 1080p rendered: {out_1080p} ({size_mb:.2f} MB)")
else:
    print(f"Error rendering 1080p: return code {res.returncode}")

print("\nRendering 4K Master Edition...")
cmd_4k = [
    "ffmpeg", "-y",
    "-ss", "00:00:00.80",
    "-to", "00:00:38.80",
    "-i", raw_video,
    "-vf", f"scale=3840:2160:force_original_aspect_ratio=decrease,pad=3840:2160:(ow-iw)/2:(oh-ih)/2,{vf_chain}",
    "-c:v", "libx264", "-preset", "medium", "-crf", "18",
    "-c:a", "aac", "-b:a", "256k",
    "-movflags", "+faststart",
    str(out_4k)
]

res4k = subprocess.run(cmd_4k)
if res4k.returncode == 0:
    size_4k = out_4k.stat().st_size / (1024 * 1024)
    print(f"SUCCESS! 4K rendered: {out_4k} ({size_4k:.2f} MB)")
else:
    print(f"Error rendering 4K: return code {res4k.returncode}")
