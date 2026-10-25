using System;
using System.Linq;
using Telegram.Bot.Exceptions;

namespace TelegramAdBot.Control;

public static class TelegramRetryAfter
{
    public static int Parse(ApiRequestException exception)
    {
        if (exception.Parameters?.RetryAfter is int serverDelay && serverDelay > 0) return serverDelay;
        string message = exception.Message;
        int marker = message.IndexOf("retry after ", StringComparison.OrdinalIgnoreCase);
        if (marker >= 0)
        {
            string digits = new string(message[(marker + 12)..].TakeWhile(char.IsDigit).ToArray());
            if (int.TryParse(digits, out int parsed) && parsed > 0) return parsed;
        }
        return 3600;
    }
}
