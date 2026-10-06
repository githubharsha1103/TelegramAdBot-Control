# Telegram control deployment (Debian 12)

The promotion engine and Telegram control bot run together in one process. The promotion engine remains the only code that launches Playwright/Chromium.

## Required environment

```ini
TELEGRAM_CONTROL_BOT_TOKEN=token-from-BotFather
ADMIN_TELEGRAM_ID=your-numeric-Telegram-user-id
DISPLAY=:1
```

Never commit this file with real values.

## Build and run

```bash
dotnet restore TelegramAdBot.sln
dotnet build TelegramAdBot.sln -c Release
export DISPLAY=:1
export TELEGRAM_CONTROL_BOT_TOKEN='token-from-BotFather'
export ADMIN_TELEGRAM_ID='your-numeric-id'
dotnet run --project TelegramAdBot.csproj -c Release
```

Create the Telegram bot with BotFather using `/newbot`, copy its token to `TELEGRAM_CONTROL_BOT_TOKEN`, and obtain your numeric user ID from a trusted ID bot. Only that exact `ADMIN_TELEGRAM_ID` receives control; every other user receives `Unauthorized.`

## systemd recommendation

Use one service with `EnvironmentFile=/etc/telegram-adbot/control.env`, `Restart=on-failure`, and `RestartSec=5`. Its `ExecStart` should run the built `TelegramAdBot.dll`. Keep `DISPLAY=:1` in the environment file.

If the process restarts, any active browser workers end with it; restart the service and begin a new explicit run from Telegram. The integrated bot never starts a second promotion engine.

## Rollback

The pre-control source snapshot is at `C:\Users\91996\OneDrive\Desktop\TelegramAdBot-before-control-20261006` in this development workspace. On a VPS, retain the prior published directory and change the systemd `WorkingDirectory`/`ExecStart` back to that version, then `systemctl daemon-reload` and restart the two services.
