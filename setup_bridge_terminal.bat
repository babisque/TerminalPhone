@echo off
setlocal EnableDelayedExpansion

:: ------------------------------------------------------------------------------------------------
:: SETTINGS & PATHS (Adjust these to your local paths)
:: ------------------------------------------------------------------------------------------------
set "SERVICE_NAME=Bridge Terminal Service"
set "DESCRIPTION=A bridge service to execute terminal commands via Telegram for Windows and Arch Linux (WSL)."
set "PROJECT_DIR=%~dp0TerminalPhone.Worker"
set "PUBLISH_DIR=%~dp0TerminalPhone.Worker\bin\Release\net10.0\publish"
set "EXE_NAME=TerminalPhone.Worker.exe"

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
echo --- STEP 2: CONFIGURING USER SECRETS ---
pushd "%PROJECT_DIR%"
echo Saving TelegramSettings:AdminId...
dotnet user-secrets set "TelegramSettings:AdminId" "%ADMIN_ID%"
echo Saving TelegramSettings:GroupId...
dotnet user-secrets set "TelegramSettings:GroupId" "%GROUP_ID%"
echo Saving TelegramSettings:Token...
dotnet user-secrets set "TelegramSettings:Token" "%BOT_TOKEN%"
popd

echo.
echo --- STEP 3: PUBLISHING PROJECT ---
echo Building and publishing in Release mode...
dotnet publish "%PROJECT_DIR%" -c Release -o "%PUBLISH_DIR%"

echo.
echo --- STEP 4: CREATING WINDOWS SERVICE ---
if not exist "%PUBLISH_DIR%\%EXE_NAME%" (
    echo ERROR: Binary not found at "%PUBLISH_DIR%\%EXE_NAME%"
    pause
    goto MENU
)

:: Create the service (note the mandatory space after binPath=)
sc.exe create "%SERVICE_NAME%" binPath= "\"%PUBLISH_DIR%\%EXE_NAME%\"" start= auto
sc.exe description "%SERVICE_NAME%" "%DESCRIPTION%"
sc.exe config "%SERVICE_NAME%" DisplayName= "%SERVICE_NAME%"

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
