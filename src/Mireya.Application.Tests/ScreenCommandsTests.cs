using Mireya.Application.Constants;

namespace Mireya.Application.Tests;

public class ScreenCommandsTests
{
    [Theory]
    [InlineData(ScreenCommands.RestartPlayback)]
    [InlineData(ScreenCommands.ReloadContent)]
    [InlineData(ScreenCommands.Identify)]
    public void IsValid_KnownCommands_ReturnsTrue(string command)
    {
        Assert.True(ScreenCommands.IsValid(command));
    }

    [Theory]
    [InlineData("")]
    [InlineData("reboot")]
    [InlineData(null)]
    public void IsValid_UnknownCommands_ReturnsFalse(string? command)
    {
        Assert.False(ScreenCommands.IsValid(command));
    }

    [Fact]
    public void Identify_IsIncludedInAllCommands()
    {
        Assert.Contains(ScreenCommands.Identify, ScreenCommands.All);
    }
}
