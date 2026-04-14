using Microsoft.Extensions.Options;
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

            var result = await terminalService.ExecuteByAlias(alias);

            var cleanedOutput = Regex.Replace(result.Output, @"%[0-9]+", "");

            cleanedOutput = cleanedOutput.Trim();

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
                _logger.LogError(ex, "Error sending formatted response");
                await _botClient.SendMessage(chatId, "⚠️ <b>Error:</b> Failed to format terminal output.");
            }
        }
    }

    public Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Telegram Bot API Error");
        return Task.CompletedTask;
    }
}