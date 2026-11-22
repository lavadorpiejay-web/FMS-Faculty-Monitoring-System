using FacultyMonitoring.Models;

namespace FacultyMonitoring.Views;

/// <summary>A small full-screen list with a tick box per row. Used to choose exactly which rows to delete. Result is null when cancelled.</summary>
public sealed class PickItemsPage : ContentPage
{
    private readonly TaskCompletionSource<IReadOnlyList<string>?> _result = new();
    private readonly List<(CheckBox Box, string Id)> _rows = new();
    private readonly Button _delete;

    public Task<IReadOnlyList<string>?> Result => _result.Task;

    public PickItemsPage(string heading, IReadOnlyList<ListRow> items)
    {
        Title = heading;
        BackgroundColor = Colors.White;

        var list = new VerticalStackLayout { Spacing = 12, Padding = new Thickness(20) };
        list.Children.Add(new Label { Text = heading, FontSize = 20, FontAttributes = FontAttributes.Bold, TextColor = Colors.Black });
        list.Children.Add(new Label { Text = "Tick the ones to delete, then tap Delete selected.", FontSize = 13, TextColor = Colors.Gray });

        var all = new HorizontalStackLayout { Spacing = 6 };
        var allBox = new CheckBox();
        allBox.CheckedChanged += (_, e) => { foreach (var r in _rows) r.Box.IsChecked = e.Value; };
        all.Children.Add(allBox);
        all.Children.Add(new Label { Text = "Select all", VerticalOptions = LayoutOptions.Center, TextColor = Colors.Black });
        list.Children.Add(all);

        foreach (var item in items)
        {
            var box = new CheckBox();
            box.CheckedChanged += (_, _) => UpdateButton();
            _rows.Add((box, item.Id));

            var text = new VerticalStackLayout { Spacing = 1, VerticalOptions = LayoutOptions.Center };
            text.Children.Add(new Label { Text = item.Title, FontAttributes = FontAttributes.Bold, FontSize = 14, TextColor = Colors.Black });
            if (item.Detail.Length > 0)
                text.Children.Add(new Label { Text = item.Detail, FontSize = 12, TextColor = Colors.Gray });

            var row = new HorizontalStackLayout { Spacing = 6 };
            row.Children.Add(box);
            row.Children.Add(text);
            list.Children.Add(row);
        }

        var cancel = new Button { Text = "Cancel" };
        if (Application.Current?.Resources.TryGetValue("OutlineButton", out var style) == true && style is Style outline)
            cancel.Style = outline;
        cancel.Clicked += (_, _) => _result.TrySetResult(null);

        _delete = new Button { Text = "Delete selected" };
        _delete.Clicked += (_, _) =>
        {
            var ids = _rows.Where(r => r.Box.IsChecked).Select(r => r.Id).ToList();
            if (ids.Count > 0) _result.TrySetResult(ids);
        };

        var buttons = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) },
            ColumnSpacing = 8, Padding = new Thickness(20, 8, 20, 16)
        };
        buttons.Add(cancel, 0, 0);
        buttons.Add(_delete, 1, 0);

        var grid = new Grid { RowDefinitions = { new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto) } };
        grid.Add(new ScrollView { Content = list }, 0, 0);
        grid.Add(buttons, 0, 1);
        Content = grid;
    }

    private void UpdateButton()
    {
        var n = _rows.Count(r => r.Box.IsChecked);
        _delete.Text = n == 0 ? "Delete selected" : $"Delete selected ({n})";
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _result.TrySetResult(null);   // Android back button or any other way out counts as cancel
    }
}
