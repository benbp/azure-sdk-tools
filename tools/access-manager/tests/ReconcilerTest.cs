namespace Azure.Sdk.Tools.AccessManager.Tests;

using Microsoft.Graph;
using Microsoft.Graph.Models;

public class ReconcilerTest
{
    ApplicationCollectionResponse AppResult { get; set; }
    AccessConfig BaseAccessConfig { get; set; }
    GraphServiceClient GraphClient { get; set; }

    [OneTimeSetUp]
    public void Before()
    {
        BaseAccessConfig = AccessConfig.Create("./test-configs/config-gh-actions.json");
    }

    [SetUp]
    public void BeforeEach()
    {
        GraphClient = Substitute.For<GraphServiceClient>();
    }

    [Test]
    public async Task TestReconcileWithExistingApp()
    {
        var reconciler = new Reconciler(GraphClient);
        var appResult = new ApplicationCollectionResponse
        {
            Value = new List<Application>
            {
                new Application
                {
                    DisplayName = "test-reconcile-with-existing-app",
                    AppId = "00000000-0000-0000-0000-000000000000",
                    Id = "00000000-0000-0000-0000-000000000000",
                }
            }
        };

        GraphClient.Applications.GetAsync().ReturnsForAnyArgs(appResult);
        GraphClient.Applications.PostAsync(Arg.Any<Application>())
            .ReturnsForAnyArgs<Task>(_ => throw new Exception("App should not be created"));

        var app = await reconciler.ReconcileApplication(BaseAccessConfig.ApplicationAccessConfigs.First());
        app.AppId.Should().Be(appResult.Value.First().AppId);
        app.Id.Should().Be(appResult.Value.First().Id);
    }

    [Test]
    public async Task TestReconcileWithNewApp()
    {
        var reconciler = new Reconciler(GraphClient);
        var appResult = new ApplicationCollectionResponse();
        var newApp = new Application
        {
            DisplayName = BaseAccessConfig.ApplicationAccessConfigs.First().AppDisplayName,
            AppId = "00000000-0000-0000-0000-000000000000",
            Id = "00000000-0000-0000-0000-000000000000",
        };

        GraphClient.Applications.GetAsync().ReturnsForAnyArgs(appResult);
        GraphClient.Applications.PostAsync(Arg.Any<Application>())
            .ReturnsForAnyArgs<Task>(_ => Task.FromResult<Application>(newApp));

        var app = await reconciler.ReconcileApplication(BaseAccessConfig.ApplicationAccessConfigs.First());
        app.AppId.Should().Be(newApp.AppId);
        app.Id.Should().Be(newApp.Id);
    }
}
