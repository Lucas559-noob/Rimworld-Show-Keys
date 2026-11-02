@echo off
setlocal EnableDelayedExpansion

title ShowTranslationKeys build

set "SCRIPT_DIR=%~dp0"
if "%SCRIPT_DIR:~-1%"=="\" set "SCRIPT_DIR=%SCRIPT_DIR:~0,-1%"
set "SOURCE_DIR=%SCRIPT_DIR%\Source"
set "ASM_DIR=%SCRIPT_DIR%\Assemblies"
set "LOG=%SCRIPT_DIR%\build.log"

echo === ShowTranslationKeys build ===
echo Script:  %SCRIPT_DIR%
echo Source:  %SOURCE_DIR%
echo Log:     %LOG%
echo.

:: --- 1. Check the Source folder exists ---
if not exist "%SOURCE_DIR%\ShowTranslationKeys.csproj" (
    echo ERROR: Could not find Source\ShowTranslationKeys.csproj
    echo Expected at: %SOURCE_DIR%\ShowTranslationKeys.csproj
    goto :fail
)

:: --- 2. Check dotnet is installed ---
where dotnet >nul 2>nul
if errorlevel 1 (
    echo ERROR: dotnet not found in PATH.
    echo Install the .NET SDK 8 from:
    echo   https://dotnet.microsoft.com/download
    goto :fail
)

echo dotnet found:
dotnet --version
echo.

:: --- 3. Build ---
pushd "%SOURCE_DIR%"
echo Building...
dotnet build -c Release > "%LOG%" 2>&1
set "BUILD_EXIT=!ERRORLEVEL!"
popd

if not "!BUILD_EXIT!"=="0" (
    echo.
    echo BUILD FAILED ^(exit code !BUILD_EXIT!^). Full log: %LOG%
    echo Last lines:
    echo ----------------------------------------
    powershell -NoProfile -Command "if (Test-Path '%LOG%') { Get-Content -Path '%LOG%' -Tail 30 }"
    echo ----------------------------------------
    goto :fail
)

:: --- 4. Copy the DLL ---
echo Copying DLL to Assemblies...
if not exist "%ASM_DIR%" mkdir "%ASM_DIR%"
copy /Y "%SOURCE_DIR%\bin\Release\net472\ShowTranslationKeys.dll" "%ASM_DIR%\ShowTranslationKeys.dll" >nul
if errorlevel 1 (
    echo ERROR: copy failed. Expected file:
    echo   %SOURCE_DIR%\bin\Release\net472\ShowTranslationKeys.dll
    goto :fail
)

echo.
echo OK: %ASM_DIR%\ShowTranslationKeys.dll
echo Restart RimWorld with Harmony and Show Translation Keys enabled.
goto :end

:fail
echo.
echo Build did not complete successfully.

:end
echo.
pause
endlocal
