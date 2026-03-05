using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using UVBStoler;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddHttpClient("Poller", client =>
{
    client.DefaultRequestHeaders.UserAgent.ParseAdd(
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
});

builder.Services.AddSingleton<BotSender>();
builder.Services.AddSingleton<ChannelPoller>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<ChannelPoller>());
builder.Services.AddHostedService<BotCommandHandler>();

var host = builder.Build();
await host.RunAsync();
