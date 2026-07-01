<#
.SYNOPSIS
    Renames retired EINVWORLD AI environment variables from the old "AIAssistant__*" prefix to the
    current "AI__*" prefix on a Windows server.

.DESCRIPTION
    EINVWORLD v1.5.1 retired the legacy "AIAssistant" configuration section; AI configuration now lives
    only in the "AI" section (env prefix AI__). If a server was configured on an earlier version with
    AIAssistant__* environment variables, AI silently stays OFF after upgrading until they are renamed.
    (Invoicing is unaffected either way — AI is optional.)

    This script finds every AIAssistant__* variable at the chosen scope, creates the matching AI__*
    variable with the same value, and removes the old one. It never touches secrets in files and only
    reads/writes environment variables. By default it will NOT overwrite an AI__* variable that already
    exists (so a value you have already migrated is preserved) — use -Force to override.

    NOTE: This only covers environment variables set at the Machine/User scope (e.g. via System
    Properties or `setx /M`). If you instead set the variables in the IIS app pool's "Environment
    Variables" dialog, or in a server-side web.config <aspNetCore><environmentVariables>, rename them
    there by hand (same names, same values) and recycle the app pool.

.PARAMETER Scope
    Where to look: Machine (default) or User.

.PARAMETER AppPool
    Optional IIS application-pool name to recycle after the rename so the app picks up the change.

.PARAMETER Force
    Overwrite an AI__* variable even if it already exists.

.PARAMETER WhatIf
    Show what would change without making any change.

.EXAMPLE
    # Preview only:
    .\Rename-AiEnvVars.ps1 -WhatIf

.EXAMPLE
    # Rename machine-level vars and recycle the app pool:
    .\Rename-AiEnvVars.ps1 -AppPool 'EINVWORLD'

.NOTES
    Run in an elevated (Administrator) PowerShell for Machine scope.
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [ValidateSet('Machine', 'User')]
    [string]$Scope = 'Machine',

    [string]$AppPool,

    [switch]$Force
)

$ErrorActionPreference = 'Stop'
$target = [System.EnvironmentVariableTarget]::$Scope

# Discover the old variables at the requested scope.
$oldNames = ([Environment]::GetEnvironmentVariables($target).Keys) |
    Where-Object { $_ -like 'AIAssistant__*' } | Sort-Object

if (-not $oldNames) {
    Write-Host "No AIAssistant__* environment variables found at $Scope scope. Nothing to rename." -ForegroundColor Green
    return
}

Write-Host "Found $($oldNames.Count) legacy variable(s) at $Scope scope:" -ForegroundColor Cyan
$renamed = 0
foreach ($old in $oldNames) {
    $new = $old -replace '^AIAssistant__', 'AI__'
    $value = [Environment]::GetEnvironmentVariable($old, $target)
    $existing = [Environment]::GetEnvironmentVariable($new, $target)

    if ($null -ne $existing -and -not $Force) {
        Write-Warning "  $new already exists (value kept). Skipping $old. Use -Force to overwrite."
        continue
    }

    if ($PSCmdlet.ShouldProcess("$old -> $new ($Scope)", 'Rename environment variable')) {
        [Environment]::SetEnvironmentVariable($new, $value, $target)   # create/overwrite AI__*
        [Environment]::SetEnvironmentVariable($old, $null, $target)    # remove AIAssistant__*
        Write-Host "  $old -> $new" -ForegroundColor Green
        $renamed++
    }
}

Write-Host "Renamed $renamed variable(s)." -ForegroundColor Cyan

if ($AppPool) {
    if ($PSCmdlet.ShouldProcess($AppPool, 'Recycle IIS application pool')) {
        Import-Module WebAdministration -ErrorAction Stop
        Restart-WebAppPool -Name $AppPool
        Write-Host "Recycled app pool '$AppPool'." -ForegroundColor Green
    }
}
else {
    Write-Host "Recycle the app pool (or run 'iisreset') so the app picks up the change, then verify at Admin -> AI Settings -> Test connection." -ForegroundColor Yellow
}
