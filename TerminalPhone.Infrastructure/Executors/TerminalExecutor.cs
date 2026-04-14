using System.Diagnostics;
using System.Text;
using TerminalPhone.Core.Entities;
using TerminalPhone.Core.Enums;
using TerminalPhone.Core.Interfaces;

namespace TerminalPhone.Infrastructure.Executors;

public class TerminalExecutor : ITerminalExecutor
{
    public async Task<ExecutionResponse> ExecuteAsync(TerminalCommand command, Action<string>? onOutputReceived = null)
    {
        return command.Environment switch
        {
            ExecutionEnvironment.Windows => await ExecuteWindows(command, onOutputReceived),
            ExecutionEnvironment.ArchLinux => await ExecuteWsl(command, onOutputReceived),
            _ => throw new NotSupportedException($"Execution environment '{command.Environment}' is not supported.")
        };
    }

    private async Task<ExecutionResponse> ExecuteWindows(TerminalCommand command, Action<string>? onOutputReceived)
    {
        return await RunProcess("cmd.exe", $"/c {command.Script.Value}", onOutputReceived, command.TimeoutSeconds);
    }

    private async Task<ExecutionResponse> ExecuteWsl(TerminalCommand command, Action<string>? onOutputReceived)
    {
        return await RunProcess("wsl.exe", $"-d Arch -u d0c -e {command.Script.Value}", onOutputReceived, command.TimeoutSeconds);
    }

    private async Task<ExecutionResponse> RunProcess(string fileName, string arguments, Action<string>? onOutputReceived, int? timeoutOverride = null)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
        };

        using var process = new Process { StartInfo = startInfo };
        var outputBuilder = new StringBuilder();

        process.OutputDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                outputBuilder.AppendLine(e.Data);
                onOutputReceived?.Invoke(e.Data);
            }
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                outputBuilder.AppendLine(e.Data);
                onOutputReceived?.Invoke(e.Data);
            }
        };

        process.Start();

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        int timeoutSeconds = timeoutOverride ?? 30;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));

        try
        {
            await process.WaitForExitAsync(cts.Token);
            return new ExecutionResponse(outputBuilder.ToString().Trim(), process.ExitCode);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            var timeoutMessage = $"Command timed out after {timeoutSeconds} seconds.";
            return new ExecutionResponse(timeoutMessage, -1);
        }
    }
}
