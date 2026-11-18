using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace FlashEditor.App.Views.Panels;

/// <summary>
/// The File drawer: slides in from the left over the window and lists everything about files (new, open, save, export, pages...).
/// The list is in FileDrawer.xaml. Esc, a click on the dimmed area or a command closes it.
/// </summary>
public partial class FileDrawer : UserControl
{
    private const double PanelWidth = 320;

    public FileDrawer()
    {
        InitializeComponent();
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape) return;
            Close();
            e.Handled = true;
        };
    }

    public bool IsOpen { get; private set; }

    public void Open()
    {
        if (IsOpen) return;
        IsOpen = true;
        Visibility = Visibility.Visible;
        Slide.BeginAnimation(TranslateTransform.XProperty,
            new DoubleAnimation(-PanelWidth, 0, TimeSpan.FromMilliseconds(180)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        Scrim.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)));
        Focus();
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        var slide = new DoubleAnimation(0, -PanelWidth, TimeSpan.FromMilliseconds(140)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };
        slide.Completed += (_, _) => { if (!IsOpen) Visibility = Visibility.Collapsed; };
        Slide.BeginAnimation(TranslateTransform.XProperty, slide);
        Scrim.BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(140)));
    }

    private void OnScrimClicked(object sender, MouseButtonEventArgs e) => Close();

    private void OnItemClick(object sender, RoutedEventArgs e) => Close();

    private void OnExit(object sender, RoutedEventArgs e) => Window.GetWindow(this)?.Close();
}
