// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;

namespace Azure.Sdk.Tools.Cli.Contract;

public abstract class MCPMultiCommandTool : MCPToolBase
{
    public abstract List<Command> GetCommands();

    public override List<Command> GetCommandInstances() => GetCommands();
}
