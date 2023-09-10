using Fumo.Exceptions;
using Fumo.Interfaces;
using Fumo.Models;
using Fumo.Shared.Regexes;
using Fumo.ThirdParty.Emotes.SevenTV;
using Fumo.ThirdParty.Exceptions;
using System.Text;
using System.Text.RegularExpressions;

namespace Fumo.Commands.SevenTV;

internal partial class SevenTVSearchCommand : ChatCommand
{
    private readonly int MaxEmoteOutput = 5;

    [GeneratedRegex("7tv$|search")]
    public override partial Regex NameRegex();

    public ISevenTVService SevenTV { get; }
    public IUserRepository UserRepository { get; }

    public SevenTVSearchCommand()
    {
        SetDescription("Search 7TV emotes");

        AddParameter(new(typeof(string), "uploader"));
        AddParameter(new(typeof(bool), "exact"));
    }

    public SevenTVSearchCommand(ISevenTVService sevenTV, IUserRepository userRepository) : this()
    {
        SevenTV = sevenTV;
        UserRepository = userRepository;
    }

    private async Task<CommandResult> GetEmoteFromName(string searchTerm, CancellationToken ct)
    {
        var exact = GetArgument<bool>("exact");

        var emotes = await SevenTV.SearchEmotesByName(searchTerm, exact, ct);

        if (TryGetArgument<string>("uploader", out var uploader))
        {
            await FilterByUploader(emotes.Items, UsernameCleanerRegex.CleanUsername(uploader), ct);
        }

        if (emotes.Items.Count == 0)
        {
            return "No emotes found";
        }

        if (emotes.Items.Count == 1)
        {
            var emote = emotes.Items[0];

            return $"{emote.Name} - https://7tv.app/emotes/{emote.Id}";
        }

        StringBuilder builder = new();

        var emotesToDisplay = emotes.Items.Take(MaxEmoteOutput);

        foreach (var emote in emotesToDisplay)
        {
            builder.Append($"{emote.Name} - https://7tv.app/emotes/{emote.Id}");

            if (emote != emotesToDisplay.Last())
            {
                builder.Append(" | ");
            }
        }

        return builder.ToString();
    }

    private async Task<CommandResult> GetEmoteFromID(string id, CancellationToken ct)
    {
        try
        {
            var emote = await SevenTV.SearchEmoteByID(id, ct);

            if (emote is null)
            {
                return $"No emote with the ID of {id} found";
            }

            return $"{emote.Name} - https://7tv.app/emotes/{emote.Id}";
        }
        catch (GraphQLException ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    private async Task FilterByUploader(List<SevenTVEmoteByNameItem> emotes, string uploader, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(uploader))
            return;

        var user = await UserRepository.SearchNameAsync(uploader, ct);
        var seventvUser = await SevenTV.GetUserInfo(user.TwitchID, ct);

        emotes.RemoveAll(x => x.Owner.Id != seventvUser.Id);
    }

    public override async ValueTask<CommandResult> Execute(CancellationToken ct)
    {
        var searchTerm = Input.ElementAtOrDefault(0) ?? throw new InvalidInputException("Missing a search term");

        var potentialID = ExtractSevenTVIDRegex.Extract(searchTerm);

        if (potentialID is string id)
        {
            return await GetEmoteFromID(id, ct);
        }

        return await GetEmoteFromName(searchTerm, ct);
    }

    public override ValueTask<List<string>> GenerateWebsiteDescription(string prefix, CancellationToken ct)
    {
        List<string> description = new()
        {
            "Search up 7TV emotes in chat",
            $"**Usage**: {prefix}7tv <search term>",
            $"**Example**: {prefix}7tv Apu",
            "",
            "-e, --exact",
            "%TAB%Search for an exact match",
            "",
            "-u, --uploader <name>",
            "%TAB%Search for emotes by a specific uploader",
            "%TAB%Requires their current Twitch username",
        };

        return ValueTask.FromResult(description);
    }
}
