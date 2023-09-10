using Fumo.Database.DTO;
using Fumo.Enums;
using Fumo.Interfaces.Command;
using Fumo.Shared.Models;
using MiniTwitch.Irc.Models;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Fumo.Models;

public abstract partial class ChatCommand : ChatCommandArguments, IChatCommand
{
    public ChannelDTO Channel { get; set; }
    public UserDTO User { get; set; }
    public List<string> Input { get; set; }
    public Privmsg Privmsg { get; set; }

    /// <summary>
    /// The command invocation is the part of the message that matches the command name
    /// </summary>
    public string CommandInvocationName
    { get; set; }

    /// <summary>
    /// Regex that matches the command
    /// </summary>
    public Regex NameMatcher { get => this.NameRegex(); }


    private ChatCommandFlags _flags = ChatCommandFlags.None;

    /// <summary>
    /// Flags that change behaviour
    /// </summary>
    public ChatCommandFlags Flags
    {
        get => _flags;
        protected set => _flags = value;
    }

    private List<string> _permissions = new() { "default" };

    /// <summary>
    /// Permissions a user requires to execute this command
    /// 
    /// Every user has "default"
    /// </summary>
    public List<string> Permissions
    {
        get => _permissions;
        protected set => _permissions = value;
    }

    private string? _description;
    public string Description
    {
        get => _description ?? "No description provided";
        protected set => _description = value;
    }

    /// <summary>
    /// If Moderators and Broadcasters are the only ones that can execute this command in a chat
    /// </summary>
    public bool ModeratorOnly => (Flags & ChatCommandFlags.ModeratorOnly) != 0;

    /// <summary>
    /// If the Broadcaster of the chat is the only one that can execute this command in a chat
    /// </summary>
    public bool BroadcasterOnly => (Flags & ChatCommandFlags.BroadcasterOnly) != 0;

    private TimeSpan _cooldown = TimeSpan.FromSeconds(5);
    public TimeSpan Cooldown
    {
        get => _cooldown;
        protected set => _cooldown = value;
    }

    public virtual Regex NameRegex() => throw new NotImplementedException();

    public virtual ValueTask<CommandResult> Execute(CancellationToken ct)
        => throw new NotImplementedException();

    public virtual ValueTask<List<string>> GenerateWebsiteDescription(string prefix, CancellationToken ct)
        => default!;

    protected void SetCooldown(TimeSpan cd)
        => this.Cooldown = cd;

    protected void SetFlags(ChatCommandFlags flags)
        => Flags = flags;

    protected void AddPermission(string permission)
        => this.Permissions.Add(permission);

    protected void SetDescription(string description)
        => this.Description = description;
}
