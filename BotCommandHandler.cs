using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace UVBStealer;

public class BotCommandHandler : BackgroundService
{
    private readonly BotSender _botSender;
    private readonly ChannelListener _channelListener;
    private readonly MemeSender _memeSender;
    private readonly MessageRecorder _recorder;
    private readonly ILogger<BotCommandHandler> _logger;
    private readonly string[] _emptyReplies;
    private readonly Random _random = new();
    private long _botUserId;
    private string _botUsername = "NotAPidorBot";

    private static readonly HashSet<string> DmRequestPhrases = new(StringComparer.Ordinal)
    {
        "перешли в личку",
        "перешли в лс",
        "перешли мне",
        "кинь в личку",
        "кинь в лс",
        "кинь мне",
        "скинь в личку",
        "скинь в лс",
        "скинь мне",
        "отправь в личку",
        "отправь в лс",
        "отправь мне",
        "пришли в личку",
        "пришли в лс",
        "пришли мне",
        "в личку кинь",
        "в личку скинь",
        "в личку перешли",
        "в личку отправь",
        "в личку пришли",
        "в лс кинь",
        "в лс скинь",
        "в лс перешли",
        "в лс отправь",
        "в лс пришли",
    };

    public BotCommandHandler(
        BotSender botSender,
        ChannelListener channelListener,
        MemeSender memeSender,
        MessageRecorder recorder,
        IConfiguration config,
        ILogger<BotCommandHandler> logger)
    {
        _botSender = botSender;
        _channelListener = channelListener;
        _memeSender = memeSender;
        _recorder = recorder;
        _logger = logger;
        _emptyReplies = config.GetSection("EmptyReplies").Get<string[]>() ?? ["ЭФИР МОЛЧИТ"];
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var me = await _botSender.Client.GetMe(stoppingToken);
        _botUserId = me.Id;
        _botUsername = me.Username ?? "NotAPidorBot";

        _botSender.Client.StartReceiving(
            updateHandler: HandleUpdateAsync,
            errorHandler: HandleErrorAsync,
            receiverOptions: new ReceiverOptions
            {
                AllowedUpdates = [UpdateType.Message]
            },
            cancellationToken: stoppingToken);

        _logger.LogInformation("BotCommandHandler started as @{Username}, listening for commands", _botUsername);
    }

    private async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken ct)
    {
        if (update.Message is { } message)
            await _recorder.RecordAsync(message, ct);

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
        else if (update.Message.ReplyToMessage is { Photo: { Length: > 0 } } replyTarget
                 && replyTarget.From?.Id == _botUserId
                 && DmRequestPhrases.Contains(normalized))
        {
            _logger.LogInformation("DM photo request '{Command}' from user {UserId} in chat {ChatId}",
                text, update.Message.From?.Id, chatId);
            await HandleDmPhotoRequestAsync(update.Message, replyTarget, ct);
        }
    }

    private async Task HandleBurnAsync(long chatId, bool en, CancellationToken ct)
    {
        try
        {
            var words = _channelListener.DrainRecentWords();

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

    private async Task HandleDmPhotoRequestAsync(Message requestMessage, Message photoMessage, CancellationToken ct)
    {
        var userId = requestMessage.From?.Id;
        if (userId is null)
            return;

        var chatId = requestMessage.Chat.Id;
        var messageId = requestMessage.MessageId;
        var fileId = photoMessage.Photo![^1].FileId;

        try
        {
            await _botSender.SendPhotoByFileIdAsync(userId.Value, fileId, ct);
            _logger.LogInformation("Sent photo to user {UserId} DM", userId);
        }
        catch (ApiRequestException ex) when (ex.ErrorCode == 403)
        {
            _logger.LogWarning("Can't DM user {UserId}: no conversation started", userId);
            var reply = $"Не могу писать в лс, сначала напиши мне <a href=\"https://t.me/{_botUsername}?start=start\">/start@{_botUsername}</a>";
            await _botSender.ReplyAsync(chatId, messageId, reply, ParseMode.Html, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send photo to user {UserId} DM", userId);
            await _botSender.ReplyAsync(chatId, messageId, "Не получилось отправить, попробуй позже", ct: ct);
        }
    }

    private Task HandleErrorAsync(ITelegramBotClient bot, Exception exception, CancellationToken ct)
    {
        _logger.LogError(exception, "Telegram bot polling error");
        return Task.CompletedTask;
    }
}
