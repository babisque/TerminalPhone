using TerminalPhone.Core.Enums;

namespace TerminalPhone.Core.Entities;

public class TerminalCommand(
    string alias,
    string script,
    ExecutionEnvironment environment,
    string description,
    int? timeoutSeconds = null,
    ResponseStrategy responseStrategy = ResponseStrategy.Verbose)
{
    public string Alias { get; private set; } = string.IsNullOrWhiteSpace(alias) 
        ? throw new ArgumentException("Alias cannot be null or whitespace.", nameof(alias)) 
        : alias.ToLower().Trim();
    public CommandScript Script { get; private set; } = new(script);
    public ExecutionEnvironment Environment { get; private set; } = environment;
    public string Description { get; private set; } = description;
    public int? TimeoutSeconds { get; private set; } = timeoutSeconds;
    public ResponseStrategy ResponseStrategy { get; private set; } = responseStrategy;
}
