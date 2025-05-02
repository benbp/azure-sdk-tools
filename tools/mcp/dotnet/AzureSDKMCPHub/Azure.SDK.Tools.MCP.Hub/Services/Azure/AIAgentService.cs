using Azure.AI.Projects;
using OpenAI.VectorStores;

namespace Azure.SDK.Tools.MCP.Hub.Services.Azure;

public interface IAIAgentService
{
    AgentsClient GetClient();
    Task UploadFileAsync(Stream contents, string filename);
    Task<(string, TokenUsage)> QueryFileAsync(string filename, string query);
}

public class TokenUsage
{
    public long PromptTokens { get; set; }
    public long CompletionTokens { get; set; }
    public long TotalTokens { get; set; }
}

public class AIAgentService : IAIAgentService
{
    private readonly string vectorStoreName;
    private string vectorStoreId;
    private readonly AgentsClient client;
    private readonly string agentId;

    public AIAgentService(IAzureService azureService)
    {
        var connectionString = System.Environment.GetEnvironmentVariable("AZURE_AI_PROJECT_CONNECTION_STRING");
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException("AZURE_AI_PROJECT_CONNECTION_STRING environment variable is not set.");
        }
        var agentId = System.Environment.GetEnvironmentVariable("AZURE_AI_AGENT_ID");
        if (string.IsNullOrEmpty(agentId))
        {
            throw new InvalidOperationException("AZURE_AI_AGENT_ID environment variable is not set.");
        }
        this.agentId = agentId;
        var modelDeploymentName = System.Environment.GetEnvironmentVariable("AZURE_AI_MODEL_DEPLOYMENT_NAME");
        if (string.IsNullOrEmpty(modelDeploymentName))
        {
            throw new InvalidOperationException("MODEL_DEPLOYMENT_NAME environment variable is not set.");
        }
        // The vector store ID is annoying to find, so support name as an alternative
        var _vectorStoreId = System.Environment.GetEnvironmentVariable("AZURE_AI_VECTOR_STORE_ID");
        var _vectorStoreName = System.Environment.GetEnvironmentVariable("AZURE_AI_VECTOR_STORE_NAME");
        if (string.IsNullOrEmpty(_vectorStoreName) && string.IsNullOrEmpty(_vectorStoreId))
        {
            throw new InvalidOperationException("AZURE_AI_VECTOR_STORE_NAME or AZURE_AI_VECTOR_STORE_ID environment variable is not set.");
        }
        this.vectorStoreId = _vectorStoreId ?? string.Empty;
        this.vectorStoreName = _vectorStoreName ?? string.Empty;

        this.client = new(connectionString, azureService.GetCredential());
    }

    public AgentsClient GetClient()
    {
        return this.client;
    }

    public async Task UploadFileAsync(Stream contents, string filename)
    {
        if (string.IsNullOrWhiteSpace(filename) || Path.GetExtension(filename) == string.Empty)
        {
            throw new ArgumentException($"Filename '{filename}' must have a file extension (*.txt, *.md, ...)", nameof(filename));
        }

        var files = await this.client.GetFilesAsync(purpose: AgentFilePurpose.Agents);
        if (files.Value.Any(f => f.Filename == filename))
        {
            Console.WriteLine($"File '{filename}' already exists. Skipping upload.");
            return;
        }

        if (string.IsNullOrEmpty(this.vectorStoreId))
        {
            AgentPageableListOfVectorStore vectors = await this.client.GetVectorStoresAsync();
            var vectorStore = vectors.Data.FirstOrDefault(v => v.Name == this.vectorStoreName);
            if (vectorStore == null)
            {
                throw new InvalidOperationException($"Vector store with name '{this.vectorStoreName}' not found.");
            }
            this.vectorStoreId = vectorStore.Id;
        }

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine($"[INFO] Starting upload of '{filename}' to vector store '{this.vectorStoreName ?? this.vectorStoreId}' at {DateTime.UtcNow:O}");
        AgentFile file = await this.client.UploadFileAsync(contents, AgentFilePurpose.Agents, filename);

        VectorStoreFileBatch batch = await this.client.CreateVectorStoreFileBatchAsync(
            vectorStoreId: this.vectorStoreId,
            fileIds: [file.Id]
        );

        while (true)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(500));
            batch = await this.client.GetVectorStoreFileBatchAsync(this.vectorStoreId, batch.Id);
            if (batch.Status == VectorStoreFileBatchStatus.Completed)
            {
                break;
            }
            else if (batch.Status == VectorStoreFileBatchStatus.Failed)
            {
                throw new Exception($"File processing failed for {filename} uploading to vector store {this.vectorStoreId}.");
            }
            await Task.Delay(TimeSpan.FromSeconds(0.5));
        }

        stopwatch.Stop();
        Console.WriteLine($"[INFO] Upload and indexing of '{filename}' completed in {stopwatch.Elapsed.TotalSeconds:F2} seconds.");
    }

    public async Task<(string, TokenUsage)> QueryFileAsync(string filename, string query)
    {
        var prompt = $"Looking only in file '{filename}' answer the following: " + query;
        Console.WriteLine($"[DEBUG] Prompt: {prompt}");
        AgentThread thread = await this.client.CreateThreadAsync();


        Agent agent = await this.client.GetAgentAsync(this.agentId);
        ThreadRun runResponse = await this.client.CreateRunAsync(thread, agent);

        do
        {
            await Task.Delay(TimeSpan.FromMilliseconds(500));
            runResponse = await this.client.GetRunAsync(thread.Id, runResponse.Id);
        }
        while (runResponse.Status == RunStatus.Queued || runResponse.Status == RunStatus.InProgress);

        PageableList<ThreadMessage> afterRunMessagesResponse = await this.client.GetMessagesAsync(thread.Id);
        var messages = afterRunMessagesResponse.Data;
        var response = new List<string>();

        // Note: messages iterate from newest to oldest, with the messages[0] being the most recent
        foreach (ThreadMessage threadMessage in messages)
        {
            foreach (MessageContent contentItem in threadMessage.ContentItems)
            {
                if (contentItem is MessageTextContent textItem)
                {
                    response.Add(textItem.Text);
                }
                else if (contentItem is MessageImageFileContent imageFileItem)
                {
                    throw new NotImplementedException("Image file content is not supported yet.");
                }
            }
        }

        var tokenUsage = new TokenUsage
        {
            PromptTokens = runResponse.Usage.PromptTokens,
            CompletionTokens = runResponse.Usage.CompletionTokens,
            TotalTokens = runResponse.Usage.TotalTokens
        };
        return (string.Join("\n", response), tokenUsage);
    }
}