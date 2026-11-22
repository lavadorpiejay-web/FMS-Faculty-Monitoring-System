using FacultyMonitoring.Models;
using FacultyMonitoring.Views;

namespace FacultyMonitoring.ViewModels;

/// <summary>
/// The "what do you want to delete?" question shared by every screen that removes schedule-like rows
/// (READS vacant times, IMS schedules). The person picks a scope (only this one, the same day, the same time,
/// the same subject, everything) or chooses exactly which rows to delete, then confirms the count.
/// Returns the ids to delete, or null when cancelled.
/// </summary>
public static class RemovePrompt
{
    public sealed record Scope(string Label, IReadOnlyList<string> Ids);

    private const string PickLabel = "Choose which ones to delete...";

    public static async Task<IReadOnlyList<string>?> AskAsync(
        string heading, string noun, IReadOnlyList<Scope> scopes, IReadOnlyList<ListRow> candidates)
    {
        var shell = Shell.Current;
        var buttons = scopes.Select(s => s.Label).ToList();
        if (candidates.Count > 1) buttons.Add(PickLabel);

        var choice = await shell.DisplayActionSheetAsync(heading, "Cancel", null, buttons.ToArray());
        if (string.IsNullOrEmpty(choice) || choice == "Cancel") return null;

        IReadOnlyList<string>? ids = choice == PickLabel
            ? await PickAsync(heading, candidates)
            : scopes.FirstOrDefault(s => s.Label == choice)?.Ids;
        if (ids is null || ids.Count == 0) return null;

        var yes = await shell.DisplayAlertAsync(heading,
            $"Delete {ids.Count} {noun}{(ids.Count == 1 ? "" : "s")}? This cannot be undone.", "Delete", "Cancel");
        return yes ? ids : null;
    }

    private static async Task<IReadOnlyList<string>?> PickAsync(string heading, IReadOnlyList<ListRow> candidates)
    {
        var nav = Shell.Current.Navigation;
        var page = new PickItemsPage(heading, candidates);
        await nav.PushModalAsync(page);
        var picked = await page.Result;
        if (nav.ModalStack.Contains(page)) await nav.PopModalAsync();   // already gone when Android's back button was used
        return picked;
    }
}
