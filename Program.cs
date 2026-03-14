using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using UVBStealer;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddHttpClient("Poller", client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
    client.DefaultRequestHeaders.UserAgent.ParseAdd(
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36");
    client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
    client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
}).ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
{
    PooledConnectionLifetime = TimeSpan.FromMinutes(2),
});

builder.Services.AddSingleton<StorageDb>();
builder.Services.AddSingleton<MessageRecorder>();
builder.Services.AddSingleton<BotSender>();
builder.Services.AddSingleton<ChannelPoller>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<ChannelPoller>());
builder.Services.AddHostedService<BotCommandHandler>();
builder.Services.AddSingleton<MemeSender>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<MemeSender>());

var host = builder.Build();

var storageDb = host.Services.GetRequiredService<StorageDb>();
await storageDb.InitializeAsync();

await host.RunAsync();
