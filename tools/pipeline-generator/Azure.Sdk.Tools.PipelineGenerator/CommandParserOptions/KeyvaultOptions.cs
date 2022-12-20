using System;
using System.Collections.Generic;
using CommandLine;

namespace PipelineGenerator.CommandParserOptions
{
    public class KeyvaultOptions : DefaultOptions
    {
        [Option('d', "service-directories", Required = true, HelpText = "Service Directories for live tests to onboard")]
        public IEnumerable<string> ServiceDirectories { get; set; }

        [Option('k', "keyvault", Required = true, HelpText = "Name of keyvault to create or update")]
        public string Keyvault { get; set; }

        [Option('g', "group", Required = false, HelpText = "Resource group for keyvault")]
        public string ResourceGroup { get; set; }

        [Option('s', "subscription", Required = false, HelpText = "Subscription for keyvault")]
        public string SubscriptionId { get; set; }

        [Option('t', "tenant", Required = false, HelpText = "Subscription for keyvault")]
        public string TenantId { get; set; }

        [Option('u', "username", Required = false, HelpText = "Service principal application id")]
        public string ApplicationId { get; set; }

        [Option('p', "password", Required = false, HelpText = "Service principal password")]
        public string ApplicationSecret { get; set; }
    }
}