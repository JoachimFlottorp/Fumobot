using Autofac;
using Fumo.Database;
using Fumo.Database.DTO;
using Fumo.Database.Extensions;
using Fumo.Exceptions;
using Fumo.Interfaces;
using Fumo.Models;
using Fumo.Repository;
using Fumo.Shared.Regexes;
using Fumo.ThirdParty.ThreeLetterAPI;
using Fumo.ThirdParty.ThreeLetterAPI.Instructions;
using Fumo.ThirdParty.ThreeLetterAPI.Response;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MiniTwitch.Irc;
using Serilog;
using System.Text.RegularExpressions;

namespace Fumo.Commands;

internal partial class JoinCommand : ChatCommand
{
    [GeneratedRegex("(re)?join")]
    public override partial Regex NameRegex();

    public ILogger Logger { get; }

    public DatabaseContext Database { get; }

    public IrcClient Irc { get; }

    public IChannelRepository ChannelRepository { get; }

    public IThreeLetterAPI ThreeLetterAPI { get; }

    public IUserRepository UserRepository { get; }

    private readonly string BotID;

    public JoinCommand()
    {
        SetDescription("Allow the bot to join you or a channel you mod");
        SetCooldown(TimeSpan.FromMinutes(1));
    }

    public JoinCommand(
        ILogger logger,
        DatabaseContext database,
        IConfiguration config,
        IrcClient irc,
        IChannelRepository channelRepository,
        IThreeLetterAPI threeLetterAPI,
        IUserRepository userRepository) : this()
    {
        Logger = logger.ForContext<JoinCommand>();
        Database = database;
        Irc = irc;
        ChannelRepository = channelRepository;
        ThreeLetterAPI = threeLetterAPI;
        UserRepository = userRepository;
        BotID = config["Twitch:UserID"] ?? throw new ArgumentException("missing Twitch:UserID config");
    }

    private async Task<bool> IsMod(UserDTO user, string channelName, CancellationToken ct)
    {
        var list = await this.ThreeLetterAPI.PaginatedQueryAsync<ChannelModsResponse>(resp =>
        {
            if (resp is null)
            {
                return new ChannelModsInstruction(login: channelName);
            }
            if (resp.User.Mods.PageInfo.HasNextPage)
            {
                var latestCursor = resp.User.Mods.Edges[resp.User.Mods.Edges.Count - 1].Cursor;

                return new ChannelModsInstruction(login: channelName, cursor: latestCursor);
            }

            return null;

        }, ct);

        // Sure it might be slow on a big channel but who cares
        var isMod = list.Any(x => x.User.Mods.Edges.Any(x => x.Node.Id == user.TwitchID));

        return isMod;
    }

    public override async ValueTask<CommandResult> Execute(CancellationToken ct)
    {
        if (Channel.TwitchID != this.BotID)
        {
            return string.Empty;
        }

        var other = false;
        var userToJoin = User;
        var otherUser = Input.ElementAtOrDefault(0) ?? "";
        var username = UsernameCleanerRegex.CleanUsername(otherUser);


        if (!string.IsNullOrEmpty(otherUser) && User.TwitchName != username)
        {
            var isMod = await this.IsMod(User, username, ct) || (User.HasPermission("admin.*"));

            if (!isMod)
            {
                return "You're not a mod in that channel 🤨";
            }

            try
            {
                userToJoin = await this.UserRepository.SearchNameAsync(username, ct);
                other = true;
            }
            catch (UserNotFoundException ex)
            {
                return ex.Message;
            }
            catch (Exception ex)
            {
                this.Logger.Error(ex, string.Empty);

                return "Something went wrong";
            }
        }

        var existingChannel = await this.Database.Channels
            .Where(x => x.TwitchID == userToJoin.TwitchID)
            .SingleOrDefaultAsync(ct);

        if (existingChannel is ChannelDTO channel)
        {
            var possesivePronoun = other ? "their" : "your";

            if (CommandInvocationName == "rejoin")
            {
                if (channel.SetForDeletion)
                {
                    return "That channel was recently removed, please wait an hour or so before trying to 'join' again";
                }

                await this.Irc.PartChannel(username, ct);
                await this.Irc.JoinChannel(username, ct);

                return $"I tried to rejoin {possesivePronoun} channel.";
            }
            else
            {
                return $"I am already in {possesivePronoun} channel.";
            }
        }

        var pronoun = other ? $"{userToJoin.TwitchName}'s" : "your";

        // TODO: Add website or smth here.
        var response = $"Joining {pronoun} channel :) ";
        var newChatReply = "FeelsDankMan 👋 Hi.";
        if (other)
        {
            newChatReply += $" I was added by {User.TwitchName}";
        }

        try
        {
            ChannelDTO newChannel = new()
            {
                TwitchID = userToJoin.TwitchID,
                TwitchName = userToJoin.TwitchName,
                UserTwitchID = userToJoin.TwitchID,
            };

            await ChannelRepository.Create(newChannel, ct);

            await this.Irc.JoinChannel(newChannel.TwitchName, ct);
            await this.Irc.SendMessage(newChannel.TwitchName, newChatReply, cancellationToken: ct);

            return response;
        }
        catch (Exception ex)
        {
            this.Logger.Error(ex, "Failed to join {Channel}", userToJoin.TwitchID);

            return "An error occured joining that channel";
        }
    }
}
