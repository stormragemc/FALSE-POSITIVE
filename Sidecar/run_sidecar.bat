@echo off
setlocal
cd /d "%~dp0"

rem The venv deliberately lives OUTSIDE this deeply-nested project folder, at
rem a short fixed path. Some installed packages (elevenlabs, in particular)
rem generate very long nested file names, and Documents\Unity\<project>\
rem Sidecar\.venv\Lib\site-packages\... routinely exceeds Windows' 260-char
rem MAX_PATH unless "Enable Win32 long paths" is turned on system-wide
rem (gpedit.msc / registry — see Sidecar/README.md). Using a short root
rem avoids needing that system change.
set "VENV_DIR=%SystemDrive%\fpsc_venv"

if not exist "%VENV_DIR%" (
    echo [Sidecar] Creating virtual environment at %VENV_DIR% ...
    python -m venv "%VENV_DIR%"
    if errorlevel 1 (
        echo [Sidecar] FATAL: could not create a virtual environment. Is Python 3.10-3.12 on PATH?
        exit /b 1
    )
)

call "%VENV_DIR%\Scripts\activate.bat"

echo [Sidecar] Installing/updating dependencies (first run only takes a while: torch + transformers)...
python -m pip install --upgrade pip >nul
pip install -r requirements.txt
if errorlevel 1 (
    echo [Sidecar] FATAL: dependency install failed. See the output above.
    exit /b 1
)

if not exist ".env" (
    echo [Sidecar] WARNING: Sidecar\.env not found.
    echo [Sidecar]          Copy .env.example to .env and fill in GEMINI_API_KEY,
    echo [Sidecar]          ELEVENLABS_API_KEY, and ELEVENLABS_VOICE_ID before playing.
)

echo [Sidecar] Starting server ...
python app.py %*

endlocal
