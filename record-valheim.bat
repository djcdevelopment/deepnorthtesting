@echo off
title Valheim Screen Recorder (Intel QuickSync 60FPS)
cd /d "%~dp0"

set "OUTFILE=valheim_raw_%date:~10,4%%date:~4,2%%date:~7,2%_%time:~0,2%%time:~3,2%%time:~6,2%.mp4"
set "OUTFILE=%OUTFILE: =0%"

echo ============================================================
echo   Valheim 60FPS QuickSync Recorder (Intel Arc Pro B70)
echo ============================================================
echo.
echo  Output File: %OUTFILE%
echo.
echo  * RECORDING STARTED *
echo  Play your death run in Valheim now!
echo.
echo  WHEN DONE: Click this window and press 'q' to stop and save.
echo ============================================================
echo.

ffmpeg -y -f gdigrab -framerate 60 -i desktop -c:v h264_qsv -b:v 15M -preset veryfast -pix_fmt nv12 "%OUTFILE%"

echo.
echo ============================================================
echo  RECORDING SAVED: %OUTFILE%
echo ============================================================
pause
