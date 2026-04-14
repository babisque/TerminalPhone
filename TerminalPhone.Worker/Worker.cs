using System.Reflection.Metadata;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;
using TerminalPhone.Application.Services;
using TerminalPhone.Worker.Handlers;

namespace TerminalPhone.Worker;

public class Worker(ITelegramBotClient botClient, TelegramBotHandler handler) : BackgroundService
{
   protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = Array.Empty<UpdateType>()
        };

        botClient.StartReceiving(
            updateHandler: handler.HandleUpdateAsync,
            errorHandler: handler.HandleErrorAsync,
            receiverOptions: receiverOptions,
            cancellationToken: stoppingToken);

        Console.WriteLine("Bot started.");

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
}
