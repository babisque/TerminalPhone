using TerminalPhone.Core.Entities;
using TerminalPhone.Core.Interfaces;

namespace TerminalPhone.Core.Services;

public class CommandExecutionGuard
{
    private readonly ICommandRepository _repository;

    public CommandExecutionGuard(ICommandRepository repository)
    {
        _repository = repository;
    }

    public async Task<(bool IsValid, TerminalCommand? Command)> ValidateRequest(string requestedAlias)
    {
        var command = await _repository.GetByAliasAsync(requestedAlias);

        if (command is null) return (false, null);

        return (true, command);
    }
}
