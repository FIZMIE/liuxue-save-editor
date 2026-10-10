@echo off
rem ===================================================================
rem  Study Abroad Simulator - save editor build script
rem
rem  Uses only the .NET Framework compiler that ships with Windows.
rem  No Visual Studio and no network access required.
rem
rem  IMPORTANT: this file MUST stay ASCII-only. cmd.exe parses .cmd
rem  files with the OEM code page, so non-ASCII bytes can decode into
rem  characters like & or | and get executed as separate commands.
rem  The Chinese output filename is built from [char] escapes below.
rem ===================================================================
setlocal

set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
  echo [ERROR] csc.exe not found - .NET Framework 4.x is required.
  exit /b 1
)

set "HERE=%~dp0"
set "SRC=%HERE%src"
set "TMPOUT=%HERE%_build_tmp.exe"

set "COMMON=/nologo /platform:x64 /optimize+ /r:System.dll /r:System.Web.Extensions.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll"
set "FILES=%SRC%\SaveEditorForm.cs %SRC%\SaveDoc.cs %SRC%\Theme.cs %SRC%\GridStyle.cs"

echo Compiling save editor...
"%CSC%" /target:winexe %COMMON% /win32icon:"%SRC%\app.ico" /out:"%TMPOUT%" %FILES%

if errorlevel 1 (
  echo [ERROR] compile failed
  exit /b 1
)

echo Compiling self-test...
"%CSC%" /target:exe /main:SelfTest %COMMON% /out:"%HERE%selftest.exe" %FILES% "%SRC%\SelfTest.cs"

if errorlevel 1 (
  echo [WARN] self-test build failed, continuing anyway
)

echo Renaming output...
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$n = -join ([char]0x7559,[char]0x5B66,[char]0x6A21,[char]0x62DF,[char]0x5668,[char]0x4FEE,[char]0x6539,[char]0x5668); $t = Join-Path '%HERE%' ($n + '.exe'); if (Test-Path -LiteralPath $t) { Remove-Item -LiteralPath $t -Force }; Move-Item -LiteralPath '%TMPOUT%' -Destination $t -Force; Write-Host ('Done: ' + $t)"

echo.
echo Run the regression test with:  selftest.exe
endlocal
