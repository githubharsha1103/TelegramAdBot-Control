using System;
using System.Linq;
using Telegram.Bot.Exceptions;

namespace TelegramAdBot.Control;

public static class TelegramRetryAfter
{
    public static int Parse(ApiRequestException exception)
    {
        if (exception.Parameters?.RetryAfter is int serverDelay && serverDelay > 0) return serverDelay;
        string text = exception.Message;
        int index = text.IndexOf("retry after ", StringComparison.OrdinalIgnoreCase);
        if (index >= 0)
        {
            string value = new string(text[(index + 12)..].TakeWhile(char.IsDigit).ToArray());
            if (int.TryParse(value, out int seconds) && seconds > 0) return seconds;
        }
        // Fail closed if a malformed 429 omits retry_after. Never guess a shorter delay.
        return 3600;
    }
}
