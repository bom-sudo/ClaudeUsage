using ClaudeUsage.Core.Models;
using ClaudeUsage.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace ClaudeUsage.Views;

public sealed partial class MainWindow : Window
{
    private const double MediumBreakpoint = 380;
    private const double LargeBreakpoint = 560;

    public MainViewModel ViewModel { get; }

    private readonly Action _onOpenSettingsRequested;
    private bool _allowClose;

    public MainWindow(MainViewModel viewModel, Action onOpenSettingsRequested)
    {
        ViewModel = viewModel;
        _onOpenSettingsRequested = onOpenSettingsRequested;

        InitializeComponent();

        Title = "ClaudeUsage";
        SystemBackdrop = new MicaBackdrop();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        RootGrid.DataContext = ViewModel;
        RootGrid.SizeChanged += (_, _) => UpdateResponsiveLayout(RootGrid.ActualWidth);
        UpdateResponsiveLayout(360);

        AppWindow.Closing += OnAppWindowClosing;
        AppWindow.Resize(new Windows.Graphics.SizeInt32(380, 560));
    }

    private void OnAppWindowClosing(Microsoft.UI.Windowing.AppWindow sender, Microsoft.UI.Windowing.AppWindowClosingEventArgs args)
    {
        if (_allowClose)
        {
            return;
        }

        // Closing the window hides it to the tray instead of exiting — the app keeps monitoring in the background.
        // Bypassed only via ForceClose(), which the tray icon's "Exit" command uses for a real shutdown.
        args.Cancel = true;
        AppWindow.Hide();
    }

    /// <summary>Actually closes the window instead of hiding it — used when the app is really quitting (tray "Exit").</summary>
    public void ForceClose()
    {
        _allowClose = true;
        Close();
    }

    private void UpdateResponsiveLayout(double width)
    {
        var showModelUsage = width >= MediumBreakpoint;
        var showHistory = width >= LargeBreakpoint;
        var showCost = width >= MediumBreakpoint;

        ModelUsageCard.Visibility = showModelUsage ? Visibility.Visible : Visibility.Collapsed;
        HistoryCard.Visibility = showHistory ? Visibility.Visible : Visibility.Collapsed;
        CostCard.Visibility = showCost ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnRefreshClicked(object sender, RoutedEventArgs e) => ViewModel.RefreshCommand.Execute(null);

    private void OnSettingsClicked(object sender, RoutedEventArgs e) => _onOpenSettingsRequested();

    private void OnPeriodToggleClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton clicked || clicked.Tag is not string tag || !Enum.TryParse<UsagePeriod>(tag, out var period))
        {
            return;
        }

        foreach (var toggle in new[] { Period24h, Period7d, Period30d })
        {
            toggle.IsChecked = ReferenceEquals(toggle, clicked);
        }

        ViewModel.SelectedHistoryPeriod = period;
    }
}
