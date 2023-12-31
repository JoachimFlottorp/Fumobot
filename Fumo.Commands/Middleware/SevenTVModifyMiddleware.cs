using Fumo.Shared.Interfaces.Command;
using Fumo.Shared.Models;

namespace Fumo.Commands.Middleware;

public class SevenTVModifyMiddleware : ICommandMiddleware
{
    public SevenTVModifyMiddleware()
    {
    }

    public ValueTask<string?> Check(ChatCommand command, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }
}
