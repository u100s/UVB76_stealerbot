using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace UVBStealer;

public class MemeSender : BackgroundService
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp"
    };

    private readonly BotSender _botSender;
    private readonly ILogger<MemeSender> _logger;
    private readonly string _memesDir;
    private readonly string _historyFile;
    private readonly int _minIntervalHours;
    private readonly int _maxIntervalHours;
    private readonly int _maxHistory;
    private readonly Random _random = new();
    private readonly LinkedList<string> _sentHistory = new();
    private readonly HashSet<string> _sentSet = new();

    public MemeSender(
        BotSender botSender,
        IConfiguration config,
        ILogger<MemeSender> logger)
    {
        _botSender = botSender;
        _logger = logger;

        _memesDir = config["Memes:Directory"] ?? "memes";
        _historyFile = config["Memes:HistoryFile"] ?? "memes_sent.txt";
        _minIntervalHours = config.GetValue("Memes:MinIntervalHours", 8);
        _maxIntervalHours = config.GetValue("Memes:MaxIntervalHours", 12);
        _maxHistory = config.GetValue("Memes:MaxHistory", 1000);

        LoadHistory();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MemeSender started. Directory: {Dir}, interval: {Min}-{Max}h",
            _memesDir, _minIntervalHours, _maxIntervalHours);

        while (!stoppingToken.IsCancellationRequested)
        {
            var delayHours = _minIntervalHours + _random.NextDouble() * (_maxIntervalHours - _minIntervalHours);
            _logger.LogInformation("Next meme in {Hours:F1} hours", delayHours);

            await Task.Delay(TimeSpan.FromHours(delayHours), stoppingToken);

            try
            {
                await SendRandomMemeAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send meme");
            }
        }

        _logger.LogInformation("MemeSender stopped");
    }

    public string? PickRandomMeme()
    {
        if (!Directory.Exists(_memesDir))
            return null;

        var allFiles = Directory.EnumerateFiles(_memesDir, "*", SearchOption.AllDirectories)
            .Where(f => ImageExtensions.Contains(Path.GetExtension(f)))
            .Select(Path.GetFullPath)
            .ToList();

        if (allFiles.Count == 0)
            return null;

        return allFiles[_random.Next(allFiles.Count)];
    }

    private async Task SendRandomMemeAsync(CancellationToken ct)
    {
        if (!Directory.Exists(_memesDir))
        {
            _logger.LogWarning("Memes directory '{Dir}' does not exist, skipping", _memesDir);
            return;
        }

        var allFiles = Directory.EnumerateFiles(_memesDir, "*", SearchOption.AllDirectories)
            .Where(f => ImageExtensions.Contains(Path.GetExtension(f)))
            .Select(Path.GetFullPath)
            .ToList();

        if (allFiles.Count == 0)
        {
            _logger.LogWarning("No images found in '{Dir}'", _memesDir);
            return;
        }

        // Filter out already sent
        var unsent = allFiles.Where(f => !_sentSet.Contains(f)).ToList();

        if (unsent.Count == 0)
        {
            // All images sent — clear oldest half of history to allow resending older ones
            var toRemove = _sentHistory.Count / 2;
            _logger.LogInformation("All {Total} images already sent, clearing oldest {Count} from history",
                allFiles.Count, toRemove);

            for (var i = 0; i < toRemove; i++)
            {
                var oldest = _sentHistory.First!.Value;
                _sentHistory.RemoveFirst();
                _sentSet.Remove(oldest);
            }

            SaveHistory();
            unsent = allFiles.Where(f => !_sentSet.Contains(f)).ToList();
        }

        var chosen = unsent[_random.Next(unsent.Count)];
        _logger.LogInformation("Sending meme: {Path} ({Unsent} unsent / {Total} total)",
            chosen, unsent.Count, allFiles.Count);

        await _botSender.SendPhotoAsync(chosen, ct);
        RecordSent(chosen);

        _logger.LogInformation("Meme sent successfully");
    }

    private void RecordSent(string filePath)
    {
        _sentHistory.AddLast(filePath);
        _sentSet.Add(filePath);

        while (_sentHistory.Count > _maxHistory)
        {
            var oldest = _sentHistory.First!.Value;
            _sentHistory.RemoveFirst();
            _sentSet.Remove(oldest);
        }

        SaveHistory();
    }

    private void LoadHistory()
    {
        if (!File.Exists(_historyFile))
            return;

        var lines = File.ReadAllLines(_historyFile);
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var fullPath = Path.GetFullPath(line);
            _sentHistory.AddLast(fullPath);
            _sentSet.Add(fullPath);
        }

        // Trim if file had more than max
        while (_sentHistory.Count > _maxHistory)
        {
            var oldest = _sentHistory.First!.Value;
            _sentHistory.RemoveFirst();
            _sentSet.Remove(oldest);
        }

        _logger.LogInformation("Loaded {Count} entries from meme history", _sentHistory.Count);
    }

    private void SaveHistory()
    {
        File.WriteAllLines(_historyFile, _sentHistory);
    }
}
