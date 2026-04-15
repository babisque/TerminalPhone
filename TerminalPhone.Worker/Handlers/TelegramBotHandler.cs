using Microsoft.Extensions.Options;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TerminalPhone.Application.DTOs;
using TerminalPhone.Application.Services;
using TerminalPhone.Core.Entities;
using TerminalPhone.Core.Enums;

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

            var (isValid, command) = await terminalService.GetCommandByAlias(alias);

            if (!isValid || command is null)
            {
                await _botClient.SendMessage(chatId, "❌ Invalid command or unauthorized.", cancellationToken: cancellationToken);
                return;
            }

            _logger.LogInformation("Command received: {Alias} with Strategy: {Strategy}", alias, command.Strategy);

            switch (command.Strategy)
            {
                case ResponseStrategy.SummaryOnly:
                    await HandleSummaryOnlyStrategy(chatId, command, terminalService, cancellationToken);
                    break;
                case ResponseStrategy.MultiStep:
                    await HandleMultiStepStrategy(chatId, command, terminalService, cancellationToken);
                    break;
                case ResponseStrategy.Verbose:
                default:
                    await HandleVerboseStrategy(chatId, command, terminalService, cancellationToken);
                    break;
            }
        }
    }

    private async Task HandleVerboseStrategy(long chatId, TerminalCommand command, TerminalApplicationService service, CancellationToken ct)
    {
        var processingMsg = await _botClient.SendMessage(
            chatId: chatId,
            text: $"⏳ <code>Executing: {command.Alias}...</code>",
            parseMode: ParseMode.Html,
            cancellationToken: ct);

        var liveOutput = new StringBuilder();
        var lastUpdate = DateTime.UtcNow;

        var result = await service.ExecuteCommand(command, async (line) =>
        {
            liveOutput.AppendLine(line);
            if ((DateTime.UtcNow - lastUpdate).TotalSeconds > 2)
            {
                await UpdateLiveMessage(chatId, processingMsg.MessageId, command.Alias, liveOutput.ToString());
                lastUpdate = DateTime.UtcNow;
            }
        });

        await SendFinalResponse(chatId, processingMsg.MessageId, command.Alias, result, ct);
    }

    private async Task HandleSummaryOnlyStrategy(long chatId, TerminalCommand command, TerminalApplicationService service, CancellationToken ct)
    {
        var processingMsg = await _botClient.SendMessage(
            chatId: chatId,
            text: $"⏳ <code>Executing {command.Alias} (Summary Mode)...</code>",
            parseMode: ParseMode.Html,
            cancellationToken: ct);

        var result = await service.ExecuteCommand(command);

        string summary = result.Output;
        if (command.Alias.Contains("ping"))
        {
            var match = Regex.Match(result.Output, @"(Ping statistics for.*|Approximate round trip times.*)", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            if (match.Success)
            {
                summary = match.Value;
            }
        }

        var finalResult = new CommandResultDto(result.Alias, summary, result.Success, result.ExecutedAt);
        await SendFinalResponse(chatId, processingMsg.MessageId, command.Alias, finalResult, ct);
    }

    private async Task HandleMultiStepStrategy(long chatId, TerminalCommand command, TerminalApplicationService service, CancellationToken ct)
    {
        if (command.Alias == "update_all")
        {
            await HandleUpdateAllMultiStep(chatId, command, service, ct);
        }
        else
        {
            // Fallback to verbose if strategy not specifically implemented
            await HandleVerboseStrategy(chatId, command, service, ct);
        }
    }

    private async Task HandleUpdateAllMultiStep(long chatId, TerminalCommand command, TerminalApplicationService service, CancellationToken ct)
    {
        var step1Msg = await _botClient.SendMessage(chatId, "🔍 <b>Phase 1:</b> Checking for available updates...", ParseMode.Html, cancellationToken: ct);

        // Phase 1: List updates
        var listCommand = new TerminalCommand("winget_list", "winget upgrade", command.Environment, "List winget updates", 60);
        var listResult = await service.ExecuteCommand(listCommand);

        if (!listResult.Success || string.IsNullOrWhiteSpace(listResult.Output) || listResult.Output.Contains("No installed package has an available upgrade"))
        {
            await _botClient.EditMessageText(chatId, step1Msg.MessageId, "✅ No updates available.", parseMode: ParseMode.Html, cancellationToken: ct);
            return;
        }

        // Phase 2: Show updates
        var updates = HttpUtility.HtmlEncode(listResult.Output);
        await _botClient.EditMessageText(chatId, step1Msg.MessageId, $"📦 <b>Phase 2:</b> Updates found:\n<pre>{updates}</pre>\n\n🚀 Starting upgrade...", parseMode: ParseMode.Html, cancellationToken: ct);

        // Phase 3: Actual upgrade
        var upgradeResult = await service.ExecuteCommand(command);

        // Phase 4: Final response
        var responseEmoji = upgradeResult.Success ? "✅" : "❌";
        var finalResponse = $"<b>{responseEmoji} Phase 3 & 4:</b> Upgrade complete.\n\n" +
                            $"<pre>{HttpUtility.HtmlEncode(upgradeResult.Output)}</pre>";

        await _botClient.SendMessage(chatId, finalResponse, ParseMode.Html, cancellationToken: ct);
    }

    private async Task SendFinalResponse(long chatId, int processingMessageId, string alias, CommandResultDto result, CancellationToken ct)
    {
        var cleanedOutput = Regex.Replace(result.Output, @"%[0-9]+", "").Trim();
        var encodedOutput = HttpUtility.HtmlEncode(cleanedOutput);
        var responseEmoji = result.Success ? "✅" : "❌";

        var responseText = $"<b>{responseEmoji} Result for:</b> <code>{alias}</code>\n\n" +
                           $"<pre>{encodedOutput}</pre>";

        try
        {
            await _botClient.DeleteMessage(chatId, processingMessageId, ct);
            await _botClient.SendMessage(
                chatId: chatId,
                text: responseText,
                parseMode: ParseMode.Html,
                cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending final response");
            await _botClient.SendMessage(chatId, "⚠️ <b>Error:</b> Failed to deliver command output.", cancellationToken: ct);
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
        catch (ApiRequestException ex)
        {
            _logger.LogWarning("Telegram API Rate Limit or update conflict during live update: {Message}", ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during live update for command {Alias}", alias);
        }
    }

    public Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Telegram Bot API Error");
        return Task.CompletedTask;
    }
}
