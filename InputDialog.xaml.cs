using System.Windows;

namespace Zametki;

public partial class InputDialog : Window
{
    public string Result => InputBox.Text;

    public InputDialog(string title, string prompt, string initialValue = "")
    {
        InitializeComponent();
        Title = title;
        PromptLabel.Text = prompt;
        InputBox.Text = initialValue;
        InputBox.SelectAll();
        InputBox.Focus();
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
