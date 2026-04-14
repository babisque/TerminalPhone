using Telegram.Bot;
using TerminalPhone.Application.Services;
using TerminalPhone.Core.Interfaces;
using TerminalPhone.Core.Services;
using TerminalPhone.Infrastructure.Executors;
using TerminalPhone.Infrastructure.Repositories;
using TerminalPhone.Worker;
using TerminalPhone.Worker.Handlers;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(opts =>
{
    opts.ServiceName = "Bridge Terminal Service";
});

var token = builder.Configuration["TelegramSettings:Token"];

if (string.IsNullOrEmpty(token))
    throw new InvalidOperationException("Telegram token not found in appsettings.json");

builder.Services.AddSingleton<ICommandRepository>(sp =>
    new JsonCommandRepository("commands.json"));

builder.Services.AddSingleton<ITelegramBotClient>(sp =>
    new TelegramBotClient(token));

builder.Services.AddScoped<CommandExecutionGuard>();
builder.Services.AddScoped<TerminalApplicationService>();
builder.Services.AddScoped<ITerminalExecutor, TerminalExecutor>();
builder.Services.AddSingleton<TelegramBotHandler>();
builder.Services.AddHostedService<Worker>();
var host = builder.Build();
host.Run();
