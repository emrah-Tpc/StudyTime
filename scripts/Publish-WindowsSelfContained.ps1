# Windows x64: .NET + Windows App SDK gömülü, çıktı ...\win10-x64\publish\
# Çalıştır: exe ile AYNI klasördeki tüm dosyalar gerekir (zip = publish klasörünün tamamı).
# Blazor WebView için hedef makinede genelde "Microsoft Edge WebView2 Runtime" kurulu olmalı:
# https://developer.microsoft.com/microsoft-edge/webview2/

$ErrorActionPreference = "Stop"
$proj = Join-Path $PSScriptRoot "..\StudyTime.DesktopClient\StudyTime.DesktopClient.csproj" | Resolve-Path

dotnet publish $proj `
  -f net9.0-windows10.0.19041.0 `
  -c Release `
  -p:RuntimeIdentifierOverride=win10-x64 `
  -p:WindowsPackageType=None `
  -p:WindowsAppSDKSelfContained=true `
  -p:SelfContained=true

Write-Host "Tamam. Çıktı: StudyTime.DesktopClient\bin\Release\net9.0-windows10.0.19041.0\win10-x64\publish\"
