using System.CommandLine;

await Entrypoint(args);

static async Task Entrypoint(string[] args)
{
    var fileOption = new Option<FileInfo?>(name: "--file", description: "Path to access config file for identities");
    var rootCommand = new RootCommand("RBAC and Federated Identity manager for Azure SDK apps");

    rootCommand.AddOption(fileOption);
    rootCommand.SetHandler(async (file) => await Run(file!), fileOption);

    await rootCommand.InvokeAsync(args);
}

static async Task Run(FileInfo config)
{
    Console.WriteLine("Using config -> " + config.FullName + Environment.NewLine);

    var accessConfig = AccessConfig.Create(config.FullName);
    Console.WriteLine(accessConfig.ToString());

    var reconciler = new Reconciler(new GraphClient(), new RbacClient());
    await reconciler.Reconcile(accessConfig);
}
