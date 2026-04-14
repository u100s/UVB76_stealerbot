using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TL;
using WTelegram;

namespace UVBStealer;

public class TelegramClientService : IHostedService, IAsyncDisposable
{
    private readonly IConfiguration _config;
    private readonly ILogger<TelegramClientService> _logger;
    private readonly TaskCompletionSource<bool> _ready = new();
    private Client? _client;

    public TelegramClientService(IConfiguration config, ILogger<TelegramClientService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public Client Client => _client ?? throw new InvalidOperationException("TelegramClient is not initialized");
    public Task<bool> Ready => _ready.Task;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var sessionPath = _config["TelegramClient:SessionPath"] ?? "data/wtelegram.session";
        var sessionDir = Path.GetDirectoryName(sessionPath);
        if (!string.IsNullOrEmpty(sessionDir))
            Directory.CreateDirectory(sessionDir);

        _client = new Client(ConfigProvider, new FileStream(sessionPath, FileMode.OpenOrCreate, FileAccess.ReadWrite));

        _logger.LogInformation("Logging in to Telegram...");
        var user = await _client.LoginUserIfNeeded();
        _logger.LogInformation("Logged in as {Name} (id: {Id})", user.first_name, user.id);

        _ready.TrySetResult(true);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_client != null)
        {
            _client.Dispose();
            _client = null;
        }

        _ready.TrySetCanceled();
        await Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_client != null)
        {
            _client.Dispose();
            _client = null;
        }

        await Task.CompletedTask;
    }

    private string? ConfigProvider(string what)
    {
        return what switch
        {
            "api_id" => _config["TelegramClient:ApiId"],
            "api_hash" => _config["TelegramClient:ApiHash"],
            "phone_number" => _config["TelegramClient:PhoneNumber"],
            "verification_code" => GetVerificationCode(),
            "password" => _config["TelegramClient:TwoFactorPassword"],
            _ => null
        };
    }

    private string? GetVerificationCode()
    {
        var envCode = Environment.GetEnvironmentVariable("TELEGRAM_VERIFICATION_CODE");
        if (!string.IsNullOrWhiteSpace(envCode))
        {
            _logger.LogInformation("Using verification code from TELEGRAM_VERIFICATION_CODE env var");
            return envCode;
        }

        var codeFile = "data/verification_code.txt";
        _logger.LogWarning(
            "Verification code required! Write it to '{File}' or set TELEGRAM_VERIFICATION_CODE env var",
            codeFile);

        var dir = Path.GetDirectoryName(codeFile);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        while (true)
        {
            if (File.Exists(codeFile))
            {
                var code = File.ReadAllText(codeFile).Trim();
                if (!string.IsNullOrWhiteSpace(code))
                {
                    _logger.LogInformation("Read verification code from {File}", codeFile);
                    try { File.Delete(codeFile); } catch { }
                    return code;
                }
            }

            _logger.LogInformation("Waiting for verification code in '{File}'...", codeFile);
            Thread.Sleep(5000);
        }
    }
}
