#/usr/bin/env pwsh

[CmdletBinding(DefaultParameterSetName = 'File')]
param(
    [Parameter(ParameterSetName = 'File', Mandatory = $true)]
    [string]$Config,

    [Parameter(ParameterSetName = 'Single', Mandatory = $true)]
    [string]$Package,
    [Parameter(ParameterSetName = 'Single', Mandatory = $false)]
    [string]$Version = '0.0.0',
    [Parameter(ParameterSetName = 'Single', Mandatory = $true)]
    [string]$Type,
    [Parameter(ParameterSetName = 'Single', Mandatory = $false)]
    [string]$Executable,
    [Parameter(ParameterSetName = 'Single', Mandatory = $false)]
    [string]$Port,
    [Parameter(ParameterSetName = 'Single', Mandatory = $false)]
    [string]$Feed,

    [switch]$SSE,
    [switch]$Run,
    [string]$CreateVSCodeConfig,

    [Parameter(ParameterSetName = 'Clean', Mandatory = $true)]
    [switch]$Clean
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = 'Stop'

. ./azure-sdk-mcp-lib.ps1

$mcpDirectory = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath("$PSScriptRoot/../../../.azure-sdk-mcp")

if ($Clean) {
    Write-Warning "Cleaning $mcpDirectory"
    Remove-Item $mcpDirectory -Force -Recurse
    exit 0
}

Main `
    -mcpDirectory $mcpDirectory `
    -config $Config `
    -CreateVSCodeConfig $CreateVSCodeConfig `
    -package $Package `
    -version $Version `
    -type $Type `
    -executable $Executable `
    -port $Port `
    -feed $Feed `
    -run:$Run `
    -sse:$SSE
