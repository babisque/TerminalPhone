namespace TerminalPhone.Core.Entities;

public class TelegramOptions
{
    public const string SectionName = "TelegramSettings";
    public string Token { get; set; } = string.Empty;
    public long AdminId {  get; set; }
}
