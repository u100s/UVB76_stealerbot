using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace UVBStealer;

public class MessageRecorder
{
    private readonly StorageDb _db;
    private readonly BotSender _botSender;
    private readonly string _mediaDir;
    private readonly ILogger<MessageRecorder> _logger;

    public MessageRecorder(
        StorageDb db,
        BotSender botSender,
        IConfiguration config,
        ILogger<MessageRecorder> logger)
    {
        _db = db;
        _botSender = botSender;
        _mediaDir = config.GetValue("Storage:MediaDirectory", "data/media")!;
        _logger = logger;
    }

    public async Task RecordAsync(Message message, CancellationToken ct)
    {
        try
        {
            await _db.UpsertChatAsync(message.Chat);

            if (message.From is { } user)
                await _db.UpsertUserAsync(user);

            string? mediaPath = null;

            if (message.Photo is { Length: > 0 } photos)
            {
                var largest = photos[^1];
                mediaPath = await DownloadPhotoAsync(message.Chat.Id, message.MessageId, largest.FileId, ct);
            }

            await _db.SaveMessageAsync(message, mediaPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record message {MessageId} in chat {ChatId}",
                message.MessageId, message.Chat.Id);
        }
    }

    private async Task<string?> DownloadPhotoAsync(long chatId, int messageId, string fileId, CancellationToken ct)
    {
        try
        {
            var dir = Path.Combine(_mediaDir, chatId.ToString());
            Directory.CreateDirectory(dir);

            var filePath = Path.Combine(dir, $"{messageId}.jpg");

            await using var stream = System.IO.File.Create(filePath);
            await _botSender.Client.GetInfoAndDownloadFile(fileId, stream, ct);

            _logger.LogDebug("Saved photo to {Path}", filePath);
            return filePath;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to download photo for message {MessageId}", messageId);
            return null;
        }
    }
}
