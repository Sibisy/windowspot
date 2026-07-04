using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WindowSpot.Models;
using WindowSpot.Services;
using MessageBox = System.Windows.MessageBox;

namespace WindowSpot;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<SearchResult> _results = new();
    private readonly SearchEngine _searchEngine;
    private CancellationTokenSource? _searchCts;

    public MainWindow(SearchEngine searchEngine)
    {
        InitializeComponent();
        _searchEngine = searchEngine;
        ResultsList.ItemsSource = _results;
    }

    /// <summary>전역 단축키로 호출됨: 활성 화면 중앙 상단에 창을 띄우고 입력창에 포커스를 준다.</summary>
    public void ShowAtCenter()
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Left + (workArea.Width - Width) / 2;
        Top = workArea.Top + workArea.Height * 0.18;

        QueryBox.Text = string.Empty;
        _results.Clear();

        Show();
        Activate();
        QueryBox.Focus();
    }

    private void Window_Deactivated(object sender, EventArgs e) => Hide();

    private async void QueryBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _searchCts?.Cancel();
        var cts = new CancellationTokenSource();
        _searchCts = cts;

        string query = QueryBox.Text;
        if (string.IsNullOrWhiteSpace(query))
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

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
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
}
