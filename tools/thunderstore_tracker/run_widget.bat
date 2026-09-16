@echo off
cd /d "%~dp0"
echo ===================================================
echo   djcdevelopment Thunderstore Vanity Tracker
echo ===================================================
echo Starting local API server and opening dashboard...
start "" http://localhost:5050/index.html
python tracker.py --serve
pause
