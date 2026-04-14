using TerminalPhone.Core.Enums;

namespace TerminalPhone.Core.Entities;

public class TerminalCommand
{
    public string Alias { get; private set;  }
    public CommandScript Script { get; private set; }
    public ExecutionEnvironment Environment { get; private set; }
    public string Description { get; private set; }

    public TerminalCommand(string alias, string script, ExecutionEnvironment environment, string description)
    {
        if (string.IsNullOrWhiteSpace(alias))
            throw new ArgumentException("Alias cannot be null or whitespace.", nameof(alias));

        Alias = alias.ToLower().Trim();
        Script = new CommandScript(script);
        Environment = environment;
        Description = description;
    }
}
