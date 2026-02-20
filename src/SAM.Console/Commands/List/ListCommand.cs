using SAM.Core;
using SAM.Core.Interfaces;
using Spectre.Console;
using Spectre.Console.Cli;
using System.Collections.Generic;
using SAM.API;

namespace SAM.Console.Commands;

internal class ListCommand
{
}

public class ListAppsSettings : SettingsBase
{

}

public class ListAppsCommand : Command<ListAppsSettings>
{
    public override int Execute(CommandContext context, ListAppsSettings settings)
    {
        AnsiConsole.MarkupLine("List command not implemented.");

        return 0;
    }
}
