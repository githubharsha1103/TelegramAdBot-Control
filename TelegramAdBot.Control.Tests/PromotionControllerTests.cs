using TelegramAdBot.Control;
using TelegramAdBot.Helpers;
using TelegramAdBot.Models;

namespace TelegramAdBot.Control.Tests;

public sealed class PromotionControllerTests
{
    [Theory]
    [InlineData(PromotionRunState.Idle)]
    [InlineData(PromotionRunState.Running)]
    [InlineData(PromotionRunState.Paused)]
    [InlineData(PromotionRunState.Stopping)]
    [InlineData(PromotionRunState.Stopped)]
    [InlineData(PromotionRunState.Completed)]
    [InlineData(PromotionRunState.Failed)]
    public void PromotionStatus_PreservesAuthoritativeRunState(PromotionRunState state)
    {
        var status = new PromotionStatus { RunState = state, CompletedCycles = 100, TotalCycles = 100 };

        Assert.Equal(state, status.RunState);
    }

    [Fact]
    public async Task ConfigureAsync_ExposesOnlyExplicitSelectedIdsAndPreservesPerInstanceAccounts()
    {
        string directory = Path.Combine(Path.GetTempPath(), "telegram-adbot-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var botConfig = new BotConfigManager(Path.Combine(directory, "bots.json"));
            Assert.True(botConfig.TryAdd("alpha_bot", out _, out _));
            Assert.True(botConfig.TryAdd("beta_bot", out _, out _));
            var controller = new PromotionController(new BotConfig { MaxSupportedInstanceCount = 100 }, botConfig);
            var expectedAccounts = new Dictionary<int, List<int>>
            {
                [2] = new() { 1, 3 },
                [11] = new() { 2 }
            };

            ControlResult result = await controller.ConfigureAsync(new PromotionRunConfiguration
            {
                SelectedBots = new() { "alpha_bot" },
                SelectedInstanceIds = new() { 11, 2 },
                InstanceCount = 2,
                CyclesPerBot = 7,
                AccountsByInstance = expectedAccounts
            });

            Assert.True(result.Success, result.Message);
            PromotionStatus status = controller.GetStatus();
            Assert.Equal(new[] { 2, 11 }, status.SelectedInstanceIds);
            Assert.Equal(new[] { 1, 3 }, status.AccountsByInstance[2]);
            Assert.Equal(new[] { 2 }, status.AccountsByInstance[11]);
            Assert.Equal(2, status.InstanceCount);
            Assert.Equal(7, status.CyclesPerBot);
            Assert.Equal(PromotionRunState.Idle, status.RunState);
            Assert.DoesNotContain(1, status.SelectedInstanceIds);
            Assert.DoesNotContain(3, status.SelectedInstanceIds);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ConfigureAsync_RejectsAccountMappingForMissingSelectedInstance()
    {
        string path = Path.Combine(Path.GetTempPath(), "telegram-adbot-tests", Guid.NewGuid().ToString("N"), "bots.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var bots = new BotConfigManager(path);
        Assert.True(bots.TryAdd("alpha_bot", out _, out _));
        var controller = new PromotionController(new BotConfig { MaxSupportedInstanceCount = 20 }, bots);

        ControlResult result = await controller.ConfigureAsync(new PromotionRunConfiguration
        {
            SelectedBots = new() { "alpha_bot" },
            SelectedInstanceIds = new() { 7 },
            InstanceCount = 1,
            CyclesPerBot = 1,
            AccountsByInstance = new Dictionary<int, List<int>>()
        });

        Assert.False(result.Success);
        Assert.Contains("Configure Telegram accounts", result.Message);
        Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
    }
}
