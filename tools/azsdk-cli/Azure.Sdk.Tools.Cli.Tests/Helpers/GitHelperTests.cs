using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Microsoft.Extensions.Logging;
using Moq;
using System.Threading;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.Cli.Tests.Helpers
{
    [TestFixture]
    internal class GitHelperTests
    {
        private GitHelper gitHelper;
        private TestLogger<GitHelper> logger;
        private Mock<IGitHubService> mockGitHubService;
        private ProcessHelper processHelper;
        private string testRepoPath = string.Empty;

        [SetUp]
        public void Setup()
        {
            mockGitHubService = new Mock<IGitHubService>();
            logger = new TestLogger<GitHelper>();
            var outputHelper = new OutputHelper(OutputHelper.OutputModes.Hidden);
            processHelper = new ProcessHelper(new TestLogger<ProcessHelper>(), outputHelper);
            gitHelper = new GitHelper(mockGitHubService.Object, logger, processHelper);
        }

        [TearDown]
        public void Teardown()
        {
            CleanupTestRepo(testRepoPath);
            testRepoPath = string.Empty;
        }

        [Test]
        public async Task DiscoverRepoRoot_WithValidGitRepo_ReturnsRepoRoot()
        {
            testRepoPath = await CreateTestRepo();
            var subDir = Path.Combine(testRepoPath, "subdirectory");
            Directory.CreateDirectory(subDir);

            var result = gitHelper.DiscoverRepoRoot(subDir, CancellationToken.None);
            Assert.That(result, Is.EqualTo(testRepoPath));
        }

        [Test]
        public async Task DiscoverRepoRoot_WithPathAsFile_ReturnsRepoRoot()
        {
            testRepoPath = await CreateTestRepo();
            var subDir = Path.Combine(testRepoPath, "subdirectory");
            Directory.CreateDirectory(subDir);
            var testFilePath = Path.Combine(subDir, "testfile.txt");
            await File.WriteAllTextAsync(testFilePath, "Test content");

            var result = gitHelper.DiscoverRepoRoot(testFilePath, CancellationToken.None);
            Assert.That(result, Is.EqualTo(testRepoPath));
        }

        [Test]
        public void DiscoverRepoRoot_WithNoGitRepo_ThrowsException()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);

            try
            {
                Assert.Throws<InvalidOperationException>(() => gitHelper.DiscoverRepoRoot(tempDir, CancellationToken.None));
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [Test]
        public async Task GetRepoRemoteUri_WithSshOrigin_ReturnsHttpsUri()
        {
            testRepoPath = await CreateTestRepo("git@github.com:Azure/azure-rest-api-specs.git");
            var result = await gitHelper.GetRepoRemoteUri(testRepoPath, CancellationToken.None);
            Assert.That(result.ToString(), Is.EqualTo("https://github.com/Azure/azure-rest-api-specs.git"));
        }

        [Test]
        public async Task GetRepoRemoteUri_WithHttpsOrigin_ReturnsHttpsUri()
        {
            testRepoPath = await CreateTestRepo("https://github.com/Azure/azure-rest-api-specs.git");
            var result = await gitHelper.GetRepoRemoteUri(testRepoPath, CancellationToken.None);
            Assert.That(result.ToString(), Is.EqualTo("https://github.com/Azure/azure-rest-api-specs.git"));
        }

        [Test]
        public async Task GetRepoRemoteUri_WithNoOrigin_ThrowsException()
        {
            testRepoPath = await CreateTestRepo();
            var ex = Assert.ThrowsAsync<InvalidOperationException>(async () => await gitHelper.GetRepoRemoteUri(testRepoPath, CancellationToken.None));
            Assert.That(ex.Message, Does.Contain("Failed to get remote origin URL"));
        }

        [Test]
        public void GetRepoRemoteUri_WithNonGitDirectory_ThrowsException()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);

            try
            {
                Assert.ThrowsAsync<InvalidOperationException>(async () => await gitHelper.GetRepoRemoteUri(tempDir, CancellationToken.None));
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [Test]
        public async Task GetRepoFullNameAsync_WithSubdirectoryPath_ReturnsCorrectFullName()
        {
            testRepoPath = await CreateTestRepo("git@github.com:Azure/azure-rest-api-specs.git");
            var subDir = Path.Combine(testRepoPath, "subdirectory");
            Directory.CreateDirectory(subDir);
            mockGitHubService.Setup(x => x.GetGitHubParentRepoUrlAsync("Azure", "azure-rest-api-specs"))
                           .ReturnsAsync(string.Empty); // Not a fork

            var result = await gitHelper.GetRepoFullName(subDir, false, CancellationToken.None);
            Assert.That(result, Is.EqualTo("Azure/azure-rest-api-specs"));
            CleanupTestRepo(testRepoPath);
        }

        [Test]
        public async Task GetRepoFullNameAsync_WithForkRepoButDontFindUpstream_ReturnsDirectFullName()
        {
            testRepoPath = await CreateTestRepo("https://github.com/UserFork/azure-rest-api-specs.git");
            var result = await gitHelper.GetRepoFullName(testRepoPath, false, CancellationToken.None);
            Assert.That(result, Is.EqualTo("UserFork/azure-rest-api-specs"));
        }

        [Test]
        public async Task GetRepoFullNameAsync_WithEmptyPath_ThrowsArgumentException()
        {
            // Test empty string
            try
            {
                await gitHelper.GetRepoFullName("", false, CancellationToken.None);
                Assert.Fail("Expected ArgumentException was not thrown");
            }
            catch (ArgumentException ex)
            {
                Assert.That(ex.ParamName, Is.EqualTo("pathInRepo"));
            }

            // Test null
            try
            {
                await gitHelper.GetRepoFullName(null!, false, CancellationToken.None);
                Assert.Fail("Expected ArgumentException was not thrown");
            }
            catch (ArgumentException ex)
            {
                Assert.That(ex.ParamName, Is.EqualTo("pathInRepo"));
            }
        }

        [Test]
        public void GetRepoFullNameAsync_WithNonGitDirectory_ThrowsException()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);

            try
            {
                Assert.ThrowsAsync<InvalidOperationException>(async () => await gitHelper.GetRepoFullName(tempDir, false, CancellationToken.None));
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        #region Helper Methods

        private async Task<string> CreateTestRepo(string? repositoryUrl = null)
        {
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);

            var initResult = await processHelper.Run(new("git", ["init", tempDir]), CancellationToken.None);
            if (initResult.ExitCode != 0)
            {
                Assert.Fail($"Failed to initialize git repository in {tempDir}: {initResult.Output}");
            }

            if (!string.IsNullOrEmpty(repositoryUrl))
            {
                var remoteResult = await processHelper.Run(new("git", ["remote", "add", "origin", repositoryUrl], workingDirectory: tempDir), CancellationToken.None);
                if (remoteResult.ExitCode != 0)
                {
                    Assert.Fail($"Failed to initialize git repository in {tempDir}: {remoteResult.Output}");
                }
            }

            return tempDir;
        }

        private void CleanupTestRepo(string path)
        {
            if (Directory.Exists(path))
            {
                try
                {
                    // Remove read-only attributes from .git folder
                    var gitDir = Path.Combine(path, ".git");
                    if (Directory.Exists(gitDir))
                    {
                        foreach (var file in Directory.GetFiles(gitDir, "*", SearchOption.AllDirectories))
                        {
                            File.SetAttributes(file, FileAttributes.Normal);
                        }
                    }

                    Directory.Delete(path, true);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to cleanup test repository");
                }
            }
        }

        #endregion
    }
}
