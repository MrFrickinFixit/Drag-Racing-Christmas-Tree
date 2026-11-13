using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;

namespace DragTree;

public partial class MainWindow
{
    private readonly Key[] launchKeys = [Key.LeftShift, Key.RightShift];
    private sealed record KeyOption(Key Key, string Label);
    private sealed record SavedKeys(string Player1, string Player2);
    private static readonly string SettingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DragTree", "keys.json");
    private static readonly Key[] AllowedKeys = [Key.LeftShift, Key.RightShift, Key.Space, Key.LeftCtrl, Key.RightCtrl, Key.Left, Key.Right, Key.Up, Key.Down,
        .. Enumerable.Range((int)Key.A, 26).Select(k => (Key)k),
        .. Enumerable.Range((int)Key.D0, 10).Select(k => (Key)k),
        .. Enumerable.Range((int)Key.NumPad0, 10).Select(k => (Key)k)];

    private static string KeyLabel(Key key) => key switch
    {
        Key.LeftShift => "Left Shift", Key.RightShift => "Right Shift",
        Key.LeftCtrl => "Left Ctrl", Key.RightCtrl => "Right Ctrl",
        >= Key.D0 and <= Key.D9 => ((int)key - (int)Key.D0).ToString(),
        >= Key.NumPad0 and <= Key.NumPad9 => $"Numpad {(int)key - (int)Key.NumPad0}",
        _ => key.ToString()
    };

    private void InitializeKeySettings()
    {
        var choices = AllowedKeys.Select(k => new KeyOption(k, KeyLabel(k))).ToArray();
        Player1Key.ItemsSource = choices;
        Player2Key.ItemsSource = choices;
        try
        {
            if (File.Exists(SettingsPath))
            {
                var saved = JsonSerializer.Deserialize<SavedKeys>(File.ReadAllText(SettingsPath));
                if (saved is not null && Enum.TryParse<Key>(saved.Player1, out var first) && Enum.TryParse<Key>(saved.Player2, out var second))
                    TrySetLaunchKeys(first, second, out _);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { }
        RefreshKeyLabels();
    }

    private bool TrySetLaunchKeys(Key first, Key second, out string error)
    {
        error = "";
        if (active || held.Any(x => x) || awaitRelease) { error = "Release both launch keys and finish the race before changing controls."; return false; }
        if (first == second) { error = "Choose a different key for each racer."; return false; }
        if (!AllowedKeys.Contains(first) || !AllowedKeys.Contains(second)) { error = "Choose keys from the list. Escape is reserved for canceling."; return false; }
        launchKeys[0] = first;
        launchKeys[1] = second;
        rawShiftInput?.ConfigureKeys(first, second);
        RefreshKeyLabels();
        return true;
    }

    private void RefreshKeyLabels()
    {
        KeyLabel1.Text = $"HOLD {KeyLabel(launchKeys[0]).ToUpperInvariant()} • RELEASE TO LAUNCH";
        KeyLabel2.Text = $"HOLD {KeyLabel(launchKeys[1]).ToUpperInvariant()} • RELEASE TO LAUNCH";
        KeysHelp.Text = $"{KeyLabel(launchKeys[0])}: player 1   •   {KeyLabel(launchKeys[1])}: player 2   •   Hold to stage / release to launch   •   Esc: cancel";
    }

    private void SettingsClicked(object sender, RoutedEventArgs e)
    {
        if (active || held.Any(x => x) || awaitRelease) return;
        Player1Key.SelectedValue = launchKeys[0];
        Player2Key.SelectedValue = launchKeys[1];
        SettingsError.Text = "";
        SettingsOverlay.Visibility = Visibility.Visible;
        Player1Key.Focus();
    }

    private void ApplySettingsClicked(object sender, RoutedEventArgs e)
    {
        if (Player1Key.SelectedValue is not Key first || Player2Key.SelectedValue is not Key second) return;
        if (!TrySetLaunchKeys(first, second, out var error)) { SettingsError.Text = error; return; }
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(new SavedKeys(first.ToString(), second.ToString())));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            SettingsError.Text = "Controls are applied for this session. Preferences could not be saved. Close to continue.";
            return;
        }
        CloseSettingsClicked(sender, e);
    }

    private void DefaultKeysClicked(object sender, RoutedEventArgs e)
    {
        Player1Key.SelectedValue = Key.LeftShift;
        Player2Key.SelectedValue = Key.RightShift;
    }

    private void CloseSettingsClicked(object sender, RoutedEventArgs e)
    {
        SettingsOverlay.Visibility = Visibility.Collapsed;
        Keyboard.ClearFocus();
        Keyboard.Focus(RaceSurface);
    }
}

