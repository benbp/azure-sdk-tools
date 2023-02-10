#nullable disable
using System.Net.Http.Json;
using System.Net.Http.Headers;

const string RequestUrl = $"https://bebroderoidcapp.azurewebsites.net/api/AzureSdkIssueLabelerService";

// This is not using the correct pattern for HttpClient since we intentionally
// are mking a single request.  Treat this as a singleton for real-world use.

using var client = new HttpClient();
client.DefaultRequestHeaders.Authorization =
    new AuthenticationHeaderValue("Bearer", System.Environment.GetEnvironmentVariable("TOKEN"));

// This is a subset of the information found in the Octokit payload; we should be able to project from that.

var payload = new
{
    IssueNumber = 33697,
    Title = "[FEATURE REQ] Azure Function EventHub allow passing of the PartitionKey in the EventData ctor",
    Body = "### Library name\n\nAzure.Messaging.EventHubs\n\n### Please describe the feature.\n\nIt would be great if could pass a PartitionKey while creating a new EventData object.\n\nThis would allow the use of user defined partition keys when adding message via the EventHub output binding's `IAsyncCollector.AddAsync` method.\n\nThis is desirable over the use of `EventHubProducerClient.SendAsync` due to the fact that `IAsyncCollector.AddAsync` will queue the message and batch behind the scenes in parallel, whereas `EventHubProducerClient.SendAsync` needs to be awaited on each send of a single message or a batch.\n\nThe two mechanisms behave quite differently from each other and we've seen large performance gains while using the `IAsyncCollector` and `IAsyncCollector.Flush` compared to `EventHubProducerClient.SendAsync`.\n\n```cs\nIAsyncCollector collector = from output binding\nEventData message = new EventData(data);\nawait collector.AddAsync(message);      <-- returns immediately and is sent in parallel\n...\nmore stuff\n...\nawait collector.Flush();      <-- waits for remaining messages to send\n```\nvs\n```cs\nEventHubProducerClient client = from output binding\nEventData message = new EventData(data);\nawait client.SendAsync(message);      <-- waits for the send operation before returning\n```\n\nThis is similar to some of the comments in https://github.com/Azure/azure-sdk-for-net/issues/28245 but it's not the same as the issue itself.\n\nWe've done some testing using the `EventHubsModelFactory.EventData` testing method and verified the partition key when set via this method appears to be used correctly by `IAsyncCollector` and the events do in fact send and arrive downstream in the correct batches with the set partition.\n\nClearly we can't use this testing and mocking method for production though. \n\nOr maybe there's another way to use the `IAsyncCollector`?\n\nThanks :-)",
    IssueUserLogin = "fuzzlebuck",
    RepositoryName = "azure-sdk-for-net",
    RepositoryOwnerName = "Azure"
};

var response = await client.PostAsJsonAsync(RequestUrl, payload).ConfigureAwait(false);
response.EnsureSuccessStatusCode();

var suggestions = await response.Content.ReadFromJsonAsync<LabelResponse>().ConfigureAwait(false);

// For illustration purposes only.

Console.WriteLine("Suggested labels: [{0}]", string.Join(", ", suggestions!.Labels));

// Private type used for deserializing the response.  Don't forget that
// System.Text.Json gets cranky if you don't have a getter/setter for
// properties.

class LabelResponse
{
    public string[] Labels { get; set; }
}
