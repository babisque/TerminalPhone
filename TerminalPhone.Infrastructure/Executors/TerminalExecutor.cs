using System.Diagnostics;
using TerminalPhone.Core.Entities;
using TerminalPhone.Core.Enums;
using TerminalPhone.Core.Interfaces;

namespace TerminalPhone.Infrastructure.Executors;

public class TerminalExecutor : ITerminalExecutor
{
    public async Task<ExecutionResponse> ExecuteAsync(TerminalCommand command)
    {
        return command.Environment switch
        {
            ExecutionEnvironment.Windows => await ExecuteWindows(command.Script.Value),
            ExecutionEnvironment.ArchLinux => await ExecuteWsl(command.Script.Value),
            _ => throw new NotSupportedException($"Execution environment '{command.Environment}' is not supported.")
        };
    }

    private async Task<ExecutionResponse> ExecuteWindows(string script)
    {
        return await RunProcess("cmd.exe", $"/c {script}");
    }

    private async Task<ExecutionResponse> ExecuteWsl(string script)
    {
        return await RunProcess("wsl.exe", $"-d Arch -u d0c -e {script}");
    }

    private async Task<ExecutionResponse> RunProcess(string fileName, string arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        string output = await process.StandardOutput.ReadToEndAsync();
        string error = await process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        var finalOutput = string.IsNullOrWhiteSpace(error) ? output : $"{output}\n{error}".Trim();

        return new ExecutionResponse(finalOutput.Trim(), process.ExitCode);
    }
}
