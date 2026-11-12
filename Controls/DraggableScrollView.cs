using Microsoft.Maui.Controls.Shapes;

namespace FacultyMonitoring.Controls;

/// <summary>
/// A ScrollView with its own scroll bar on the right edge that the user can drag, or tap to jump.
/// Use it exactly like a ScrollView: put the page content inside it.
/// The built-in Android scroll bar is hidden so only this one shows.
/// </summary>
[ContentProperty(nameof(Body))]
public class DraggableScrollView : ContentView
{
    private const double TrackWidth = 20;
    private const double ThumbWidth = 6;
    private const double MinThumbHeight = 40;

    private readonly ScrollView _scroll;
    private readonly Grid _track;
    private readonly BoxView _rail;
    private readonly Border _thumb;

    private bool _dragging;
    private double _dragStartThumbY;
    private View? _body;

    public DraggableScrollView()
    {
        _scroll = new ScrollView
        {
            Orientation = ScrollOrientation.Vertical,
            VerticalScrollBarVisibility = ScrollBarVisibility.Never,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Never
        };

        _rail = new BoxView
        {
            WidthRequest = 3,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Fill,
            CornerRadius = 2,
            Color = Color.FromArgb("#DCE3DE")
        };

        _thumb = new Border
        {
            WidthRequest = ThumbWidth,
            HeightRequest = MinThumbHeight,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Start,
            BackgroundColor = Color.FromArgb("#1B6B40"),
            Stroke = Colors.Transparent,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 3 }
        };

        _track = new Grid
        {
            WidthRequest = TrackWidth,
            HorizontalOptions = LayoutOptions.End,
            VerticalOptions = LayoutOptions.Fill,
            Margin = new Thickness(0, 6),
            BackgroundColor = Colors.Transparent,
            IsVisible = false
        };
        _track.Children.Add(_rail);
        _track.Children.Add(_thumb);

        // Drag: gestures sit on the track, which never moves, so the touch position stays stable.
        var pan = new PanGestureRecognizer();
        pan.PanUpdated += OnPanUpdated;
        _track.GestureRecognizers.Add(pan);

        // Tap: jump so the thumb is centred on the tapped spot.
        var tap = new TapGestureRecognizer();
        tap.Tapped += OnTrackTapped;
        _track.GestureRecognizers.Add(tap);

        var root = new Grid();
        root.Children.Add(_scroll);
        root.Children.Add(_track);
        base.Content = root;

        _scroll.Scrolled += (_, _) => { if (!_dragging) UpdateThumb(); };
        _scroll.SizeChanged += (_, _) => UpdateThumb();
        _track.SizeChanged += (_, _) => UpdateThumb();
    }

    /// <summary>The scrolling content (what you would normally put inside a ScrollView).</summary>
    public View? Body
    {
        get => _body;
        set
        {
            if (_body != null) _body.SizeChanged -= OnBodySizeChanged;
            _body = value;
            _scroll.Content = value;
            if (_body != null) _body.SizeChanged += OnBodySizeChanged;
        }
    }

    private void OnBodySizeChanged(object? sender, EventArgs e) => UpdateThumb();

    private double MaxScroll => Math.Max(0, _scroll.ContentSize.Height - _scroll.Height);

    private double ThumbTravel => Math.Max(0, _track.Height - _thumb.HeightRequest);

    private void UpdateThumb()
    {
        var viewport = _scroll.Height;
        var content = _scroll.ContentSize.Height;
        var trackHeight = _track.Height;

        if (viewport <= 0 || trackHeight <= 0 || content <= viewport + 1)
        {
            _track.IsVisible = false;
            return;
        }

        _track.IsVisible = true;

        var thumbHeight = Math.Clamp(trackHeight * viewport / content, MinThumbHeight, trackHeight);
        _thumb.HeightRequest = thumbHeight;

        var travel = Math.Max(0, trackHeight - thumbHeight);
        var ratio = MaxScroll <= 0 ? 0 : Math.Clamp(_scroll.ScrollY / MaxScroll, 0, 1);
        _thumb.TranslationY = travel * ratio;
    }

    private async void OnPanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        switch (e.StatusType)
        {
            case GestureStatus.Started:
                _dragging = true;
                _dragStartThumbY = _thumb.TranslationY;
                break;

            case GestureStatus.Running:
                if (!_dragging) return;
                var travel = ThumbTravel;
                if (travel <= 0) return;
                var y = Math.Clamp(_dragStartThumbY + e.TotalY, 0, travel);
                _thumb.TranslationY = y;
                await _scroll.ScrollToAsync(0, MaxScroll * (y / travel), false);
                break;

            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                _dragging = false;
                UpdateThumb();
                break;
        }
    }

    private async void OnTrackTapped(object? sender, TappedEventArgs e)
    {
        var point = e.GetPosition(_track);
        var travel = ThumbTravel;
        if (point == null || travel <= 0) return;

        var y = Math.Clamp(point.Value.Y - _thumb.HeightRequest / 2, 0, travel);
        await _scroll.ScrollToAsync(0, MaxScroll * (y / travel), true);
    }
}
