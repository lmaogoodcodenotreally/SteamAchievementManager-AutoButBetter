@echo off
setlocal enabledelayedexpansion

echo Building SAM and helper...
dotnet build SAM\SAM.csproj --configuration Release
dotnet publish SAM.Unlocker\SAM.Unlocker.csproj --configuration Release -r win-x64 -p:PublishSingleFile=true -p:SelfContained=true --output "SAM\bin\Release"

if errorlevel 1 (
    echo Build failed!
    exit /b 1
)

echo Creating upload directory...
set SCRIPT_DIR=%~dp0
set UPLOAD_DIR=%SCRIPT_DIR%upload
if exist "%UPLOAD_DIR%" rd /s /q "%UPLOAD_DIR%"
mkdir "%UPLOAD_DIR%"

echo Copying build artifacts to upload directory...
copy /Y "SAM\bin\Release\SAM.exe" "%UPLOAD_DIR%\" >nul
copy /Y "SAM\bin\Release\SAM.dll" "%UPLOAD_DIR%\" >nul
copy /Y "SAM\bin\Release\*.dll" "%UPLOAD_DIR%\" >nul
copy /Y "SAM\bin\Release\*.config" "%UPLOAD_DIR%\" >nul
copy /Y "SAM\bin\Release\log4net.config" "%UPLOAD_DIR%\" >nul
copy /Y "SAM\bin\Release\SAM.Unlocker.exe" "%UPLOAD_DIR%\" >nul

rem Compute SHA256 and size for SAM.Unlocker.exe and write metadata
set HELPER_PATH=SAM\bin\Release\SAM.Unlocker.exe
if exist "%HELPER_PATH%" (
    powershell -NoProfile -Command "Get-FileHash -Algorithm SHA256 -Path '%HELPER_PATH%' | Select-Object -Property Hash" > "%TEMP%\helper_hash.txt"
    for /f "usebackq tokens=*" %%h in ("%TEMP%\helper_hash.txt") do set HELPER_HASH=%%h
    rem HELPER_HASH contains header + value; extract the hash line
    for /f "tokens=1" %%a in ('powershell -NoProfile -Command "(Get-FileHash -Algorithm SHA256 -Path '%HELPER_PATH%').Hash"') do set HELPER_HASH=%%a
    for %%s in ("%HELPER_PATH%") do set HELPER_SIZE=%%~zs
    rem write JSON metadata
    set META_FILE=%UPLOAD_DIR%\testapp_metadata.json
    > "%META_FILE%" echo {"helperFile":"SAM.Unlocker.exe","hash":"%HELPER_HASH%","size":%HELPER_SIZE%}
)

echo Build complete. Output files are in: %UPLOAD_DIR%
echo.
dir "%UPLOAD_DIR%"