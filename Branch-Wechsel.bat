@echo off
setlocal EnableExtensions EnableDelayedExpansion
cd /d "%~dp0"
git rev-parse --is-inside-work-tree >nul 2>&1
if errorlevel 1 (echo Kein Git-Repository gefunden.& pause & exit /b 1)
:menu
cls
echo ===== THE NORLOLAND - BRANCH AUSWAHL =====
echo.
echo Aktualisiere Branches von GitHub...
git fetch origin --prune
if errorlevel 1 echo WARNUNG: Fetch fehlgeschlagen. Zeige vorhandene Branches.
echo.
set /a count=0
for /f "delims=" %%B in ('git for-each-ref --format="%%(refname:strip=3)" refs/remotes/origin') do (
    if /i not "%%B"=="HEAD" (
        set /a count+=1
        set "branch[!count!]=%%B"
        echo   !count!^) %%B
    )
)
echo.
if !count! EQU 0 (echo Keine Remote-Branches gefunden.& pause & exit /b 1)
echo   0^) Beenden
echo.
set "choice="
set /p "choice=Nummer eingeben: "
if "!choice!"=="0" exit /b 0
set "valid="
for /l %%N in (1,1,!count!) do if "!choice!"=="%%N" set "valid=1"
if not defined valid (echo Ungueltige Auswahl.& pause & goto menu)
set "branch=!branch[%choice%]!"
echo.
echo Wechsle zu !branch! ...
git show-ref --verify --quiet "refs/heads/!branch!"
if errorlevel 1 (
    git switch --track "origin/!branch!"
) else (
    git switch "!branch!"
)
if errorlevel 1 (echo Wechsel fehlgeschlagen. Lokale Aenderungen pruefen.& pause & goto menu)
git pull --ff-only origin "!branch!"
if errorlevel 1 echo Aktualisierung fehlgeschlagen. Git-Meldung beachten.
echo.
pause
goto menu
