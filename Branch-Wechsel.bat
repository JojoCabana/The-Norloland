@echo off
setlocal EnableExtensions DisableDelayedExpansion
cd /d "%~dp0"
git rev-parse --is-inside-work-tree >nul 2>&1 || (echo Kein Git-Repository gefunden.& pause & exit /b 1)
echo Aktualisiere Branch-Liste...
git fetch origin --prune || (echo Fetch fehlgeschlagen.& pause & exit /b 1)
:menu
echo.
echo === Verfuegbare Branches ===
git branch -a
echo.
set "branch="
set /p "branch=Branchname eingeben (oder Q zum Beenden): "
if /i "%branch%"=="Q" exit /b 0
if not defined branch goto menu
git show-ref --verify --quiet "refs/heads/%branch%"
if not errorlevel 1 goto switch
git show-ref --verify --quiet "refs/remotes/origin/%branch%"
if errorlevel 1 (echo Branch nicht gefunden.& goto menu)
git switch --track "origin/%branch%"
goto check
:switch
git switch "%branch%"
:check
if errorlevel 1 (echo Wechsel fehlgeschlagen. Lokale Aenderungen pruefen.& goto menu)
git pull --ff-only
if errorlevel 1 echo Pull fehlgeschlagen. Bitte Git-Meldung pruefen.
echo.
pause
goto menu
