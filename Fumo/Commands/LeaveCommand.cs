using Fumo.Enums;
using Fumo.Models;
using Fumo.Repository;
using MiniTwitch.Irc;
using Serilog;
using System.Text.RegularExpressions;

namespace Fumo.Commands;

internal partial class LeaveCommand : ChatCommand
{
    [GeneratedRegex("leave|part")]
    public override partial Regex NameRegex();

    public LeaveCommand()
    {
        SetFlags(ChatCommandFlags.BroadcasterOnly);
    }

    public ILogger Logger { get; }

    public IChannelRepository ChannelRepository { get; }

    public IrcClient IrcClient { get; }

    public LeaveCommand(ILogger logger, IChannelRepository channelRepository, IrcClient ircClient) : this()
    {
        Logger = logger.ForContext<LeaveCommand>();
        ChannelRepository = channelRepository;
        IrcClient = ircClient;
    }


    public override async ValueTask<CommandResult> Execute(CancellationToken ct)
    {
        try
        {
            await ChannelRepository.Delete(Channel, ct);

            await this.IrcClient.PartChannel(Channel.TwitchName, ct);
        }
        catch (Exception ex)
        {
            this.Logger.Error(ex, "Failed to leave {Channel}", Channel.TwitchName);
            return "An error occured, try again later";
        }

        return "👍";
    }
}
