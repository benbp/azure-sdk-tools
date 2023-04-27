using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
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
        private const string VariableGroupType = "variablegroup";
        private const string PermissionsApiVersion = "7.0-preview.1";
        private Dictionary<string, PipelinePermissionsResource> _permissionsCache = new Dictionary<string, PipelinePermissionsResource>();

        public PipelinePermissionsClient(Uri baseUrl, VssCredentials credentials) : base(baseUrl, credentials) {}
        public PipelinePermissionsClient(Uri baseUrl, VssCredentials credentials, VssHttpRequestSettings settings) : base(baseUrl, credentials, settings) {}
        public PipelinePermissionsClient(Uri baseUrl, VssCredentials credentials, params DelegatingHandler[] handlers) : base(baseUrl, credentials, handlers) {}
        public PipelinePermissionsClient(Uri baseUrl, HttpMessageHandler pipeline, bool disposeHandler) : base(baseUrl, pipeline, disposeHandler) {}
        public PipelinePermissionsClient(Uri baseUrl, VssCredentials credentials, VssHttpRequestSettings settings, params DelegatingHandler[] handlers) : base(baseUrl, credentials, settings, handlers) {}

        public async Task<PipelinePermissionsResource> GetVariableGroupPipelinePermissionsAsync(
            Guid projectId,
            int variableGroupId,
            CancellationToken cancellationToken)
        {
            if (_permissionsCache.ContainsKey(variableGroupId.ToString()))
            {
                return _permissionsCache[variableGroupId.ToString()];
            }

            var url = $"{projectId}/_apis/pipelines/pipelinePermissions/{VariableGroupType}/{variableGroupId}/?api-version={PermissionsApiVersion}";
            var req = new HttpRequestMessage(HttpMethod.Get, url);
            var result = await SendAsync(req, null, cancellationToken);
            var content = await result.Content.ReadAsStringAsync();
            var permissions = JsonSerializer.Deserialize<PipelinePermissionsResource>(content);

            _permissionsCache.Add(variableGroupId.ToString(), permissions);

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
            var req = new HttpRequestMessage(HttpMethod.Patch, $"{projectId}/_apis/pipelines/pipelinePermissions?api-version={PermissionsApiVersion}")
            {
                Content = new StringContent(content)
            };

            var result = await SendAsync(req, null, cancellationToken);
            if (!result.IsSuccessStatusCode)
            {
                throw new Exception($"Failed to authorize pipeline for variable groups: {result.StatusCode}");
            }

            foreach (var permission in request)
            {
                _permissionsCache[permission.Resource.Id] = permission;
            }
        }
    }

    public class Permission
    {
        public Int32 Id { get; set; }
        public Boolean Authorized { get; set; }
        public IdentityRef AuthorizedBy { get; set; }
        public DateTime AuthorizedOn { get; set; }
    }

    public class Resource
    {
        public string Name { get; set; }
        public string Id { get; set; }
        public string Type { get; set; }
    }

    public class PipelinePermissionsResource
    {
        public Resource Resource { get; set; }
        public Permission AllPipelines { get; set; }
        public List<Permission> Pipelines { get; set; }
    }
}