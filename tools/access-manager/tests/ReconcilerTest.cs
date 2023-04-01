namespace Azure.Sdk.Tools.AccessManager.Tests;

using NSubstitute;
using NUnit.Framework;
using Azure.Sdk.Tools.AccessManager;
using Microsoft.Graph;

public class ReconcilerTest
{
    GraphServiceClient GraphClient { get; set; }

    [SetUp]
    public void Setup()
    {
        GraphClient = Substitute.For<GraphServiceClient>();
    }

    [BeforeEach]
    public void BeforeEach()
    {
    }

    [Test]
    public void TestReconcile()
    {
        var reconciler = new Reconciler(GraphClient);
        reconciler.Reconcile();
    }
}
