using Fumo.Enums;
using Fumo.Exceptions;
using Fumo.Extensions;
using Fumo.Models;
using Fumo.ThirdParty.Emotes.SevenTV;
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;
using System.Text.RegularExpressions;

namespace Fumo.Commands.SevenTV;

internal partial class SevenTVAliasCommand : ChatCommand
{
    [GeneratedRegex("(7tv)?alias")]
    public override partial Regex NameRegex();

    public ISevenTVService SevenTVService { get; }

    public IDatabase Redis { get; }

    public string BotID { get; }

    public SevenTVAliasCommand()
    {
        SetDescription("Set or Reset the alias of an emote");
        SetFlags(ChatCommandFlags.Reply);
    }

    public SevenTVAliasCommand(ISevenTVService sevenTVService, IConfiguration configuration, IDatabase redis) : this()
    {
        SevenTVService = sevenTVService;
        Redis = redis;
        BotID = configuration["Twitch:UserID"]!;
    }

    public override async ValueTask<CommandResult> Execute(CancellationToken ct)
    {
        var (EmoteSet, _) = await SevenTVService.EnsureCanModify(BotID, Redis, Channel, User);

        var input = Input.ElementAtOrDefault(0) ?? throw new InvalidInputException("Missing source emote");

        var srcEmote = (await SevenTVService.GetEnabledEmotes(EmoteSet, ct))
            .Where(x => x.Name == input)
            .SingleOrDefault();

        if (srcEmote is null)
        {
            return $"{input} is not an emote";
        }

        var dstEmoteName = Input.ElementAtOrDefault(1);

        var newEmoteName = await SevenTVService.ModifyEmoteSet(EmoteSet, ThirdParty.ListItemAction.Update, srcEmote.Id, dstEmoteName, ct);

        if (dstEmoteName is null)
        {
            return $"I reset the alias of {srcEmote.Name}";
        }

        return $"I set the alias of {srcEmote.Name} to {dstEmoteName}";
    }

    public override ValueTask<List<string>> GenerateWebsiteDescription(string prefix, CancellationToken ct)
    {
        List<string> strings = new()
        {
            "Set or Reset the alias of an emote",
            "",
            $"**Usage**: {prefix}alias <emote> [alias]",
            $"**Example**: {prefix}alias Floppal xqcL",
            $"**Example**: {prefix}alias FloppaL",
            "%TAB%Removes the alias from the FloppaL emote",
            "",
            "**Required 7TV Flags**",
            "Modify Emotes"
        };

        return ValueTask.FromResult(strings);
    }
}
