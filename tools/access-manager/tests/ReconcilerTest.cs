namespace Azure.Sdk.Tools.AccessManager.Tests;

using Microsoft.Graph;
using Microsoft.Graph.Models;

public class ReconcilerTest
{
    ApplicationCollectionResponse AppResult { get; set; }
    AccessConfig BaseAccessConfig { get; set; }
    Mock<IGraphClient> GraphClientMock { get; set; }

    [OneTimeSetUp]
    public void Before()
    {
        BaseAccessConfig = AccessConfig.Create("./test-configs/config-gh-actions.json");
    }

    [SetUp]
    public void BeforeEach()
    {
        GraphClientMock = new Mock<IGraphClient>();
    }

    [Test]
    public async Task TestReconcileWithExistingApp()
    {
        var reconciler = new Reconciler(GraphClientMock.Object);
        var application = new Application
        {
            DisplayName = "test-reconcile-with-existing-app",
            AppId = "00000000-0000-0000-0000-000000000000",
            Id = "00000000-0000-0000-0000-000000000000",
        };

        GraphClientMock.Setup(c => c.GetApplicationByDisplayName(It.IsAny<string>()).Result).Returns(application);
        GraphClientMock.Setup(c => c.CreateApplication(It.IsAny<Application>())).Throws(new Exception("App should not be created"));

        var app = await reconciler.ReconcileApplication(BaseAccessConfig.ApplicationAccessConfigs.First());
        app.DisplayName.Should().Be(application.DisplayName);
        app.AppId.Should().Be(application.AppId);
        app.Id.Should().Be(application.Id);
    }

    [Test]
    public async Task TestReconcileWithNewApp()
    {
        var reconciler = new Reconciler(GraphClientMock.Object);
        var newApp = new Application
        {
            DisplayName = BaseAccessConfig.ApplicationAccessConfigs.First().AppDisplayName,
            AppId = "00000000-0000-0000-0000-000000000000",
            Id = "00000000-0000-0000-0000-000000000000",
        };

        GraphClientMock.Setup(c => c.GetApplicationByDisplayName(It.IsAny<string>()).Result).Returns<Application>(null);
        GraphClientMock.Setup(c => c.CreateApplication(It.IsAny<Application>()).Result).Returns(newApp);

        var app = await reconciler.ReconcileApplication(BaseAccessConfig.ApplicationAccessConfigs.First());
        app.AppId.Should().Be(newApp.AppId);
        app.Id.Should().Be(newApp.Id);
    }

    [Test]
    public async Task TestReconcileWithEmptyFederatedIdentityCredentials()
    {
        var reconciler = new Reconciler(GraphClientMock.Object);
        var configApp = BaseAccessConfig.ApplicationAccessConfigs.First();
        var app = new Application
        {
            DisplayName = configApp.AppDisplayName,
            AppId = "00000000-0000-0000-0000-000000000000",
            Id = "00000000-0000-0000-0000-000000000000",
        };

        GraphClientMock.Setup(c => c.ListFederatedIdentityCredentials(It.IsAny<Application>()).Result).Returns(new List<FederatedIdentityCredential>());

        await reconciler.ReconcileFederatedIdentityCredentials(app, configApp);

        GraphClientMock.Verify(c => c.DeleteFederatedIdentityCredential(It.IsAny<Application>(), It.IsAny<FederatedIdentityCredential>()), Times.Never);
        GraphClientMock.Verify(
            c => c.CreateFederatedIdentityCredential(
                It.Is<Application>(a => a.DisplayName == configApp.AppDisplayName),
                It.IsAny<FederatedIdentityCredential>()), Times.Exactly(2));
    }

    [Test]
    public async Task TestReconcileMergingFederatedIdentityCredentials()
    {
        var reconciler = new Reconciler(GraphClientMock.Object);
        var configApp = BaseAccessConfig.ApplicationAccessConfigs.First();
        var app = new Application
        {
            DisplayName = configApp.AppDisplayName,
            AppId = "00000000-0000-0000-0000-000000000000",
            Id = "00000000-0000-0000-0000-000000000000",
        };

        var existingCredentials = new List<FederatedIdentityCredential>
        {
            configApp.FederatedIdentityCredentials.First(),
            new FederatedIdentityCredential(),
            new FederatedIdentityCredential
            {
                Audiences = new List<string> { "api://azureadtokenexchange" },
                Description = "Test PreExisting To Replace",
                Issuer = "https://token.actions.githubusercontent.com",
                Name = "test-pre-existing-replace-1",
                Subject = "repo:accessmanagertest/azure-sdk-tools:ref:refs/heads/main"
            },
        };

        GraphClientMock.Setup(c => c.ListFederatedIdentityCredentials(It.IsAny<Application>()).Result).Returns(existingCredentials);

        await reconciler.ReconcileFederatedIdentityCredentials(app, configApp);

        // Delete two, keep one, create one
        GraphClientMock.Verify(c => c.DeleteFederatedIdentityCredential(
            It.IsAny<Application>(), It.IsAny<FederatedIdentityCredential>()), Times.Exactly(2));
        GraphClientMock.Verify(c => c.CreateFederatedIdentityCredential(
            It.Is<Application>(a => a.DisplayName == configApp.AppDisplayName),
            It.IsAny<FederatedIdentityCredential>()), Times.Once);
    }

    [Test]
    public async Task TestReconcileRoleBasedAccessControl()
    {

    }
}
