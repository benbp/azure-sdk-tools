// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Microagents;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.Languages;
using Azure.Sdk.Tools.Cli.Telemetry;
using Azure.Sdk.Tools.Cli.Tools.EngSys;
using Azure.Sdk.Tools.Cli.Tests.Mocks.Services;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;

namespace Azure.Sdk.Tools.Cli.Tests.Helpers;

/// <summary>
/// Contract-focused tests for all language-specific <see cref="IPackageInfoHelper"/> implementations.
/// Ensures consistent parsing of repo root, relative path, and language-specific version extraction
/// without duplicating per-language edge case tests.
/// </summary>
[TestFixture]
public class PackageInfoContractTests
{
    private PackageInfoTool tool;
    private TempDirectory _tempRoot = null!;

    [SetUp]
    public void Setup()
    {
        var gitCommandHelper = new GitCommandHelper(Mock.Of<ILogger<GitCommandHelper>>(), Mock.Of<IRawOutputHelper>());
        var gitHelper = new Mock<GitHelper>(Mock.Of<IGitHubService>(), gitCommandHelper, Mock.Of<ILogger<GitHelper>>());
        var processHelper = new ProcessHelper(new TestLogger<ProcessHelper>(), Mock.Of<IRawOutputHelper>());
        var pythonHelper = new PythonHelper(new TestLogger<PythonHelper>(), Mock.Of<IRawOutputHelper>());
        var languageServices = new List<LanguageService> {
            new DotnetLanguageService(processHelper, Mock.Of<IPowershellHelper>(), gitHelper.Object, new TestLogger<DotnetLanguageService>(), Mock.Of<ICommonValidationHelpers>(), Mock.Of<IFileHelper>(), Mock.Of<ISpecGenSdkConfigHelper>(), Mock.Of<IChangelogHelper>()),
            new JavaLanguageService(new Mock<IProcessHelper>().Object, gitHelper.Object, new Mock<IMavenHelper>().Object, new Mock<IMicroagentHostService>().Object, new TestLogger<JavaLanguageService>(), Mock.Of<ICommonValidationHelpers>(), Mock.Of<IFileHelper>(), Mock.Of<ISpecGenSdkConfigHelper>(), Mock.Of<IChangelogHelper>()),
            new PythonLanguageService(new Mock<IProcessHelper>().Object, pythonHelper, new Mock<INpxHelper>().Object, gitHelper.Object, new TestLogger<PythonLanguageService>(), Mock.Of<ICommonValidationHelpers>(), Mock.Of<IFileHelper>(), Mock.Of<ISpecGenSdkConfigHelper>(), Mock.Of<IChangelogHelper>()),
            new JavaScriptLanguageService(new Mock<IProcessHelper>().Object, new Mock<INpxHelper>().Object, gitHelper.Object, new TestLogger<JavaScriptLanguageService>(), Mock.Of<ICommonValidationHelpers>(), Mock.Of<IFileHelper>(), Mock.Of<ISpecGenSdkConfigHelper>(), Mock.Of<IChangelogHelper>()),
            new GoLanguageService(processHelper, Mock.Of<IPowershellHelper>(), gitHelper.Object, new TestLogger<GoLanguageService>(), Mock.Of<ICommonValidationHelpers>(), Mock.Of<IFileHelper>(), Mock.Of<ISpecGenSdkConfigHelper>(), Mock.Of<IChangelogHelper>())
        };
        var logger = new TestLogger<PackageInfoTool>();
        var outputHelper = new OutputHelper(OutputHelper.OutputModes.Hidden);
        tool = new PackageInfoTool(gitHelper.Object, logger, languageServices);
        tool.Initialize(outputHelper, new Mock<ITelemetryService>().Object, new MockUpgradeService());

        _tempRoot = TempDirectory.Create("azsdk_pkginfo_contract_tests");
    }

    [TearDown]
    public void TearDown() => _tempRoot.Dispose();

    private void CreateTestFile(string packagePath, string relativePath, string content)
    {
        var fullPath = Path.Combine(packagePath, relativePath);
        var directory = Path.GetDirectoryName(fullPath);
        if (directory != null && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        File.WriteAllText(fullPath, content);
    }

    private void SetupDotNetPackage(string packagePath, string packageName, string version, SdkType sdkType)
    {
        var sdkTypeValue = sdkType switch
        {
            SdkType.Dataplane => "client",
            SdkType.Management => "mgmt",
            SdkType.Functions => "functions",
            _ => "client"
        };

        var csprojContent = $@"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>

  <Target Name=""GetPackageInfo"" Returns=""@(PackageInfoItem)"">
    <ItemGroup>
      <PackageInfoItem Include=""'$(MSBuildProjectDirectory)' 'testservice' '{packageName}' '{version}' '{sdkTypeValue}' 'true' 'bin/Release/net8.0' 'false'"" />
    </ItemGroup>
  </Target>
</Project>";
        CreateTestFile(packagePath, $"src/{packageName}.csproj", csprojContent);
    }

    private void SetupJavaPackage(string packagePath, string artifactId, string version)
    {
        CreateTestFile(packagePath, "pom.xml", $"<project><modelVersion>4.0.0</modelVersion><groupId>com.azure</groupId><artifactId>{artifactId}</artifactId><version>{version}</version></project>");
    }

    private async Task SetupPythonPackageAsync(string packagePath, string packageName, string version)
    {
        // Create the eng/scripts directory structure and the get_package_properties.py script
        var repoRoot = Path.Combine("../../", packagePath);
        var scriptsDir = Path.Combine(repoRoot, "eng", "scripts");
        Directory.CreateDirectory(scriptsDir);

        // Create a minimal Python script that outputs the package info
        var scriptContent = $@"#!/usr/bin/env python
import sys
import os

# Simple mock that returns the expected format
package_path = sys.argv[sys.argv.index('-s') + 1] if '-s' in sys.argv else ''
package_name = '{packageName}'
version = '{version}'

print(f'{{package_name}} {{version}} True {{package_path}} ')
";
        CreateTestFile(repoRoot, Path.Combine("eng", "scripts", "get_package_properties.py"), scriptContent);
    }

    private void SetupJavaScriptPackage(string packagePath, string packageName, string version, SdkType sdkType)
    {
        CreateTestFile(packagePath, "package.json", $$"""
{
  "name": "@azure/{{packageName}}",
  "version": "{{version}}",
  "sdk-type": "{{sdkType switch
        {
            SdkType.Dataplane => "client",
            SdkType.Management => "mgmt",
            _ => ""
        }}}"
}
""");
    }

    private async Task SetupGoPackageAsync(string packagePath, string version)
    {
        var gitCommandHelper = new GitCommandHelper(NullLogger<GitCommandHelper>.Instance, Mock.Of<IRawOutputHelper>());
        var gitHelper = new GitHelper(Mock.Of<IGitHubService>(), gitCommandHelper, Mock.Of<ILogger<GitHelper>>());

        CreateTestFile(Path.Join(await gitHelper.DiscoverRepoRootAsync(packagePath, CancellationToken.None), "eng", "common", "scripts"), "common.ps1",
            $@"function Get-GoModuleProperties($goModPath) {{
                return @{{
                    Version = ""{version}""
                }}
            }}");
    }


    [Test]
    [TestCase(SdkLanguage.DotNet, "azure-sdk-for-net", "storage", "Azure.Storage.Blobs")]
    [TestCase(SdkLanguage.Java, "azure-sdk-for-java", "storage", "azure-storage-blob")]
    [TestCase(SdkLanguage.Python, "azure-sdk-for-python", "storage", "storage-blob")]
    [TestCase(SdkLanguage.JavaScript, "azure-sdk-for-js", "storage", "storage-blob")]
    [TestCase(SdkLanguage.Go, "azure-sdk-for-go", "security/keyvault", "azkeys")]
    public async Task CommonProperties_AreDerivedCorrectly(SdkLanguage language, string repoName, string serviceDirectory, string package)
    {
        var service = serviceDirectory.Contains('/') ? serviceDirectory.Split('/')[^1] : serviceDirectory;
        var repoRoot = Path.Combine(_tempRoot.DirectoryPath, repoName);
        var sdkPath = Path.Combine(repoRoot, "sdk", service, package);

        Directory.CreateDirectory(repoRoot);
        if (!Directory.Exists(Path.Combine(repoRoot, ".git")))
        {
            await GitTestHelper.GitInitAsync(repoRoot);
        }
        Directory.CreateDirectory(sdkPath);

        var sdkLanguage = SdkLanguageHelpers.GetLanguageForRepo(repoName);
        var languageService = tool.GetLanguageService(sdkLanguage);
        var info = await languageService.GetPackageInfo(sdkPath);

        Assert.Multiple(() =>
        {
            Assert.That(info.PackagePath, Is.EqualTo(RealPath.GetRealPath(sdkPath)));
            Assert.That((string)info.RepoRoot, Does.EndWith(repoName));
            var expectedRelative = Path.Combine(service, package);
            Assert.That(info.RelativePath, Is.EqualTo(expectedRelative));
            Assert.That(info.ServiceName, Is.EqualTo(service));
            Assert.That(info.PackageName, Is.Null);
            Assert.That(info.Language, Is.EqualTo(language));
        });
    }

    [Test]
    [TestCase(SdkLanguage.DotNet, "azure-sdk-for-net", "data", "Azure.Data.Test", "5.6.7")]
    [TestCase(SdkLanguage.Java, "azure-sdk-for-java", "core", "azure-core", "1.2.3")]
    [TestCase(SdkLanguage.Python, "azure-sdk-for-python", "ai", "azure-ai-test", "1.0.1")]
    [TestCase(SdkLanguage.JavaScript, "azure-sdk-for-js", "test", "azure-testpkg", "2.3.4")]
    [TestCase(SdkLanguage.Go, "azure-sdk-for-go", "security/keyvault", "azkeys", "v1.4.1-beta.1")]
    public async Task VersionParsing_Works(SdkLanguage language, string repoName, string serviceDirectory, string package, string expectedVersion)
    {
        var service = serviceDirectory.Contains('/') ? serviceDirectory.Split('/')[^1] : serviceDirectory;
        var repoRoot = Path.Combine(_tempRoot.DirectoryPath, repoName);
        var sdkPath = Path.Combine(repoRoot, "sdk", service, package);

        Directory.CreateDirectory(repoRoot);
        if (!Directory.Exists(Path.Combine(repoRoot, ".git")))
        {
            await GitTestHelper.GitInitAsync(repoRoot);
        }
        Directory.CreateDirectory(sdkPath);

        switch (language)
        {
            case SdkLanguage.DotNet:
                SetupDotNetPackage(sdkPath, package, expectedVersion, SdkType.Unknown);
                break;
            case SdkLanguage.Java:
                SetupJavaPackage(sdkPath, package, expectedVersion);
                break;
            case SdkLanguage.Python:
                await SetupPythonPackageAsync(sdkPath, package, expectedVersion);
                break;
            case SdkLanguage.JavaScript:
                SetupJavaScriptPackage(sdkPath, package, expectedVersion, SdkType.Unknown);
                break;
            case SdkLanguage.Go:
                await SetupGoPackageAsync(sdkPath, expectedVersion);
                break;
        }

        var languageService = tool.GetLanguageService(language);
        var info = await languageService.GetPackageInfo(sdkPath);
        Assert.That(info.PackageVersion, Is.EqualTo(expectedVersion));
    }

    [Test]
    [TestCase(SdkLanguage.JavaScript, "azure-sdk-for-js", "storage", "azure-storage-blob", SdkType.Dataplane)]
    [TestCase(SdkLanguage.JavaScript, "azure-sdk-for-js", "storage", "azure-storage-blob", SdkType.Management)]
    public async Task SdkType_IsDerivedCorrectly(SdkLanguage language, string repoName, string serviceDirectory, string package, SdkType sdkType)
    {
        var service = serviceDirectory.Contains('/') ? serviceDirectory.Split('/')[^1] : serviceDirectory;
        var repoRoot = Path.Combine(_tempRoot.DirectoryPath, repoName);
        var sdkPath = Path.Combine(repoRoot, "sdk", service, package);

        Directory.CreateDirectory(repoRoot);
        if (!Directory.Exists(Path.Combine(repoRoot, ".git")))
        {
            await GitTestHelper.GitInitAsync(repoRoot);
        }
        Directory.CreateDirectory(sdkPath);

        SetupJavaScriptPackage(sdkPath, package, "1.2.3", sdkType);

        var languageService = tool.GetLanguageService(language);
        var info = await languageService.GetPackageInfo(sdkPath);
        Assert.That(info.SdkType, Is.EqualTo(sdkType));
    }

    [Test]
    [TestCase(SdkLanguage.DotNet, "azure-sdk-for-net", "missing", "Azure.Data.Empty")]
    [TestCase(SdkLanguage.Java, "azure-sdk-for-java", "missing", "azure-empty")]
    [TestCase(SdkLanguage.Python, "azure-sdk-for-python", "missing", "azure-empty")]
    [TestCase(SdkLanguage.JavaScript, "azure-sdk-for-js", "missing", "azure-empty")]
    [TestCase(SdkLanguage.Go, "azure-sdk-for-go", "security/keyvault", "azempty")]
    public async Task VersionParsing_MissingFile_ReturnsNull(SdkLanguage language, string repoName, string serviceDirectory, string package)
    {
        var service = serviceDirectory.Contains('/') ? serviceDirectory.Split('/')[^1] : serviceDirectory;
        var repoRoot = Path.Combine(_tempRoot.DirectoryPath, repoName);
        var sdkPath = Path.Combine(repoRoot, "sdk", service, package);

        Directory.CreateDirectory(repoRoot);
        if (!Directory.Exists(Path.Combine(repoRoot, ".git")))
        {
            await GitTestHelper.GitInitAsync(repoRoot);
        }
        Directory.CreateDirectory(sdkPath);

        var languageService = tool.GetLanguageService(language);
        var info = await languageService.GetPackageInfo(sdkPath);
        Assert.That(info.PackageVersion, Is.Null);
    }
}
