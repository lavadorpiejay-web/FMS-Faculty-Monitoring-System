using FacultyMonitoring.Services;
using Microsoft.Maui.Controls.Shapes;

namespace FacultyMonitoring.Controls;

/// <summary>
/// The top bar of a page: the page title on the left and a bell on the right with a red count badge (like social media).
/// Tapping the bell opens the Notifications page. The count refreshes when the page appears and whenever a notification changes.
/// </summary>
public sealed class NotificationTitleView : Grid
{
    private readonly NotificationService _service;
    private readonly Label _count;
    private readonly Border _badge;

    public NotificationTitleView(Page page, NotificationService service)
    {
        _service = service;
        ColumnDefinitions = new ColumnDefinitionCollection { new(GridLength.Star), new(GridLength.Auto) };
        HorizontalOptions = LayoutOptions.Fill;
        VerticalOptions = LayoutOptions.Fill;

        var title = new Label
        {
            FontSize = 20, FontFamily = "OpenSansSemibold", TextColor = Colors.White,
            VerticalOptions = LayoutOptions.Center, LineBreakMode = LineBreakMode.TailTruncation
        };
        title.SetBinding(Label.TextProperty, new Binding(nameof(Page.Title), source: page));

        _count = new Label
        {
            FontSize = 11, FontAttributes = FontAttributes.Bold, TextColor = Colors.White,
            HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center
        };
        _badge = new Border
        {
            BackgroundColor = Color.FromArgb("#E0301E"),
            Stroke = Colors.White, StrokeThickness = 1.5,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(10) },
            Padding = new Thickness(5, 0), MinimumWidthRequest = 20, HeightRequest = 20,
            HorizontalOptions = LayoutOptions.End, VerticalOptions = LayoutOptions.Start,
            Margin = new Thickness(0, 2, 0, 0), IsVisible = false, Content = _count
        };

        var bell = new Grid
        {
            WidthRequest = 48, HeightRequest = 44, VerticalOptions = LayoutOptions.Center, Margin = new Thickness(0, 0, 4, 0)
        };
        bell.Children.Add(new Label
        {
            Text = "\U0001F514", FontSize = 24, TextColor = Colors.White,
            HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center
        });
        bell.Children.Add(_badge);
        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) => await Shell.Current.GoToAsync("Notifications");
        bell.GestureRecognizers.Add(tap);
        SemanticProperties.SetDescription(bell, "Notifications");

        title.SetValue(ColumnProperty, 0);
        bell.SetValue(ColumnProperty, 1);
        Children.Add(title);
        Children.Add(bell);

        page.Appearing += (_, _) => _ = RefreshAsync();
        Loaded += (_, _) => { _service.Changed += OnChanged; _ = RefreshAsync(); };
        Unloaded += (_, _) => _service.Changed -= OnChanged;
    }

    private void OnChanged() => MainThread.BeginInvokeOnMainThread(() => _ = RefreshAsync());

    public async Task RefreshAsync()
    {
        try
        {
            var n = await _service.UnreadCountAsync();
            _badge.IsVisible = n > 0;
            _count.Text = n > 9 ? "9+" : n.ToString();
        }
        catch { /* the bell is only a hint; never break the page */ }
    }
}
