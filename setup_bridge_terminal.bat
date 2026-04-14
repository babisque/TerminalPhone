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
powershell -NoProfile -Command ^
    "$user = '%RUN_AS_USER%'; ^
    $sid = (New-Object System.Security.Principal.NTAccount($user)).Translate([System.Security.Principal.SecurityIdentifier]).Value; ^
    $tmpFile = [System.IO.Path]::GetTempFileName(); ^
    secedit /export /cfg $tmpFile /areas USER_RIGHTS /quiet; ^
    $content = Get-Content $tmpFile; ^
    if ($content -match 'SeServiceLogonRight') { ^
        if ($content -match $sid) { Write-Host 'Permission already granted.' } ^
        else { $content = $content -replace 'SeServiceLogonRight = ', ('SeServiceLogonRight = *' + $sid + ','); Set-Content $tmpFile $content; secedit /configure /db $env:temp\secedit.sdb /cfg $tmpFile /areas USER_RIGHTS /quiet; Write-Host 'Permission granted successfully.' } ^
    } else { ^
        $content += 'SeServiceLogonRight = *' + $sid; Set-Content $tmpFile $content; secedit /configure /db $env:temp\secedit.sdb /cfg $tmpFile /areas USER_RIGHTS /quiet; Write-Host 'Permission granted successfully.' ^
    }; ^
    Remove-Item $tmpFile -ErrorAction SilentlyContinue"

goto DATA_COLLECTION

:INSTALL_SYSTEM
set "RUN_AS_USER="
set "USER_PASS="
goto DATA_COLLECTION

:DATA_COLLECTION
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
echo --- STEP 3: PUBLISHING PROJECT ---
dotnet publish "%PROJECT_DIR%" -c Release -o "%PUBLISH_DIR%"

echo.
echo --- STEP 4: CREATING WINDOWS SERVICE ---
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
echo --- STEP 5: STARTING SERVICE ---
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
