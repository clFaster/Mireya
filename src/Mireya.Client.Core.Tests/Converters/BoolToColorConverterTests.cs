using Avalonia.Data;
using Mireya.Client.Avalonia.Converters;

namespace Mireya.Client.Core.Tests.Converters;

public sealed class BoolToColorConverterTests
{
    [Fact]
    public void ConvertBackDoesNotAttemptToUpdateTheSourceValue()
    {
        var converter = new BoolToColorConverter();

        var result = converter.ConvertBack(true, typeof(bool), null, null!);

        Assert.Same(BindingOperations.DoNothing, result);
    }
}
