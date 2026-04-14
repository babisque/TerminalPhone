using TerminalPhone.Core.Entities;

namespace TerminalPhone.Core.Interfaces;

public interface ICommandRepository
{
    Task<TerminalCommand?> GetByAliasAsync(string alias);
    Task<IEnumerable<TerminalCommand>> GetAllAsync();
}
