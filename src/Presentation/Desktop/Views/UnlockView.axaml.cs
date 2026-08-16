using Avalonia.Controls;
using Avalonia.Input;
using BitwardenSharp.Desktop.ViewModels;

namespace BitwardenSharp.Desktop.Views;

public partial class UnlockView : UserControl
{
    public UnlockView()
    {
        InitializeComponent();

        // Read the account and lock state once the view-model is attached, and put the caret
        // where the user is going to type.
        DataContextChanged += async (_, _) =>
        {
            if (DataContext is UnlockViewModel vm) await vm.InitialiseAsync();
        };

        AttachedToVisualTree += (_, _) => this.FindControl<TextBox>("PasswordBox")?.Focus();
    }
}
