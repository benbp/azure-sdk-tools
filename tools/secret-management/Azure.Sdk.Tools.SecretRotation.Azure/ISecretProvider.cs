using Azure.Core;

namespace Azure.Sdk.Tools.SecretManagement.Azure;

public interface ISecretProvider
{
    Task<string> GetSecretValueAsync(TokenCredential credential, Uri secretUri);
}
