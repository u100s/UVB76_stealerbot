using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using UVBStealer;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton<StorageDb>();
builder.Services.AddSingleton<MessageRecorder>();
builder.Services.AddSingleton<BotSender>();
builder.Services.AddSingleton<TelegramClientService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<TelegramClientService>());
builder.Services.AddSingleton<ChannelListener>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<ChannelListener>());
builder.Services.AddHostedService<BotCommandHandler>();
builder.Services.AddSingleton<MemeSender>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<MemeSender>());

var host = builder.Build();

var storageDb = host.Services.GetRequiredService<StorageDb>();
await storageDb.InitializeAsync();

await host.RunAsync();
