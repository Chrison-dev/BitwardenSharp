using Avalonia.Controls;
using Avalonia.Input;

namespace BitwardenSharp.Desktop.Views;

/// <summary>
/// A one-field modal prompt. Avalonia ships no input dialog, and folder create/rename both need
/// one; this keeps that need from turning into an inline-editing mechanism in the tree.
/// </summary>
public partial class TextPromptWindow : Window
{
    public TextPromptWindow()
    {
        InitializeComponent();
        OkButton.Click += (_, _) => Accept();
        CancelButton.Click += (_, _) => Close(null);

        // Enter accepts, Escape cancels — what any rename dialog is expected to do.
        Input.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) { Accept(); e.Handled = true; }
            else if (e.Key == Key.Escape) { Close(null); e.Handled = true; }
        };

        Opened += (_, _) => { Input.Focus(); Input.SelectAll(); };
    }

    private void Accept()
    {
        var text = Input.Text?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            ValidationMessage.Text = "A name is required.";
            ValidationMessage.IsVisible = true;
            return;
        }
        Close(text);
    }

    /// <summary>Shows the prompt and returns the entered text, or null if cancelled.</summary>
    public static async Task<string?> ShowAsync(Window owner, string title, string label, string? initial)
    {
        var window = new TextPromptWindow { Title = title };
        window.PromptLabel.Text = label;
        window.Input.Text = initial ?? string.Empty;
        return await window.ShowDialog<string?>(owner);
    }
}
