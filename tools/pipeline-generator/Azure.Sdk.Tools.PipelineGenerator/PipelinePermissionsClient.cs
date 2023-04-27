using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;

namespace PipelineGenerator
{
    public class PipelinePermissionsClient : VssHttpClientBase
    {
        protected struct CacheKey
        {
            public Guid ProjectId;
            public string VariableGroupId;

            public CacheKey(Guid projectId, string variableGroupId)
            {
                ProjectId = projectId;
                VariableGroupId = variableGroupId;
            }
        }

        private const string VariableGroupType = "variablegroup";
        private const string PermissionsApiVersion = "7.0-preview.1";
        private const string BaseUrlFormatString = "{0}/{1}/_apis/pipelines/pipelinePermissions{2}?api-version={3}";
        private Dictionary<CacheKey, PipelinePermissionsResource> _permissionsCache = new Dictionary<CacheKey, PipelinePermissionsResource>();

        public PipelinePermissionsClient(Uri baseUrl, VssCredentials credentials) : base(baseUrl, credentials) {}
        public PipelinePermissionsClient(Uri baseUrl, VssCredentials credentials, VssHttpRequestSettings settings) : base(baseUrl, credentials, settings) {}
        public PipelinePermissionsClient(Uri baseUrl, VssCredentials credentials, params DelegatingHandler[] handlers) : base(baseUrl, credentials, handlers) {}
        public PipelinePermissionsClient(Uri baseUrl, HttpMessageHandler pipeline, bool disposeHandler) : base(baseUrl, pipeline, disposeHandler) {}
        public PipelinePermissionsClient(Uri baseUrl, VssCredentials credentials, VssHttpRequestSettings settings, params DelegatingHandler[] handlers) : base(baseUrl, credentials, settings, handlers) {}

        private (string, Guid, string) GetCacheKey(string organization, Guid projectId, string variableGroupId)
        {
            return (organization, projectId, variableGroupId);
        }

        public async Task<PipelinePermissionsResource> GetVariableGroupPipelinePermissionsAsync(
            Guid projectId,
            int variableGroupId,
            CancellationToken cancellationToken)
        {
            var key = new CacheKey(projectId, variableGroupId.ToString());
            if (_permissionsCache.ContainsKey(key))
            {
                return _permissionsCache[key];
            }

            var url = string.Format(BaseUrlFormatString, BaseAddress, projectId, $"/{VariableGroupType}/{variableGroupId}", PermissionsApiVersion);
            var req = new HttpRequestMessage(HttpMethod.Get, url);
            var result = await SendAsync(req, null, cancellationToken);
            var content = await result.Content.ReadAsStringAsync();
            var permissions = JsonSerializer.Deserialize<PipelinePermissionsResource>(content);

            _permissionsCache.Add(key, permissions);

            return permissions;
        }

        public async Task CacheVariableGroupPipelinePermissionsAsync(
            Guid projectId,
            int variableGroupId,
            CancellationToken cancellationToken)
        {
            await GetVariableGroupPipelinePermissionsAsync(projectId, variableGroupId, cancellationToken);
        }

        public async Task AuthorizePipelineForVariableGroupsAsync(
            Guid projectId,
            int[] variableGroups,
            Int32 pipelineId,
            CancellationToken cancellationToken)
        {
            var request = new List<PipelinePermissionsResource>();
            foreach (var variableGroup in variableGroups.ToHashSet())
            {
                var pipelinePermissionsResource = new PipelinePermissionsResource
                {
                    Resource = new Resource
                    {
                        Id = variableGroup.ToString(),
                        Type = VariableGroupType
                    },
                    AllPipelines = new Permission { Authorized = false },
                    Pipelines = new List<Permission>
                    {
                        new Permission
                        {
                            Id = pipelineId,
                            Authorized = true
                        }
                    }
                };
                request.Add(pipelinePermissionsResource);
            }

            var content = JsonSerializer.Serialize(request);
            var url = string.Format(BaseUrlFormatString, BaseAddress, projectId, "", PermissionsApiVersion);
            var req = new HttpRequestMessage(HttpMethod.Patch, url)
            {
                Content = new StringContent(content, System.Text.Encoding.UTF8, "application/json")
            };

            var result = await SendAsync(req, null, cancellationToken);
            if (!result.IsSuccessStatusCode)
            {
                throw new Exception($"Failed to authorize pipeline for variable groups: {result.StatusCode}");
            }

            foreach (var permission in request)
            {
                var key = new CacheKey(projectId, permission.Resource.Id);
                _permissionsCache[key] = permission;
            }
        }
    }

    public class Permission
    {
        [JsonPropertyName("id")]
        public Int32 Id { get; set; }
        [JsonPropertyName("authorized")]
        public Boolean Authorized { get; set; }
        // [JsonPropertyName("authorizedBy")]
        // public IdentityRef AuthorizedBy { get; set; }
        // [JsonPropertyName("authorizedOn")]
        // public DateTime AuthorizedOn { get; set; }
    }

    public class Resource
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }
        [JsonPropertyName("id")]
        public string Id { get; set; }
        [JsonPropertyName("type")]
        public string Type { get; set; }
    }

    public class PipelinePermissionsResource
    {
        [JsonPropertyName("resource")]
        public Resource Resource { get; set; }
        [JsonPropertyName("allPipelines")]
        public Permission AllPipelines { get; set; }
        [JsonPropertyName("pipelines")]
        public List<Permission> Pipelines { get; set; }
    }
}