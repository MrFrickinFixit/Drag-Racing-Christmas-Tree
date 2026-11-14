using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DragTree;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        try { Run(); return 0; }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    private static void Run()
    {
        var app = new App();
        app.InitializeComponent();
        var window = new MainWindow();
        using var source = new System.Windows.Interop.HwndSource(new System.Windows.Interop.HwndSourceParameters("Reaction input checks") { Width = 1, Height = 1, WindowStyle = 0 });
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        object? Call(string name, params object?[] args) => typeof(MainWindow).GetMethod(name, flags)!.Invoke(window, args);
        T Field<T>(string name) => (T)typeof(MainWindow).GetField(name, flags)!.GetValue(window)!;
        void Set(string name, object value) => typeof(MainWindow).GetField(name, flags)!.SetValue(window, value);
        T Control<T>(string name) => (T)window.FindName(name);
        void Check(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
            Console.WriteLine($"PASS: {message}");
        }
        bool Bind(Key first, Key second) => (bool)Call("TrySetLaunchKeys", first, second, "")!;
        Check(Bind(Key.LeftShift, Key.RightShift), "Tests begin with default bindings regardless of saved preferences");
        void KeyEvent(Key key, bool down) => window.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, key) { RoutedEvent = down ? Keyboard.PreviewKeyDownEvent : Keyboard.PreviewKeyUpEvent });
        void Down(Key key) => KeyEvent(key, true);
        void Up(Key key) => KeyEvent(key, false);
        bool Lit(string name) => ((string, bool))Control<System.Windows.Shapes.Ellipse>(name).Tag is var state && state.Item2;
        void AdvanceStaging() => Call("UpdateStaging", Field<double[]>("stagingDue").Max());
        void Both() { Down(Key.LeftShift); Down(Key.RightShift); AdvanceStaging(); }

        Check(Control<ComboBox>("TreeMode").Items.Count == 2, "Tree selector offers only Sportsman and Pro");
        Down(Key.RightShift);
        Check(!Field<bool>("active"), "Right Shift does not stage in single-player mode");
        Up(Key.RightShift);
        Down(Key.LeftShift);
        Check(!Field<bool>("active") && Lit("PreLeft") && !Lit("StageLeft"), "Shift down lights pre-stage before stage");
        Call("UpdateStaging", Field<double[]>("stagingDue")[0] - .000001);
        Check(!Lit("StageLeft") && !Field<bool>("active"), "Stage bulbs wait for the full 1000 ms staging hold");
        AdvanceStaging();
        Check(Field<bool>("active") && !Field<bool[]>("done")[0] && Lit("StageLeft"), "Stage bulbs light after 1000 ms and start single-player countdown");
        Down(Key.LeftShift);
        Check(!Field<bool[]>("done")[0], "Holding Shift does not repeatedly launch");
        Up(Key.LeftShift);
        Check(!Field<bool>("active") && Field<double?[]>("reactions")[0] < 0, "Shift up launches and detects an early release");

        Control<ComboBox>("PlayerMode").SelectedIndex = 1;
        Down(Key.LeftShift);
        Check(!Field<bool>("active") && Lit("PreLeft") && !Lit("StageLeft") && !Lit("StageRight"), "Player 1 stages independently and waits for player 2");
        AdvanceStaging();
        Check(Control<FrameworkElement>("PreStageWarning2").Visibility == Visibility.Visible && Control<FrameworkElement>("PreStageWarning1").Visibility == Visibility.Collapsed, "Fully staged player 1 prompts only player 2 to pre-stage");
        Check(Control<TextBlock>("PreStageWarningText2").Text.Contains("Right Shift"), "Pre-stage warning identifies the racer's selected key");
        Down(Key.RightShift);
        Check(Control<FrameworkElement>("PreStageWarning2").Visibility == Visibility.Collapsed, "Warning disappears as soon as player 2 pre-stages");
        Up(Key.RightShift);
        Check(Control<FrameworkElement>("PreStageWarning2").Visibility == Visibility.Visible, "Warning returns if the waiting player unstages before the countdown");
        Up(Key.LeftShift);
        Check(!Field<bool>("active") && !Lit("StageLeft") && Field<double?[]>("reactions")[0] is null, "Release before both stage unstages without recording a run");

        Check(Control<FrameworkElement>("PreStageWarning2").Visibility == Visibility.Collapsed, "Warning clears if the staged opponent releases");
        Down(Key.RightShift); AdvanceStaging();
        Check(Control<FrameworkElement>("PreStageWarning1").Visibility == Visibility.Visible, "Fully staged player 2 prompts player 1 on their own side");
        Down(Key.Escape); Up(Key.RightShift);
        Check(Control<FrameworkElement>("PreStageWarning1").Visibility == Visibility.Collapsed, "Cancel clears the pre-stage warning");
        Control<TextBox>("Name1").Text = "Alex";
        Control<TextBox>("Name2").Text = "Sam";
        Both();
        Check(Field<bool>("active") && Lit("StageLeft") && Lit("StageRight"), "Countdown starts only when both players hold their Shift keys");
        Check(!Control<TextBox>("Name1").IsEnabled && !Control<ComboBox>("TreeMode").IsEnabled, "Names and settings lock during staging and countdown");
        Up(Key.RightShift);
        Check(Field<bool[]>("done")[1] && !Field<bool[]>("done")[0] && Field<bool>("active"), "Right Shift up launches only player 2 and leaves opponent running");
        var first = Field<double?[]>("reactions")[1];
        Up(Key.RightShift);
        Check(Field<double?[]>("reactions")[1] == first, "Duplicate key-up cannot overwrite a result");
        Set("greenTime", -.2);
        Up(Key.LeftShift);
        Check(Control<TextBlock>("Status").Text == "Alex wins!", "Optional names appear in winner results");
        Check(Control<FrameworkElement>("WinnerBanner").Visibility == Visibility.Visible && Control<TextBlock>("WinnerBannerText").Text.StartsWith("Alex wins!"), "Named two-player winner appears in the banner");
        Check(Field<System.Windows.Threading.DispatcherTimer>("bannerTimer").Interval == TimeSpan.FromSeconds(3), "Winner banner is configured for three seconds");
        var frame = new System.Windows.Threading.DispatcherFrame();
        var wait = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(3150) };
        wait.Tick += (_, _) => { wait.Stop(); frame.Continue = false; };
        wait.Start();
        System.Windows.Threading.Dispatcher.PushFrame(frame);
        Check(Control<FrameworkElement>("WinnerBanner").Visibility == Visibility.Collapsed, "Winner banner automatically disappears after three seconds");
        Check(!Lit("Green2") && Lit("Red2") && Lit("Green"), "A fouled lane stays red while clean opponent receives green");

        Both();
        Up(Key.LeftShift);
        Up(Key.RightShift);
        Check(Control<TextBlock>("Status").Text.StartsWith("No winner"), "Two early releases produce no winner");
        Check(Control<FrameworkElement>("WinnerBanner").Visibility == Visibility.Collapsed, "No winner banner appears for two red lights");

        Both();
        Set("greenTime", -.1);
        Up(Key.LeftShift);
        Set("greenTime", -1.0);
        Up(Key.RightShift);
        Check(Control<TextBlock>("Status").Text == "Alex wins!", "Faster clean release wins");
        Call("Cancel");
        Check(Control<FrameworkElement>("WinnerBanner").Visibility == Visibility.Collapsed, "Reset dismisses an active winner banner");

        Both();
        Set("greenTime", -6.0);
        Call("Tick", null, EventArgs.Empty);
        Check(!Field<bool>("active") && Control<TextBlock>("Result1").Text == "TIMEOUT" && Control<TextBlock>("Result2").Text == "TIMEOUT", "Held keys time out five seconds after green");
        Check(Field<bool>("awaitRelease"), "A timed-out race requires releasing held keys before retry");
        Up(Key.LeftShift); Up(Key.RightShift);
        Check(!Field<bool>("awaitRelease") && Control<ComboBox>("TreeMode").IsEnabled, "Releasing after timeout rearms without launching");

        foreach (var mode in new[] { 0, 1 })
        {
            Control<ComboBox>("TreeMode").SelectedIndex = mode;
            Both();
            Check(Math.Abs((Field<double>("greenTime") - Field<double>("countdownStart")) - (mode == 0 ? 1.5 : .4)) < 1e-9, $"Mode {mode} configures the correct green deadline");
            Set("countdownStart", 10.0);
            Set("greenTime", mode == 0 ? 11.5 : 10.4);
            Call("UpdateLights", 9.999);
            Check(!Lit("Amber1") && !Lit("Amber2") && !Lit("Amber3") && !Lit("Green"), "No countdown lights before scheduled start");
            Call("UpdateLights", 10.0);
            if (mode == 0)
            {
                Check(Lit("Amber1") && !Lit("Amber2") && !Lit("Amber3"), "Sportsman starts with only top amber");
                Call("UpdateLights", 10.499999);
                Check(Lit("Amber1") && !Lit("Amber2"), "Sportsman top amber lasts a full 500 ms");
                Call("UpdateLights", 10.5);
                Check(!Lit("Amber1") && Lit("Amber2") && !Lit("Amber3"), "Sportsman advances to second amber at 500 ms");
                Call("UpdateLights", 11.0);
                Check(!Lit("Amber2") && Lit("Amber3") && !Lit("Green"), "Sportsman advances to third amber at 1000 ms");
                Call("UpdateLights", 11.499999);
                Check(Lit("Amber3") && !Lit("Green"), "Sportsman green waits for full 1500 ms");
                Call("UpdateLights", 11.5);
            }
            else
            {
                Check(Lit("Amber1") && Lit("Amber2") && Lit("Amber3"), "Pro lights all three ambers together");
                Call("UpdateLights", 10.399999);
                Check(Lit("Amber1") && !Lit("Green"), "Pro ambers last a full 400 ms");
                Call("UpdateLights", 10.4);
            }
            Check(Lit("Green") && Lit("Green2") && !Lit("Amber1") && !Lit("Amber2") && !Lit("Amber3"), "Both lanes switch from amber to green at the exact deadline");
            Check(Field<double>("greenTime") == (mode == 0 ? 11.5 : 10.4), "Rendering never shifts the scheduled green deadline");
            Set("greenTime", -.1);
            Up(Key.LeftShift);
            var delta = Field<double?[]>("reactions")[0]!.Value;
            Check(Control<TextBlock>("Result1").Text == (delta + (mode == 0 ? .5 : .4)).ToString("0.000"), $"Mode {mode} displays reaction time from final amber");
            Check(Control<TextBlock>("Delta1").Text.StartsWith("From green: +"), $"Mode {mode} also displays the positive difference from green");
            Set("greenTime", 100.0);
            Up(Key.RightShift);
            Check(Control<TextBlock>("Detail2").Text.StartsWith("RED LIGHT"), $"Mode {mode} still judges early launches against green");
            Down(Key.Escape); Up(Key.LeftShift); Up(Key.RightShift);
        }

        Both();
        Call("OnDeactivated", null, EventArgs.Empty);
        Check(!Field<bool>("active") && !Field<bool[]>("held").Any(x => x), "Focus loss cancels and clears held-key state");
        var rawType = typeof(MainWindow).Assembly.GetType("DragTree.RawShiftInput")!;
        var raw = Activator.CreateInstance(rawType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null,
            new object[] { new Action<int, bool>((p, isUp) => Call("HandleShiftInput", p, !isUp)) }, null)!;
        var processRaw = rawType.GetMethod("ProcessKeyboard", BindingFlags.Instance | BindingFlags.NonPublic)!;
        void Raw(ushort code, ushort flags) => processRaw.Invoke(raw, new object[] { code, flags });
        foreach (var rightFirst in new[] { true, false })
        {
            Raw(0x2A, 0); Raw(0x36, 0);
            AdvanceStaging();
            Set("greenTime", -.1);
            Raw(rightFirst ? (ushort)0x36 : (ushort)0x2A, 1);
            var firstPlayer = rightFirst ? 1 : 0;
            Check(Field<bool[]>("done")[firstPlayer] && !Field<bool[]>("done")[1 - firstPlayer] && Field<bool[]>("held")[1 - firstPlayer], $"Raw {(rightFirst ? "right" : "left")} Shift release launches independently while other Shift stays held");
            Raw(rightFirst ? (ushort)0x2A : (ushort)0x36, 1);
            Check(!Field<bool>("active") && Field<bool[]>("done").All(x => x), "Raw second Shift release finishes the two-player race");
        }
        Raw(0x2A, 0); Raw(0x36, 0); AdvanceStaging();
        Set("greenTime", -.1);
        Raw(0x2A, 1); Raw(0x36, 1);
        Check(!Field<bool>("active") && Field<bool[]>("done").All(x => x), "Back-to-back raw Shift releases both record results");
        rawType.GetMethod("Attach", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(raw, new object[] { source });
        Check((bool)rawType.GetProperty("IsAttached", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(raw)!, "Windows accepts the foreground raw keyboard registration");
        ((IDisposable)raw).Dispose();
        Check(Bind(Key.A, Key.L), "Racers can select distinct letter keys");
        Check(Control<TextBlock>("KeyLabel1").Text.Contains("HOLD A") && Control<TextBlock>("KeyLabel2").Text.Contains("HOLD L"), "Lane labels show the configured keys");
        Check(!Bind(Key.A, Key.A), "Duplicate racer keys are rejected");
        Check(!Bind(Key.Escape, Key.L), "Escape cannot be assigned as a launch key");
        Down(Key.LeftShift); Up(Key.LeftShift);
        Check(!Field<bool>("active") && !Field<bool[]>("held").Any(x => x), "Previous bindings no longer stage racers");
        Down(Key.A); Down(Key.L); AdvanceStaging();
        Set("greenTime", -.1);
        Up(Key.L);
        Check(Field<bool[]>("done")[1] && Field<bool[]>("held")[0], "Configured letter key releases launch the correct racer independently");
        Up(Key.A);
        Check(!Field<bool>("active"), "Both configured letter key releases finish the race");
        rawType.GetMethod("ConfigureKeys", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(raw, new object[] { Key.A, Key.L });
        Raw(0x1E, 0); Raw(0x26, 0); AdvanceStaging();
        Set("greenTime", -.1);
        Raw(0x26, 1);
        Check(Field<bool[]>("done")[1] && Field<bool[]>("held")[0], "Raw Input supports independently mapped letter keys");
        Raw(0x1E, 1);
        Check(!Field<bool>("active"), "Mapped raw key releases finish the race");
        Call("SettingsClicked", window, new RoutedEventArgs());
        Down(Key.A);
        Check(Control<FrameworkElement>("SettingsOverlay").Visibility == Visibility.Visible && !Field<bool[]>("held").Any(x => x), "Settings prevent accidental staging while choosing keys");
        Down(Key.Escape);
        Check(Control<FrameworkElement>("SettingsOverlay").Visibility == Visibility.Collapsed, "Escape closes settings without starting a race");
        Check(Bind(Key.LeftShift, Key.RightShift), "Default Shift controls can be restored");
        Control<TextBox>("Name1").Text = "   ";
        Check((string)Call("PlayerName", 0)! == "Player 1", "Blank names fall back to player labels");
        var format = typeof(MainWindow).GetMethod("FormatReaction", BindingFlags.NonPublic | BindingFlags.Static)!;
        Check((string)format.Invoke(null, [-.00001])! == "-<0.001", "Sub-millisecond red lights cannot display as a legal zero");
        Check((string)format.Invoke(null, [0.0])! == "0.000", "An exact zero reaction displays the ideal 0.000");
        var treeFormat = typeof(MainWindow).GetMethod("FormatTreeReaction", BindingFlags.NonPublic | BindingFlags.Static)!;
        foreach (var baseline in new[] { .5, .4 })
        {
            Check((string)treeFormat.Invoke(null, new object[] { -.0001, baseline })! == $"<{baseline:0.000}", "A rounded early launch stays visibly below the perfect tree time");
            Check((string)treeFormat.Invoke(null, new object[] { -double.Epsilon, baseline })! == $"<{baseline:0.000}", "Any negative release offset remains visibly red even at floating-point limits");
            Check((string)treeFormat.Invoke(null, new object[] { 0.0, baseline })! == baseline.ToString("0.000"), "An exact perfect launch retains its tree's target time");
            Check((string)treeFormat.Invoke(null, new object[] { -.01, baseline })! == (baseline - .01).ToString("0.000"), "Clearly early tree reaction times retain their normal numeric display");
            Check((string)treeFormat.Invoke(null, new object[] { .01, baseline })! == (baseline + .01).ToString("0.000"), "Clean tree reaction times retain their normal numeric display");
        }
        window.Close();
        app.Shutdown();
        Console.WriteLine("All checks passed.");
    }
}
