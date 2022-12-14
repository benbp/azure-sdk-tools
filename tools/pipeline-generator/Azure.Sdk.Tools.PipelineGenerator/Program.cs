using CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PipelineGenerator.Conventions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PipelineGenerator
{
    public class DefaultOptions
    {
        [Option('o', "organization", Required = false, Default = "azure-sdk", HelpText = "Azure DevOps organization name. Default: azure-sdk")]
        public string Organization { get; set; }
        [Option('p', "project", Required = false, Default = "internal", HelpText = "Azure DevOps project name. Default: internal")]
        public string Project { get; set; }
        [Option('t', "patvar", Required = false, Default = "PATVAR", HelpText = "Environment variable name containing a Personal Access Token. Default: PATVAR")]
        public string Patvar { get; set; }
        [Option("whatif", Required = false, HelpText = "Dry Run changes")]
        public bool WhatIf { get; set; }
    }

    public class GeneratorOptions : DefaultOptions
    {
        [Option('x', "prefix", Required = true, HelpText = "The prefix to append to the pipeline name")]
        public string Prefix { get; set; }
        [Option("path", Required = true, HelpText = "The directory from which to scan for components")]
        public string Path { get; set; }
        [Option('d', "devopspath", Required = false, HelpText = "The DevOps directory for created pipelines")]
        public string DevOpsPath { get; set; }
        [Option('e', "endpoint", Required = true, HelpText = "Name of the service endpoint to configure repositories with")]
        public string Endpoint { get; set; }
        [Option('r', "repository", Required = true, HelpText = "Name of the GitHub repo in the form [org]/[repo]")]
        public string Repository { get; set; }
        [Option('b', "branch", Required = false, Default = "refs/heads/main", HelpText = "Default: refs/heads/main")]
        public string Branch { get; set; }
        [Option('a', "agentpool", Required = false, Default = "Hosted", HelpText = "Name of the agent pool to use when pool isn't specified. Default: hosted")]
        public string Agentpool { get; set; }
        [Option('c', "convention", Required = true, HelpText = "The convention to build pipelines for: [ci|up|upweekly|tests|testsweekly]")]
        public string Convention { get; set; }
        [Option('v', "variablegroups", Required = true, HelpText = "Variable groups to link, separated by a space, e.g. --variablegroups '1 9 64'")]
        public IEnumerable<string> VariableGroups { get; set; }
        [Option("open", Required = false, HelpText = "Open a browser window to the definitions that are created")]
        public bool Open { get; set; }
        [Option("destroy", Required = false, HelpText = "Use this switch to delete the pipelines instead (DANGER!)")]
        public bool Destroy { get; set; }
        [Option("debug", Required = false, HelpText = "Turn on debug level logging")]
        public bool Debug { get; set; }
        [Option("no-schedule", Required = false, HelpText = "Skip creating any scheduled triggers")]
        public bool NoSchedule { get; set; }
        [Option("set-managed-variables", Required = false, HelpText = "Set managed meta.* variable values")]
        public bool SetManagedVariables { get; set; }
        [Option("overwrite-triggers",Required = false, HelpText = "Overwrite existing pipeline triggers (triggers may be manually modified, use with caution)")]
        public bool OverwriteTriggers { get; set; }
    }
    public class Program
    {

        public static async Task Main(string[] args)
        {

            var cancellationTokenSource = new CancellationTokenSource();
            Console.CancelKeyPress += (sender, e) =>
            {
                cancellationTokenSource.Cancel();
            };

            await Parser.Default.ParseArguments<GeneratorOptions>(args)
                .WithParsedAsync<GeneratorOptions>(async o =>
                {
                    var serviceProvider = GetServiceProvider(o.Debug);
                    var program = serviceProvider.GetService<Program>();
                    var code = await program.RunAsync(
                        o.Organization,
                        o.Project,
                        o.Prefix,
                        o.Path,
                        o.Patvar,
                        o.Endpoint,
                        o.Repository,
                        o.Branch,
                        o.Agentpool,
                        o.Convention,
                        o.VariableGroups.ToArray(),
                        o.DevOpsPath,
                        o.WhatIf,
                        o.Open,
                        o.Destroy,
                        o.NoSchedule,
                        o.SetManagedVariables,
                        o.OverwriteTriggers,
                        cancellationTokenSource.Token
                    );

                    Environment.Exit((int)code);
                });
        }

        private static IServiceProvider GetServiceProvider(bool debug)
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddLogging(config => config.AddConsole().SetMinimumLevel(debug ? LogLevel.Debug : LogLevel.Information))
                .AddTransient<Program>()
                .AddTransient<SdkComponentScanner>()
                .AddTransient<PullRequestValidationPipelineConvention>()
                .AddTransient<IntegrationTestingPipelineConvention>();

            return serviceCollection.BuildServiceProvider();
        }

        public Program(IServiceProvider serviceProvider, ILogger<Program> logger)
        {
            this.serviceProvider = serviceProvider;
            this.logger = logger;
        }

        private IServiceProvider serviceProvider;
        private ILogger<Program> logger;

        public ILoggerFactory LoggerFactory { get; }

        private PipelineConvention GetPipelineConvention(string convention, PipelineGenerationContext context)
        {
            var normalizedConvention = convention.ToLower();

            switch (normalizedConvention)
            {
                case "ci":
                    var ciLogger = serviceProvider.GetService<ILogger<PullRequestValidationPipelineConvention>>();
                    return new PullRequestValidationPipelineConvention(ciLogger, context);

                case "up":
                    var upLogger = serviceProvider.GetService<ILogger<UnifiedPipelineConvention>>();
                    return new UnifiedPipelineConvention(upLogger, context);

                case "upweekly":
                    var upWeeklyTestLogger = serviceProvider.GetService<ILogger<WeeklyUnifiedPipelineConvention>>();
                    return new WeeklyUnifiedPipelineConvention(upWeeklyTestLogger, context);

                case "tests":
                    var testLogger = serviceProvider.GetService<ILogger<IntegrationTestingPipelineConvention>>();
                    return new IntegrationTestingPipelineConvention(testLogger, context);

                case "testsweekly":
                    var weeklyTestLogger = serviceProvider.GetService<ILogger<WeeklyIntegrationTestingPipelineConvention>>();
                    return new WeeklyIntegrationTestingPipelineConvention(weeklyTestLogger, context);

                default: throw new ArgumentOutOfRangeException(nameof(convention), "Could not find matching convention.");
            }
        }

        public async Task<ExitCondition> RunAsync(
            string organization,
            string project,
            string prefix,
            string path,
            string patvar,
            string endpoint,
            string repository,
            string branch,
            string agentPool,
            string convention,
            string[] variableGroups,
            string devOpsPath,
            bool whatIf,
            bool open,
            bool destroy,
            bool noSchedule,
            bool setManagedVariables,
            bool overwriteTriggers,
            CancellationToken cancellationToken)
        {
            try
            {
                logger.LogDebug("Creating context.");

                // Fall back to a form of prefix if DevOps path is not specified
                var devOpsPathValue = string.IsNullOrEmpty(devOpsPath) ? $"\\{prefix}" : devOpsPath;

                var context = new PipelineGenerationContext(
                    this.logger,
                    organization,
                    project,
                    patvar,
                    endpoint,
                    repository,
                    branch,
                    agentPool,
                    variableGroups,
                    devOpsPathValue,
                    prefix,
                    whatIf,
                    noSchedule,
                    setManagedVariables,
                    overwriteTriggers
                    );

                var pipelineConvention = GetPipelineConvention(convention, context);
                var components = ScanForComponents(path, pipelineConvention.SearchPattern);

                if (components.Count() == 0)
                {
                    logger.LogWarning("No components were found.");
                    return ExitCondition.NoComponentsFound;
                }

                logger.LogInformation("Found {0} components", components.Count());

                if (HasPipelineDefinitionNameDuplicates(pipelineConvention, components))
                {
                    return ExitCondition.DuplicateComponentsFound;
                }

                foreach (var component in components)
                {
                    logger.LogInformation("Processing component '{0}' in '{1}'.", component.Name, component.Path);
                    if (destroy)
                    {
                        var definition = await pipelineConvention.DeleteDefinitionAsync(component, cancellationToken);
                    }
                    else
                    {
                        var definition = await pipelineConvention.CreateOrUpdateDefinitionAsync(component, cancellationToken);

                        if (open)
                        {
                            OpenBrowser(definition.GetWebUrl());
                        }
                    }
                }

                return ExitCondition.Success;

            }
            catch (Exception ex)
            {
                logger.LogCritical(ex, "BOOM! Something went wrong, try running with --debug.");
                return ExitCondition.Exception;
            }
        }

        private void OpenBrowser(string url)
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return;
            }


            logger.LogDebug("Launching browser window for: {0}", url);

            var processStartInfo = new ProcessStartInfo()
            {
                FileName = url,
                UseShellExecute = true,
            };

            // TODO: Need to test this on macOS and Linux.
            System.Diagnostics.Process.Start(processStartInfo);
        }

        private IEnumerable<SdkComponent> ScanForComponents(string path, string searchPattern)
        {
            var scanner = serviceProvider.GetService<SdkComponentScanner>();

            var scanDirectory = new DirectoryInfo(path);
            var components = scanner.Scan(scanDirectory, searchPattern);
            return components;
        }

        private bool HasPipelineDefinitionNameDuplicates(PipelineConvention convention, IEnumerable<SdkComponent> components)
        {
            var pipelineNames = new Dictionary<string, SdkComponent>();
            var duplicates = new HashSet<SdkComponent>();

            foreach (var component in components)
            {
                var definitionName = convention.GetDefinitionName(component);
                if (pipelineNames.TryGetValue(definitionName, out var duplicate))
                {
                    duplicates.Add(duplicate);
                    duplicates.Add(component);
                }
                else
                {
                    pipelineNames.Add(definitionName, component);
                }
            }

            if (duplicates.Count > 0) {
                logger.LogError("Found multiple pipeline definitions that will result in name collisions. This can happen when nested directory names are the same.");
                logger.LogError("Suggested fix: add a 'variant' to the yaml filename, e.g. 'sdk/keyvault/internal/ci.yml' => 'sdk/keyvault/internal/ci.keyvault.yml'");
                var paths = duplicates.Select(d => $"'{d.RelativeYamlPath}'");
                logger.LogError($"Pipeline definitions affected: {String.Join(", ", paths)}");

                return true;
            }

            return false;
        }
    }
}
