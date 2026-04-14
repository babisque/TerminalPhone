using TerminalPhone.Core.Entities;

namespace TerminalPhone.Core.Interfaces;

public interface ITerminalExecutor
{
    Task<ExecutionResponse> ExecuteAsync(TerminalCommand command);
}

public record ExecutionResponse(string Output, int ExitCode);