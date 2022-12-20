using CommandLine;

namespace PipelineGenerator.CommandParserOptions
{
    public class DefaultOptions
    {
        [Option("debug", Required = false, HelpText = "Turn on debug level logging")]
        public bool Debug { get; set; }

        [Option("whatif", Required = false, HelpText = "Dry Run changes")]
        public bool WhatIf { get; set; }
    }
}