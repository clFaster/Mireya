using Mireya.Client.Avalonia.Platform;

namespace Mireya.Client.Core.Tests.Platform;

public sealed class AdaptivePlatformTests
{
    [Theory]
    [InlineData(double.NaN, SizeClass.Compact)]
    [InlineData(0, SizeClass.Compact)]
    [InlineData(639, SizeClass.Compact)]
    [InlineData(640, SizeClass.Medium)]
    [InlineData(1007, SizeClass.Medium)]
    [InlineData(1008, SizeClass.Expanded)]
    public void WidthMapsToStableSizeClass(double width, SizeClass expected)
    {
        Assert.Equal(expected, LayoutBreakpoints.FromWidth(width));
    }

    [Fact]
    public void TelevisionAlwaysUsesExpandedLayout()
    {
        Assert.Equal(SizeClass.Expanded, LayoutBreakpoints.Resolve(540, FormFactor.Tv));
    }

    [Theory]
    [InlineData(FormFactor.Desktop, UiDensity.Pointer, false, false)]
    [InlineData(FormFactor.Phone, UiDensity.Touch, true, false)]
    [InlineData(FormFactor.Tablet, UiDensity.Touch, true, false)]
    [InlineData(FormFactor.Tv, UiDensity.Television, false, true)]
    public void FormFactorSelectsInputDensity(
        FormFactor formFactor,
        UiDensity expectedDensity,
        bool isTouchFirst,
        bool isTelevision
    )
    {
        var capabilities = new ClientPlatformCapabilities { FormFactor = formFactor };

        Assert.Equal(expectedDensity, capabilities.Density);
        Assert.Equal(isTouchFirst, capabilities.IsTouchFirst);
        Assert.Equal(isTelevision, capabilities.IsTelevision);
    }
}
