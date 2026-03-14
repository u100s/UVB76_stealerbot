using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace UVBStealer;

public class ChannelPoller : BackgroundService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly BotSender _botSender;
    private readonly ILogger<ChannelPoller> _logger;
    private readonly string _sourceUrl;
    private readonly int _intervalSeconds;
    private readonly HashSet<string> _seenPostIds = new();
    private readonly List<string> _recentWords = new();
    private readonly object _recentLock = new();
    private readonly Random _random = new();
    private bool _initialSeedDone;
    private int _consecutiveErrors;

    public ChannelPoller(
        IHttpClientFactory httpClientFactory,
        BotSender botSender,
        IConfiguration config,
        ILogger<ChannelPoller> logger)
    {
        _httpClientFactory = httpClientFactory;
        _botSender = botSender;
        _logger = logger;

        _sourceUrl = config["Poller:SourceUrl"] ?? "https://t.me/s/uvb76logs";
        _intervalSeconds = config.GetValue("Poller:IntervalSeconds", 60);
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
        _logger.LogInformation("ChannelPoller started. Polling {Url} every ~{Interval}s",
            _sourceUrl, _intervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollAsync(stoppingToken);
                _consecutiveErrors = 0;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _consecutiveErrors++;
                _logger.LogWarning(ex, "Polling error #{Count}", _consecutiveErrors);
            }

            // Exponential backoff on errors, capped at 10 min; wide jitter (±50%) to look less like a bot
            var backoff = _consecutiveErrors > 0
                ? Math.Min(_intervalSeconds * Math.Pow(2, _consecutiveErrors), 600)
                : _intervalSeconds;
            var jitter = backoff * 0.5;
            var delay = backoff + (_random.NextDouble() * 2 - 1) * jitter;
            await Task.Delay(TimeSpan.FromSeconds(delay), stoppingToken);
        }

        _logger.LogInformation("ChannelPoller stopped");
    }

    private async Task PollAsync(CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient("Poller");
        var html = await client.GetStringAsync(_sourceUrl, ct);

        _logger.LogInformation("Fetched {Length} bytes from {Url}", html.Length, _sourceUrl);

        var messages = MessageParser.ParseLatestWords(html);

        if (!_initialSeedDone)
        {
            foreach (var msg in messages)
                _seenPostIds.Add(msg.PostId);

            _initialSeedDone = true;
            _logger.LogInformation("Initial seed: {Count} messages marked as seen", _seenPostIds.Count);
            return;
        }

        foreach (var msg in messages)
        {
            if (!_seenPostIds.Add(msg.PostId))
                continue;

            _logger.LogInformation("New message #{PostId}: words = '{Words}'",
                msg.PostId, string.Join(", ", msg.Words));

            lock (_recentLock)
                _recentWords.AddRange(msg.Words);

            var text = string.Join(" ", msg.Words);
            try
            {
                await _botSender.SendWordAsync(text, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send words '{Words}'", text);
            }
        }

        if (_seenPostIds.Count > 500)
        {
            var toRemove = _seenPostIds.OrderBy(x => x).Take(_seenPostIds.Count - 500).ToList();
            foreach (var id in toRemove)
                _seenPostIds.Remove(id);
        }
    }
}
