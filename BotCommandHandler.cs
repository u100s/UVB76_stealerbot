using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace UVBStealer;

public class BotCommandHandler : BackgroundService
{
    private readonly BotSender _botSender;
    private readonly ChannelPoller _channelPoller;
    private readonly MemeSender _memeSender;
    private readonly ILogger<BotCommandHandler> _logger;
    private readonly string[] _emptyReplies;
    private readonly Random _random = new();

    public BotCommandHandler(
        BotSender botSender,
        ChannelPoller channelPoller,
        MemeSender memeSender,
        IConfiguration config,
        ILogger<BotCommandHandler> logger)
    {
        _botSender = botSender;
        _channelPoller = channelPoller;
        _memeSender = memeSender;
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

        var chatId = update.Message.Chat.Id;
        var lang = update.Message.From?.LanguageCode;
        var en = lang != null && lang.StartsWith("en", StringComparison.OrdinalIgnoreCase);

        if (normalized is "бот жги" or "/burn")
        {
            _logger.LogInformation("Received '{Command}' from chat {ChatId}", text, chatId);
            await HandleBurnAsync(chatId, en, ct);
        }
        else if (normalized is "бот мем" or "бот дай мем" or "бот мемас" or "бот дай мемас" or "/meme")
        {
            _logger.LogInformation("Received '{Command}' from chat {ChatId}", text, chatId);
            await HandleMemeAsync(chatId, en, ct);
        }
        else if (normalized is "/help" or "бот помоги" or "бот хелп")
        {
            _logger.LogInformation("Received '{Command}' from chat {ChatId}", text, chatId);
            await HandleHelpAsync(chatId, en, ct);
        }
    }

    private async Task HandleBurnAsync(long chatId, bool en, CancellationToken ct)
    {
        try
        {
            var words = _channelPoller.DrainRecentWords();

            if (words.Count == 0)
            {
                var reply = en
                    ? "THE AIR IS SILENT"
                    : _emptyReplies[_random.Next(_emptyReplies.Length)];
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
            _logger.LogError(ex, "Error handling burn command in chat {ChatId}", chatId);
            await _botSender.SendMessageAsync(chatId, en ? "RECEIVE ERROR" : "ОШИБКА ПРИЁМА", ct);
        }
    }

    private async Task HandleMemeAsync(long chatId, bool en, CancellationToken ct)
    {
        try
        {
            var memePath = _memeSender.PickRandomMeme();

            if (memePath is null)
            {
                await _botSender.SendMessageAsync(chatId, en ? "NO MEMES" : "МЕМОВ НЕТ", ct);
                _logger.LogWarning("No memes available for chat {ChatId}", chatId);
                return;
            }

            await _botSender.SendPhotoAsync(chatId, memePath, ct);
            _logger.LogInformation("Sent meme {Path} to chat {ChatId}", memePath, chatId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling meme command in chat {ChatId}", chatId);
            await _botSender.SendMessageAsync(chatId, en ? "TRANSMISSION ERROR" : "ОШИБКА ПЕРЕДАЧИ", ct);
        }
    }

    private async Task HandleHelpAsync(long chatId, bool en, CancellationToken ct)
    {
        var help = en
            ? """
              📡 NOTAPIDOR-LITE — COMMANDS

              /burn — latest intercepted words
              /meme — random meme
              /help — this help
              """
            : """
              📡 НЕПИДОРАСИЙ-ЛАЙТ — КОМАНДЫ

              /burn, бот жги — последние перехваченные слова
              /meme, бот мем — случайный мем
              /help, бот помоги — эта справка
              """;
        await _botSender.SendMessageAsync(chatId, help, ct);
    }

    private Task HandleErrorAsync(ITelegramBotClient bot, Exception exception, CancellationToken ct)
    {
        _logger.LogError(exception, "Telegram bot polling error");
        return Task.CompletedTask;
    }
}
