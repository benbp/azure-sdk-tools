using System.CommandLine;

var fileOption = new Option<FileInfo?>(name: "--file", description: "Path to access config file for identities");

var rootCommand = new RootCommand("RBAC and Federated Identity manager for Azure SDK apps");
rootCommand.AddOption(fileOption);

rootCommand.SetHandler((file) =>
    {
        Run(file!);
    },
    fileOption);

rootCommand.Invoke(args);

static void Run(FileInfo config)
{
    Console.WriteLine("Using config -> " + config.FullName + Environment.NewLine);

    var accessConfig = new AccessConfig(config.FullName);
    accessConfig.Initialize();
    Console.WriteLine(accessConfig.ToString());
}