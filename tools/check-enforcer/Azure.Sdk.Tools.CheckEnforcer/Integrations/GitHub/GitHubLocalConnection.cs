using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Octokit;
using Octokit.Internal;

namespace Azure.Sdk.Tools.CheckEnforcer
{
    public class GitHubLocalConnection : IConnection
    {
        public class LocalResponse : IResponse
        {
            [Obsolete("Use the constructor with maximum parameters to avoid shortcuts")]
            public LocalResponse() : this(new Dictionary<string, string>())
            {
            }

            [Obsolete("Use the constructor with maximum parameters to avoid shortcuts")]
            public LocalResponse(IDictionary<string, string> headers)
            {
            }

            public LocalResponse(HttpStatusCode statusCode, object body, IDictionary<string, string> headers, string contentType)
            {
            }

            public object Body { get; private set; }
            public IReadOnlyDictionary<string, string> Headers { get; private set; }
            public ApiInfo ApiInfo { get; internal set; }
            public HttpStatusCode StatusCode { get; private set; }
            public string ContentType { get; private set; }
        }

        public class LocalConnectionUnsupportedException : Exception
        {
            public LocalConnectionUnsupportedException(string method) : base($"Unsupported method: {method}")
            {
            }

            public LocalConnectionUnsupportedException(string method, Uri uri) : base($"Unsupported method and route: {method} {uri}")
            {
            }
        }

        private ILogger log;

        public GitHubLocalConnection(ILogger log)
        {
            this.log = log;
        }

        public Task<IApiResponse<string>> GetHtml(Uri uri, IDictionary<string, string> parameters)
        {
            throw new LocalConnectionUnsupportedException("GetHtml", uri);
        }

        public Task<IApiResponse<byte[]>> GetRaw(Uri uri, IDictionary<string, string> parameters)
        {
            throw new LocalConnectionUnsupportedException("GetRaw", uri);
        }

        public Task<IApiResponse<T>> Get<T>(Uri uri, IDictionary<string, string> parameters, string accepts)
        {
            throw new LocalConnectionUnsupportedException("Get", uri);
        }

        public Task<IApiResponse<T>> Get<T>(Uri uri, IDictionary<string, string> parameters, string accepts, CancellationToken cancellationToken)
        {
            throw new LocalConnectionUnsupportedException("GetWithCancellation", uri);
        }

        public Task<IApiResponse<T>> Get<T>(Uri uri, TimeSpan timeout)
        {
            throw new LocalConnectionUnsupportedException("GetWithTimeout", uri);
        }

        public Task<HttpStatusCode> Patch(Uri uri)
        {
            throw new LocalConnectionUnsupportedException("Patch", uri);
        }

        public Task<HttpStatusCode> Patch(Uri uri, string accepts)
        {
            throw new LocalConnectionUnsupportedException("PatchWithAccepts", uri);
        }

        public Task<IApiResponse<T>> Patch<T>(Uri uri, object body)
        {
            throw new LocalConnectionUnsupportedException("PatchWithBody", uri);
        }

        public Task<IApiResponse<T>> Patch<T>(Uri uri, object body, string accepts)
        {
            throw new LocalConnectionUnsupportedException("PatchWithBodyAccepts", uri);
        }

        public Task<HttpStatusCode> Post(Uri uri, CancellationToken cancellationToken = default)
        {
            throw new LocalConnectionUnsupportedException("Post", uri);
        }

        public Task<HttpStatusCode> Post(Uri uri, object body, string accepts, CancellationToken cancellationToken = default)
        {
            throw new LocalConnectionUnsupportedException("PostWithBody", uri);
        }

        public Task<IApiResponse<T>> Post<T>(Uri uri, CancellationToken cancellationToken = default)
        {
            throw new LocalConnectionUnsupportedException("PostWithCancellation", uri);
        }

        public Task<IApiResponse<T>> Post<T>(
            Uri uri,
            object body,
            string accepts,
            string contentType,
            IDictionary<string, string> parameters = null,
            CancellationToken cancellationToken = default)
        {
            // throw new LocalConnectionUnsupportedException("PostWithParameters", uri);
            log.LogInformation($"PostWithParameters {uri}");
            var response = new LocalResponse(HttpStatusCode.OK, "", new Dictionary<string, string>{}, contentType);
            return Task.FromResult<IApiResponse<T>>(new ApiResponse<T>(response));
        }

        public Task<IApiResponse<T>> Post<T>(Uri uri, object body, string accepts, string contentType, string twoFactorAuthenticationCode, CancellationToken cancellationToken = default)
        {
            throw new LocalConnectionUnsupportedException("PostWith2fa", uri);
        }

        public Task<IApiResponse<T>> Post<T>(Uri uri, object body, string accepts, string contentType, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            throw new LocalConnectionUnsupportedException("PostWithTimeout", uri);
        }

        public Task<IApiResponse<T>> Post<T>(Uri uri, object body, string accepts, string contentType, Uri baseAddress, CancellationToken cancellationToken = default)
        {
            throw new LocalConnectionUnsupportedException("PostWithBaseAddress", uri);
        }

        public Task<IApiResponse<T>> Put<T>(Uri uri, object body)
        {
            throw new LocalConnectionUnsupportedException("PutWithBody", uri);
        }

        public Task<IApiResponse<T>> Put<T>(Uri uri, object body, string twoFactorAuthenticationCode)
        {
            throw new LocalConnectionUnsupportedException("PutWith2fa", uri);
        }

        public Task<IApiResponse<T>> Put<T>(Uri uri, object body, string twoFactorAuthenticationCode, string accepts)
        {
            throw new LocalConnectionUnsupportedException("PutWith2faAccepts", uri);
        }

        public Task<HttpStatusCode> Put(Uri uri)
        {
            throw new LocalConnectionUnsupportedException("Put", uri);
        }

        public Task<HttpStatusCode> Put(Uri uri, string accepts)
        {
            throw new LocalConnectionUnsupportedException("PutWithAccepts", uri);
        }

        public Task<HttpStatusCode> Delete(Uri uri)
        {
            throw new LocalConnectionUnsupportedException("Delete", uri);
        }

        public Task<HttpStatusCode> Delete(Uri uri, string twoFactorAuthenticationCode)
        {
            throw new LocalConnectionUnsupportedException("DeleteWith2fa", uri);
        }

        public Task<HttpStatusCode> Delete(Uri uri, object data)
        {
            throw new LocalConnectionUnsupportedException("DeleteWithData", uri);
        }

        public Task<HttpStatusCode> Delete(Uri uri, object data, string accepts)
        {
            throw new LocalConnectionUnsupportedException("DeleteWithDataAccepts", uri);
        }

        public Task<IApiResponse<T>> Delete<T>(Uri uri, object data)
        {
            throw new LocalConnectionUnsupportedException("DeleteTWithData", uri);
        }

        public Task<IApiResponse<T>> Delete<T>(Uri uri, object data, string accepts)
        {
            throw new LocalConnectionUnsupportedException("DeleteTWithDataAccepts", uri);
        }

        public Uri BaseAddress { get; }

        public ICredentialStore CredentialStore { get; }

        public Credentials Credentials { get; set; }

        public void SetRequestTimeout(TimeSpan timeout)
        {
        }

        public ApiInfo GetLastApiInfo()
        {
            throw new LocalConnectionUnsupportedException("GetLastApiInfo");
        }
    }
}