using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace DragTree;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer timer = new(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(2) };
    private readonly Stopwatch clock = new();
    private RawShiftInput? rawShiftInput;
    private readonly DispatcherTimer bannerTimer = new() { Interval = TimeSpan.FromSeconds(3) };
    private readonly List<double>[] cleanRuns = [[], []];
    private readonly Queue<string>[] recent = [new(), new()];
    private readonly bool[] done = new bool[2];
    private readonly double?[] reactions = new double?[2];
    private bool active, greenShown;
    private readonly bool[] staged = new bool[2];
    private readonly double[] stagingDue = new double[2];
    private const double StagingHoldSeconds = 1.0;
    private static double TimestampSeconds => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
    private bool UsesProTree => TreeMode.SelectedIndex == 1;
    private double countdownStart, greenTime, interval;
    private int players = 1;

    public MainWindow()
    {
        InitializeComponent();
        InitializeKeySettings();
        timer.Tick += Tick;
        bannerTimer.Tick += (_, _) => HideWinnerBanner();
        SourceInitialized += (_, _) =>
        {
            rawShiftInput = new RawShiftInput((player, isUp) =>
            {
                if (IsActive) HandleShiftInput(player, !isUp);
            });
            rawShiftInput.ConfigureKeys(launchKeys[0], launchKeys[1]);
            rawShiftInput.Attach((System.Windows.Interop.HwndSource)PresentationSource.FromVisual(this));
        };
        Closed += (_, _) => { timer.Stop(); bannerTimer.Stop(); rawShiftInput?.Dispose(); };
        ResetLights();
        UpdatePlayerMode();
        UpdateTreeDescription();
        ContentRendered += (_, _) => Keyboard.Focus(RaceSurface);
    }

    private static void Light(Ellipse bulb, string color, bool on)
    {
        var tint = (Color)ColorConverter.ConvertFromString(color);
        if (Equals(bulb.Tag, (color, on))) return;
        bulb.Tag = (color, on);
        Color Scale(double factor) => Color.FromRgb((byte)(tint.R * factor), (byte)(tint.G * factor), (byte)(tint.B * factor));
        bulb.Fill = new RadialGradientBrush
        {
            GradientOrigin = new Point(.35, .28),
            Center = new Point(.45, .4),
            RadiusX = .65, RadiusY = .65,
            GradientStops =
            [
                new GradientStop(on ? Colors.White : Scale(.24), 0),
                new GradientStop(on ? tint : Scale(.16), .3),
                new GradientStop(on ? Scale(.8) : Scale(.07), 1)
            ]
        };
        bulb.Stroke = new SolidColorBrush(on ? tint : Color.FromRgb(49, 60, 76));
        bulb.StrokeThickness = 2;
        bulb.Effect = on ? new DropShadowEffect { Color = tint, BlurRadius = 24, ShadowDepth = 0, Opacity = .65 } : null;
    }

    private void ResetLights()
    {
        foreach (var light in new[] { PreLeft, PreLeft2, PreRight, PreRight2, StageLeft, StageLeft2, StageRight, StageRight2 }) Light(light, "#FFF0A5", false);
        foreach (var light in new[] { Amber1, Amber1Right, Amber2, Amber2Right, Amber3, Amber3Right }) Light(light, "#FFB930", false);
        Light(Green, "#35E399", false);
        Light(Green2, "#35E399", false);
        Light(Red1, "#FF5267", false);
        Light(Red2, "#FF5267", false);
    }

    private readonly bool[] held = new bool[2];
    private bool awaitRelease;
    private readonly string[] raceNames = ["Player 1", "Player 2"];

    private string PlayerName(int p)
    {
        var text = (p == 0 ? Name1 : Name2).Text.Trim();
        return string.IsNullOrWhiteSpace(text) ? $"Player {p + 1}" : text;
    }

    private void LockSettings(bool locked)
    {
        TreeMode.IsEnabled = PlayerMode.IsEnabled = Name1.IsEnabled = Name2.IsEnabled = SettingsButton.IsEnabled = !locked;
    }

    private void LanePreStage(int p, bool on)
    {
        foreach (var bulb in p == 0 ? new[] { PreLeft, PreLeft2 } : new[] { PreRight, PreRight2 }) Light(bulb, "#FFF0A5", on);
    }

    private void LaneStage(int p, bool on)
    {
        foreach (var bulb in p == 0 ? new[] { StageLeft, StageLeft2 } : new[] { StageRight, StageRight2 }) Light(bulb, "#FFF0A5", on);
    }

    private void RefreshPreStageWarnings()
    {
        if (PreStageWarning1 is null || PreStageWarning2 is null) return;
        for (var p = 0; p < 2; p++)
        {
            var show = players == 2 && !active && !awaitRelease && !held[p] && held[1 - p] && staged[1 - p];
            (p == 0 ? PreStageWarning1 : PreStageWarning2).Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            if (show) (p == 0 ? PreStageWarningText1 : PreStageWarningText2).Text = $"{PlayerName(p)}, PRE-STAGE\nHold {KeyLabel(launchKeys[p])}";
        }
    }

    private void UpdateStaging(double now)
    {
        for (var p = 0; p < players; p++)
        {
            if (!held[p] || staged[p] || now < stagingDue[p]) continue;
            staged[p] = true;
            LaneStage(p, true);
            (p == 0 ? Detail1 : Detail2).Text = "STAGED • keep holding your key";
        }
        if (!active && Enumerable.Range(0, players).All(p => held[p] && staged[p])) StartRace();
        RefreshPreStageWarnings();
    }

    private void UpdateTreeDescription()
    {
        if (TimingInfo is null) return;
        TimingInfo.Text = TreeMode.SelectedIndex switch
        {
            1 => "Pro: all ambers together → green after 0.400 s",
            _ => "Sportsman: amber 1 → 0.500 s → amber 2 → 0.500 s → amber 3 → 0.500 s → green"
        };
    }

    private void TreeModeChanged(object sender, SelectionChangedEventArgs e) => UpdateTreeDescription();

    // Preserve an early release's sign even when it is less than one displayed millisecond.
    private static string FormatReaction(double reaction) => reaction is < 0 and > -.001
        ? "-<0.001"
        : reaction.ToString("+0.000;-0.000;0.000");

    private void Hold(int p)
    {
        if (p >= players || held[p]) return;
        if (awaitRelease)
        {
            Status.Text = "Release both launch keys before staging again";
            return;
        }
        if (active) return;
        if (!held.Any(h => h))
        {
            HideWinnerBanner();
            ResetLights();
            Array.Clear(done);
            Array.Clear(staged);
            Array.Clear(reactions);
            greenShown = false;
            Result1.Text = Result2.Text = "—.———";
            Delta1.Text = Delta2.Text = "From green: —";
            Result1.Foreground = Result2.Foreground = Brushes.White;
            for (var i = 0; i < 2; i++) raceNames[i] = PlayerName(i);
            LockSettings(true);
        }
        held[p] = true;
        RefreshPreStageWarnings();
        stagingDue[p] = TimestampSeconds + StagingHoldSeconds;
        LanePreStage(p, true);
        LaneStage(p, false);
        (p == 0 ? Detail1 : Detail2).Text = "PRE-STAGED • hold for stage lights";
        Status.Text = "Hold your key • pre-stage, then stage";
        timer.Start();
    }

    private void Release(int p)
    {
        if (p >= players || !held[p]) return;
        held[p] = false;
        staged[p] = false;
        RefreshPreStageWarnings();
        LanePreStage(p, false);
        LaneStage(p, false);
        if (active) Launch(p);
        else if (!awaitRelease)
        {
            (p == 0 ? Detail1 : Detail2).Text = "Hold your key to stage";
            if (!held.Any(h => h))
            {
                timer.Stop();
                LockSettings(false);
                Status.Text = "Hold your key to stage";
            }
            else Status.Text = "Waiting for both players to stage";
        }
        if (awaitRelease && !held.Any(h => h))
        {
            awaitRelease = false;
            LockSettings(false);
        }
    }

    private void StartRace()
    {
        interval = UsesProTree ? .4 : .5;
        countdownStart = 1.2 + Random.Shared.NextDouble() * 1.8;
        greenTime = countdownStart + (UsesProTree ? .4 : 1.5);
        active = true;
        Status.Text = "Staged • hold your key, release to launch";
        clock.Restart();
        timer.Start();
    }

    // Absolute stopwatch deadlines prevent dispatcher delays accumulating between lights.
    private void UpdateLights(double now)
    {
        var elapsed = now - countdownStart;
        var pro = UsesProTree;
        var amber1 = elapsed >= 0 && elapsed < interval;
        var amber2 = pro ? amber1 : elapsed >= interval && elapsed < 2 * interval;
        var amber3 = pro ? amber1 : elapsed >= 2 * interval && elapsed < 3 * interval;
        Light(Amber1, "#FFB930", amber1);
        Light(Amber2, "#FFB930", amber2);
        Light(Amber3, "#FFB930", amber3);
        Light(Amber1Right, "#FFB930", players == 2 && amber1);
        Light(Amber2Right, "#FFB930", players == 2 && amber2);
        Light(Amber3Right, "#FFB930", players == 2 && amber3);
        if (!greenShown && now >= greenTime)
        {
            Light(Green, "#35E399", reactions[0] is not < 0);
            Light(Green2, "#35E399", players == 2 && reactions[1] is not < 0);
            greenShown = true;
            Status.Text = "GO • release your key!";
        }
    }

    private void Tick(object? sender, EventArgs e)
    {
        if (!active) UpdateStaging(TimestampSeconds);
        if (!active) return;
        var now = clock.Elapsed.TotalSeconds;
        UpdateLights(now);
        if (now - greenTime >= 5)
        {
            for (var p = 0; p < players; p++)
                if (!done[p])
                {
                    done[p] = true;
                    (p == 0 ? Result1 : Result2).Text = "TIMEOUT";
                    (p == 0 ? Delta1 : Delta2).Text = "From green: —";
                    (p == 0 ? Detail1 : Detail2).Text = "Release your key, then hold to retry";
                    LaneStage(p, false);
                    AddHistory(p, "TIMEOUT");
                }
            CompleteRace();
        }
    }

    private void Launch(int p)
    {
        if (!active || p >= players || done[p]) return;
        var now = clock.Elapsed.TotalSeconds;
        UpdateLights(now);
        var reaction = now - greenTime;
        var foul = reaction < 0;
        reactions[p] = reaction;
        done[p] = true;
        Light(p == 0 ? Red1 : Red2, "#FF5267", foul);
        var result = p == 0 ? Result1 : Result2;
        result.Text = (reaction + interval).ToString("0.000");
        (p == 0 ? Delta1 : Delta2).Text = $"From green: {FormatReaction(reaction)} s";
        result.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(foul ? "#FF5267" : "#35E399"));
        (p == 0 ? Detail1 : Detail2).Text = $"{(foul ? "RED LIGHT" : "CLEAN")} • perfect RT {interval:0.000} s";
        if (!foul) cleanRuns[p].Add(reaction);
        AddHistory(p, $"{(foul ? "RED" : "CLEAN")}  RT {reaction + interval:0.000} s\nFrom green {FormatReaction(reaction)} s");
        if (Enumerable.Range(0, players).All(i => done[i])) CompleteRace();
        else Status.Text = $"{raceNames[p]} finished • waiting for opponent";
    }

    private void AddHistory(int p, string text)
    {
        recent[p].Enqueue(text);
        while (recent[p].Count > 5) recent[p].Dequeue();
        (p == 0 ? History1 : History2).Text = string.Join(Environment.NewLine, recent[p].Reverse());
        var runs = cleanRuns[p];
        (p == 0 ? Stats1 : Stats2).Text = runs.Count == 0 ? "Clean runs: 0" : $"Clean runs: {runs.Count}\nBest from green: {runs.Min():0.000} s\nAvg from green: {runs.Average():0.000} s";
    }

    private void CompleteRace()
    {
        Finish();
        var clean = Enumerable.Range(0, players).Where(i => reactions[i] is >= 0).ToArray();
        if (players == 1)
            Status.Text = clean.Length == 1 ? $"{raceNames[0]} • clean launch" : reactions[0].HasValue ? $"{raceNames[0]} • red light" : "Run timed out";
        else if (clean.Length == 0) Status.Text = "No winner • no clean launches";
        else if (clean.Length == 2 && Math.Round(reactions[0]!.Value, 3) == Math.Round(reactions[1]!.Value, 3))
        {
            Status.Text = "Tie • equal reactions to the millisecond";
            ShowWinnerBanner($"{raceNames[0]} & {raceNames[1]} tie!  RT {reactions[0]!.Value + interval:0.000} s (from green {FormatReaction(reactions[0]!.Value)} s)");
        }
        else
        {
            var winner = clean.MinBy(i => reactions[i]!.Value);
            Status.Text = $"{raceNames[winner]} wins!";
            ShowWinnerBanner($"{raceNames[winner]} wins!  RT {reactions[winner]!.Value + interval:0.000} s (from green {FormatReaction(reactions[winner]!.Value)} s)");
        }
    }

    private void ShowWinnerBanner(string message)
    {
        bannerTimer.Stop();
        WinnerBannerText.Text = message;
        WinnerBanner.Visibility = Visibility.Visible;
        bannerTimer.Start();
    }

    private void HideWinnerBanner()
    {
        bannerTimer.Stop();
        WinnerBanner.Visibility = Visibility.Collapsed;
    }

    private void Finish()
    {
        timer.Stop();
        clock.Stop();
        active = false;
        awaitRelease = held.Any(h => h);
        LockSettings(awaitRelease);
        RefreshPreStageWarnings();
    }

    private void Cancel()
    {
        HideWinnerBanner();
        Finish();
        ResetLights();
        Array.Clear(staged);
        RefreshPreStageWarnings();
        Status.Text = "Cancelled • release your key, then hold to stage";
        Detail1.Text = "Hold your selected key to stage";
        Detail2.Text = players == 2 ? "Hold your selected key to stage" : "Select two players to compete";
    }

    private void UpdatePlayerMode()
    {
        if (Player2Panel is null) return;
        players = PlayerMode.SelectedIndex == 1 ? 2 : 1;
        Player2Panel.Opacity = players == 2 ? 1 : .55;
        RefreshPreStageWarnings();
        Detail2.Text = players == 2 ? "Hold your selected key to stage" : "Select two players to compete";
        Status.Text = "Hold your key to stage";
    }

    private void PlayerModeChanged(object sender, SelectionChangedEventArgs e) => UpdatePlayerMode();
    private void StageClicked(object sender, RoutedEventArgs e) => Cancel();
    private void OnDeactivated(object? sender, EventArgs e)
    {
        if (active || held.Any(h => h)) Cancel();
        // Key-up may happen in another app. Focus loss ends the input session.
        Array.Clear(held);
        awaitRelease = false;
        LockSettings(false);
    }
    private void OnPointerDown(object sender, MouseButtonEventArgs e)
    {
        var element = e.OriginalSource as DependencyObject;
        while (element is not null)
        {
            // Preserve focus while controls process clicks, especially popup selections.
            if (element is TextBox or ComboBox or ComboBoxItem or System.Windows.Controls.Primitives.ButtonBase) return;
            element = element is Visual ? VisualTreeHelper.GetParent(element) : LogicalTreeHelper.GetParent(element);
        }
        Keyboard.ClearFocus();
        Keyboard.Focus(RaceSurface);
    }

    private void HandleShiftInput(int player, bool isDown)
    {
        if (SettingsOverlay.Visibility == Visibility.Visible) return;
        if (isDown)
        {
            if (Keyboard.FocusedElement is TextBox && !active && !held.Any(h => h)) return;
            Hold(player);
        }
        else Release(player);
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (SettingsOverlay.Visibility == Visibility.Visible)
        {
            if (e.Key == Key.Escape) { CloseSettingsClicked(this, e); e.Handled = true; }
            return;
        }
        if (e.Key == Key.Enter && Keyboard.FocusedElement is TextBox)
        {
            Keyboard.ClearFocus(); Keyboard.Focus(RaceSurface); e.Handled = true; return;
        }
        var player = Array.IndexOf(launchKeys, e.Key);
        if (player < 0 && e.Key != Key.Escape) return;
        if (Keyboard.FocusedElement is TextBox && !active && !held.Any(h => h) && e.Key != Key.Escape) return;
        e.Handled = true;
        if (e.IsRepeat) return;
        if (e.Key == Key.Escape) Cancel();
        else if (rawShiftInput?.IsAttached != true) HandleShiftInput(player, true);
    }

    private void OnKeyUp(object sender, KeyEventArgs e)
    {
        if (SettingsOverlay.Visibility == Visibility.Visible) return;
        var player = Array.IndexOf(launchKeys, e.Key);
        if (player < 0) return;
        if (Keyboard.FocusedElement is TextBox && !held[player]) return;
        e.Handled = true;
        if (rawShiftInput?.IsAttached != true) HandleShiftInput(player, false);
    }
}
