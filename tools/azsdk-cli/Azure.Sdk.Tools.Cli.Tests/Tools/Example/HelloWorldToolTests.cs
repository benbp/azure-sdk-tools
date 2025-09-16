using System.CommandLine;
using Moq;
using Azure.Sdk.Tools.Cli.Tools.Example;

namespace Azure.Sdk.Tools.Cli.Tests.Tools;

internal class HelloWorldToolTests
{
    [Test]
    public async Task TestHelloWorldCLIOptions()
    {
        var (commands, logger) = GetTestInstanceWithLogger<HelloWorldTool>();

        var output = "";
        outputHelperMock
            .Setup(s => s.Output(It.IsAny<string>()))
            .Callback<string>(s => output = s);

        var exitCode = await cmd.InvokeAsync(["hello-world", "HI. MY NAME IS"]);
        Assert.That(exitCode, Is.EqualTo(0));

        var expected = @"
Message: RESPONDING TO 'HI. MY NAME IS' with SUCCESS: 0
Duration: 1ms
".TrimStart();

        outputHelperMock
            .Verify(s => s.Output(It.IsAny<string>()), Times.Once);

        var input = output.Replace("\r", "");

        Assert.That(output, Is.EqualTo(expected));
    }

    [Test]
    public async Task TestHelloWorldCLIOptionsFail()
    {
        var (cmd, logger) = GetTestInstanceWithLogger<HelloWorldTool>();

        var output = "";
        outputHelperMock
            .Setup(s => s.Output(It.IsAny<string>()))
            .Callback<string>(s => output = s);

        var exitCode = await cmd.InvokeAsync(["hello-world", "HI. MY NAME IS", "--fail"]);
        Assert.That(exitCode, Is.EqualTo(1));

        var expected = "[ERROR] RESPONDING TO 'HI. MY NAME IS' with FAIL: 1";

        outputHelperMock
            .Verify(s => s.Output(It.IsAny<string>()), Times.Once);

        var input = output.Replace("\r", "");

        Assert.That(output, Is.EqualTo(expected));
    }
}
