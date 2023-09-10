using Fumo.Database;
using Fumo.Database.Extensions;
using Fumo.Exceptions;
using Fumo.Extensions;
using Fumo.Interfaces;
using Fumo.Models;
using Fumo.Shared.Utils;
using Fumo.ThirdParty;
using Fumo.ThirdParty.Emotes.SevenTV;
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace Fumo.Commands.SevenTV;

internal partial class SevenTVYoinkCommand : ChatCommand
{
    private static readonly char[] ChannelPrefixes = new[] { '@', '#' };

    [GeneratedRegex("(7tv)?yoink|steal")]
    public override partial Regex NameRegex();

    public ISevenTVService SevenTVService { get; }
    public IDatabase Redis { get; }
    public IMessageSenderHandler MessageSender { get; }
    public IUserRepository UserRepository { get; }

    private string BotID { get; }

    public SevenTVYoinkCommand()
    {
        SetDescription("Yoink emotes from another channel");

        AddParameter(new(typeof(bool), "alias"));
    }

    public SevenTVYoinkCommand(
        ISevenTVService sevenTVService,
        IDatabase redis,
        IMessageSenderHandler messageSender,
        IConfiguration configuration,
        IUserRepository userRepository) : this()
    {
        SevenTVService = sevenTVService;
        Redis = redis;
        MessageSender = messageSender;
        UserRepository = userRepository;

        BotID = configuration["Twitch:UserID"]!;
    }

    private async Task<string> ConvertToEmoteSet(string channel, CancellationToken ct)
    {
        var user = await UserRepository.SearchNameAsync(channel, ct);

        var seventvUser = await SevenTVService.GetUserInfo(user.TwitchID, ct);

        var con = seventvUser.Connections.GetTwitchConnection();

        if (string.IsNullOrEmpty(con.EmoteSetId)) throw new InvalidInputException("User or Channel does not have a 7TV account");

        return con.EmoteSetId;
    }

    public override async ValueTask<CommandResult> Execute(CancellationToken ct)
    {
        var keepAlias = GetArgument<bool>("alias");

        if (Input.Count <= 0)
        {
            throw new InvalidInputException("Provide emotes to take, if you want them taken from a different channel provide a channel name prefixed with @ or #. e.g @forsen");
        }

        var chanIdx = Input.FindIndex((x => ChannelPrefixes.Contains(x[0])));

        var emotesWant = Input.Select((emote, idx) =>
            {
                if (idx == chanIdx) return null;

                return emote;
            })
            .Where(x => x != null)
            .ToList();

        string writeChannel, readChannel;

        // No channel was given
        if (chanIdx == -1)
        {
            writeChannel = User.TwitchName;
            readChannel = Channel.TwitchName;
        }
        else
        {
            writeChannel = Channel.TwitchName;
            readChannel = Input.ElementAt(chanIdx)[1..];
        }

        if (writeChannel == readChannel)
        {
            // TODO: use sillE 
            throw new InvalidInputException("You can't steal from yourself silly");
        }

        string? readSet = null, writeSet = null;

        if (Channel.TwitchName == readChannel)
        {
            readSet = Channel.GetSetting(ChannelSettingKey.SevenTV_EmoteSet);

        }
        else if (Channel.TwitchName == writeChannel)
        {
            await SevenTVService.EnsureCanModify(BotID, Redis, Channel, User);

            writeSet = Channel.GetSetting(ChannelSettingKey.SevenTV_EmoteSet);
        }

        readSet ??= await ConvertToEmoteSet(readChannel, ct);
        writeSet ??= await ConvertToEmoteSet(writeChannel, ct);

        var toAdd = (await SevenTVService.GetEnabledEmotes(readSet, ct))
            .Where(x => emotesWant.Contains(x.Name))
            .ToList();

        if (toAdd.Count <= 0)
        {
            return "Could not find any emotes to add";
        }

        var writeChannelPrompt = Channel.TwitchName == writeChannel
            ? ""
            : $" (in #{UnpingUser.Unping(writeChannel)})";

        ConcurrentQueue<string> output = new();

        await Parallel.ForEachAsync(toAdd, ct, async (emote, pct) =>
            {
                try
                {
                    var aliasName = keepAlias ? emote.Name : null;

                    var name = await SevenTVService.ModifyEmoteSet(writeSet, ListItemAction.Add, emote.Id, aliasName, pct) ?? throw new Exception("Idk what happened");

                    output.Enqueue($"👍 Added {name} {writeChannelPrompt}");
                }
                catch (Exception ex)
                {
                    var e = emote.Name;
                    if (emote.HasAlias)
                    {
                        e += $" (alias of {emote.Name})";
                    }

                    output.Enqueue($"👎 Failed to add {e} {ex.Message} {writeChannelPrompt}");
                }
            });

        foreach (var message in output)
        {
            MessageSender.ScheduleMessage(Channel.TwitchName, message);
        }

        return string.Empty;
    }

    public override ValueTask<List<string>> GenerateWebsiteDescription(string prefix, CancellationToken ct)
    {
        List<string> strings = new()
        {
            "Steal emotes from another channel",
            "",
            $"**Usage:**: {prefix} yoink #channel <emote names>",
            $"**Example**: {prefix} yoink #pajlada WideDankCrouching",
            $"**Example**: {prefix} yoink @forsen FloppaDank FloppaL",
            $"**Example**: {prefix} yoink 30Dank @forsen",
            $"**Example**: {prefix} yoink DankG",
            "",
            "The yoink command has the ability to add emote both ways, if you do not include a channel the emotes are taken from the current channel and added to your own channel.",
            "While adding a channel e.g (@forsen) would take emotes from forsen and add them to the current channel.",
            "",
            "-a, --alias",
            "%TAB%By default emotes have their aliases removed, -a will retain the alias",
        };

        return ValueTask.FromResult(strings);
    }
}
