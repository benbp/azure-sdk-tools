// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.CommandLine.Invocation;
using System.ComponentModel;
using System.Xml;
using Azure.Sdk.Tools.Cli.Contract;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;
using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Cli.Tools;

[McpServerToolType, Description("Fetches data from an Azure Pipelines run.")]
public class TestAnalysisTool : MCPTool
{
    private readonly IOutputService output;
    private readonly ILogger<PipelineAnalysisTool> logger;

    // Options
    private readonly Option<string> trxPathOpt = new(["--trx-file"], "Path to the TRX file for failed test runs") { IsRequired = true };
    private readonly Option<string> filterOpt = new(["--filter-title"], "Test case title to filter results");
    private readonly Option<bool> titlesOpt = new(["--titles"], "Only return test case titles, not full details");

    public TestAnalysisTool(
        IAzureService azureService,
        IAzureAgentServiceFactory azureAgentServiceFactory,
        ILogAnalysisHelper logAnalysisHelper,
        IOutputService output,
        ILogger<PipelineAnalysisTool> logger
    ) : base()
    {
        this.output = output;
        this.logger = logger;
    }

    public override Command GetCommand()
    {
        var analyzeTestCommand = new Command("test", "Analyze test results") {
            trxPathOpt, filterOpt, titlesOpt
        };
        analyzeTestCommand.SetHandler(async ctx => { await HandleCommand(ctx, ctx.GetCancellationToken()); });
        return analyzeTestCommand;
    }

    public override async Task HandleCommand(InvocationContext ctx, CancellationToken ct)
    {
        var cmd = ctx.ParseResult.CommandResult.Command.Name;
        var trxPath = ctx.ParseResult.GetValueForOption(trxPathOpt);
        var filterTitle = ctx.ParseResult.GetValueForOption(filterOpt);
        var titlesOnly = ctx.ParseResult.GetValueForOption(titlesOpt);

        if (titlesOnly)
        {
            var testTitles = await GetFailedTestCases(trxPath);
            ctx.ExitCode = ExitCode;
            output.Output(testTitles);
            return;
        }

        if (!string.IsNullOrEmpty(filterTitle))
        {
            var testCase = await GetFailedTestCase(trxPath, filterTitle);
            ctx.ExitCode = ExitCode;
            output.Output(testCase);
            return;
        }

        var testResult = await GetFailedTestRunDataFromTrx(trxPath);
        ctx.ExitCode = ExitCode;
        output.Output(testResult);
        return;
    }

    [McpServerTool, Description("Get details for a failed test from a TRX file")]
    public async Task<List<string>> GetFailedTestCases(string trxFilePath)
    {
        try
        {
            var failedTestRuns = await GetFailedTestRunDataFromTrx(trxFilePath);
            if (failedTestRuns.Count == 0)
            {
                return [];
            }
            return failedTestRuns.Select(run => run.TestCaseTitle).ToList();
        }
        catch (Exception ex)
        {
            logger.LogError("Failed to process TRX file {trxFilePath}: {exception}", trxFilePath, ex.Message);
            logger.LogError("Stack Trace: {stackTrace}", ex.StackTrace);
            SetFailure();
            return [];
        }
    }

    [McpServerTool, Description("Get details for a failed test from a TRX file")]
    public async Task<FailedTestRunResponse> GetFailedTestCase(string trxFilePath, string testCaseTitle)
    {
        try
        {
            var failedTestRuns = await GetFailedTestRunDataFromTrx(trxFilePath);
            var testRun = failedTestRuns.FirstOrDefault(run => run.TestCaseTitle.Equals(testCaseTitle, StringComparison.OrdinalIgnoreCase));
            if (testRun == null)
            {
                return new FailedTestRunResponse
                {
                    ResponseError = $"No failed test run found for test case title: {testCaseTitle}"
                };
            }
            return testRun;
        }
        catch (Exception ex)
        {
            logger.LogError("Failed to process TRX file {trxFilePath}: {exception}", trxFilePath, ex.Message);
            logger.LogError("Stack Trace: {stackTrace}", ex.StackTrace);
            SetFailure();
            return new FailedTestRunResponse
            {
                ResponseError = $"Failed to process TRX file {trxFilePath}: {ex.Message}"
            };
        }
    }

    [McpServerTool, Description("Get failed test run data from a TRX file")]
    public async Task<List<FailedTestRunResponse>> GetFailedTestRunDataFromTrx(string trxFilePath)
    {
        try
        {
            var failedTestRuns = new List<FailedTestRunResponse>();
            if (!File.Exists(trxFilePath))
            {
                logger.LogError("TRX file not found: {trxFilePath}", trxFilePath);
                return failedTestRuns;
            }

            var xmlContent = await File.ReadAllTextAsync(trxFilePath);
            var doc = new XmlDocument();
            doc.LoadXml(xmlContent);
            var unitTestResults = doc.GetElementsByTagName("UnitTestResult");
            foreach (XmlNode resultNode in unitTestResults)
            {
                var outcome = resultNode.Attributes?["outcome"]?.Value ?? "";
                if (!string.Equals(outcome, "Failed", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var testId = resultNode.Attributes?["testId"]?.Value ?? "";
                var testName = resultNode.Attributes?["testName"]?.Value ?? "";
                var errorMessage = "";
                var stackTrace = "";

                var outputNode = resultNode.ChildNodes.OfType<XmlNode>().FirstOrDefault(n => n.Name == "Output");
                if (outputNode != null)
                {
                    var errorInfoNode = outputNode.ChildNodes.OfType<XmlNode>().FirstOrDefault(n => n.Name == "ErrorInfo");
                    if (errorInfoNode != null)
                    {
                        var messageNode = errorInfoNode.ChildNodes.OfType<XmlNode>().FirstOrDefault(n => n.Name == "Message");
                        if (messageNode != null)
                        {
                            errorMessage = messageNode.InnerText ?? "";
                        }

                        var stackTraceNode = errorInfoNode.ChildNodes.OfType<XmlNode>().FirstOrDefault(n => n.Name == "StackTrace");
                        if (stackTraceNode != null)
                        {
                            stackTrace = stackTraceNode.InnerText ?? "";
                        }
                    }
                }

                failedTestRuns.Add(new FailedTestRunResponse
                {
                    TestCaseTitle = testName,
                    ErrorMessage = errorMessage,
                    StackTrace = stackTrace,
                    Outcome = outcome,
                    Uri = $"file://{trxFilePath}"
                });
            }

            return failedTestRuns;
        }
        catch (Exception ex)
        {
            logger.LogError("Failed to process TRX file {trxFilePath}: {exception}", trxFilePath, ex.Message);
            logger.LogError("Stack Trace: {stackTrace}", ex.StackTrace);
            SetFailure();
            return
            [
                new FailedTestRunResponse
                {
                    ResponseError = $"Failed to process TRX file {trxFilePath}: {ex.Message}"
                }
            ];
        }
    }
}
