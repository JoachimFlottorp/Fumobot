using Fumo.Exceptions;
using Fumo.Extensions;
using Fumo.Models;
using Fumo.Shared.Regexes;
using Fumo.ThirdParty;
using Fumo.ThirdParty.Emotes.SevenTV;
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;
using Fumo.ThirdParty.Exceptions;
using System.Text.RegularExpressions;

namespace Fumo.Commands.SevenTV;

internal partial class SevenTVAddCommand : ChatCommand
{
    [GeneratedRegex("(7tv)?add")]
    public override partial Regex NameRegex();

    private ISevenTVService SevenTVService { get; }

    private IDatabase Redis { get; }

    private string BotID { get; }

    public SevenTVAddCommand()
    {
        SetDescription("Adds a 7TV emote to the channel.");

        AddParameter(new(typeof(string), "alias"));
        AddParameter(new(typeof(bool), "exact"));
    }

    public SevenTVAddCommand(ISevenTVService sevenTVService, IConfiguration configuration, IDatabase redis) : this()
    {
        SevenTVService = sevenTVService;
        Redis = redis;
        BotID = configuration["Twitch:UserID"]!;
    }

    private async Task<SevenTVBasicEmote> ResolveEmote(string search, CancellationToken ct)
    {
        var id = ExtractSevenTVIDRegex.Extract(search);

        return id switch
        {
            string => await SevenTVService.SearchEmoteByID(id, ct),
            null => await GetEmoteFromName(search, ct)
        };
    }

    private async Task<SevenTVBasicEmote> GetEmoteFromName(string search, CancellationToken ct)
    {
        var exact = GetArgument<bool>("exact");
        var index = GetArgument<int>("index"); // Fuck it not writing documentation for this one

        var emotes = await SevenTVService.SearchEmotesByName(search, exact, ct);

        if (emotes.Items.Count <= 0) throw new InvalidInputException("No emotes found");

        if (index >= emotes.Items.Count) throw new InvalidInputException("Index out of range");

        return emotes.Items.ElementAt(index).AsBasicEmote();
    }

    public override async ValueTask<CommandResult> Execute(CancellationToken ct)
    {
        var aaaaa = await SevenTVService.EnsureCanModify(BotID, Redis, Channel, User);

        if (Input.Count <= 0) throw new InvalidInputException("You need to specify an emote to add");

        var search = Input[0].ToLowerInvariant();

        var emote = await ResolveEmote(search, ct);

        var emoteName = emote.Name;

        if (TryGetArgument<string>("alias", out var alias))
        {
            emoteName = alias;
        }

        try
        {
            var newEmote = await SevenTVService.ModifyEmoteSet(aaaaa.EmoteSet, ListItemAction.Add, emote.Id, emoteName, ct);
            return $"Added emote {newEmote}";
        }
        catch (GraphQLException ex)
        {
            return ex.Message;
        }
    }

    public override ValueTask<List<string>> GenerateWebsiteDescription(string prefix, CancellationToken ct)
    {
        List<string> strings = new()
        {
            "Add a 7TV emote",
            $"**Usage**: {prefix}add <emote>",
            $"**Usage**: {prefix}add FloppaL",
            "",
            "You can also add emotes by ID or URL",
            $"**Example**: {prefix}add 60aeab8df6a2c3b332d21139",
            $"**Example**: {prefix}add https://7tv.app/emotes/60aeab8df6a2c3b332d21139",
            "",
            "-a, --alias <alias>",
            "%TAB%Set an alias for the emote",
            "",
            "-e, --exact",
            "%TAB%Search for an exact match",
            "",
            "",
            "**Required 7TV Permissions**",
            "Modify Emotes",
        };

        return ValueTask.FromResult(strings);
    }
}
