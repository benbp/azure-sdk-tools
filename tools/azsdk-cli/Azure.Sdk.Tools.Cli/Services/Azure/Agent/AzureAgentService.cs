using Azure.AI.Agents.Persistent;
using Azure.Sdk.Tools.Cli.Helpers;

namespace Azure.Sdk.Tools.Cli.Services;

public interface IAzureAgentService
{
    PersistentAgentsClient GetClient();
    Task DeleteAgents();
    Task<(string, TokenUsageHelper)> QueryFile(Stream contents, string filename, string session, string query);
}

public class AzureAgentService(IAzureService azureService, ILogger<AzureAgentService> logger, string? projectEndpoint, string? model) : IAzureAgentService
{
    private readonly string projectEndpoint = projectEndpoint ?? "https://ai-prmarottai3149546654251245.services.ai.azure.com/api/projects/prmarott-apiview";
    private readonly string model = model ?? "gpt-4o-mini";

    private PersistentAgentsClient client;

    private const string LogQueryPrompt = @"You are an assistant that analyzes Azure Pipelines failure logs.
You will be provided with a log file from an Azure Pipelines build.
Your task is to analyze the log and provide a summary of the failure.
Include relevant data like error type, error messages, functions and error lines.
Find other log lines in addition to the final error that may be descriptive of the problem.
Errors like 'Powershell exited with code 1' are not error messages, but the error message may be in the logs above it.
Provide suggested next steps. Respond only in valid JSON, in the following format:
{
    ""summary"": ""..."",
    ""errors"": [
        { ""file"": ""..."", ""line"": ..., ""message"": ""..."" }
    ],
    ""suggested_fix"": ""...""
}";

    private void Initialize()
    {
        client = new(projectEndpoint, azureService.GetCredential());
    }

    public PersistentAgentsClient GetClient()
    {
        if (client == null)
        {
            Initialize();
        }
        return client;
    }

    public async Task DeleteAgents()
    {
        var client = GetClient();
        AsyncPageable<PersistentAgent> agents = client.Administration.GetAgentsAsync();
        await foreach (var agent in agents)
        {
            var i = 1;
            if (agent.Name.StartsWith("internal") || agent.Name.StartsWith("public"))
            {
                logger.LogInformation("[{i}] Deleting agent {AgentId} ({AgentName})", i++, agent.Id, agent.Name);
                // await client.DeleteAgentAsync(agent.Id);
            }
        }
    }

    public async Task<(string, TokenUsageHelper)> QueryFile(Stream contents, string filename, string session, string query)
    {
        if (string.IsNullOrWhiteSpace(filename) || Path.GetExtension(filename) == string.Empty)
        {
            throw new ArgumentException($"Filename '{filename}' must have a file extension (*.txt, *.md, ...)", nameof(filename));
        }

        var client = GetClient();
        PersistentAgentFileInfo uploaded = await client.Files.UploadFileAsync(contents, PersistentAgentFilePurpose.Agents, filename);
        Dictionary<string, string> fileIds = new() { { uploaded.Id, uploaded.Filename } };
        PersistentAgentsVectorStore vectorStore = await client.VectorStores.CreateVectorStoreAsync(fileIds: [uploaded.Id], name: filename);
        FileSearchToolResource tool = new();
        tool.VectorStoreIds.Add(vectorStore.Id);

        PersistentAgent agent = await client.Administration.CreateAgentAsync(
            model: model,
            name: session,
            instructions: LogQueryPrompt,
            tools: [new FileSearchToolDefinition()],
            toolResources: new ToolResources() { FileSearch = tool });

        PersistentAgentThread thread = await client.Threads.CreateThreadAsync();
        PersistentThreadMessage messageResponse = await client.Messages.CreateMessageAsync(thread.Id, MessageRole.User, query);
        ThreadRun run = await client.Runs.CreateRunAsync(thread, agent);

        do
        {
            Thread.Sleep(TimeSpan.FromMilliseconds(500));
            run = await client.Runs.GetRunAsync(thread.Id, run.Id);
        }
        while (run.Status == RunStatus.Queued || run.Status == RunStatus.InProgress);

        if (run.Status != RunStatus.Completed)
        {
            throw new Exception("Run did not complete successfully, error: " + run.LastError?.Message);
        }

        AsyncPageable<PersistentThreadMessage> messages = client.Messages.GetMessagesAsync(
            threadId: thread.Id,
            order: ListSortOrder.Ascending
        );

        var response = new List<string>();

        await foreach (var threadMessage in messages)
        {
            foreach (MessageContent contentItem in threadMessage.ContentItems)
            {
                if (contentItem is MessageTextContent textItem)
                {
                    if (textItem.Text != query)
                    {
                        response.Add(textItem.Text);
                    }
                }
                else
                {
                    throw new NotImplementedException($"Content type of {contentItem.GetType()} is not supported yet.");
                }
            }
        }

        // NOTE: in the future we will want to keep these around if the user wants to keep querying the file in a session
        logger.LogDebug("Deleting temporary resources: agent {AgentId}, vector store {VectorStoreId}, file {FileId}",
            agent.Id, vectorStore.Id, uploaded.Id);

        await client.VectorStores.DeleteVectorStoreAsync(vectorStore.Id);
        await client.Files.DeleteFileAsync(uploaded.Id);
        await client.Threads.DeleteThreadAsync(thread.Id);
        await client.Administration.DeleteAgentAsync(agent.Id);

        var tokenUsage = new TokenUsageHelper(model, run.Usage.PromptTokens, run.Usage.CompletionTokens);
        return (string.Join("\n", response), tokenUsage);
    }
}
