using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using FlashEditor.App.Rendering;
using FlashEditor.Core.Objects;

namespace FlashEditor.App.Views;

/// <summary>Insert > Shape: every shape in groups, each with a small picture of itself.</summary>
public partial class MainWindow
{
    private static readonly (string Title, ShapeKind[] Kinds)[] ShapeGroups =
    {
        ("Basic", new[] { ShapeKind.Rectangle, ShapeKind.Ellipse, ShapeKind.Triangle, ShapeKind.RightTriangle, ShapeKind.Diamond, ShapeKind.Pentagon,
            ShapeKind.Hexagon, ShapeKind.Octagon, ShapeKind.Trapezoid, ShapeKind.Parallelogram, ShapeKind.Polygon }),
        ("Stars and symbols", new[] { ShapeKind.Star, ShapeKind.Sparkle, ShapeKind.Burst, ShapeKind.Heart, ShapeKind.Cross, ShapeKind.Check, ShapeKind.XMark,
            ShapeKind.Lightning, ShapeKind.Cloud, ShapeKind.Teardrop, ShapeKind.Crescent, ShapeKind.Shield, ShapeKind.Home, ShapeKind.Tag }),
        ("Arrows and speech bubbles", new[] { ShapeKind.Arrow, ShapeKind.DoubleArrow, ShapeKind.Chevron, ShapeKind.SpeechBubble, ShapeKind.OvalBubble }),
        ("Rings, slices and frames", new[] { ShapeKind.Ring, ShapeKind.HalfRing, ShapeKind.HalfCircle, ShapeKind.QuarterCircle, ShapeKind.Pie, ShapeKind.Frame }),
        ("Line", new[] { ShapeKind.Line }),
    };

    /// <summary>The shape list is a small gallery: a grid of pictures in groups (hover for the name), not a long vertical list.</summary>
    private void BuildShapeMenu()
    {
        var menu = Ribbon.ShapeMenu;
        var panel = new StackPanel { Width = 292, Margin = new Thickness(6, 2, 6, 6) };
        foreach (var (title, kinds) in ShapeGroups)
        {
            panel.Children.Add(new TextBlock
            {
                Text = title, FontSize = 11, FontWeight = FontWeights.SemiBold, Margin = new Thickness(2, 8, 0, 3),
                Foreground = (Brush)FindResource("SubtleTextBrush"),
            });
            var wrap = new WrapPanel();
            foreach (var kind in kinds)
            {
                var button = new Button
                {
                    Style = (Style)FindResource("IconButton"), Width = 34, Height = 34, Margin = new Thickness(1),
                    Content = IconFor(kind), ToolTip = ShapeNames.Of(kind),
                    Command = _viewModel.Insert.AddShapeCommand, CommandParameter = kind.ToString(),
                };
                button.Click += (_, _) => menu.IsOpen = false;
                wrap.Children.Add(button);
            }
            panel.Children.Add(wrap);
        }
        menu.Items.Add(panel);

        // The most used shapes also sit in the Insert tab, one click away.
        Ribbon.FillQuickShapes = quick =>
        {
            foreach (var kind in QuickShapes)
                quick.Children.Add(new Button
                {
                    Style = (Style)FindResource("IconButton"), Width = 32, Height = 28, Margin = new Thickness(1),
                    Content = IconFor(kind), ToolTip = ShapeNames.Of(kind),
                    Command = _viewModel.Insert.AddShapeCommand, CommandParameter = kind.ToString(),
                });
        };
    }

    private static readonly ShapeKind[] QuickShapes =
    {
        ShapeKind.Rectangle, ShapeKind.Ellipse, ShapeKind.Triangle, ShapeKind.Diamond, ShapeKind.Pentagon, ShapeKind.Hexagon,
        ShapeKind.Star, ShapeKind.Heart, ShapeKind.Arrow, ShapeKind.Line, ShapeKind.SpeechBubble, ShapeKind.Cloud,
    };

    /// <summary>A small drawing of the shape (the real outline, in the text color).</summary>
    private static readonly string[] GroupColors = { "Blue", "Amber", "Green", "Purple", "Sky" };

    private static string ColorOf(ShapeKind kind)
    {
        for (int g = 0; g < ShapeGroups.Length; g++)
            if (ShapeGroups[g].Kinds.Contains(kind)) return GroupColors[g % GroupColors.Length];
        return "Blue";
    }

    private object IconFor(ShapeKind kind)
    {
        string colorKey = "Icon." + ColorOf(kind);
        var sample = new ShapeObject(new ObjectBox(0, 0, 100, 100), kind, ColorRgbaWhite, null, 0, Points: 5, Inner: kind == ShapeKind.Pie ? 0 : 0.5, Sweep: 270);
        var path = new Path
        {
            Data = ShapeGeometryFactory.Build(sample, 100, 100),
            Stretch = Stretch.Uniform,
            Width = 18,
            Height = 18,
            StrokeThickness = kind == ShapeKind.Line ? 6 : 0,
        };
        path.SetResourceReference(kind == ShapeKind.Line ? Shape.StrokeProperty : Shape.FillProperty, colorKey);
        return path;
    }

    private static readonly Core.Imaging.ColorRgba ColorRgbaWhite = new(255, 255, 255);
}
