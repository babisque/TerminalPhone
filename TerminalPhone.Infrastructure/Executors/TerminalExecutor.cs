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
            ExecutionEnvironment.Windows => await ExecuteWindows(command.Script.Value, onOutputReceived),
            ExecutionEnvironment.ArchLinux => await ExecuteWsl(command.Script.Value, onOutputReceived),
            _ => throw new NotSupportedException($"Execution environment '{command.Environment}' is not supported.")
        };
    }

    private async Task<ExecutionResponse> ExecuteWindows(string script, Action<string>? onOutputReceived)
    {
        return await RunProcess("cmd.exe", $"/c {script}", onOutputReceived);
    }

    private async Task<ExecutionResponse> ExecuteWsl(string script, Action<string>? onOutputReceived)
    {
        return await RunProcess("wsl.exe", $"-d Arch -u d0c -e {script}", onOutputReceived);
    }

    private async Task<ExecutionResponse> RunProcess(string fileName, string arguments, Action<string>? onOutputReceived)
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

        await process.WaitForExitAsync();

        return new ExecutionResponse(outputBuilder.ToString().Trim(), process.ExitCode);
    }
}