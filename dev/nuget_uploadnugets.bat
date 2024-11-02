@echo off

set "file_masks=DrawnUi.Maui*.1.2.4.6*.nupkg AppoMobi.Maui.DrawnUi.1.2.4.6*.*nupkg"
set "source_dir=E:\Nugets"

setlocal enabledelayedexpansion
if "%~1"=="" (
    REM Ask for API key
    set /p "APIKEY=Please enter your NUGET.ORG API key: "
) else (
    set "APIKEY=%~1"
)

REM Loop over each file mask
for %%m in (%file_masks%) do (
    echo Processing files matching %%m
    REM Loop through each package file in the source directory matching current mask
    for %%f in ("%source_dir%\%%m") do (
        echo Uploading %%f to NUGET.ORG with API key.
        nuget push %%f -Source https://api.nuget.org/v3/index.json -ApiKey %APIKEY%
        if errorlevel 1 (
            echo An error occurred while uploading %%f.
        ) else (
            echo %%f uploaded successfully.
        )
    )
)

pause
endlocal
