using System.CommandLine;
using Moq;
using Azure.Sdk.Tools.Cli.Telemetry;
using Azure.Sdk.Tools.Cli.Tests.Mocks.Helpers;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.Cli.Tools.Example;

namespace Azure.Sdk.Tools.Cli.Tests.Tools;

internal class HelloWorldToolTests
{
    [Test]
    public async Task TestHelloWorldCLIOptions()
    {
        TestOutputHelper outputHelper = new();
        var tool = new HelloWorldTool(new TestLogger<HelloWorldTool>());
        tool.Initialize(outputHelper, new Mock<ITelemetryService>().Object);
        var cmd = tool.GetCommandInstances().First();

        var exitCode = await cmd.InvokeAsync(["hello-world", "HI. MY NAME IS"]);
        Assert.That(exitCode, Is.EqualTo(0));

        var expected = @"
Message: RESPONDING TO 'HI. MY NAME IS' with SUCCESS: 0
Duration: 1ms
".TrimStart();

        Assert.That(outputHelper.Outputs.Count(), Is.EqualTo(1));
        Assert.That(outputHelper.Outputs.First().Method, Is.EqualTo(nameof(outputHelper.Output)));
        Assert.That(outputHelper.Outputs.First().OutputValue, Is.EqualTo(expected));
    }

    [Test]
    public async Task TestHelloWorldCLIOptionsFail()
    {
        TestOutputHelper outputHelper = new();
        var tool = new HelloWorldTool(new TestLogger<HelloWorldTool>());
        tool.Initialize(outputHelper, new Mock<ITelemetryService>().Object);
        var cmd = tool.GetCommandInstances().First();

        var exitCode = await cmd.InvokeAsync(["hello-world", "HI. MY NAME IS", "--fail"]);
        Assert.That(exitCode, Is.EqualTo(1));

        var expected = "[ERROR] RESPONDING TO 'HI. MY NAME IS' with FAIL: 1";

        Assert.That(outputHelper.Outputs.Count(), Is.EqualTo(1));
        Assert.That(outputHelper.Outputs.First().Method, Is.EqualTo(nameof(outputHelper.OutputError)));
        Assert.That(outputHelper.Outputs.First().OutputValue, Is.EqualTo(expected));
    }
}
