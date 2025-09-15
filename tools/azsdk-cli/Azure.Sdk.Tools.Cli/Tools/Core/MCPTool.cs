// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;

namespace Azure.Sdk.Tools.Cli.Contract;

public abstract class MCPTool : MCPToolBase
{
    public abstract Command GetCommand();

    public override List<Command> GetCommandInstances()
    {
        var command = GetCommand();
        command.SetHandler(async ctx => await InstrumentedCommandHandler(command, ctx));
        return [command];
    }
}
