using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TL;

namespace UVBStealer;

public class ChannelListener : BackgroundService
{
    private readonly TelegramClientService _telegramClient;
    private readonly BotSender _botSender;
    private readonly ILogger<ChannelListener> _logger;
    private readonly string _channelUsername;
    private readonly List<string> _recentWords = new();
    private readonly object _recentLock = new();
    private long _channelId;

    public ChannelListener(
        TelegramClientService telegramClient,
        BotSender botSender,
        IConfiguration config,
        ILogger<ChannelListener> logger)
    {
        _telegramClient = telegramClient;
        _botSender = botSender;
        _logger = logger;
        _channelUsername = config["TelegramClient:ChannelUsername"] ?? "uvb76logs";
    }

    public List<string> DrainRecentWords()
    {
        lock (_recentLock)
        {
            var result = _recentWords.ToList();
            _recentWords.Clear();
            return result;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ChannelListener waiting for Telegram client...");
        await _telegramClient.Ready;

        var client = _telegramClient.Client;

        var resolved = await client.Contacts_ResolveUsername(_channelUsername);
        if (resolved.Chat is not Channel channel)
        {
            _logger.LogError("Could not resolve channel @{Username}", _channelUsername);
            return;
        }

        _channelId = channel.id;
        _logger.LogInformation("Subscribed to channel @{Username} (id: {Id})", _channelUsername, _channelId);

        client.OnUpdates += OnUpdates;

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
        }

        client.OnUpdates -= OnUpdates;
        _logger.LogInformation("ChannelListener stopped");
    }

    private async Task OnUpdates(UpdatesBase updates)
    {
        foreach (var update in updates.UpdateList)
        {
            if (update is not UpdateNewChannelMessage { message: Message message })
                continue;

            if (message.peer_id is not PeerChannel peerChannel || peerChannel.channel_id != _channelId)
                continue;

            if (string.IsNullOrEmpty(message.message))
                continue;

            _logger.LogInformation("New channel message: {Text}", message.message);

            var words = MessageParser.ExtractWords(message.message);
            if (words.Count == 0)
                continue;

            _logger.LogInformation("Extracted words: {Words}", string.Join(", ", words));

            lock (_recentLock)
                _recentWords.AddRange(words);

            var text = string.Join(" ", words);
            try
            {
                await _botSender.SendWordAsync(text);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send words '{Words}'", text);
            }
        }
    }
}
