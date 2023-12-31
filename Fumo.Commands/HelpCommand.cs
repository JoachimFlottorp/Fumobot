﻿using Fumo.Shared.Enums;
using Fumo.Shared.Models;
using Microsoft.Extensions.Configuration;

namespace Fumo.Commands;

public class HelpCommand : ChatCommand
{
    private readonly IConfiguration Configuration;

    public HelpCommand()
    {
        SetName("help");
        SetFlags(ChatCommandFlags.Reply | ChatCommandFlags.IgnoreBanphrase);
        SetCooldown(TimeSpan.FromSeconds(10));
    }

    public HelpCommand(IConfiguration configuration) : this()
    {
        Configuration = configuration;
    }

    public override ValueTask<CommandResult> Execute(CancellationToken ct)
    {
        var baseUrl = Configuration["Website:PublicURL"]!;

        var url = new Uri(new Uri(baseUrl), "/commands/index.html");

        return ValueTask.FromResult(new CommandResult
        {
            Message = url.ToString()
        });
    }
}
