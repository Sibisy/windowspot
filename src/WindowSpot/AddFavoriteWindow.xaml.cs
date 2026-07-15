using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WindowSpot.Services;

namespace WindowSpot;

public partial class AddFavoriteWindow : Window
{
    private class Row
    {
        public required string Display { get; init; }
        public bool IsUrl { get; init; }
        public AppEntry? App { get; init; }
        public string? Domain { get; init; }
    }

    private readonly IReadOnlyList<AppEntry> _apps;
    private readonly ObservableCollection<Row> _rows = new();

    public string? SelectedKey { get; private set; }
    public string? SelectedLabel { get; private set; }
    public string? SelectedAppPath { get; private set; }

    public AddFavoriteWindow(IReadOnlyList<AppEntry> apps)
    {
        InitializeComponent();
        _apps = apps;
        RowsList.ItemsSource = _rows;
        Refresh(string.Empty);
        Loaded += (_, _) => FilterBox.Focus();
    }

    private void Refresh(string filter)
    {
        _rows.Clear();

        if (!string.IsNullOrWhiteSpace(filter))
        {
            _rows.Add(new Row { Display = $"\"{filter.Trim()}\" 사이트로 추가", IsUrl = true, Domain = filter.Trim() });
        }

        var matches = string.IsNullOrWhiteSpace(filter)
            ? _apps
            : _apps.Where(a => a.Name.Contains(filter.Trim(), System.StringComparison.OrdinalIgnoreCase)
                                || KoreanSearch.Matches(a.Name, filter.Trim()));

        foreach (var app in matches.OrderBy(a => a.Name))
        {
            _rows.Add(new Row { Display = app.Name, App = app });
        }

        if (_rows.Count > 0) RowsList.SelectedIndex = 0;
    }

    private void FilterBox_TextChanged(object sender, TextChangedEventArgs e) => Refresh(FilterBox.Text);

    private void RowsList_MouseDoubleClick(object sender, MouseButtonEventArgs e) => Confirm();

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                DialogResult = false;
                Close();
                e.Handled = true;
                break;
            case Key.Enter:
                Confirm();
                e.Handled = true;
                break;
            case Key.Down:
                if (RowsList.SelectedIndex < _rows.Count - 1) RowsList.SelectedIndex++;
                e.Handled = true;
                break;
            case Key.Up:
                if (RowsList.SelectedIndex > 0) RowsList.SelectedIndex--;
                e.Handled = true;
                break;
        }
    }

    private void Confirm()
    {
        if (RowsList.SelectedItem is not Row row) return;

        if (row.IsUrl && row.Domain is not null)
        {
            SelectedKey = FavoritesStore.UrlKey(row.Domain);
            SelectedLabel = row.Domain;
        }
        else if (row.App is not null)
        {
            SelectedKey = FavoritesStore.AppKey(row.App.Path);
            SelectedLabel = row.App.Name;
            SelectedAppPath = row.App.Path;
        }
        else
        {
            return;
        }

        DialogResult = true;
        Close();
    }
}
