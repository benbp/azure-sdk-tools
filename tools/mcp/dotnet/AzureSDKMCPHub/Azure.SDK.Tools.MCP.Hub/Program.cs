using System.Text;
using Azure.SDK.Tools.MCP.Hub.Services.Azure;
using Azure.SDK.Tools.MCP.Hub.Tools.AzurePipelinesTool;

namespace Azure.SDK.Tools.MCP.Hub;

public sealed class Program
{
    public static void Main(string[] args)
    {
        // var task = TestAzp();
        var task = TestAI();
        task.Wait();
        return;
        // todo: parse a command bundle here. pass it to CreateHostBuilder once we have an actual type
        // todo: can we have a "start" verb and a <tool> verb? EG if someone calls <server.exe> HelloWorld
        //   "This is a hello world input" -> we invoke just that tool
        //   "<server.exe> start" -> runs server responding to vscode copilot chat
        var host = CreateAppBuilder(args).Build();
        // For testing SSE can be easier to use. Comment above and uncomment below. Eventually this will be
        // behind a command line flag or we could try to run in both modes at once if possible.
        host.MapMcp();
        host.Run();
    }

    public static WebApplicationBuilder CreateAppBuilder(string[] args)
    {
        // todo: implement our own module discovery that takes the `--tools` or `--tools-exclude` when booting
        var builder = WebApplication.CreateBuilder(args);
        builder.Logging.AddConsole(consoleLogOptions =>
        {
            consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Error;
        });

        builder.Services.AddSingleton<IAzureService, AzureService>();

        builder.Services
            .AddMcpServer()
            //.WithStdioServerTransport()
            // For testing SSE can be easier to use. Comment above and uncomment below. Eventually this will be
            // behind a command line flag or we could try to run in both modes at once if possible.
            .WithHttpTransport()
            // todo: we can definitely honor the --tools param here to filter down the provided tools
            // for now, lets just use WithtoolsFromAssembly to actually run this thing
            .WithToolsFromAssembly();

        return builder;
    }

    public static async Task TestAzp()
    {
        var azureService = new AzureService();
        var aiAgentService = new AIAgentService(azureService);
        var azpTool = new AzurePipelinesTool(azureService, aiAgentService)
        {
            project = "public"
        };

        Console.WriteLine("Testing Azure Pipelines Tool...");
        var output = await azpTool.AnalyzePipelineFailureLog(4815910, 174); Console.WriteLine(output);
    }

    public static async Task TestAI()
    {
        var azureService = new AzureService();
        var ai = new AIAgentService(azureService);

        Console.WriteLine("Testing AI Agents Service...");
        // var filename = "public-4817839-187.txt";
        // var filename = "internal-4815544-2642.txt";

        Console.WriteLine("Testing golang ci lint 4817839...");
        var filename1 = "public-4817839-187.txt";
        var contents1 = await File.ReadAllTextAsync(filename1);
        await ai.UploadFileAsync(new MemoryStream(Encoding.UTF8.GetBytes(contents1)), filename1);
        var (response1, usage1) = await ai.QueryFileAsync(filename1, "Why did this pipeline fail?");
        Console.WriteLine(response1);
        usage1.LogCost();

        Console.WriteLine("Testing cpp core 4820258...");
        var filename2 = "internal-4820258-2224.txt";
        var contents2 = await File.ReadAllTextAsync(filename2);
        await ai.UploadFileAsync(new MemoryStream(Encoding.UTF8.GetBytes(contents2)), filename2);
        var (response2, usage2) = await ai.QueryFileAsync(filename2, "Why did this pipeline fail?");
        Console.WriteLine(response2);
        usage2.LogCost();

        (usage1 + usage2).LogCost();
    }
}