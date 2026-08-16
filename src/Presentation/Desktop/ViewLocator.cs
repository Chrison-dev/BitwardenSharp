using Avalonia.Controls;
using Avalonia.Controls.Templates;
using BitwardenSharp.Desktop.ViewModels;

namespace BitwardenSharp.Desktop;

/// <summary>Resolves a view for a view-model by naming convention: Foo/ViewModels/XViewModel -> Views/XView.</summary>
public class ViewLocator : IDataTemplate
{
    public Control Build(object? data)
    {
        if (data is null) return new TextBlock { Text = "no view-model" };

        var name = data.GetType().FullName!
            .Replace("ViewModels", "Views", StringComparison.Ordinal)
            .Replace("ViewModel", "View", StringComparison.Ordinal);

        var type = Type.GetType(name);
        return type is not null
            ? (Control)Activator.CreateInstance(type)!
            : new TextBlock { Text = $"view not found: {name}" };
    }

    public bool Match(object? data) => data is ViewModelBase;
}
