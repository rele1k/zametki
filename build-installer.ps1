# ============================================================
#  Заметки для Windows — автосборка портативного приложения
#  1) Распакуйте архив, впишите адрес сервера в Services/ApiConfig.cs
#  2) Правый клик по этому файлу → «Выполнить с помощью PowerShell»
#  3) Готовая папка Zametki появится на рабочем столе
# ============================================================

$ErrorActionPreference = "Stop"

Write-Host "=== Заметки: сборка приложения ===" -ForegroundColor Cyan

if ($env:OS -ne "Windows_NT") {
    Write-Host "Этот скрипт нужно запускать на Windows." -ForegroundColor Red
    exit 1
}

# .NET 8 SDK
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Host "Устанавливаю .NET 8 SDK..." -ForegroundColor Yellow
    winget install Microsoft.DotNet.SDK.8 --silent --accept-source-agreements --accept-package-agreements
    $env:Path = [System.Environment]::GetEnvironmentVariable("Path","Machine") + ";" + [System.Environment]::GetEnvironmentVariable("Path","User")
}
if (-not (dotnet --list-sdks 2>$null | Where-Object { $_ -match "^8\." })) {
    Write-Host "ОШИБКА: .NET 8 SDK не найден: https://dotnet.microsoft.com/download/dotnet/8.0" -ForegroundColor Red
    exit 1
}

# Сборка
Write-Host "Собираю приложение (5-10 минут)..." -ForegroundColor Yellow
dotnet publish -c Release -r win-x64 --self-contained true -o "$env:TEMP\zametki-out" 2>&1 | Select-Object -Last 3

# На рабочий стол
$dest = Join-Path ([Environment]::GetFolderPath("Desktop")) "Zametki"
if (Test-Path "$env:TEMP\zametki-out\Zametki.exe") {
    if (Test-Path $dest) { Remove-Item -Recurse -Force $dest }
    Copy-Item -Recurse "$env:TEMP\zametki-out" $dest
    Write-Host ""
    Write-Host "ГОТОВО! Приложение здесь: $dest" -ForegroundColor Green
    Write-Host "Запустите Zametki.exe" -ForegroundColor Green
} else {
    Write-Host "Сборка не удалась — пришлите текст ошибки в чат." -ForegroundColor Red
    exit 1
}
