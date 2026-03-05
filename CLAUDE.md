# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

UVBStealer is a .NET 9 console app that monitors the public Telegram channel `@uvb76logs` for UVB-76 radio station messages, extracts the keyword (СЛОВО) from the standard format `НЖТИ XXXXX СЛОВО XXXX XXXX`, and forwards it to a private Telegram group via bot. Users can also request recent words via bot commands (`бот жги` or `/burn`).

## Build & Run

```bash
dotnet run                    # Run locally
dotnet build                  # Build only
./deploy.sh                   # Publish self-contained linux-x64 and deploy to botsvm via rsync+systemd
```

No tests exist in this project.

## Configuration

`appsettings.json` is the base config (committed). Environment-specific overrides (`appsettings.Production.json`, `appsettings.Development.json`) are gitignored. Required secrets: `Bot:Token` and `Bot:TargetChatId`.

On first deploy, copy `appsettings.json` to the server: `scp appsettings.json botsvm:/opt/uvbstealer/`

## Architecture

Four source files, all in the `UVBStealer` namespace. The app uses `Microsoft.Extensions.Hosting` with two `BackgroundService` implementations:

- **Program.cs** — Host setup, DI registration. Registers `HttpClient`, `BotSender`, `ChannelPoller`, and `BotCommandHandler`.
- **ChannelPoller** — Polls `https://t.me/s/uvb76logs` HTML page on a jittered interval (~20s). On first poll, seeds seen post IDs without sending. New posts are parsed and forwarded via `BotSender`. Exposes `DrainRecentWords()` for the command handler.
- **MessageParser** — Static class using source-generated regexes to parse Telegram's public embed HTML. Extracts post IDs and the СЛОВО keyword from message blocks.
- **BotSender** — Wraps `Telegram.Bot` client. Sends words to the configured target chat with random jitter delay (1-5s). Also exposes general `SendMessageAsync` for command replies.
- **BotCommandHandler** — Listens for incoming bot messages via `Telegram.Bot` polling. Responds to `бот жги` / `/burn` with recently captured words (drained from `ChannelPoller`), or a random "empty" reply from the configured list.

## Deployment

Target: Linux x64 server aliased as `botsvm` in SSH config. Published as self-contained binary to `/opt/uvbstealer/`. Runs as systemd service `uvbstealer` with `DOTNET_ENVIRONMENT=Production`.

## Key Dependencies

- `Telegram.Bot` v22 — Telegram Bot API client
- `Microsoft.Extensions.Hosting` / `Microsoft.Extensions.Http` v9
