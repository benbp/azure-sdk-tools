using Octokit;
using Octokit.Internal;
using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.CheckEnforcer
{
    public class GitHubLocalConnection : IConnection
    {
        public Task<IApiResponse<string>> GetHtml(Uri uri, IDictionary<string, string> parameters)
        {
            throw new Exception("Unsupported");
        }

        public Task<IApiResponse<byte[]>> GetRaw(Uri uri, IDictionary<string, string> parameters)
        {
            throw new Exception("Unsupported");
        }

        public Task<IApiResponse<T>> Get<T>(Uri uri, IDictionary<string, string> parameters, string accepts)
        {
            throw new Exception("Unsupported");
        }

        public Task<IApiResponse<T>> Get<T>(Uri uri, IDictionary<string, string> parameters, string accepts, CancellationToken cancellationToken)
        {
            throw new Exception("Unsupported");
        }

        public Task<IApiResponse<T>> Get<T>(Uri uri, TimeSpan timeout)
        {
            throw new Exception("Unsupported");
        }

        public Task<HttpStatusCode> Patch(Uri uri)
        {
            throw new Exception("Unsupported");
        }

        public Task<HttpStatusCode> Patch(Uri uri, string accepts)
        {
            throw new Exception("Unsupported");
        }

        public Task<IApiResponse<T>> Patch<T>(Uri uri, object body)
        {
            throw new Exception("Unsupported");
        }

        public Task<IApiResponse<T>> Patch<T>(Uri uri, object body, string accepts)
        {
            throw new Exception("Unsupported");
        }

        public Task<HttpStatusCode> Post(Uri uri, CancellationToken cancellationToken = default)
        {
            throw new Exception("Unsupported");
        }

        public Task<HttpStatusCode> Post(Uri uri, object body, string accepts, CancellationToken cancellationToken = default)
        {
            throw new Exception("Unsupported");
        }

        public Task<IApiResponse<T>> Post<T>(Uri uri, CancellationToken cancellationToken = default)
        {
            throw new Exception("Unsupported");
        }

        public Task<IApiResponse<T>> Post<T>(
            Uri uri,
            object body,
            string accepts,
            string contentType,
            IDictionary<string, string> parameters = null,
            CancellationToken cancellationToken = default)
        {
            throw new Exception("Unsupported");
        }

        public Task<IApiResponse<T>> Post<T>(Uri uri, object body, string accepts, string contentType, string twoFactorAuthenticationCode, CancellationToken cancellationToken = default)
        {
            throw new Exception("Unsupported");
        }

        public Task<IApiResponse<T>> Post<T>(Uri uri, object body, string accepts, string contentType, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            throw new Exception("Unsupported");
        }

        public Task<IApiResponse<T>> Post<T>(Uri uri, object body, string accepts, string contentType, Uri baseAddress, CancellationToken cancellationToken = default)
        {
            throw new Exception("Unsupported");
        }

        public Task<IApiResponse<T>> Put<T>(Uri uri, object body)
        {
            throw new Exception("Unsupported");
        }

        public Task<IApiResponse<T>> Put<T>(Uri uri, object body, string twoFactorAuthenticationCode)
        {
            throw new Exception("Unsupported");
        }

        public Task<IApiResponse<T>> Put<T>(Uri uri, object body, string twoFactorAuthenticationCode, string accepts)
        {
            throw new Exception("Unsupported");
        }

        public Task<HttpStatusCode> Put(Uri uri)
        {
            throw new Exception("Unsupported");
        }

        public Task<HttpStatusCode> Put(Uri uri, string accepts)
        {
            throw new Exception("Unsupported");
        }

        public Task<HttpStatusCode> Delete(Uri uri)
        {
            throw new Exception("Unsupported");
        }

        public Task<HttpStatusCode> Delete(Uri uri, string twoFactorAuthenticationCode)
        {
            throw new Exception("Unsupported");
        }

        public Task<HttpStatusCode> Delete(Uri uri, object data)
        {
            throw new Exception("Unsupported");
        }

        public Task<HttpStatusCode> Delete(Uri uri, object data, string accepts)
        {
            throw new Exception("Unsupported");
        }

        public Task<IApiResponse<T>> Delete<T>(Uri uri, object data)
        {
            throw new Exception("Unsupported");
        }

        public Task<IApiResponse<T>> Delete<T>(Uri uri, object data, string accepts)
        {
            throw new Exception("Unsupported");
        }

        public Uri BaseAddress { get; }

        public ICredentialStore CredentialStore { get; }

        public Credentials Credentials { get; set; }

        public void SetRequestTimeout(TimeSpan timeout)
        {
            throw new Exception("Unsupported");
        }

        public ApiInfo GetLastApiInfo()
        {
            throw new Exception("Unsupported");
        }
    }
}