using Fumo.Enums;
using Fumo.Exceptions;
using Fumo.Models;
using Fumo.Repository;
using System.Text.RegularExpressions;

namespace Fumo.Commands;

internal partial class HelpCommand : ChatCommand
{
    [GeneratedRegex("help")]
    public override partial Regex NameRegex();

    public CommandRepository CommandRepository { get; }

    public HelpCommand()
    {
        SetFlags(ChatCommandFlags.Reply);
        SetCooldown(TimeSpan.FromSeconds(10));
    }

    public HelpCommand(CommandRepository commandRepository) : this()
    {
        CommandRepository = commandRepository;
    }

    public override ValueTask<CommandResult> Execute(CancellationToken ct)
    {
        // TODO: Add website url whenever that happens

        if (Input.Count <= 0)
        {
            throw new InvalidInputException("No command provided");
        }

        var commandName = Input[0];

        var command = this.CommandRepository.GetCommand(commandName);

        if (command is null)
        {
            return new ValueTask<CommandResult>($"The command {commandName} does not exist");
        }

        var name = command.NameMatcher;
        var description = command.Description;
        var cooldown = command.Cooldown.TotalSeconds;
        var permissions = string.Join(", ", command.Permissions);

        return new ValueTask<CommandResult>($"{name} Description - {description} Cooldown - {cooldown}s - Requires - {permissions}");
    }
}
