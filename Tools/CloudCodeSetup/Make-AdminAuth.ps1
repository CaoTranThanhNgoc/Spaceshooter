<#
.SYNOPSIS
  Builds the value of the UGS_ADMIN_AUTH secret from a service account key - and proves Unity accepts it BEFORE
  it goes on your clipboard, so a typo or a wrong paste can no longer end up in the Dashboard.

.DESCRIPTION
  UGS_ADMIN_AUTH = base64("KEY_ID:SECRET_KEY") of a service account with the project roles Authentication Admin
  (Editor) and Leaderboards Admin. This script
    1. asks for the Key ID and the Secret key (the secret is hidden while you paste it),
    2. builds the value and sends ONE harmless read (list the leaderboards) to Unity with it,
    3. only if Unity answers 200: copies the value to the clipboard - paste it as the secret's Value.
  The key never leaves this window except in that request to services.api.unity.com.

  Usage:   powershell -ExecutionPolicy Bypass -File Tools\CloudCodeSetup\Make-AdminAuth.ps1
#>
param(
    [string] $KeyId,
    [string] $SecretKey,
    [string] $ProjectId = "4758b19c-89d7-4d12-87bc-36872752d77e",
    [string] $EnvironmentId = "f9b951d8-e175-495d-b979-d0cf4d35d062"
)

$ErrorActionPreference = "Stop"

function ConvertTo-PlainText([Security.SecureString] $secure) {
    $ptr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
    try { return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($ptr) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ptr) }
}

if (-not $KeyId) { $KeyId = Read-Host "Key ID" }
if (-not $SecretKey) { $SecretKey = ConvertTo-PlainText (Read-Host "Secret key (hidden)" -AsSecureString) }
$KeyId = $KeyId.Trim()
$SecretKey = $SecretKey.Trim()
if (-not $KeyId -or -not $SecretKey) { throw "Both the Key ID and the Secret key are needed." }

$value = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes("${KeyId}:${SecretKey}"))
$url = "https://services.api.unity.com/leaderboards/v1/projects/$ProjectId/environments/$EnvironmentId/leaderboards"

$status = 0
try {
    $response = Invoke-WebRequest -Uri $url -Headers @{ Authorization = "Basic $value" } -UseBasicParsing
    $status = [int] $response.StatusCode
}
catch {
    if ($_.Exception.Response) { $status = [int] $_.Exception.Response.StatusCode } else { throw }
}
$SecretKey = $null

if ($status -ge 200 -and $status -lt 300) {
    Set-Clipboard -Value $value
    Write-Host "Unity accepted this key (HTTP $status)." -ForegroundColor Green
    Write-Host "The value is on your clipboard ($($value.Length) characters): in the Dashboard > Secrets, delete UGS_ADMIN_AUTH," -ForegroundColor Green
    Write-Host "add it again (Key: UGS_ADMIN_AUTH, Value: Ctrl+V, Service access: Cloud Code) and do not copy anything else first." -ForegroundColor Green
}
elseif ($status -eq 403) {
    Write-Host "Unity knows this key, but it is not allowed to read leaderboards (HTTP 403)." -ForegroundColor Yellow
    Write-Host "Give the service account the project role 'Leaderboards Admin' (and 'Authentication Editor'), then run this again." -ForegroundColor Yellow
}
elseif ($status -eq 401) {
    Write-Host "Unity rejected this key (HTTP 401): the Key ID or the Secret key is wrong." -ForegroundColor Red
    Write-Host "A key's secret is shown only once, when the key is created - if it is lost, use 'Add key' on the service account and try the new one." -ForegroundColor Red
}
else {
    Write-Host "Unity answered HTTP $status - nothing was copied. Try again in a minute." -ForegroundColor Red
}
