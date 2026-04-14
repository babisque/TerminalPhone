using TerminalPhone.Application.DTOs;
using TerminalPhone.Core.Interfaces;
using TerminalPhone.Core.Services;

namespace TerminalPhone.Application.Services;

public class TerminalApplicationService
{
    private readonly CommandExecutionGuard _guard;
    private readonly ITerminalExecutor _executor;

    public TerminalApplicationService(CommandExecutionGuard guard, ITerminalExecutor executor)
    {
        _guard = guard;
        _executor = executor;
    }

    public async Task<CommandResultDto> ExecuteByAlias(string alias, Action<string>? onOutputReceived = null)
    {
        var (isValid, command) = await _guard.ValidateRequest(alias);

        if (!isValid || command is null) return new CommandResultDto
        (
            alias,
            "Invalid command or insufficient permissions.",
            false,
            DateTime.UtcNow
        );

        var result = await _executor.ExecuteAsync(command, onOutputReceived);

        return new CommandResultDto
        (
            command.Alias,
            result.Output,
            result.ExitCode == 0,
            DateTime.UtcNow
        );
    }
}