@echo off
setlocal
pushd "%~dp0"

where dotnet >nul 2>&1
if errorlevel 1 (
    echo [ERROR] .NET SDK was not found.
    echo Install .NET SDK 10 and run this file again.
    pause
    popd
    exit /b 1
)

curl.exe --silent --output NUL http://localhost:5072/api/health >nul 2>&1
if errorlevel 1 (
    echo [START] API: http://localhost:5072
    start "ArtifactGrade API" cmd /k "dotnet watch --project src\ArtifactGrade.Api\ArtifactGrade.Api.csproj run"
) else (
    echo [READY] API is already running.
)

curl.exe --silent --fail --output NUL http://localhost:5111/ >nul 2>&1
if errorlevel 1 (
    echo [START] Client: http://localhost:5111
    start "ArtifactGrade Client" cmd /k "dotnet watch --project src\ArtifactGrade.Client\ArtifactGrade.Client.csproj run"
) else (
    echo [READY] Client is already running.
)

echo [WAIT] Waiting for the web page...
for /L %%i in (1,1,60) do (
    curl.exe --silent --fail --output NUL http://localhost:5111/ >nul 2>&1
    if not errorlevel 1 goto open_browser
    timeout /t 1 /nobreak >nul
)

echo [WARN] The page did not start within 60 seconds.
echo Check the API and Client windows for error messages.
pause
popd
exit /b 1

:open_browser
echo [OPEN] http://localhost:5111
start "" "http://localhost:5111"
popd
exit /b 0
