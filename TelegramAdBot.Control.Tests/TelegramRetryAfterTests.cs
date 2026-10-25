using TelegramAdBot.Control;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;

namespace TelegramAdBot.Control.Tests;

public sealed class TelegramRetryAfterTests
{
    [Fact]
    public void UsesTelegramStructuredRetryAfterValue()
    {
        var exception = new ApiRequestException("Too Many Requests", 429, new ResponseParameters { RetryAfter = 2807 });
        Assert.Equal(2807, TelegramRetryAfter.Parse(exception));
    }

    [Fact]
    public void ParsesRetryAfterFromApiErrorMessage()
    {
        var exception = new ApiRequestException("Too Many Requests: retry after 45", 429);
        Assert.Equal(45, TelegramRetryAfter.Parse(exception));
    }

    [Fact]
    public void UsesConservativeDelayWhenTelegramOmitsRetryAfter()
    {
        var exception = new ApiRequestException("Too Many Requests", 429);
        Assert.Equal(3600, TelegramRetryAfter.Parse(exception));
    }
}
