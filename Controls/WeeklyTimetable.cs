using System.Windows.Input;
using FacultyMonitoring.Models;
using FacultyMonitoring.Services;
using Microsoft.Maui.Controls.Shapes;

namespace FacultyMonitoring.Controls;

/// <summary>
/// A weekly timetable: Monday to Sunday across, the hours down, one coloured block per class or vacant time.
/// Swipe sideways on a phone. Set Compact for a smaller version. Tapping a block runs BlockTappedCommand with the block.
/// The hours shown follow the blocks (at least six hours).
/// </summary>
public sealed class WeeklyTimetable : ContentView
{
    public static readonly BindableProperty BlocksProperty = BindableProperty.Create(
        nameof(Blocks), typeof(IEnumerable<TimetableBlock>), typeof(WeeklyTimetable), null,
        propertyChanged: (b, _, _) => ((WeeklyTimetable)b).Rebuild());

    public static readonly BindableProperty BlockTappedCommandProperty = BindableProperty.Create(
        nameof(BlockTappedCommand), typeof(ICommand), typeof(WeeklyTimetable), null);

    public static readonly BindableProperty CompactProperty = BindableProperty.Create(
        nameof(Compact), typeof(bool), typeof(WeeklyTimetable), false,
        propertyChanged: (b, _, _) => ((WeeklyTimetable)b).Rebuild());

    public IEnumerable<TimetableBlock>? Blocks { get => (IEnumerable<TimetableBlock>?)GetValue(BlocksProperty); set => SetValue(BlocksProperty, value); }
    public ICommand? BlockTappedCommand { get => (ICommand?)GetValue(BlockTappedCommandProperty); set => SetValue(BlockTappedCommandProperty, value); }
    public bool Compact { get => (bool)GetValue(CompactProperty); set => SetValue(CompactProperty, value); }

    private static readonly string[] Palette =
        { "#97CBCF", "#C5DA86", "#CA9AD0", "#E38585", "#F8B88C", "#FFE074", "#C5F2C5", "#9FC3F2" };

    private static readonly DayOfWeek[] Week =
        { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday };

    private static Color LineColor => Color.FromArgb("#C9D3CC");
    private static Color InkColor => Color.FromArgb("#16221B");

    public WeeklyTimetable() => Rebuild();

    private static Color ColorFor(string key)
    {
        if (string.IsNullOrEmpty(key)) return Color.FromArgb(Palette[6]);
        unchecked
        {
            var h = 2166136261u;
            foreach (var ch in key) h = (h ^ ch) * 16777619u;
            return Color.FromArgb(Palette[(int)(h % (uint)Palette.Length)]);
        }
    }

    private static void Place(View view, double x, double y, double w, double h)
        => view.SetValue(AbsoluteLayout.LayoutBoundsProperty, new Rect(x, y, w, h));

    private void Rebuild()
    {
        var compact = Compact;
        double timeW = compact ? 46 : 58, dayW = compact ? 64 : 118, hourH = compact ? 38 : 72, headerH = compact ? 24 : 30;
        var blocks = Blocks?.ToList() ?? new List<TimetableBlock>();

        var minM = blocks.Count > 0 ? blocks.Min(b => ScheduleRules.Minutes(b.Start)) : 8 * 60;
        var maxM = blocks.Count > 0 ? blocks.Max(b => ScheduleRules.Minutes(b.End)) : 14 * 60;
        var startH = Math.Max(0, minM / 60);
        var endH = Math.Min(24, (maxM + 59) / 60);
        if (endH - startH < 6) endH = Math.Min(24, startH + 6);
        if (endH - startH < 6) startH = Math.Max(0, endH - 6);

        var width = timeW + 7 * dayW;
        var height = headerH + (endH - startH) * hourH;
        var layout = new AbsoluteLayout { WidthRequest = width, HeightRequest = height, BackgroundColor = Colors.White };

        // Day headings.
        for (var i = 0; i < 7; i++)
        {
            var name = Week[i].ToString();
            var heading = new Label
            {
                Text = compact ? name[..3] : name, FontSize = compact ? 10 : 12, TextColor = InkColor,
                HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center
            };
            Place(heading, timeW + i * dayW, 0, dayW, headerH);
            layout.Children.Add(heading);
        }

        // Hour lines and labels.
        for (var h = startH; h <= endH; h++)
        {
            var y = headerH + (h - startH) * hourH;
            var line = new BoxView { Color = LineColor };
            Place(line, timeW, y, 7 * dayW, 1);
            layout.Children.Add(line);
            var label = new Label
            {
                Text = ScheduleRules.Pretty($"{h % 24:00}:00"), FontSize = compact ? 9 : 10, TextColor = InkColor,
                HorizontalTextAlignment = TextAlignment.End, VerticalTextAlignment = TextAlignment.Center
            };
            Place(label, 0, y - 8, timeW - 6, 16);
            layout.Children.Add(label);
        }

        // Day columns.
        for (var i = 0; i <= 7; i++)
        {
            var col = new BoxView { Color = i == 0 || i == 7 ? InkColor : LineColor };
            Place(col, timeW + i * dayW, headerH, 1, height - headerH);
            layout.Children.Add(col);
        }

        // Blocks.
        foreach (var b in blocks)
        {
            var d = Array.IndexOf(Week, b.Day);
            if (d < 0) continue;
            var startM = ScheduleRules.Minutes(b.Start);
            var endM = ScheduleRules.Minutes(b.End);
            var y = headerH + (startM - startH * 60) / 60.0 * hourH + 1;
            var h = Math.Max(22, (endM - startM) / 60.0 * hourH - 2);

            // Order in a full-size block: subject, then the room (Line3), then the time (Line2).
            // The room used to come last and was cut off when the subject or the time wrapped onto a second line.
            var text = new VerticalStackLayout { Spacing = 1 };
            text.Children.Add(new Label
            {
                Text = b.Title, FontAttributes = FontAttributes.Bold, FontSize = compact ? 9 : 11, TextColor = InkColor,
                MaxLines = 2, LineBreakMode = LineBreakMode.TailTruncation
            });
            if (!compact && b.Line3.Length > 0)
                text.Children.Add(new Label
                {
                    Text = b.Line3, FontSize = 11, FontAttributes = FontAttributes.Bold, TextColor = InkColor,
                    MaxLines = 1, LineBreakMode = LineBreakMode.TailTruncation
                });
            if (b.Line2.Length > 0)
                text.Children.Add(new Label { Text = b.Line2, FontSize = compact ? 8 : 10, TextColor = InkColor, MaxLines = compact ? 2 : 1, LineBreakMode = LineBreakMode.TailTruncation });

            var card = new Border
            {
                BackgroundColor = ColorFor(b.ColorKey), Stroke = Color.FromArgb("#2B3A31"), StrokeThickness = 1,
                StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(6) },
                Padding = new Thickness(compact ? 3 : 5, compact ? 2 : 3), Content = text
            };
            var tap = new TapGestureRecognizer();
            var block = b;
            tap.Tapped += (_, _) =>
            {
                var cmd = BlockTappedCommand;
                if (cmd?.CanExecute(block) == true) cmd.Execute(block);
            };
            card.GestureRecognizers.Add(tap);
            Place(card, timeW + d * dayW + 1, y, dayW - 2, h);
            layout.Children.Add(card);
        }

        Content = new Border
        {
            Stroke = Color.FromArgb("#2B3A31"), StrokeThickness = 1.5, Padding = 0, BackgroundColor = Colors.White,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(12) },
            Content = new ScrollView { Orientation = ScrollOrientation.Horizontal, Content = layout }
        };
    }
}
