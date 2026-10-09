<#
.SYNOPSIS
  Deploys the account server script (Assets/CloudCode/AccountRecovery.js) to the Space Hawk Unity project with a
  service account key you type here - and forgets the key again when it is done (ugs logout), so nothing stays
  stored on the computer.

.DESCRIPTION
  Run it whenever the script changes (display names, account recovery...). It needs a service account key with the
  project roles Cloud Code Editor / Script Publisher / Viewer and Unity Environments Admin (the key of the
  `spacehawk-server` service account has them). A key's secret is shown only once, when the key is created: if you
  no longer have it, create a new key (Dashboard > Service accounts > spacehawk-server > Add key).

  The Secrets (UGS_ADMIN_AUTH, MAIL_RELAY_URL...) live in the Dashboard and are not touched by this.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File Tools\CloudCodeSetup\Deploy-CloudCode.ps1

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File Tools\CloudCodeSetup\Deploy-CloudCode.ps1 -DryRun
  Shows the steps without logging in or deploying.
#>
[CmdletBinding()]
param(
    [string] $ProjectId = "4758b19c-89d7-4d12-87bc-36872752d77e",
    [string] $Environment = "production",
    [switch] $DryRun
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path

function Write-Step($text) { Write-Host ""; Write-Host "== $text" -ForegroundColor Cyan }
function Write-Ok($text)   { Write-Host "   OK  $text" -ForegroundColor Green }
function Write-Note($text) { Write-Host "   $text" -ForegroundColor Gray }

function ConvertTo-PlainText([System.Security.SecureString] $secure) {
    $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
    try { return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr) }
}

function Invoke-Ugs {
    param([string[]] $Arguments, [string] $StdIn)
    if ($DryRun) { Write-Note "[dry run] ugs $($Arguments -join ' ')"; return }
    if ($PSBoundParameters.ContainsKey("StdIn")) { $StdIn | & ugs @Arguments } else { & ugs @Arguments }
    if ($LASTEXITCODE -ne 0) { throw "ugs $($Arguments[0]) failed (exit code $LASTEXITCODE)." }
}

Write-Host ""
Write-Host "Space Hawk - deploy the account server script" -ForegroundColor White
if ($DryRun) { Write-Note "DRY RUN: nothing is logged in or deployed." }

if (-not (Test-Path (Join-Path $repoRoot "Assets\CloudCode\AccountRecovery.js"))) { throw "Missing Assets\CloudCode\AccountRecovery.js" }
if (-not $DryRun -and -not (Get-Command ugs -ErrorAction SilentlyContinue)) {
    throw "The ugs CLI is not installed. Run Tools\CloudCodeSetup\Setup-CloudCode.ps1 once (it installs it), or: npm install -g ugs"
}
if (-not $DryRun -and -not (Get-Command node -ErrorAction SilentlyContinue)) {
    throw "Node.js is not available in this window. ugs needs it to read the script's parameters (without it the script is deployed without them and nothing works). Install Node.js (https://nodejs.org) and open a NEW terminal."
}

Write-Step "1. Service account key"
if ($DryRun) {
    $keyId = "DRY-RUN-KEY-ID"; $secretKey = "DRY-RUN-SECRET"
}
else {
    $keyId = (Read-Host "Key ID").Trim()
    $secretKey = ConvertTo-PlainText (Read-Host "Secret key (hidden)" -AsSecureString)
    if (-not $keyId -or -not $secretKey) { throw "Both the Key ID and the Secret key are needed." }
}

$loggedIn = $false
try {
    Write-Step "2. Log in and deploy"
    Invoke-Ugs -Arguments @("login", "--service-key-id", $keyId, "--secret-key-stdin") -StdIn $secretKey
    $loggedIn = -not $DryRun
    $secretKey = $null
    Invoke-Ugs -Arguments @("config", "set", "project-id", $ProjectId)
    Invoke-Ugs -Arguments @("config", "set", "environment-name", $Environment)
    Push-Location $repoRoot
    try { Invoke-Ugs -Arguments @("deploy", "Assets/CloudCode") } finally { Pop-Location }
    if (-not $DryRun) { Write-Ok "AccountRecovery deployed to '$Environment'" }

    # The deploy tool reads the script's parameters (module.exports.params) with Node.js and says nothing when that
    # fails - the script then runs WITHOUT parameters and every call is answered "unknown_action". Look at what the
    # server really has.
    Write-Step "3. Check the deployed script"
    if ($DryRun) {
        Write-Note "[dry run] ugs cloud-code scripts get AccountRecovery --json"
    }
    else {
        function Get-DeployedScript {
            $text = (& ugs cloud-code scripts get AccountRecovery --json) | Out-String
            # the source code is long and not interesting here
            $short = [regex]::Replace($text, '"code"\s*:\s*"(?:[^"\\]|\\.)*"', '"code":"..."')
            $names = @()
            $start = $text.IndexOf("{")
            if ($start -ge 0) {
                try {
                    $obj = $text.Substring($start) | ConvertFrom-Json
                    $found = $obj.activeScript.params
                    if ($found -is [array]) { $names = @($found | ForEach-Object { if ($_.name) { $_.name } else { "$_" } }) }
                    elseif ($found) { $names = @($found.PSObject.Properties.Name) }
                }
                catch { }
            }
            if ($names.Count -eq 0 -and $text -match '"name"\s*:\s*"action"') { $names = @("action") }
            return [pscustomobject]@{ Names = $names; Short = $short }
        }

        $deployed = Get-DeployedScript
        if ($deployed.Names -notcontains "action") {
            Write-Host "   !!  The server has this script without its parameters - trying once more with 'scripts update'." -ForegroundColor Yellow
            Write-Host $deployed.Short -ForegroundColor DarkGray
            & ugs cloud-code scripts update AccountRecovery (Join-Path $repoRoot "Assets\CloudCode\AccountRecovery.js")
            & ugs cloud-code scripts publish AccountRecovery
            $deployed = Get-DeployedScript
        }

        if ($deployed.Names -contains "action") {
            Write-Ok ("parameters on the server: " + ($deployed.Names -join ", "))
        }
        else {
            Write-Host "   !!  The server has this script WITHOUT its parameters. What it holds:" -ForegroundColor Red
            Write-Host $deployed.Short -ForegroundColor Gray
            Write-Host "       Every call would answer 'unknown_action'. Send the lines above (from '== 2.' down) to Claude." -ForegroundColor Red
            try { Write-Host ("       node: " + (& node --version) + "   ugs: " + (& ugs --version)) -ForegroundColor Gray } catch { }
            throw "The script was deployed without parameters."
        }
    }
}
finally {
    # Whatever happened: do not leave the key stored on this computer.
    if ($loggedIn) {
        Write-Step "4. Forget the key"
        & ugs logout
        Write-Ok "logged out - no key is stored on this computer"
    }
}

Write-Host ""
Write-Note "Check it: in Unity press Play, then Tools > Space Hawk > Check Account Server."
Write-Note "A new secret or script can take up to 5 minutes to reach the running server."
Write-Host ""
