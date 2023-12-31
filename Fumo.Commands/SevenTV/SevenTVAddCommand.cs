using Fumo.Shared.Exceptions;
using Fumo.Shared.Extensions;
using Fumo.Shared.Models;
using Fumo.Shared.Regexes;
using Fumo.ThirdParty.Emotes.SevenTV;
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;
using Fumo.ThirdParty.Exceptions;
using Fumo.ThirdParty.Emotes.SevenTV.Enums;
using Fumo.ThirdParty.Emotes.SevenTV.Models;
using Fumo.Commands.Middleware;

namespace Fumo.Commands.SevenTV;

public class SevenTVAddCommand : ChatCommand
{
    private readonly ISevenTVService SevenTVService;
    private readonly IDatabase Redis;
    private readonly string BotID;

    public SevenTVAddCommand()
    {
        SetName("(7tv)?add");
        SetDescription("Adds a 7TV emote to the channel.");

        AddParameter(new(typeof(string), "alias"));
        AddParameter(new(typeof(bool), "exact"));

        AddMiddleware<SevenTVModifyMiddleware>();
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

        if (emotes.Items.Count <= 0) throw new InvalidInputException("No emote found");

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
            if (SevenTVErrorMapper.TryErrorCodeFromGQL(ex, out var errorCode))
            {
                if (errorCode == SevenTVErrorMapper.ErrorEmoteAlreadyEnabled)
                {
                    return $"Emote {emote.Name} is already enabled";
                }
            }

            return ex.Message;
        }
    }
}
