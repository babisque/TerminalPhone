using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TerminalPhone.Core.Interfaces;
using TerminalPhone.Worker.Handlers;

namespace TerminalPhone.Worker;

public class Worker(
    ITelegramBotClient botClient,
    TelegramBotHandler handler,
    ILogger<Worker> logger,
    ICommandRepository commandRepository) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await commandRepository.InitializeAsync();
        
        var commands = await commandRepository.GetAllAsync();
        var botCommands = commands.Select(c => new BotCommand
        {
            Command = c.Alias,
            Description = c.Description
        });

        await botClient.SetMyCommands(botCommands, cancellationToken: stoppingToken);

        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = Array.Empty<UpdateType>()
        };

        botClient.StartReceiving(
            updateHandler: handler.HandleUpdateAsync,
            errorHandler: handler.HandleErrorAsync,
            receiverOptions: receiverOptions,
            cancellationToken: stoppingToken);

        logger.LogInformation("Bot started with Slash Commands.");

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
}