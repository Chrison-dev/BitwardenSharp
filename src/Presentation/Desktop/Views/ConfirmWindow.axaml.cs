using Avalonia.Controls;

namespace BitwardenSharp.Desktop.Views;

/// <summary>A yes/no modal for destructive actions. Cancel is the default.</summary>
public partial class ConfirmWindow : Window
{
    public ConfirmWindow()
    {
        InitializeComponent();
        ConfirmButton.Click += (_, _) => Close(true);
        CancelButton.Click += (_, _) => Close(false);
        Opened += (_, _) => CancelButton.Focus();
    }

    public static async Task<bool> ShowAsync(Window owner, string title, string message)
    {
        var window = new ConfirmWindow { Title = title };
        window.MessageText.Text = message;
        return await window.ShowDialog<bool>(owner);
    }
}
