using Fumo.Enums;
using Fumo.Models;
using Fumo.Utils;
using Serilog;
using System.Text.RegularExpressions;

namespace Fumo.Commands;

internal partial class PingCommand : ChatCommand
{
    [GeneratedRegex("[Pp]ing")]
    public override partial Regex NameRegex();

    public ILogger Logger { get; }
    public IApplication Application { get; }

    public PingCommand()
    {
        SetFlags(ChatCommandFlags.Reply);
    }

    public PingCommand(ILogger logger, IApplication application) : this()
    {
        Logger = logger.ForContext<PingCommand>();
        Application = application;
    }

    public override ValueTask<CommandResult> Execute(CancellationToken ct)
    {
        var uptime = DateTime.Now - this.Application.StartTime;

        string time = new SecondsFormatter().SecondsFmt(uptime.TotalSeconds);
        return ValueTask.FromResult(new CommandResult
        {
            Message = $"🕴️ Uptime: {time}",
        });
    }
}
