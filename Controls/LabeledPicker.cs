using System.Collections;

namespace FacultyMonitoring.Controls;

/// <summary>A caption above a bordered Picker, so a form field takes one line of XAML. Bind ItemsSource and SelectedItem like on a Picker.</summary>
public class LabeledPicker : VerticalStackLayout
{
    public static readonly BindableProperty CaptionProperty = BindableProperty.Create(
        nameof(Caption), typeof(string), typeof(LabeledPicker), "",
        propertyChanged: (b, _, n) => ((LabeledPicker)b)._caption.Text = (string)n);

    public static readonly BindableProperty TitleProperty = BindableProperty.Create(
        nameof(Title), typeof(string), typeof(LabeledPicker), "",
        propertyChanged: (b, _, n) => ((LabeledPicker)b)._picker.Title = (string)n);

    public static readonly BindableProperty ItemsSourceProperty = BindableProperty.Create(
        nameof(ItemsSource), typeof(IList), typeof(LabeledPicker), null,
        propertyChanged: (b, _, n) => ((LabeledPicker)b)._picker.ItemsSource = (IList?)n);

    public static readonly BindableProperty SelectedItemProperty = BindableProperty.Create(
        nameof(SelectedItem), typeof(object), typeof(LabeledPicker), null, BindingMode.TwoWay,
        propertyChanged: (b, _, n) =>
        {
            var p = ((LabeledPicker)b)._picker;
            if (!Equals(p.SelectedItem, n)) p.SelectedItem = n;
        });

    private readonly Label _caption;
    private readonly Picker _picker;

    public LabeledPicker()
    {
        Spacing = 4;
        _caption = new Label { FontSize = 13, FontFamily = "OpenSansSemibold", TextColor = Color.FromArgb("#0B4D2C") };
        _picker = new Picker { TextColor = Color.FromArgb("#16221B"), TitleColor = Color.FromArgb("#8FA096") };
        _picker.SelectedIndexChanged += (_, _) =>
        {
            if (!Equals(SelectedItem, _picker.SelectedItem)) SelectedItem = _picker.SelectedItem;
        };
        var border = new Border
        {
            Stroke = Color.FromArgb("#DCE3DE"),
            Padding = new Thickness(12, 0),
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = new CornerRadius(10) },
            Content = _picker
        };
        Children.Add(_caption);
        Children.Add(border);
    }

    public string Caption { get => (string)GetValue(CaptionProperty); set => SetValue(CaptionProperty, value); }
    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public IList? ItemsSource { get => (IList?)GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }
    public object? SelectedItem { get => GetValue(SelectedItemProperty); set => SetValue(SelectedItemProperty, value); }
}
