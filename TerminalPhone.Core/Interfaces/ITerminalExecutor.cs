using TerminalPhone.Core.Entities;

namespace TerminalPhone.Core.Interfaces;

public interface ITerminalExecutor
{
    Task<ExecutionResponse> ExecuteAsync(TerminalCommand command, Action<string>? onOutputReceived = null);
}

public record ExecutionResponse(string Output, int ExitCode);