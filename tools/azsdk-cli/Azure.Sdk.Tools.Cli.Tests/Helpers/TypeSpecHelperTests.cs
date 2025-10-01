using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Moq;
using System.Threading;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.Cli.Tests.Helpers
{
    internal class TypeSpecHelperTests
    {
        private ITypeSpecHelper typeSpecHelper;
        private IGitHelper gitHelper;
        private Mock<IGitHubService> gitHubService;
        private Mock<IProcessHelper> processHelper;


        [SetUp]
        public void setup()
        {
            var logger = new TestLogger<GitHelper>();
            gitHubService = new Mock<IGitHubService>();
            processHelper = new Mock<IProcessHelper>();
            gitHelper = new GitHelper(gitHubService.Object, logger, processHelper.Object);
            typeSpecHelper = new TypeSpecHelper(gitHelper);
        }

        [Test]
        public void Verify_IsValidTypeSpecProject()
        {
            var testCodeFilePath = "TypeSpecTestData/specification/testcontoso/Contoso.Management";
            var result = typeSpecHelper.IsValidTypeSpecProjectPath(testCodeFilePath);
            Assert.That(result, Is.True);
            testCodeFilePath = "TypeSpecTestData/specification/testcontoso";
            result = typeSpecHelper.IsValidTypeSpecProjectPath(testCodeFilePath);
            Assert.That(result, Is.False);
        }

        [Test]
        public void Verify_IsTypeSpecProjectForMgmtPlane()
        {
            var testCodeFilePath = "TypeSpecTestData/specification/testcontoso/Contoso.Management";
            var result = typeSpecHelper.IsTypeSpecProjectForMgmtPlane(testCodeFilePath);
            Assert.That(result, Is.True);
        }

        [Test]
        public void Test_GetSpecRepoPath()
        {
            var testCodeFilePath = "TypeSpecTestData/specification/testcontoso/Contoso.Management";
            var result = typeSpecHelper.GetSpecRepoRootPath(testCodeFilePath);
            Assert.That(result.EndsWith("TypeSpecTestData"), Is.True);
        }

        [TestCase("https://github.com/Azure/azure-rest-api-specs.git")]
        [TestCase("https://github.com/Azure/azure-rest-api-specs")]
        [TestCase("https://github.com/myuser/azure-rest-api-specs.git")]
        [TestCase("https://github.com/Azure/azure-rest-api-specs-pr.git")]
        [TestCase("https://github.com/myuser/azure-rest-api-specs-pr.git")]
        [TestCase("git@github.com:Azure/azure-rest-api-specs.git")]
        [TestCase("git@github.com:myuser/azure-rest-api-specs.git")]
        [Test]
        public async Task Test_IsRepoPathForSpecRepo(Uri repo)
        {
            var gitHelper = CreateGitHelper(repo);
            var helper = new TypeSpecHelper(gitHelper);
            var result = await helper.IsRepoPathForSpecRepo("unused because of mock", CancellationToken.None);
            Assert.That(result, Is.True, "is a specs repo (public or private)");
        }

        [TestCase("https://github.com/Azure/azure-rest-api-specs-pr.git")]
        [TestCase("https://github.com/myuser/azure-rest-api-specs-pr.git")]
        [TestCase("git@github.com:Azure/azure-rest-api-specs-pr.git")]
        [TestCase("git@github.com:myuser/azure-rest-api-specs-pr.git")]
        [TestCase("git@github.com:Azure/azure-sdk-for-php.git")]
        [Test]
        public async Task Test_IsRepoPathForPublicSpecRepo(Uri repo)
        {
            var helper = new TypeSpecHelper(CreateGitHelper(repo));
            var result = await helper.IsRepoPathForPublicSpecRepo("unused because of the mock", CancellationToken.None);
            Assert.That(result, Is.False, "not the public specs repo");
        }

        private static IGitHelper CreateGitHelper(Uri getRepoRemoteUri)
        {
            var gitHelperMock = new Mock<IGitHelper>();
            gitHelperMock
                .Setup(ghm => ghm.GetRepoRemoteUri(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(getRepoRemoteUri);
            gitHelperMock
                .Setup(ghm => ghm.DiscoverRepoRoot(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns((string path, CancellationToken _) => path);
            return gitHelperMock.Object;
        }
    }
}
