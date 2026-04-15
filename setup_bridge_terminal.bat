@echo off
setlocal EnableDelayedExpansion

:: ------------------------------------------------------------------------------------------------
:: SETTINGS & PATHS
:: ------------------------------------------------------------------------------------------------
set "SERVICE_NAME=BridgeTerminal"
set "DISPLAY_NAME=Bridge Terminal Service"
set "DESCRIPTION=A bridge service to execute terminal commands via Telegram for Windows and Arch Linux (WSL)."
set "PROJECT_DIR=%~dp0TerminalPhone.Worker"
set "PUBLISH_DIR=%~dp0TerminalPhone.Worker\bin\Release\net10.0\publish"
set "EXE_NAME=TerminalPhone.Worker.exe"

:: ------------------------------------------------------------------------------------------------
:: STEP 1: SELF-ELEVATION
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
echo [1] Install Service (Run as Current User - Auto Grant Permission)
echo [2] Install Service (Run as LocalSystem)
echo [3] Uninstall Service
echo [4] Exit
echo.
set /p "choice=Select an option: "

if "%choice%"=="1" goto INSTALL_USER
if "%choice%"=="2" goto INSTALL_SYSTEM
if "%choice%"=="3" goto UNINSTALL
if "%choice%"=="4" exit /b
goto MENU

:INSTALL_USER
set "RUN_AS_USER=%COMPUTERNAME%\%USERNAME%"
echo.
echo --- SERVICE ACCOUNT CONFIGURATION ---
echo Account: %RUN_AS_USER%
set /p "USER_PASS=Enter your Windows password: "

echo.
echo --- GRANTING 'LOG ON AS A SERVICE' PERMISSION ---
:: One-liner PowerShell command to avoid Batch line-continuation bugs
powershell -NoProfile -Command "$u='%RUN_AS_USER%';$s=(New-Object System.Security.Principal.NTAccount($u)).Translate([System.Security.Principal.SecurityIdentifier]).Value;$f=[System.IO.Path]::GetTempFileName();secedit /export /cfg $f /areas USER_RIGHTS /quiet;$c=Get-Content $f;if($c -match 'SeServiceLogonRight'){if($c -match $s){echo 'Already granted'}else{$c=$c -replace 'SeServiceLogonRight = ',('SeServiceLogonRight = *' + $s + ',');Set-Content $f $c;secedit /configure /db $env:temp\secedit.sdb /cfg $f /areas USER_RIGHTS /quiet;echo 'Granted'}}else{$c+='SeServiceLogonRight = *' + $s;Set-Content $f $c;secedit /configure /db $env:temp\secedit.sdb /cfg $f /areas USER_RIGHTS /quiet;echo 'Granted'};Remove-Item $f -ErrorAction SilentlyContinue"

goto DATA_COLLECTION

:INSTALL_SYSTEM
set "RUN_AS_USER="
set "USER_PASS="
goto DATA_COLLECTION

:DATA_COLLECTION
echo.
echo --- STEP 2: DATA COLLECTION ---

set "USE_EXISTING=N"
if defined TelegramSettings__AdminId (
    if defined TelegramSettings__Token (
        echo.
        echo [INFO] Existing configuration found:
        echo        Admin ID: %TelegramSettings__AdminId%
        echo        Group ID: %TelegramSettings__GroupId%
        echo        Token:    [ALREADY CONFIGURED]
        echo.
        set /p "USE_EXISTING=Use these existing settings? (Y/N): "
    )
)

if /i "%USE_EXISTING%"=="Y" (
    echo.
    echo Skipping configuration step...
    goto STEP4
)

set /p "ADMIN_ID=Enter Telegram Admin ID (AdminId): "
set /p "GROUP_ID=Enter Telegram Group ID (GroupId): "
set /p "BOT_TOKEN=Enter Telegram Bot Token (Token): "

echo.
echo --- STEP 3: CONFIGURING SYSTEM ENVIRONMENT VARIABLES ---
setx /M TelegramSettings__AdminId "%ADMIN_ID%"
setx /M TelegramSettings__GroupId "%GROUP_ID%"
setx /M TelegramSettings__Token "%BOT_TOKEN%"

:STEP4
echo.
echo --- STEP 4: STOPPING EXISTING SERVICE (IF RUNNING) ---
sc.exe query "%SERVICE_NAME%" >nul 2>&1
if %errorLevel% equ 0 (
    echo Stopping %SERVICE_NAME% to release file locks...
    sc.exe stop "%SERVICE_NAME%" >nul 2>&1
    timeout /t 5 /nobreak >nul
)

echo.
echo --- STEP 5: PUBLISHING PROJECT ---
dotnet publish "%PROJECT_DIR%" -c Release -o "%PUBLISH_DIR%"

echo.
echo --- STEP 6: CREATING WINDOWS SERVICE ---
if not exist "%PUBLISH_DIR%\%EXE_NAME%" (
    echo ERROR: Binary not found at "%PUBLISH_DIR%\%EXE_NAME%"
    pause
    goto MENU
)

:: Create Service
if "%RUN_AS_USER%"=="" (
    sc.exe create "%SERVICE_NAME%" binPath= "\"%PUBLISH_DIR%\%EXE_NAME%\"" start= auto
) else (
    sc.exe create "%SERVICE_NAME%" binPath= "\"%PUBLISH_DIR%\%EXE_NAME%\"" start= auto obj= "%RUN_AS_USER%" password= "%USER_PASS%"
)

sc.exe description "%SERVICE_NAME%" "%DESCRIPTION%"
sc.exe config "%SERVICE_NAME%" DisplayName= "%DISPLAY_NAME%"

echo.
echo --- STEP 7: STARTING SERVICE ---
sc.exe start "%SERVICE_NAME%"

echo.
echo Done! Service is now installed and running with correct permissions.
pause
goto MENU

:UNINSTALL
echo.
echo --- UNINSTALLING BRIDGE TERMINAL ---
sc.exe stop "%SERVICE_NAME%"
timeout /t 2 /nobreak >nul
sc.exe delete "%SERVICE_NAME%"
echo.
echo Cleanup complete.
pause
goto MENU
