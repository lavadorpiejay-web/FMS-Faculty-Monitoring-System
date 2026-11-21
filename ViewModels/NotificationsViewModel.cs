using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FacultyMonitoring.Models;
using FacultyMonitoring.Services;

namespace FacultyMonitoring.ViewModels;

/// <summary>The page the bell opens: the signed-in person's notifications, newest first. Tap one to mark it read.</summary>
public partial class NotificationsViewModel : ObservableObject
{
    private readonly NotificationService _notifications;
    public NotificationsViewModel(NotificationService notifications) => _notifications = notifications;

    public ObservableCollection<NotificationRow> Items { get; } = new();

    [ObservableProperty] private bool _isEmpty = true;
    [ObservableProperty] private bool _hasUnread;
    [ObservableProperty] private string _summary = "";

    public async Task LoadAsync()
    {
        var list = await _notifications.GetMineAsync();
        Items.Clear();
        foreach (var n in list) Items.Add(new NotificationRow(n.Id, n.Title, n.Message, TimeText(n.CreatedUtc), !n.IsRead));
        var unread = list.Count(n => !n.IsRead);
        IsEmpty = list.Count == 0;
        HasUnread = unread > 0;
        Summary = unread == 0 ? "You are all caught up." : unread == 1 ? "1 unread notification." : $"{unread} unread notifications.";
    }

    [RelayCommand]
    private async Task OpenAsync(NotificationRow? row)
    {
        if (row is null || !row.IsUnread) return;
        await _notifications.MarkReadAsync(row.Id);
        await LoadAsync();
    }

    [RelayCommand]
    private async Task MarkAllReadAsync()
    {
        await _notifications.MarkAllReadAsync();
        await LoadAsync();
    }

    private static string TimeText(DateTime utc)
    {
        var age = DateTime.UtcNow - utc;
        if (age.TotalMinutes < 1) return "Just now";
        if (age.TotalMinutes < 60) return $"{(int)age.TotalMinutes} min ago";
        if (age.TotalHours < 24) return $"{(int)age.TotalHours} h ago";
        return utc.ToLocalTime().ToString("MMM d, h:mm tt");
    }
}
