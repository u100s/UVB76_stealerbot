# UVB-76 Telegram Stoler

Monitors the public Telegram channel [@uvb76logs](https://t.me/s/uvb76logs) for radio messages from UVB-76 station, extracts the keyword (SLOVO) and forwards it to a private Telegram group via bot.

## Message format

```
НЖТИ XXXXX СЛОВО XXXX XXXX
```

The extracted `СЛОВО` is sent to the target chat.

## Configuration

Edit `appsettings.json`:

```json
{
  "Bot": {
    "Token": "your-bot-token",
    "TargetChatId": "-100xxxxxxxxxx"
  }
}
```

## Run

```bash
dotnet run
```

## Deploy

```bash
chmod +x deploy.sh
./deploy.sh
```

Copy `appsettings.json` to the server manually on first deploy:
```bash
scp appsettings.json botsvm:/opt/uvbstoler/
```
