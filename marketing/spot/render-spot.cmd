@echo off
rem Spot reklamowy -> marketing\spot\out\icanhelp-spot.mp4 (1920x1080, 30 kl./s, dzwiek AAC). Wymaga ffmpeg: winget install Gyan.FFmpeg
node "%~dp0render-video.mjs" %*
exit /b %ERRORLEVEL%
