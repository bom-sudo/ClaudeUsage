using ClaudeUsage.Core.Models;
using ClaudeUsage.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace ClaudeUsage.Views;

public sealed partial class SettingsWindow : Window
{
    private static readonly RefreshInterval[] ComboOrder =
    [
        RefreshInterval.Seconds30,
        RefreshInterval.Minute1,
        RefreshInterval.Minutes5,
        RefreshInterval.Minutes15,
        RefreshInterval.Disabled,
    ];

    public SettingsViewModel ViewModel { get; }

    public SettingsWindow(SettingsViewModel viewModel)
    {
        ViewModel = viewModel;

        InitializeComponent();

        Title = "ClaudeUsage Settings";
        SystemBackdrop = new MicaBackdrop();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        RootGrid.DataContext = ViewModel;
        AppWindow.Resize(new Windows.Graphics.SizeInt32(460, 640));

        RootGrid.Loaded += OnRootGridLoaded;
    }

    private async void OnRootGridLoaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.LoadAsync();
        RefreshIntervalCombo.SelectedIndex = Array.IndexOf(ComboOrder, ViewModel.RefreshInterval);
    }

    private void OnRefreshIntervalChanged(object sender, SelectionChangedEventArgs e)
    {
        var index = RefreshIntervalCombo.SelectedIndex;
        if (index >= 0 && index < ComboOrder.Length)
        {
            ViewModel.RefreshInterval = ComboOrder[index];
        }
    }
}
