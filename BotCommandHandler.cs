using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace UVBStoler;

public class BotCommandHandler : BackgroundService
{
    private readonly BotSender _botSender;
    private readonly ChannelPoller _channelPoller;
    private readonly ILogger<BotCommandHandler> _logger;
    private readonly string[] _emptyReplies;
    private readonly Random _random = new();

    public BotCommandHandler(
        BotSender botSender,
        ChannelPoller channelPoller,
        IConfiguration config,
        ILogger<BotCommandHandler> logger)
    {
        _botSender = botSender;
        _channelPoller = channelPoller;
        _logger = logger;
        _emptyReplies = config.GetSection("EmptyReplies").Get<string[]>() ?? ["ЭФИР МОЛЧИТ"];
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _botSender.Client.StartReceiving(
            updateHandler: HandleUpdateAsync,
            errorHandler: HandleErrorAsync,
            receiverOptions: new ReceiverOptions
            {
                AllowedUpdates = [UpdateType.Message]
            },
            cancellationToken: stoppingToken);

        _logger.LogInformation("BotCommandHandler started, listening for commands");
        return Task.CompletedTask;
    }

    private async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken ct)
    {
        if (update.Message?.Text is not { } text)
            return;

        var normalized = text.Trim().ToLowerInvariant().Replace(",", "").Replace("!", "");

        if (normalized is not ("бот жги" or "/burn"))
            return;

        var chatId = update.Message.Chat.Id;
        _logger.LogInformation("Received '{Command}' from chat {ChatId}", text, chatId);

        try
        {
            var words = _channelPoller.DrainRecentWords();

            if (words.Count == 0)
            {
                var reply = _emptyReplies[_random.Next(_emptyReplies.Length)];
                await _botSender.SendMessageAsync(chatId, reply, ct);
                _logger.LogInformation("No recent words, sent empty reply to chat {ChatId}", chatId);
                return;
            }

            var response = string.Join("\n", words.Select((w, i) => $"{i + 1}. {w}"));
            await _botSender.SendMessageAsync(chatId, response, ct);

            _logger.LogInformation("Sent {Count} recent words to chat {ChatId}", words.Count, chatId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling command in chat {ChatId}", chatId);
            await _botSender.SendMessageAsync(chatId, "ОШИБКА ПРИЁМА", ct);
        }
    }

    private Task HandleErrorAsync(ITelegramBotClient bot, Exception exception, CancellationToken ct)
    {
        _logger.LogError(exception, "Telegram bot polling error");
        return Task.CompletedTask;
    }
}
