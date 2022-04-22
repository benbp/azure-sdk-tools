using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Web;
using Microsoft.Extensions.Primitives;
using CommandLine;
using Azure.Sdk.Tools.WebhookRouter;
using Azure.Sdk.Tools.WebhookRouter.Routing;

namespace Azure.Sdk.Tools.CheckEnforcer.Tools
{
    class Program
    {
        private const string FunctionsRoute = "http://localhost:7071/admin/functions/";

        [Verb("trigger", HelpText = "Trigger local azure functions route.")]
        public class TriggerOptions
        {
            [Option('n', "name", Required = false, HelpText = "Trigger function name.")]
            public string FunctionName { get; set; }

            [Option('f', "file", Required = false, HelpText = "Function body json payload.")]
            public string PayloadFile { get; set; }
        }

        static async Task<int> Main(string[] args)
        {
            return await CommandLine.Parser.Default.ParseArguments<TriggerOptions>(args)
                .MapResult(
                    (TriggerOptions opts) => HandleTrigger(opts),
                    errs => Task.FromResult(0)
                );
        }

        static async Task<int> HandleTrigger(TriggerOptions opts)
        {
            Console.WriteLine(opts.FunctionName);
            Console.WriteLine(opts.PayloadFile);

            var rawPayload = File.ReadAllText(opts.PayloadFile);
            rawPayload = rawPayload.Trim('\n');

            var headers = new Dictionary<string, StringValues>{};
            headers.Add("X-Github-Event", "check_run");
            headers.Add("X-Hub-Signature", "");
            var payload = new Payload(headers, Encoding.ASCII.GetBytes(rawPayload));
            var payloadJson = JsonSerializer.Serialize(payload);

            var stringified = HttpUtility.JavaScriptStringEncode(payloadJson);
            var eventBody = $"{{ \"input\": \"{stringified}\" }}";

            var client = new HttpClient();
            var content = new StringContent(eventBody, Encoding.UTF8, "application/json");
            var route = FunctionsRoute + opts.FunctionName;
            Console.WriteLine(route);
            File.WriteAllText("./body.json", eventBody);
            var result = await client.PostAsync(route, content);

            result.EnsureSuccessStatusCode();
            return 0;
        }
    }
}