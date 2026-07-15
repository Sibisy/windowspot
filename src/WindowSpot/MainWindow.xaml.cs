using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using WindowSpot.Models;
using WindowSpot.Providers;
using WindowSpot.Services;
using MessageBox = System.Windows.MessageBox;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Button = System.Windows.Controls.Button;

namespace WindowSpot;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<SearchResult> _results = new();
    private readonly ObservableCollection<IconButtonItem> _favoritesItems = new();
    private readonly ObservableCollection<IconButtonItem> _topAppsItems = new();
    private readonly ObservableCollection<ChatMessageView> _chatMessages = new();

    private readonly SearchEngine _searchEngine;
    private readonly AppIndexer _appIndexer;
    private readonly FavoritesStore _favoritesStore;
    private readonly UsageStore _usageStore;
    private readonly SettingsStore _settingsStore;
    private readonly OpenRouterClient _openRouterClient = new();
    private readonly ChatSessionStore _chatSessionStore = new();

    private CancellationTokenSource? _searchCts;
    private string? _currentSuggestion;
    private bool _inChatMode;
    private bool _aiEnabled;
    private bool _dialogOpen;

    public MainWindow(
        SearchEngine searchEngine,
        AppIndexer appIndexer,
        FavoritesStore favoritesStore,
        UsageStore usageStore,
        SettingsStore settingsStore)
    {
        InitializeComponent();
        _searchEngine = searchEngine;
        _appIndexer = appIndexer;
        _favoritesStore = favoritesStore;
        _usageStore = usageStore;
        _settingsStore = settingsStore;

        ResultsList.ItemsSource = _results;
        FavoritesList.ItemsSource = _favoritesItems;
        TopAppsList.ItemsSource = _topAppsItems;
        ChatMessagesList.ItemsSource = _chatMessages;

        RefreshAiEnabled();
    }

    /// <summary>전역 단축키로 호출됨: 활성 화면 중앙 상단에 창을 띄우고 입력창에 포커스를 준다.</summary>
    public void ShowAtCenter()
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Left + (workArea.Width - Width) / 2;
        Top = workArea.Top + workArea.Height * 0.18;

        _inChatMode = false;
        ChatModePanel.Visibility = Visibility.Collapsed;
        SearchModePanel.Visibility = Visibility.Visible;

        RefreshFavorites();
        RefreshTopApps();
        QueryBox.Text = string.Empty;
        _results.Clear();

        Show();
        Activate();
        QueryBox.Focus();
    }

    /// <summary>AI 설정이 저장된 뒤 Ask AI 섹션 표시 여부를 갱신하기 위해 App에서 호출한다.</summary>
    public void RefreshAiEnabled()
    {
        _aiEnabled = !string.IsNullOrWhiteSpace(_settingsStore.GetOpenRouterApiKey())
                     && !string.IsNullOrWhiteSpace(_settingsStore.GetOpenRouterModel());
        AskAiSection.Visibility = _aiEnabled ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Window_Deactivated(object sender, EventArgs e)
    {
        if (_dialogOpen) return;
        Hide();
    }

    private async void QueryBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        string query = QueryBox.Text;
        bool isBlank = string.IsNullOrWhiteSpace(query);

        FavoritesSection.Visibility = isBlank ? Visibility.Visible : Visibility.Collapsed;
        TopAppsSection.Visibility = isBlank && _topAppsItems.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        AskAiSection.Visibility = _aiEnabled ? Visibility.Visible : Visibility.Collapsed;
        AskAiLabel.Text = isBlank ? "Ask AI" : query.Trim();

        UpdateGhostSuggestion(query);

        _searchCts?.Cancel();
        var cts = new CancellationTokenSource();
        _searchCts = cts;

        if (isBlank)
        {
            _results.Clear();
            return;
        }

        try
        {
            var results = await _searchEngine.SearchAsync(query, cts.Token);
            if (cts.IsCancellationRequested) return;

            _results.Clear();
            foreach (var r in results) _results.Add(r);
            if (_results.Count > 0) ResultsList.SelectedIndex = 0;
        }
        catch (OperationCanceledException)
        {
            // 더 최신 입력이 들어와 취소됨 — 무시
        }
    }

    /// <summary>
    /// 입력한 문자열로 시작하는 앱 이름(우선) 또는 흔한 도메인을 찾아 Tab 자동완성 후보로 삼고,
    /// 입력창 뒤에 반투명 "고스트 텍스트"로 겹쳐 그린다 (안드로이드판의 인라인 자동완성 대응).
    /// </summary>
    private void UpdateGhostSuggestion(string query)
    {
        if (string.IsNullOrEmpty(query))
        {
            _currentSuggestion = null;
            GhostTextBlock.Text = string.Empty;
            return;
        }

        string? suggestion = _appIndexer.Apps
            .FirstOrDefault(a => a.Name.StartsWith(query, StringComparison.OrdinalIgnoreCase))?.Name;

        suggestion ??= UrlProvider.CommonDomains.FirstOrDefault(d =>
            d.StartsWith(query, StringComparison.OrdinalIgnoreCase)
            && !d.Equals(query, StringComparison.OrdinalIgnoreCase));

        _currentSuggestion = suggestion;
        GhostTextBlock.Text = suggestion ?? string.Empty;
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_inChatMode)
        {
            if (e.Key == Key.Escape)
            {
                Hide();
                e.Handled = true;
            }
            return;
        }

        switch (e.Key)
        {
            case Key.Escape:
                Hide();
                e.Handled = true;
                break;
            case Key.Down:
                MoveSelection(1);
                e.Handled = true;
                break;
            case Key.Up:
                MoveSelection(-1);
                e.Handled = true;
                break;
            case Key.Tab:
                if (_currentSuggestion is not null)
                {
                    QueryBox.Text = _currentSuggestion;
                    QueryBox.CaretIndex = _currentSuggestion.Length;
                }
                e.Handled = true;
                break;
            case Key.Enter:
                bool ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
                ExecuteSelected(openContainingFolder: ctrl);
                e.Handled = true;
                break;
        }
    }

    private void ResultsList_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source)
        {
            var item = ItemsControl.ContainerFromElement(ResultsList, source) as ListBoxItem;
            if (item is not null)
            {
                ResultsList.SelectedItem = item.DataContext;
                ExecuteSelected(openContainingFolder: false);
            }
        }
    }

    private void MoveSelection(int delta)
    {
        if (_results.Count == 0) return;
        int next = Math.Clamp(ResultsList.SelectedIndex + delta, 0, _results.Count - 1);
        ResultsList.SelectedIndex = next;
        ResultsList.ScrollIntoView(ResultsList.SelectedItem);
    }

    private void ExecuteSelected(bool openContainingFolder)
    {
        if (ResultsList.SelectedItem is not SearchResult selected) return;

        try
        {
            if (openContainingFolder && selected.OpenContainingFolder is not null)
                selected.OpenContainingFolder();
            else
                selected.Execute?.Invoke();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"실행 중 오류가 발생했습니다:\n{ex.Message}", "WindowSpot",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        Hide();
    }

    // ----- 즐겨찾기 / 자주 쓰는 앱 -----

    private void RefreshFavorites()
    {
        _favoritesItems.Clear();
        foreach (var key in _favoritesStore.Keys)
        {
            if (key.StartsWith("app:", StringComparison.Ordinal))
            {
                string path = key["app:".Length..];
                var app = _appIndexer.Apps.FirstOrDefault(a => a.Path.Equals(path, StringComparison.OrdinalIgnoreCase));
                if (app is null) continue;

                _favoritesItems.Add(new IconButtonItem
                {
                    Key = key,
                    Label = app.Name,
                    Icon = IconExtractor.GetIcon(app.Path),
                    OnClick = () => LaunchApp(app),
                });
            }
            else if (key.StartsWith("url:", StringComparison.Ordinal))
            {
                string domain = key["url:".Length..];
                _favoritesItems.Add(new IconButtonItem
                {
                    Key = key,
                    Label = domain,
                    Icon = null,
                    OnClick = () => OpenUrl(domain),
                });
            }
        }
    }

    private void RefreshTopApps()
    {
        _topAppsItems.Clear();
        foreach (var app in _usageStore.GetTopApps(4, _appIndexer.Apps))
        {
            _topAppsItems.Add(new IconButtonItem
            {
                Key = app.Path,
                Label = app.Name,
                Icon = IconExtractor.GetIcon(app.Path),
                OnClick = () => LaunchApp(app),
            });
        }
        TopAppsSection.Visibility = _topAppsItems.Count > 0 && string.IsNullOrWhiteSpace(QueryBox.Text)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void LaunchApp(AppEntry app)
    {
        try
        {
            _usageStore.Increment(app.Path);
            Process.Start(new ProcessStartInfo(app.Path)
            {
                Arguments = app.Arguments,
                UseShellExecute = true,
                WorkingDirectory = System.IO.Path.GetDirectoryName(app.Path),
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"실행 중 오류가 발생했습니다:\n{ex.Message}", "WindowSpot",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        Hide();
    }

    private void OpenUrl(string domain)
    {
        string target = domain.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || domain.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            ? domain
            : $"https://{domain}";

        try
        {
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"열기 중 오류가 발생했습니다:\n{ex.Message}", "WindowSpot",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        Hide();
    }

    private void IconButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: IconButtonItem item }) item.OnClick();
    }

    private void RemoveFavorite_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Parent: ContextMenu { PlacementTarget: Button { DataContext: IconButtonItem item } } })
        {
            _favoritesStore.Remove(item.Key);
            RefreshFavorites();
        }
    }

    private void AddFavoriteButton_Click(object sender, RoutedEventArgs e)
    {
        _dialogOpen = true;
        var dialog = new AddFavoriteWindow(_appIndexer.Apps) { Owner = this };
        bool? result = dialog.ShowDialog();
        _dialogOpen = false;

        if (result == true && dialog.SelectedKey is not null)
        {
            _favoritesStore.Add(dialog.SelectedKey);
            RefreshFavorites();
        }
    }

    // ----- Ask AI 채팅 -----

    private void AskAiButton_Click(object sender, RoutedEventArgs e)
    {
        string query = QueryBox.Text.Trim();
        EnterChatMode(string.IsNullOrEmpty(query) ? null : query);
    }

    private void EnterChatMode(string? initialQuery)
    {
        _chatMessages.Clear();
        _inChatMode = true;
        SearchModePanel.Visibility = Visibility.Collapsed;
        ChatModePanel.Visibility = Visibility.Visible;
        ChatInputBox.Focus();

        if (!string.IsNullOrWhiteSpace(initialQuery)) SendChat(initialQuery);
    }

    private void BackFromChat_Click(object sender, MouseButtonEventArgs e) => BackFromChat();

    private void BackFromChat()
    {
        _inChatMode = false;
        ChatModePanel.Visibility = Visibility.Collapsed;
        SearchModePanel.Visibility = Visibility.Visible;
        QueryBox.Focus();
    }

    private void ChatInputBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            SendCurrentChatInput();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Hide();
            e.Handled = true;
        }
    }

    private void SendChat_Click(object sender, MouseButtonEventArgs e) => SendCurrentChatInput();

    private void SendCurrentChatInput()
    {
        string text = ChatInputBox.Text;
        if (string.IsNullOrWhiteSpace(text)) return;
        ChatInputBox.Clear();
        SendChat(text);
    }

    private async void SendChat(string text)
    {
        string trimmed = text.Trim();

        if (trimmed == "/resume")
        {
            var saved = _chatSessionStore.LoadLast();
            if (saved.Count > 0)
            {
                _chatMessages.Clear();
                foreach (var m in saved) _chatMessages.Add(m);
                ScrollChatToEnd();
            }
            return;
        }

        _chatMessages.Add(new ChatMessageView { Text = trimmed, IsUser = true });
        var loadingMessage = new ChatMessageView { Text = "•••", IsUser = false };
        _chatMessages.Add(loadingMessage);
        ScrollChatToEnd();

        try
        {
            string apiKey = _settingsStore.GetOpenRouterApiKey();
            string model = _settingsStore.GetOpenRouterModel();
            var history = _chatMessages.Where(m => m != loadingMessage).ToList();
            string reply = await _openRouterClient.ChatAsync(apiKey, model, history);

            _chatMessages.Remove(loadingMessage);
            _chatMessages.Add(new ChatMessageView { Text = reply, IsUser = false });
        }
        catch (Exception ex)
        {
            _chatMessages.Remove(loadingMessage);
            _chatMessages.Add(new ChatMessageView { Text = $"오류: {ex.Message}", IsUser = false });
        }

        _chatSessionStore.Save(_chatMessages);
        ScrollChatToEnd();
    }

    private void ScrollChatToEnd()
    {
        Dispatcher.InvokeAsync(() => ChatScroll.ScrollToEnd(), DispatcherPriority.Background);
    }
}
