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
        //
        // This is an async void handler, so anything it lets escape kills the process rather
        // than surfacing. InitialiseAsync handles its own failures; the catch is the backstop.
        UnlockViewModel? initialised = null;
        DataContextChanged += async (_, _) =>
        {
            if (DataContext is not UnlockViewModel vm || ReferenceEquals(vm, initialised)) return;
            initialised = vm;

            try { await vm.InitialiseAsync(); }
            catch (Exception ex) { vm.Error = ex.Message; }
        };

        AttachedToVisualTree += (_, _) => this.FindControl<TextBox>("PasswordBox")?.Focus();
    }
}
