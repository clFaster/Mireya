using System;
using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Mireya.Client.Avalonia.ViewModels;

namespace Mireya.Client.Avalonia;

/// <summary>
///     Given a view model, returns the corresponding view if possible.
/// </summary>
[RequiresUnreferencedCode(
    "Default implementation of ViewLocator involves reflection which may be trimmed away.",
    Url = "https://docs.avaloniaui.net/docs/concepts/view-locator"
)]
public class ViewLocator : IDataTemplate
{
    public Control? Build(object? param)
    {
        if (param is null)
            return null;

        var name = param.GetType().FullName?.Replace("ViewModel", "View", StringComparison.Ordinal);
        if (name is null)
            return null;
        var type = Type.GetType(name);

        if (type != null)
            return Activator.CreateInstance(type) as Control;

        return new TextBlock { Text = "Not Found: " + name };
    }

    public bool Match(object? data)
    {
        return data is ViewModelBase;
    }
}
