using Microsoft.UI.Xaml.Controls;

namespace ClaudeUsage.Views.Controls;

/// <summary>Subtle pulsing placeholder used while the first usage snapshot is loading, instead of a blank card.</summary>
public sealed partial class SkeletonBlock : UserControl
{
    public SkeletonBlock()
    {
        InitializeComponent();
    }

    private void OnLoaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) => PulseStoryboard.Begin();

    private void OnUnloaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) => PulseStoryboard.Stop();
}
