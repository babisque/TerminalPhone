using Microsoft.Extensions.Options;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TerminalPhone.Application.Services;
using TerminalPhone.Core.Entities;

namespace TerminalPhone.Worker.Handlers;

public class TelegramBotHandler
{
    private readonly ITelegramBotClient _botClient;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TelegramBotHandler> _logger;
    private readonly TelegramOptions _options;

    public TelegramBotHandler(
        ITelegramBotClient botClient,
        IServiceScopeFactory scopeFactory,
        ILogger<TelegramBotHandler> logger,
        IOptions<TelegramOptions> options)
    {
        _botClient = botClient;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
    }

    public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        // 1. Security Check via IOptions
        if (update.Message?.From?.Id != _options.AdminId) return;

        if (update.Type != UpdateType.Message || update.Message?.Text is not { } messageText)
            return;

        using (var scope = _scopeFactory.CreateScope())
        {
            var terminalService = scope.ServiceProvider.GetRequiredService<TerminalApplicationService>();
            var chatId = update.Message.Chat.Id;
            var alias = messageText.Replace("/", "").Trim().ToLower();

            _logger.LogInformation("Command received: {Alias}", alias);

            var processingMsg = await _botClient.SendMessage(
                chatId: chatId,
                text: $"⏳ <code>Executing: {alias}...</code>",
                parseMode: ParseMode.Html,
                cancellationToken: cancellationToken);

            var liveOutput = new StringBuilder();
            var lastUpdate = DateTime.UtcNow;

            var result = await terminalService.ExecuteByAlias(alias, async (line) =>
            {
                liveOutput.AppendLine(line);

                // Throttle updates to every 2 seconds to avoid Telegram Rate Limits
                if ((DateTime.UtcNow - lastUpdate).TotalSeconds > 2)
                {
                    await UpdateLiveMessage(chatId, processingMsg.MessageId, alias, liveOutput.ToString());
                    lastUpdate = DateTime.UtcNow;
                }
            });

            var cleanedOutput = Regex.Replace(result.Output, @"%[0-9]+", "").Trim();
            var encodedOutput = HttpUtility.HtmlEncode(cleanedOutput);
            var responseEmoji = result.Success ? "✅" : "❌";

            var responseText = $"<b>{responseEmoji} Result for:</b> <code>{alias}</code>\n\n" +
                               $"<pre>{encodedOutput}</pre>";

            try
            {
                await _botClient.DeleteMessage(chatId, processingMsg.MessageId, cancellationToken);
                await _botClient.SendMessage(
                    chatId: chatId,
                    text: responseText,
                    parseMode: ParseMode.Html,
                    cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending final response");
                await _botClient.SendMessage(chatId, "⚠️ <b>Error:</b> Failed to send terminal output.");
            }
        }
    }

    private async Task UpdateLiveMessage(long chatId, int messageId, string alias, string currentOutput)
    {
        try
        {
            var encoded = HttpUtility.HtmlEncode(currentOutput);
            var text = $"⏳ <b>Executing:</b> <code>{alias}</code>\n\n<pre>{encoded}</pre>";

            await _botClient.EditMessageText(chatId, messageId, text, parseMode: ParseMode.Html);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to update live log: {Message}", ex.Message);
        }
    }

    public Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Telegram Bot API Error");
        return Task.CompletedTask;
    }
}