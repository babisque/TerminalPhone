@echo off
setlocal EnableDelayedExpansion

:: ------------------------------------------------------------------------------------------------
:: SETTINGS & PATHS
:: ------------------------------------------------------------------------------------------------
set "SERVICE_NAME=Bridge Terminal Service"
set "DESCRIPTION=A bridge service to execute terminal commands via Telegram for Windows and Arch Linux (WSL)."
set "EXE_NAME=TerminalPhone.Worker.exe"
set "CURRENT_DIR=%~dp0"

:: ------------------------------------------------------------------------------------------------
:: STEP 1: SELF-ELEVATION (UAC Prompt)
:: ------------------------------------------------------------------------------------------------
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo Requesting Administrator privileges...
    powershell -Command "Start-Process -FilePath '%0' -Verb RunAs"
    exit /b
)

:MENU
cls
echo ======================================================================
echo           BRIDGE TERMINAL - SERVICE MANAGEMENT TOOL
echo ======================================================================
echo.
echo [1] Install Service
echo [2] Uninstall Service
echo [3] Exit
echo.
set /p "choice=Select an option: "

if "%choice%"=="1" goto INSTALL
if "%choice%"=="2" goto UNINSTALL
if "%choice%"=="3" exit /b
goto MENU

:INSTALL
echo.
echo --- STEP 1: DATA COLLECTION ---
set /p "ADMIN_ID=Enter Telegram Admin ID (AdminId): "
set /p "GROUP_ID=Enter Telegram Group ID (GroupId): "
set /p "BOT_TOKEN=Enter Telegram Bot Token (Token): "

echo.
echo --- STEP 2: CONFIGURING SYSTEM ENVIRONMENT VARIABLES ---
setx /M TelegramSettings__AdminId "%ADMIN_ID%"
setx /M TelegramSettings__GroupId "%GROUP_ID%"
setx /M TelegramSettings__Token "%BOT_TOKEN%"

echo.
if exist "%CURRENT_DIR%%EXE_NAME%" (
    echo --- RELEASE PACKAGE DETECTED ---
    echo Skipping build process. Using existing binary.
    set "FINAL_BIN_PATH=%CURRENT_DIR%%EXE_NAME%"
    goto STEP4
)

echo --- STEP 3: PUBLISHING PROJECT (Dev Clone Mode) ---
set "PROJECT_DIR=%CURRENT_DIR%TerminalPhone.Worker"
set "PUBLISH_DIR=%PROJECT_DIR%\bin\Release\net10.0\publish"

if not exist "%PROJECT_DIR%" (
    echo ERROR: Project directory not found at %PROJECT_DIR%
    pause
    goto MENU
)

echo Building and publishing in Release mode...
dotnet publish "%PROJECT_DIR%" -c Release -o "%PUBLISH_DIR%"
set "FINAL_BIN_PATH=%PUBLISH_DIR%\%EXE_NAME%"

:STEP4
echo.
echo --- STEP 4: CREATING WINDOWS SERVICE ---
if not exist "%FINAL_BIN_PATH%" (
    echo ERROR: Binary not found at "%FINAL_BIN_PATH%"
    pause
    goto MENU
)

sc.exe create "%SERVICE_NAME%" binPath= "\"%FINAL_BIN_PATH%\"" start= auto
sc.exe description "%SERVICE_NAME%" "%DESCRIPTION%"
sc.exe config "%SERVICE_NAME%" DisplayName= "Bridge Terminal Service"

echo.
echo --- STEP 5: STARTING SERVICE ---
sc.exe start "%SERVICE_NAME%"

echo.
echo Done! Bridge Terminal Service is now installed and running.
pause
goto MENU

:UNINSTALL
echo.
echo --- UNINSTALLING BRIDGE TERMINAL ---
echo Stopping service...
sc.exe stop "%SERVICE_NAME%"
timeout /t 5 /nobreak >nul
echo Deleting service...
sc.exe delete "%SERVICE_NAME%"

echo.
echo Cleanup complete.
pause
goto MENU