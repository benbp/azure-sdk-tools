using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PipelineGenerator.Conventions;
using PipelineGenerator.CommandParserOptions;

namespace PipelineGenerator
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var cancellationTokenSource = new CancellationTokenSource();
            Console.CancelKeyPress += (sender, e) =>
            {
                cancellationTokenSource.Cancel();
            };

            var parsed = Parser.Default.ParseArguments<DevOpsDefaultOptions, GenerateOptions, KeyvaultOptions>(args);
            parsed = await parsed.WithParsedAsync<GenerateOptions>(async opts =>
            {
                var serviceProvider = GetServiceProvider(opts.Debug);
                var program = serviceProvider.GetService<Program>();
                var code = await program.Generate(opts, cancellationTokenSource.Token);
                Environment.Exit((int)code);
            });
            parsed = await parsed.WithParsedAsync<KeyvaultOptions>(async opts =>
            {
                var serviceProvider = GetServiceProvider(opts.Debug);
                var program = serviceProvider.GetService<Program>();
                var code = await program.CreateOrUpdateKeyvault(opts, cancellationTokenSource.Token);
                Environment.Exit((int)code);
            });
            parsed.WithNotParsed(_ => { Environment.Exit((int)ExitCondition.InvalidArguments); });
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

        public async Task<ExitCondition> CreateOrUpdateKeyvault(KeyvaultOptions options, CancellationToken cancellationToken)
        {
        }

        public async Task<ExitCondition> Generate(GenerateOptions options, CancellationToken cancellationToken)
        {
            try
            {
                logger.LogDebug("Creating context.");

                // Fall back to a form of prefix if DevOps path is not specified
                var devOpsPathValue = string.IsNullOrEmpty(options.DevOpsPath) ? $"\\{options.Prefix}" : options.DevOpsPath;

                var context = new PipelineGenerationContext(
                    this.logger,
                    options.Organization,
                    options.Project,
                    options.Patvar,
                    options.Endpoint,
                    options.Repository,
                    options.Branch,
                    options.Agentpool,
                    options.VariableGroups.ToArray(),
                    options.DevOpsPath,
                    options.Prefix,
                    options.WhatIf,
                    options.NoSchedule,
                    options.SetManagedVariables,
                    options.OverwriteTriggers
                    );

                var pipelineConvention = GetPipelineConvention(options.Convention, context);
                var components = ScanForComponents(options.Path, pipelineConvention.SearchPattern);

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
                    if (options.Destroy)
                    {
                        var definition = await pipelineConvention.DeleteDefinitionAsync(component, cancellationToken);
                    }
                    else
                    {
                        var definition = await pipelineConvention.CreateOrUpdateDefinitionAsync(component, cancellationToken);

                        if (options.Open)
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
