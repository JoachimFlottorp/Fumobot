using Fumo.Exceptions;
using Fumo.Extensions;
using Fumo.Interfaces;
using Fumo.Models;
using Fumo.ThirdParty.Emotes.SevenTV;
using Fumo.ThirdParty.Exceptions;
using Microsoft.Extensions.Configuration;
using Serilog;
using StackExchange.Redis;
using System.Text;
using System.Text.RegularExpressions;

namespace Fumo.Commands.SevenTV;

internal partial class SevenTVRemoveCommand : ChatCommand
{
    [GeneratedRegex("(7tv)?remove")]
    public override partial Regex NameRegex();

    public ILogger Logger { get; }
    private ISevenTVService SevenTVService { get; }

    private IDatabase Redis { get; }
    public IMessageSenderHandler MessageSender { get; }
    private string BotID { get; }

    public SevenTVRemoveCommand()
    {
        SetDescription("Remove 7TV emotes");
    }

    public SevenTVRemoveCommand(ILogger logger, ISevenTVService sevenTVService, IDatabase redis, IConfiguration configuration, IMessageSenderHandler messageSender) : this()
    {
        Logger = logger.ForContext<SevenTVRemoveCommand>();
        SevenTVService = sevenTVService;
        Redis = redis;
        MessageSender = messageSender;
        BotID = configuration["Twitch:UserID"]!;
    }

    public override async ValueTask<CommandResult> Execute(CancellationToken ct)
    {
        StringBuilder output = new();

        var aaaa = await SevenTVService.EnsureCanModify(BotID, Redis, Channel, User);

        if (Input.Count <= 0)
        {
            throw new InvalidInputException("Provide emotes to remove");
        }

        List<SevenTVEnabledEmote> emotesToRemove = new();
        var currentEmotes = await SevenTVService.GetEnabledEmotes(aaaa.EmoteSet, ct);

        foreach (var emote in currentEmotes)
        {
            if (!Input.Remove(emote.Name)) continue;

            emotesToRemove.Add(emote);

            if (Input.Count <= 0) break;
        }

        // Some emotes could not be found
        if (Input.Count > 0)
        {
            output.Append($"Could not find the following emote{(Input.Count > 1 ? "s" : "")}");
            Input.ForEach(x => output.Append($" {x}"));

            MessageSender.ScheduleMessage(Channel.TwitchName, output.ToString());

            output.Clear();
        }

        List<string> failedToRemove = new();

        foreach (var emote in emotesToRemove)
        {
            try
            {
                await SevenTVService.ModifyEmoteSet(aaaa.EmoteSet, ThirdParty.ListItemAction.Remove, emote.Id, ct: ct);
            }
            catch (GraphQLException ex)
            {
                Logger.Warning(ex, "Failed to remove emote {Emote} in {Channel}", emote.Name, Channel.TwitchName);

                failedToRemove.Add(emote.Name);
            }
        }

        if (failedToRemove.Count > 0)
        {
            output.Append($"\u2022 Failed to remove the following emotes:");

            failedToRemove.ForEach(x => output.Append(x));
        }
        else
        {
            output.Append("All emotes were successfully removed");
        }

        return output.ToString();
    }

    public override ValueTask<List<string>> GenerateWebsiteDescription(string prefix, CancellationToken ct)
    {
        List<string> strings = new()
        {
            "Removes 7TV emotes from your emote set",
            $"Usage: {prefix}remove <emote names>",
            "",
            "**Required 7TV Permissions**",
            "Manage Emotes",
        };

        return ValueTask.FromResult(strings);
    }
}
