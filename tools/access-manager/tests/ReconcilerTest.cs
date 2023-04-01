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
        var app = new Application
        {
            DisplayName = "test-reconcile-with-empty-federated-identity-credentials",
            AppId = "00000000-0000-0000-0000-000000000000",
            Id = "00000000-0000-0000-0000-000000000000",
        };

        GraphClientMock.Setup(c => c.ListFederatedIdentityCredentials(It.IsAny<Application>()).Result).Returns(new List<FederatedIdentityCredential>());

        var credentials = await reconciler.ReconcileFederatedIdentityCredentials(app, BaseAccessConfig.ApplicationAccessConfigs.First());

        credentials.Count.Should().Be(1);
        credentials.First().Should().Be(BaseAccessConfig.ApplicationAccessConfigs.First().FederatedIdentityCredentials.First());
    }
}
