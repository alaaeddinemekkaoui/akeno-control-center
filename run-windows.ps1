$ErrorActionPreference = 'Stop'

Write-Host "AKENO Control Center" -ForegroundColor Red
Write-Host "Checking .NET 8..."

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
  Write-Host "Install the .NET 8 SDK first: https://dotnet.microsoft.com/download/dotnet/8.0" -ForegroundColor Yellow
  exit 1
}

$env:AKENO_URLS = "http://0.0.0.0:5077"
if (-not $env:AKENO_REQUIRE_PAIRING) { $env:AKENO_REQUIRE_PAIRING = "false" }

$ip = Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue |
  Where-Object { $_.IPAddress -notlike '169.254*' -and $_.IPAddress -ne '127.0.0.1' } |
  Select-Object -First 1 -ExpandProperty IPAddress

Write-Host "Desktop: http://localhost:5077" -ForegroundColor Green
if ($ip) { Write-Host "Phone/LAN: http://$ip`:5077" -ForegroundColor Green }
Write-Host "Pairing required: $env:AKENO_REQUIRE_PAIRING"
Write-Host "Press Ctrl+C to stop."

dotnet run --project src/Akeno.Host
