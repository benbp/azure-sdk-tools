function Get-Feed([string]$feedName, [string]$packageType) {
    if ($feedName -eq 'public') {
        if ($packageType -eq 'node') {
            return "https://pkgs.dev.azure.com/azure-sdk/public/_packaging/azure-sdk-mcp/npm/registry/"
        }
        if ($packageType -eq 'python') {
            return "https://pkgs.dev.azure.com/azure-sdk/public/_packaging/azure-sdk-mcp/pypi/simple/"
        }
        if ($packageType -eq 'static') {
            return @{
                Org     = "https://dev.azure.com/azure-sdk/"
                Project = "29ec6040-b234-4e31-b139-33dc4287b756"
                Feed    = "azure-sdk-mcp"
            }
        }
        throw "Unsupported package type '$packageType' for feed"
    }
    if ($feedName -eq 'internal') {
        if ($packageType -eq 'node') {
            return "https://pkgs.dev.azure.com/azure-sdk/internal/_packaging/azure-sdk-mcp-private/npm/registry/"
        }
        if ($packageType -eq 'python') {
            return "https://pkgs.dev.azure.com/azure-sdk/internal/_packaging/azure-sdk-mcp-private/pypi/simple/"
        }
        if ($packageType -eq 'static') {
            return @{
                Org     = "https://dev.azure.com/azure-sdk/"
                Project = "590cfd2a-581c-4dcb-a12e-6568ce786175"
                Feed    = "azure-sdk-mcp-private"
            }
        }
        throw "Unsupported package type '$packageType' for feed"
    }
    throw "Unsupported feed '$feedName'"
}

function Get-Registry([string]$mcpDirectory, [string]$registryConfig) {
    if (-not (Test-Path -Path $mcpDirectory)) {
        New-Item -ItemType Directory -Path $mcpDirectory -Force | Out-Null
    }
    if (-not (Test-Path -Path $registryConfig)) {
        '{}' | Out-File $registryConfig
    }

    $artifactRegistry = Get-Content -Path $registryConfig | ConvertFrom-Json -AsHashtable
    return $artifactRegistry
}

function Set-NodeServerCache() {
}

function Set-ServerCache([object]$mcp, [string]$mcpDirectory) {
    $npmrc = Join-Path $mcpDirectory ".npmrc"
    if ($mcp.Type -eq 'node' -and !(Test-Path $npmrc)) {
        $feedUrl = Get-Feed $mcp.Feed 'node'
        "registry=https://registry.npmjs.org/" | Out-File $npmrc
        $scope = $mcp.Package -split '/' | Select-Object -First 1
        "${scope}:registry=$feedUrl" | Out-File -Append $npmrc
    }
    elseif ($mcp.Type -eq 'local') {
        if (-not (Split-Path -IsAbsolute $mcp.Executable)) {
            throw "Executable '$($mcp.Executable)' must be an absolute path for package '$($mcp.Package)'"
        }
        if (!(Test-Path $mcp.Executable)) {
            throw "Executable '$($mcp.Executable)' does not exist for package '$($mcp.Package)'"
        }
    }
    elseif ($mcp.Type -eq 'static') {
        if (!(Get-Command az)) {
            throw "az must be installed to run static mcp servers: https://learn.microsoft.com/cli/azure/install-azure-cli"
        }
        $extensions = az extension list -o json
        if ($extensions.Name -notcontains 'azure-devops') {
            Write-Host "Installing azure-devops extension for az artifacts universal package download"
            az extension add --name azure-devops
        }
        if (!(Test-Path $mcp.Executable)) {
            $feedData = Get-Feed $mcp.Feed 'static'
            Write-Host "az artifacts universal download --organization $($feedData.Org) --project $($feedData.Project) --scope project --feed $($feedData.Feed) --name $($mcp.Package) --version $($mcp.Version) --path ."
            $out = az artifacts universal download --organization $feedData.Org --project $feedData.Project --scope project --feed $feedData.Feed --name $mcp.Package --version $mcp.Version --path . 2>&1
            Write-Host $out
            if (!(Test-Path $mcp.Executable)) {
                throw "Static server universal package '$($mcp.Package)' must contain executable named '$($mcp.Executable)'"
            }
            chmod +x $mcp.Executable
        }
    }
}

function Set-Registry([object]$mcp, [string]$packageDirectory, [object]$artifactRegistry, [string]$registryConfig) {
    $artifactRegistry[$mcp.Package] = @{}
    $artifactRegistry[$mcp.Package].Version = $mcp.Version
    $artifactRegistry[$mcp.Package].Directory = $packageDirectory
    if ($mcp.Executable) {
        $artifactRegistry[$mcp.Package].Executable = $mcp.Executable
    }
    $artifactRegistry | ConvertTo-Json -Depth 10 | Set-Content -Path $registryConfig
}

function Get-LogPath([string]$packageName) {
    $logFile = "${packageName}_{0}.log" -f (Get-Date -Format "yyyyMMdd_HHmmss")
    $log = Join-Path (Get-Location) "$logFile.txt"
    $errorLog = Join-Path (Get-Location) "$logFile.error.txt"
    New-Item -ItemType File -Path $log -Force | Out-Null
    return $log, $errorLog
}

function Get-TransportArgs([switch]$sse, [int]$port) {
    if ($sse) {
        return @("--transport", "sse", "--port", "$port")
    }
}

function Start-ServerProcess([string]$packageName, [string]$filePath, [string]$argumentList, [object]$environment, [switch]$sse) {
    $log, $errorLog = Get-LogPath $packageName

    $process = $null
    if ($sse) {
        $process = Start-Process `
            -FilePath $filePath `
            -ArgumentList $argumentList `
            -RedirectStandardOutput $log -RedirectStandardError $errorLog `
            -Environment $environment `
            -NoNewWindow `
            -PassThru
    }
    else {
        $process = New-Object System.Diagnostics.Process
        $process.StartInfo.FileName = $filePath
        $process.StartInfo.Arguments = $argumentList
        $process.StartInfo.RedirectStandardInput = $true
        $process.StartInfo.UseShellExecute = $false
        $process.Start() | Out-Null
    }

    "$filePath $argumentList" | Out-File $PSScriptRoot/debug.log -Append

    return $process, $log, $errorLog
}

function Get-NodeServerCommand([object]$mcp, [string]$mcpDirectory, [switch]$sse) {
    if (!(Get-Command npx) -or !(Get-Command node)) {
        throw "npx/node must be installed to run js/ts mcp servers: https://nodejs.org/download"
    }
    $feedArgs = @()
    if ($mcp.Feed) {
        $feedArgs += "--userconfig"
        $feedArgs += "$mcpDirectory/.npmrc"
    }
    return @('npx',
        '-y',
        $feedArgs,
        $mcp.Package,
        '--',
        'server',
        'start',
            (Get-TransportArgs -sse:$sse -port $mcp.Port)) | ForEach-Object { $_ }
}

function Get-PythonServerCommand([object]$mcp, [switch]$sse) {
    if (!(Get-Command uvx) -or !(Get-Command python)) {
        throw "uv/uvx/python must be installed to run python mcp servers: https://github.com/astral-sh/uv?tab=readme-ov-file#installation"
    }
    $feedArgs = @()
    if ($mcp.Feed) {
        $feedUrl = Get-Feed $mcp.Feed 'python'
        $feedArgs += "--index"
        $feedArgs += $feedUrl
    }
    return @('uvx', $feedArgs, "$($mcp.Package)", "--", "server", "start", (Get-TransportArgs -sse:$sse $mcp.Port)) | ForEach-Object { $_ }
}

function Get-LocalServerCommand([object]$mcp, [switch]$sse) {
    return @($mcp.Executable, "server", "start", (Get-TransportArgs -sse:$sse -port $mcp.Port)) | ForEach-Object { $_ }
}

function Get-ServerCommand([object]$mcp, [string]$mcpDirectory, [switch]$sse) {
    if ($mcp.Type -eq 'node') {
        return Get-NodeServerCommand $mcp $mcpDirectory -sse:$sse
    }
    if ($mcp.Type -eq 'python') {
        return Get-PythonServerCommand $mcp -sse:$sse
    }
    if ($mcp.Type -eq 'local') {
        return Get-LocalServerCommand $mcp -sse:$sse
    }
    throw "Unsupported server type '$($mcp.Type)' for command"
}

function Start-StaticServer([object]$mcp, [switch]$sse) {
    $exePath = (Get-ChildItem $mcp.Executable).FullName
    return Start-ServerProcess `
        -packageName $mcp.Package `
        -filePath $exePath `
        -argumentList "server start $(Get-TransportArgs -sse:$sse $mcp.Port)".Trim() `
        -sse:$sse
}

function Start-Server([object]$mcp, [string]$mcpDirectory, [object]$artifactRegistry, [switch]$sse) {
    if (!$artifactRegistry[$mcp.Package]) {
        throw "Package $($mcp.Package) not found in registry"
    }

    $serverDirectory = $artifactRegistry[$mcp.Package]["Directory"]
    Push-Location -Path $serverDirectory

    if ($sse -and !$mcp.Port) {
        throw "-Port must be specified when -SSE is enabled"
    }

    try {
        $command, $commandArgs = Get-ServerCommand $mcp $mcpDirectory -sse:$sse
        return Start-ServerProcess -packageName $mcp.Package -filePath $command -argumentList $commandArgs -sse:$sse
    }
    finally {
        Pop-Location
    }
}

function Start-FromConfig([string]$mcpDirectory, [object]$serverConfig, [object]$artifactRegistry, [switch]$sse) {
    $servers = @()
    foreach ($mcp in $serverConfig) {
        $process, $log, $errorLog = Start-Server $mcp $mcpDirectory $artifactRegistry -sse:$SSE
        $servers += @{ Process = $process; Log = $log; ErrorLog = $errorLog; Package = $mcp.Package }
    }
    return $servers
}

function Trace-Servers([array]$servers) {
    $colors = @("White", "Blue", "Green", "Cyan", "Red", "Magenta", "Yellow", "Gray", "DarkGray", "DarkBlue", "DarkGreen", "DarkCyan", "DarkMagenta", "DarkYellow")
    $colorIdx = 0
    $toTail = @()

    foreach ($server in $servers) {
        $server['LogColor'] = $colors[$colorIdx]
        $colorIdx++
    }

    foreach ($server in $servers) {
        $toTail += @{ Name = $server.Package; File = $server.Log; Color = $server.LogColor }
        $toTail += @{ Name = $server.Package; File = $server.ErrorLog; Color = "Red" }
    }

    $toTail | ForEach-Object -Parallel {
        $tail = $_
        Write-Host "Tailing logs for [$($tail.Name)] at [$($tail.File)]"
        Get-Content $tail.File -Tail 1 -Wait `
        | ForEach-Object {
            Write-Host -ForegroundColor $tail.Color "[$($tail.Name)] $_"
        }
    }
}

function Get-ServerConfigEntry([object]$mcp, [string]$mcpDirectory, [switch]$sse) {
    if ($sse) {
        return @{
            command = "sse"
            url     = "http://localhost:$($mcpServerConfig.Port)/sse"
        }
    }

    $command, $commandArgs = Get-ServerCommand $mcp $mcpDirectory -sse:$sse

    return @{
        type    = "stdio"
        command = $command
        args    = $commandArgs
    }
}

function Set-VsCodeConfig([string]$configPath, [object]$serverConfig, [string]$mcpDirectory, [switch]$sse) {
    $mcpConfig = @{ servers = @{} }
    foreach ($mcp in $serverConfig) {
        $mcpConfig.servers[$mcp.Package] = Get-ServerConfigEntry $mcp $mcpDirectory -sse:$sse
    }

    if ($configPath -notlike "*.vscode/mcp.json") {
        $configPath = Join-Path -Path $configPath -ChildPath ".vscode/mcp.json"
    }

    Write-Host "Creating VSCode config at [$configPath]"
    New-Item -ItemType File -Path $configPath -Force | Out-Null
    $mcpConfig | ConvertTo-Json -Depth 100 | Set-Content -Path $configPath
}

function Start([string]$mcpDirectory, [string]$vscodeConfigPath, [array]$serverConfig, [switch]$run, [switch]$sse) {
    $error.Clear()
    $registryConfig = Join-Path -Path $McpDirectory -ChildPath "mcp-servers.json"
    $artifactRegistry = Get-Registry $mcpDirectory $registryConfig
    foreach ($mcp in $serverConfig) {
        $packageDirectory = "$mcpDirectory" + "/" + ($mcp.Package -replace "[^A-Za-z0-9_]", "_") + "_$($mcp.Version)"
        if (
            (!$artifactRegistry[$mcp.Package]) -or
            ($mcp.Version -and $artifactRegistry[$mcp.Package]['Version'] -ne $mcp.Version) -or
            ($mcp.Executable -and $artifactRegistry[$mcp.Package]['Executable'] -ne $mcp.Executable) -or
            !(Test-Path -Path $packageDirectory)
        ) {
            New-Item -ItemType Directory -Path $packageDirectory -Force | Out-Null
            Set-ServerCache $mcp $mcpDirectory
            Set-Registry -mcp $mcp -packageDirectory $packageDirectory -artifactRegistry $artifactRegistry -registryConfig $registryConfig
        }
    }

    if ($vscodeConfigPath) {
        Set-VsCodeConfig $vscodeConfigPath $serverConfig $mcpDirectory -sse:$sse
    }
    if (!$run) {
        Write-Host "Add `-Run -SSE` to start $($serverConfig.Length) MCP servers or start vscode from [$vscodeConfigPath]"
        return
    }

    $servers = @()

    try {
        Write-Host "Starting $($servers.Count) servers"
        $servers = @(Start-FromConfig -mcpDirectory $mcpDirectory -serverConfig $serverConfig -artifactRegistry $artifactRegistry -sse:$sse)
        Trace-Servers $servers
    }
    finally {
        $error | ForEach-Object {
            $_
        }
        if ($servers) {
            foreach ($server in $servers) {
                Write-Host "Stopping [$($servers.Package)]"
                $server.Process | Stop-Process -Force
            }
        }
    }
}

function Main([string]$mcpDirectory, [string]$config, [string]$CreateVSCodeConfig, [string]$package, [string]$version, [string]$type, [string]$executable, [string]$port, [string]$feed, [switch]$run, [switch]$sse) {
    $serverConfig = if (!$config) {
        @( @{ Package = $package; Version = $version; Type = $type; Executable = $executable; Port = $port; Feed = $feed } )
    }
    else {
        Get-Content -Raw $config | ConvertFrom-Json -AsHashtable
    }

    Start -serverConfig $serverConfig -vscodeConfigPath $CreateVSCodeConfig -mcpDirectory $mcpDirectory -run:$Run -sse:$sse
}
