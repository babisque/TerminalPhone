using System.Text.Json;
using System.Text.Json.Serialization;
using TerminalPhone.Core.Entities;
using TerminalPhone.Core.Enums;
using TerminalPhone.Core.Interfaces;

namespace TerminalPhone.Infrastructure.Repositories;

public class JsonCommandRepository : ICommandRepository
{
    private readonly List<TerminalCommand> _commands = [];

    public JsonCommandRepository(string fileName)
    {
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);

        if (!File.Exists(path))
        {
            Console.WriteLine($"[ERROR] Command file not found: {path}");
            return;
        }

        try
        {
            var json = File.ReadAllText(path);

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            options.Converters.Add(new JsonStringEnumConverter());

            var dtos = JsonSerializer.Deserialize<List<CommandDto>>(json, options);

            if (dtos != null)
            {
                foreach (var dto in dtos)
                {
                    _commands.Add(new TerminalCommand(
                        dto.Alias,
                        dto.Script,
                        dto.Environment,
                        dto.Description));
                }
                Console.WriteLine($"[INFO] {_commands.Count} commands loaded successfully.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to load commands: {ex.Message}");
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
    }
}