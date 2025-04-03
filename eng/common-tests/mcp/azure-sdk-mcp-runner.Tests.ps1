Import-Module Pester

Describe "MCP runner config tests" -Tag "mcp" {
    BeforeAll {
        . $PSScriptRoot/../../common/mcp/azure-sdk-mcp-lib.ps1

        $testOutputDir = Join-Path ([System.IO.Path]::GetTempPath()) "mcp-test"
        if (Test-Path $testOutputDir) { Remove-Item $testOutputDir -Recurse -Force }
    }

    It "Should create a valid mcp.json file for a config" {
        $exePath = "$testOutputDir/test-$serverType.exe"
        New-Item -Path $exePath -ItemType File -Force | Out-Null

        @"
[
    {
        "Package": "test-node",
        "Version": "0.0.1",
        "Type": "node",
        "Port": 3001,
        "Feed": "public"
    },
    {
        "Package": "test-python",
        "Version": "0.0.1",
        "Type": "python",
        "Port": 3002,
        "Feed": "public"
    },
    {
        "Package": "test-local",
        "Type": "local",
        "Executable": "$exePath",
        "Port": 3003
    }
]

"@ | Out-File -FilePath "test-config.json" -Force

        Main -Config "test-config.json" -CreateVSCodeConfig $testOutputDir

        $json = Get-Content $testOutputDir/.vscode/mcp.json | ConvertFrom-Json -AsHashtable
        $json | Should -Not -BeNullOrEmpty

        $json.ContainsKey("servers") | Should -Be $true
        $json.servers['test-node'].type | Should -Be "stdio"
        $json.servers['test-node'].command | Should -Be "npx"
        $json.servers['test-python'].type | Should -Be "stdio"
        $json.servers['test-python'].command | Should -Be "uvx"
        $json.servers['test-local'].type | Should -Be "stdio"
        $json.servers['test-local'].command | Should -Be $exePath
    }

    It "Should create a valid mcp.json file for node" {
        Main `
            -Package "test-node" `
            -Version "0.0.1" `
            -Type "node" `
            -Feed "public" `
            -Port 5000 `
            -CreateVSCodeConfig $testOutputDir

        $json = Get-Content $testOutputDir/.vscode/mcp.json | ConvertFrom-Json -AsHashtable
        $json | Should -Not -BeNullOrEmpty

        $json.ContainsKey("servers") | Should -Be $true
        $json.servers['test-node'].type | Should -Be "stdio"
        $json.servers['test-node'].command | Should -Be "npx"
    }

    It "Should create a valid mcp.json file for python" {
        Main `
            -Package "test-python" `
            -Version "0.0.1" `
            -Type "python" `
            -Feed "public" `
            -Port 5000 `
            -CreateVSCodeConfig $testOutputDir

        $json = Get-Content $testOutputDir/.vscode/mcp.json | ConvertFrom-Json -AsHashtable
        $json | Should -Not -BeNullOrEmpty

        $json.ContainsKey("servers") | Should -Be $true
        $json.servers['test-python'].type | Should -Be "stdio"
        $json.servers['test-python'].command | Should -Be "uvx"
    }

    It "Should create a valid mcp.json file for local" {
        $exePath = "$testOutputDir/test-$serverType.exe"
        New-Item -Path $exePath -ItemType File -Force | Out-Null

        Main `
            -Package "test-local" `
            -Version "0.0.1" `
            -Type "local" `
            -Feed "public" `
            -Port 5000 `
            -Executable $exePath `
            -CreateVSCodeConfig $testOutputDir

        $json = Get-Content $testOutputDir/.vscode/mcp.json | ConvertFrom-Json -AsHashtable
        $json | Should -Not -BeNullOrEmpty

        $json.ContainsKey("servers") | Should -Be $true
        $json.servers['test-local'].type | Should -Be "stdio"
        $json.servers['test-local'].command | Should -Be $exePath
    }
}
