using Fumo.Shared.Models;

namespace Fumo.Shared.Interfaces.Command;

/// <summary>
/// Middleware called before the command is executed.
/// <br />
/// Can be registered in the command constructor with <see cref="ChatCommand.AddMiddleware{T}"/>
/// </summary>
public interface ICommandMiddleware
{
    /// <returns>
    /// Returns null if the command can be executed, otherwise returns a string the message to send to chat.
    /// </returns>
    ValueTask<string?> Check(ChatCommand command, CancellationToken ct = default);
}
