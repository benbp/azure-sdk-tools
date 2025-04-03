#/usr/bin/env pwsh

param(
    [string]$Type,
    [string]$Package,
    [string]$Version,
    [switch]$Clean
)

$ErrorActionPreference = 'Stop'

$org = "https://dev.azure.com/azure-sdk/"
$project = "29ec6040-b234-4e31-b139-33dc4287b756"
$feed = "azure-sdk-mcp"
$McpDirectory = "$HOME/.azure-mcp"
$registryConfig = Join-Path -Path $McpDirectory -ChildPath "mcp-servers.json"

if ($Clean) {
    Write-Warning "Cleaning $McpDirectory"
    Remove-Item $McpDirectory -Force -Recurse
    exit 0
}

function Init-Registry() {
    if (-not (Test-Path -Path $McpDirectory)) {
        New-Item -ItemType Directory -Path $McpDirectory -Force | Out-Null
    }
    if (-not (Test-Path -Path $registryConfig)) {
        '{}' | Out-File $registryConfig
    }

    $artifactRegistry = Get-Content -Path $registryConfig | ConvertFrom-Json -AsHashtable
    return $artifactRegistry
}

function Set-Registry([string]$packageName, [string]$packageVersion, [string]$packageDirectory, [object]$artifactRegistry) {
    $artifactRegistry[$Package] = @{}
    $artifactRegistry[$Package].Version = $packageVersion
    $artifactRegistry[$Package].Directory = $packageDirectory
    $artifactRegistry | ConvertTo-Json -Depth 10 | Set-Content -Path $registryConfig
}

function Set-Node-Env() {
    $env:PATH += ";$HOME/.nvm/versions/node/v22.14.0/bin"
}

function Start-Node-Server([string]$packageName) {
    if (!(Test-Path .npmrc)) {
        "registry=https://registry.npmjs.org/" | Out-File .npmrc
        "@azure-sdk-mcp:registry=https://pkgs.dev.azure.com/azure-sdk/public/_packaging/azure-sdk-mcp/npm/registry/" | Out-File -Append .npmrc
    }
    #Set-Node-Env
    & /home/ben/.nvm/versions/node/v22.14.0/bin/npx -y $packageName
    if ($LASTEXITCODE) {
        Write-Error "MCP Server failed with code $LASTEXITCODE"
        exit $LASTEXITCODE
    }
}

function Start-Server([string]$packageName, [object]$artifactRegistry) {
    if (!$artifactRegistry[$Package]) {
        throw "Package $Package not found in registry"
    }

    $serverDirectory = $artifactRegistry[$Package]["Directory"]
    Push-Location -Path $serverDirectory

    try {
        if ($Type -eq 'node') {
            Start-Node-Server $packageName
        }
        else {
            throw "MCP server type '$serverType' not supported"
        }
    }
    finally { Pop-Location }
}

$registry = Init-Registry

$packageDirectory = "$McpDirectory" + "/" + ($Package -replace "[^A-Za-z0-9_]", "_")
if (!$registry[$Package] -or !(Test-Path -Path $packageDirectory)) {
    New-Item -ItemType Directory -Path $packageDirectory -Force | Out-Null
    Set-Registry $Package $Version $packageDirectory $registry
}

Start-Server $Package $registry
