using Avalonia.Controls;
using BitwardenSharp.Desktop.ViewModels;

namespace BitwardenSharp.Desktop.Views;

public partial class DuplicatesView : UserControl
{
    public DuplicatesView()
    {
        InitializeComponent();

        // The view-model asks for confirmation through a callback rather than referencing a
        // Window itself; a modal needs an owner, which is a view concern.
        DataContextChanged += (_, _) =>
        {
            if (DataContext is not DuplicatesViewModel vm) return;
            vm.Confirm = async (title, message) =>
                TopLevel.GetTopLevel(this) is Window owner
                && await ConfirmWindow.ShowAsync(owner, title, message);
        };
    }
}
