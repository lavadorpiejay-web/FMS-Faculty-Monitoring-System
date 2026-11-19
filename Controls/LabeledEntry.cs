namespace FacultyMonitoring.Controls;

/// <summary>A caption above a bordered Entry. Bind Text two-way like on an Entry.</summary>
public class LabeledEntry : VerticalStackLayout
{
    public static readonly BindableProperty CaptionProperty = BindableProperty.Create(
        nameof(Caption), typeof(string), typeof(LabeledEntry), "",
        propertyChanged: (b, _, n) => ((LabeledEntry)b)._caption.Text = (string)n);

    public static readonly BindableProperty PlaceholderProperty = BindableProperty.Create(
        nameof(Placeholder), typeof(string), typeof(LabeledEntry), "",
        propertyChanged: (b, _, n) => ((LabeledEntry)b)._entry.Placeholder = (string)n);

    public static readonly BindableProperty TextProperty = BindableProperty.Create(
        nameof(Text), typeof(string), typeof(LabeledEntry), "", BindingMode.TwoWay,
        propertyChanged: (b, _, n) =>
        {
            var e = ((LabeledEntry)b)._entry;
            if (e.Text != (string?)n) e.Text = (string?)n;
        });

    private readonly Label _caption;
    private readonly Entry _entry;

    public LabeledEntry()
    {
        Spacing = 4;
        _caption = new Label { FontSize = 13, FontFamily = "OpenSansSemibold", TextColor = Color.FromArgb("#0B4D2C") };
        _entry = new Entry();
        _entry.TextChanged += (_, e) => { if (Text != e.NewTextValue) Text = e.NewTextValue ?? ""; };
        var border = new Border
        {
            Stroke = Color.FromArgb("#DCE3DE"),
            Padding = new Thickness(12, 0),
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = new CornerRadius(10) },
            Content = _entry
        };
        Children.Add(_caption);
        Children.Add(border);
    }

    public string Caption { get => (string)GetValue(CaptionProperty); set => SetValue(CaptionProperty, value); }
    public string Placeholder { get => (string)GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }
    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
}
