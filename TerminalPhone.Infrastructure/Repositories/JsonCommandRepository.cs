using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using TerminalPhone.Core.Entities;
using TerminalPhone.Core.Enums;
using TerminalPhone.Core.Interfaces;

namespace TerminalPhone.Infrastructure.Repositories;

public class JsonCommandRepository(string fileName, ILogger<JsonCommandRepository> logger) : ICommandRepository
{
    private readonly List<TerminalCommand> _commands = [];
    private readonly string _path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);

    public async Task InitializeAsync()
    {
        if (!File.Exists(_path))
        {
            logger.LogError("Command file not found: {Path}", _path);
            return;
        }

        try
        {
            var json = await File.ReadAllTextAsync(_path);

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            options.Converters.Add(new JsonStringEnumConverter());

            var dtos = JsonSerializer.Deserialize<List<CommandDto>>(json, options);

            if (dtos != null)
            {
                _commands.Clear();
                foreach (var dto in dtos)
                {
                    _commands.Add(new TerminalCommand(
                        dto.Alias,
                        dto.Script,
                        dto.Environment,
                        dto.Description,
                        dto.TimeoutSeconds,
                        dto.ResponseStrategy));
                }
                logger.LogInformation("{Count} commands loaded successfully.", _commands.Count);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load commands from {Path}", _path);
        }
    }

    public Task<TerminalCommand?> GetByAliasAsync(string alias)
    {
        var cmd = _commands.FirstOrDefault(c => c.Alias.Equals(alias.Trim(), StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(cmd);
    }

    public Task<IEnumerable<TerminalCommand>> GetAllAsync() => Task.FromResult(_commands.AsEnumerable());

    private class CommandDto
    {
        public string Alias { get; set; } = "";
        public string Script { get; set; } = "";
        public ExecutionEnvironment Environment { get; set; }
        public string Description { get; set; } = "";
        public int? TimeoutSeconds { get; set; }
        public ResponseStrategy ResponseStrategy { get; set; } = ResponseStrategy.Verbose;
    }
}