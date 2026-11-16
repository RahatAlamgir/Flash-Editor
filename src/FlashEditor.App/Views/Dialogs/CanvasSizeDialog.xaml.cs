using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using FlashEditor.App.Services;
using FlashEditor.Core.IO;

namespace FlashEditor.App.Views.Dialogs;

/// <summary>Change the canvas size; a 3 x 3 anchor grid picks where the existing picture stays.</summary>
public partial class CanvasSizeDialog : Window
{
    private static readonly string[] Arrows = { "↖", "↑", "↗", "←", "●", "→", "↙", "↓", "↘" };
    private readonly List<ToggleButton> _anchors = new();
    private int _anchorIndex = 4; // center

    public CanvasSizeDialog(int width, int height)
    {
        InitializeComponent();
        CurrentSizeText.Text = $"Current canvas: {width} x {height} px";
        WidthBox.Text = width.ToString();
        HeightBox.Text = height.ToString();

        for (int i = 0; i < 9; i++)
        {
            int index = i;
            var button = new ToggleButton { Content = Arrows[i], Margin = new Thickness(2), FontSize = 14, Padding = new Thickness(0) };
            button.Click += (_, _) => SelectAnchor(index);
            _anchors.Add(button);
            AnchorGrid.Children.Add(button);
        }
        SelectAnchor(4);
        Loaded += (_, _) => { WidthBox.Focus(); WidthBox.SelectAll(); };
    }

    public CanvasSizeSettings? Result { get; private set; }

    private void SelectAnchor(int index)
    {
        _anchorIndex = index;
        for (int i = 0; i < _anchors.Count; i++) _anchors[i].IsChecked = i == index;
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(WidthBox.Text, out int w) || !int.TryParse(HeightBox.Text, out int h)
            || w < 1 || h < 1 || w > ImageDecoder.MaxDimension || h > ImageDecoder.MaxDimension)
        {
            ErrorText.Text = $"Width and height must be between 1 and {ImageDecoder.MaxDimension} px.";
            ErrorText.Visibility = Visibility.Visible;
            return;
        }
        Result = new CanvasSizeSettings(w, h, (_anchorIndex % 3) / 2.0, (_anchorIndex / 3) / 2.0);
        DialogResult = true;
    }
}
