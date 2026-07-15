using System.Windows;
using WindowSpot.Services;

namespace WindowSpot;

public partial class SettingsWindow : Window
{
    private readonly SettingsStore _settingsStore;

    public SettingsWindow(SettingsStore settingsStore)
    {
        InitializeComponent();
        _settingsStore = settingsStore;
        ApiKeyBox.Password = _settingsStore.GetOpenRouterApiKey();
        ModelBox.Text = _settingsStore.GetOpenRouterModel();
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        _settingsStore.SetOpenRouterCredentials(ApiKeyBox.Password, ModelBox.Text);
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
