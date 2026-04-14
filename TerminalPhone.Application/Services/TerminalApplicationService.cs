using TerminalPhone.Application.DTOs;
using TerminalPhone.Core.Entities;
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

    public async Task<(bool isValid, TerminalCommand? command)> GetCommandByAlias(string alias)
    {
        return await _guard.ValidateRequest(alias);
    }

    public async Task<CommandResultDto> ExecuteCommand(TerminalCommand command, Action<string>? onOutputReceived = null)
    {
        var result = await _executor.ExecuteAsync(command, onOutputReceived);

        return new CommandResultDto
        (
            command.Alias,
            result.Output,
            result.ExitCode == 0,
            DateTime.UtcNow
        );
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

        return await ExecuteCommand(command, onOutputReceived);
    }
}