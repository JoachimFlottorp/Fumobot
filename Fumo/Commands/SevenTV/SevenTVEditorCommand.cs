using Fumo.Enums;
using Fumo.Exceptions;
using Fumo.Extensions;
using Fumo.Interfaces;
using Fumo.Models;
using Fumo.ThirdParty.Emotes.SevenTV;
using Fumo.ThirdParty.Exceptions;
using Microsoft.Extensions.Configuration;
using Serilog;
using StackExchange.Redis;
using System.Text.RegularExpressions;

namespace Fumo.Commands.SevenTV;

internal partial class SevenTVEditorCommand : ChatCommand
{
    [GeneratedRegex("(7tv)?(?(1)e|editor)")]
    public override partial Regex NameRegex();

    private ILogger Logger { get; }
    public IDatabase Redis { get; }
    public ISevenTVService SevenTVService { get; }
    public IUserRepository UserRepository { get; }
    private string BotID { get; }

    public SevenTVEditorCommand()
    {
        SetDescription("Add and Remove 7TV editors from the channel");

        SetFlags(ChatCommandFlags.BroadcasterOnly | ChatCommandFlags.Reply);
    }

    public SevenTVEditorCommand(
        ILogger logger,
        IConfiguration configuration,
        IDatabase redis,
        ISevenTVService sevenTVService,
        IUserRepository userRepository) : this()
    {
        Logger = logger.ForContext<SevenTVEditorCommand>();
        Redis = redis;
        SevenTVService = sevenTVService;
        UserRepository = userRepository;
        BotID = configuration["Twitch:UserID"]!;
    }

    private async Task<SevenTVUser> GetUser(CancellationToken ct)
    {
        var username = Input.ElementAtOrDefault(0) ?? throw new InvalidInputException("Provide a username to add or remove");

        var user = await UserRepository.SearchNameAsync(username, ct);

        return await SevenTVService.GetUserInfo(user.TwitchID, ct);
    }

    private string HumanizeError(GraphQLException ex)
    {
        if (ex.Message.StartsWith("70403")) return "I don't have permission to do this";

        return ex.Message;
    }

    public override async ValueTask<CommandResult> Execute(CancellationToken ct)
    {
        var (_, UserID) = await SevenTVService.EnsureCanModify(BotID, Redis, Channel, User);

        var userToMutate = await GetUser(ct);
        var twitchId = userToMutate.Connections.GetTwitchConnection().Id;

        if (twitchId == BotID)
        {
            return "FailFish";
        }

        var key = SevenTVService.EditorKey(Channel.TwitchID);
        var isAlreadyEditor = await Redis.SetContainsAsync(key, twitchId);

        if (isAlreadyEditor)
        {
            try
            {
                await SevenTVService.ModifyEditorPermissions(UserID, userToMutate.Id, UserEditorPermissions.None, ct);
            }
            catch (GraphQLException ex)
            {
                return HumanizeError(ex);
            }

            await Redis.SetRemoveAsync(key, twitchId);

            return $"{userToMutate.Username} is no longer an editor";
        }
        else
        {
            try
            {
                await SevenTVService.ModifyEditorPermissions(UserID, userToMutate.Id, UserEditorPermissions.Default, ct);
            }
            catch (GraphQLException ex)
            {
                return HumanizeError(ex);
            }

            await Redis.SetAddAsync(key, twitchId);

            return $"{userToMutate.Username} is now an editor";
        }
    }

    public override ValueTask<List<string>> GenerateWebsiteDescription(string prefix, CancellationToken ct)
    {
        List<string> strings = new()
        {
            "This command allows the broadcaster to add and remove users as 7TV editors",
            "",
            $"**Usage**: {prefix}editor <username>",
            $"**Example**: {prefix}editor forsen",
            "",
            "",
            "Required 7TV Flags",
            "Manage Editors",
        };

        return ValueTask.FromResult(strings);
    }
}
