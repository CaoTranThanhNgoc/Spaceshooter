<#
.SYNOPSIS
  One-command helper that deploys the Space Hawk account server (Assets/CloudCode/AccountRecovery.js)
  to your Unity Gaming Services project and prepares the secret values it needs.

.DESCRIPTION
  It does the parts that can be automated:
    1. installs the Unity Gaming Services CLI ("ugs") through npm when it is missing,
    2. logs in with YOUR service account key, points the CLI at this project's id and environment,
    3. deploys Assets/CloudCode (the AccountRecovery script),
    4. builds the value of the UGS_ADMIN_AUTH secret and a fresh random RECOVERY_PEPPER, and puts them on the
       clipboard one at a time so you can paste them into the Unity Dashboard.
  It cannot create the service account, the Gmail mail relay or the Dashboard secrets for you: those need your own
  logins, and Unity's Secret Manager has no CLI. Your key is only typed here, in this window - it is passed to
  the ugs CLI on stdin, never written to a file, never printed.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File Tools\CloudCodeSetup\Setup-CloudCode.ps1

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File Tools\CloudCodeSetup\Setup-CloudCode.ps1 -DryRun
  Shows what would happen (no installs, no login, no deploy) - handy to check the helper itself.
#>
[CmdletBinding()]
param(
    [string] $ProjectId,
    [string] $Environment = "production",
    [switch] $DryRun
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path

function Write-Step($text)  { Write-Host ""; Write-Host "== $text" -ForegroundColor Cyan }
function Write-Ok($text)    { Write-Host "   OK  $text" -ForegroundColor Green }
function Write-Note($text)  { Write-Host "   $text" -ForegroundColor Gray }
function Write-Warn2($text) { Write-Host "   !!  $text" -ForegroundColor Yellow }

function ConvertTo-PlainText([System.Security.SecureString] $secure) {
    $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
    try { return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr) }
}

function New-RandomSecret([int] $bytes = 36) {
    $buffer = New-Object byte[] $bytes
    $rng = [Security.Cryptography.RandomNumberGenerator]::Create()
    try { $rng.GetBytes($buffer) } finally { $rng.Dispose() }
    return ([Convert]::ToBase64String($buffer)).TrimEnd("=").Replace("+", "-").Replace("/", "_")
}

function Get-UgsAdminAuth([string] $keyId, [string] $secretKey) {
    return [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes("${keyId}:${secretKey}"))
}

function Invoke-Ugs {
    param([string[]] $Arguments, [string] $StdIn)
    if ($DryRun) { Write-Note "[dry run] ugs $($Arguments -join ' ')"; return }
    if ($PSBoundParameters.ContainsKey("StdIn")) { $StdIn | & ugs @Arguments } else { & ugs @Arguments }
    if ($LASTEXITCODE -ne 0) { throw "ugs $($Arguments[0]) failed (exit code $LASTEXITCODE)." }
}

function Show-SecretForPaste([string] $name, [string] $value, [string] $hint) {
    Write-Host ""
    Write-Host "   Secret  $name" -ForegroundColor White
    Write-Note $hint
    if ($DryRun) {
        Write-Note "[dry run] value ready ($($value.Length) characters), not copied"
        return
    }
    Set-Clipboard -Value $value
    Write-Ok "copied to the clipboard - paste it as the secret's Value"
    [void](Read-Host "   Press Enter once it is saved in the Dashboard")
}

Write-Host ""
Write-Host "Space Hawk - account server setup" -ForegroundColor White
if ($DryRun) { Write-Warn2 "DRY RUN: nothing is installed, logged in or deployed." }

# ---------------------------------------------------------------- 0. the project
Write-Step "0. This project"
if (-not $ProjectId) {
    $settings = Join-Path $repoRoot "ProjectSettings\ProjectSettings.asset"
    if (Test-Path $settings) {
        $match = Select-String -Path $settings -Pattern "cloudProjectId:\s*([0-9a-fA-F-]{36})" | Select-Object -First 1
        if ($match) { $ProjectId = $match.Matches[0].Groups[1].Value }
    }
}
if (-not $ProjectId) { $ProjectId = Read-Host "Unity project id (Dashboard > your project > Settings)" }
if ($ProjectId -notmatch "^[0-9a-fA-F-]{36}$") { throw "That does not look like a Unity project id: '$ProjectId'" }
Write-Ok "project id    $ProjectId"
Write-Ok "environment   $Environment"

$scriptFile = Join-Path $repoRoot "Assets\CloudCode\AccountRecovery.js"
if (-not (Test-Path $scriptFile)) { throw "Missing $scriptFile" }
Write-Ok "script        Assets\CloudCode\AccountRecovery.js"

# ---------------------------------------------------------------- 1. the CLI
Write-Step "1. Unity Gaming Services CLI (ugs)"
$hasUgs = [bool](Get-Command ugs -ErrorAction SilentlyContinue)
if ($hasUgs) {
    Write-Ok "ugs is installed"
}
elseif ($DryRun) {
    Write-Note "[dry run] would run: npm install -g ugs"
}
else {
    if (-not (Get-Command npm -ErrorAction SilentlyContinue)) { throw "Node.js / npm is needed to install the ugs CLI: https://nodejs.org" }
    Write-Note "installing with: npm install -g ugs"
    & npm install -g ugs
    if ($LASTEXITCODE -ne 0) { throw "npm install -g ugs failed." }
    $env:Path = $env:Path + ";" + (& npm config get prefix)
    if (-not (Get-Command ugs -ErrorAction SilentlyContinue)) { throw "ugs was installed but is not on PATH - open a new terminal and run this helper again." }
    Write-Ok "ugs installed"
}
if (-not (Get-Command node -ErrorAction SilentlyContinue) -and -not $DryRun) {
    Write-Warn2 "Node.js was not found; ugs needs it to read the script's inline parameters."
}

# ---------------------------------------------------------------- 2. the service account key
Write-Step "2. Service account key (from the Unity Dashboard)"
Write-Note "Needs the project roles: Cloud Code Script Editor / Publisher / Viewer and Unity Environments Admin"
Write-Note "(to deploy) plus Authentication Admin and Leaderboards Admin (what the server script calls)."
if ($DryRun) {
    $keyId = "DRY-RUN-KEY-ID"
    $secretKey = "DRY-RUN-SECRET"
}
else {
    $keyId = (Read-Host "Key ID").Trim()
    $secretKey = ConvertTo-PlainText (Read-Host "Secret key (hidden)" -AsSecureString)
    if (-not $keyId -or -not $secretKey) { throw "Both the Key ID and the Secret key are needed." }
}

# ---------------------------------------------------------------- 3. login, configure, deploy
Write-Step "3. Log in and deploy"
Invoke-Ugs -Arguments @("login", "--service-key-id", $keyId, "--secret-key-stdin") -StdIn $secretKey
Invoke-Ugs -Arguments @("config", "set", "project-id", $ProjectId)
Invoke-Ugs -Arguments @("config", "set", "environment-name", $Environment)
Push-Location $repoRoot
try { Invoke-Ugs -Arguments @("deploy", "Assets/CloudCode") } finally { Pop-Location }
if (-not $DryRun) { Write-Ok "AccountRecovery deployed" }

# ---------------------------------------------------------------- 4. secrets
Write-Step "4. Secrets (Unity Dashboard > your project > Secrets > Add secret)"
Write-Note "For every secret set  Service access = Cloud Code.  (Secret Manager has no CLI, so this part is by hand.)"

$runtimeKeyId = $keyId
$runtimeSecret = $secretKey
if (-not $DryRun) {
    $same = Read-Host "Use this same service account for the server's admin calls? [Y/n]"
    if ($same -match "^[nN]") {
        $runtimeKeyId = (Read-Host "Key ID of the narrower account (Authentication Admin + Leaderboards Admin only)").Trim()
        $runtimeSecret = ConvertTo-PlainText (Read-Host "Its secret key (hidden)" -AsSecureString)
    }
}
$adminAuth = Get-UgsAdminAuth $runtimeKeyId $runtimeSecret
$pepper = New-RandomSecret

Show-SecretForPaste "UGS_ADMIN_AUTH" $adminAuth "Key: UGS_ADMIN_AUTH   Value: (the clipboard)"
Show-SecretForPaste "RECOVERY_PEPPER" $pepper "Key: RECOVERY_PEPPER   Value: (the clipboard) - keep it, changing it later invalidates pending codes"

# the key material is no longer needed in memory
$secretKey = $null; $runtimeSecret = $null; $adminAuth = $null; $pepper = $null
if (-not $DryRun) { Set-Clipboard -Value " " }

Write-Host ""
Write-Host "Still to add by hand - the mail relay that sends the codes from your own Gmail (no domain needed):" -ForegroundColor White
Write-Note "1. script.google.com > New project > paste Tools\MailRelay\Code.gs > Project Settings > Script properties: RELAY_KEY = a long random string"
Write-Note "2. Deploy > New deployment > Web app > Execute as: Me, Who has access: Anyone > copy the /exec URL"
Write-Note "3. Unity secrets:  MAIL_RELAY_URL = that URL,  MAIL_RELAY_KEY = the same value as RELAY_KEY"
Write-Note "To rebuild UGS_ADMIN_AUTH later, use Tools\CloudCodeSetup\Make-AdminAuth.ps1 - it tests the key against Unity first."

Write-Step "5. Check it"
Write-Note "In Unity: press Play, then Tools > Space Hawk > Check Account Server."
Write-Note "It lists what the server sees (deployed? admin key? e-mail ready?) without showing any value."
Write-Host ""
