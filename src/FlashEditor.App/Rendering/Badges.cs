using FlashEditor.Core.Imaging;
using FlashEditor.Core.Objects;

namespace FlashEditor.App.Rendering;

/// <summary>Ready-made labels and badges (SALE, NEW, 5 stars, speech bubble...) made of shapes and text. Each one is a group, so it can be ungrouped and changed.</summary>
public static class Badges
{
    public sealed record BadgeInfo(string Id, string Name);

    public static readonly IReadOnlyList<BadgeInfo> All = new BadgeInfo[]
    {
        new("sale", "SALE"), new("new", "NEW"), new("hot", "HOT"), new("off", "50% OFF"), new("free", "FREE"), new("best", "Best seller"),
        new("limited", "Limited"), new("thanks", "Thank you"), new("verified", "Verified"), new("stars", "5 stars"), new("hello", "Speech bubble"),
        new("price", "Price"), new("swipe", "Swipe"), new("shield", "Shield"), new("hundred", "100%"), new("lightning", "Lightning"), new("arrival", "New arrival"),
    };

    private static ColorRgba C(int r, int g, int b) => new((byte)r, (byte)g, (byte)b);

    private static readonly ColorRgba White = C(255, 255, 255), Dark = C(17, 24, 39);

    /// <summary>The badge, centered on a canvas, about <paramref name="size"/> pixels wide.</summary>
    public static GroupObject Build(string id, double canvasWidth, double canvasHeight, double size)
    {
        double s = size / 100.0, ox = (canvasWidth - size) / 2, oy = (canvasHeight - size) / 2;
        var parts = new List<GroupMember>();

        void Shape(string name, ShapeKind kind, double x, double y, double w, double h, ColorRgba? fill, ColorRgba? stroke = null, double strokeWidth = 0, double radius = 0)
            => parts.Add(new GroupMember(new ShapeObject(new ObjectBox(ox + x * s, oy + y * s, w * s, h * s), kind, fill, stroke, strokeWidth * s, CornerRadius: radius * s), name, 1));

        void Text(string text, double cx, double cy, double fontSize, ColorRgba color)
        {
            var t = new TextObject(new ObjectBox(0, 0, 1, 1), text, "Segoe UI", Math.Max(6, fontSize * s), color, Bold: true);
            var (w, h) = ObjectRendering.Renderer.MeasureText(t);
            parts.Add(new GroupMember(t with { Box = new ObjectBox(ox + cx * s - w / 2, oy + cy * s - h / 2, w, h) }, text.Replace("\n", " "), 1));
        }

        switch (id)
        {
            case "sale": Shape("Burst", ShapeKind.Burst, 0, 0, 100, 100, C(225, 29, 72)); Text("SALE", 50, 50, 24, White); break;
            case "new": Shape("Circle", ShapeKind.Ellipse, 0, 0, 100, 100, C(37, 99, 235)); Text("NEW", 50, 50, 27, White); break;
            case "hot": Shape("Burst", ShapeKind.Burst, 0, 0, 100, 100, C(249, 115, 22)); Text("HOT", 50, 50, 27, White); break;
            case "off":
                Shape("Burst", ShapeKind.Burst, 0, 0, 100, 100, C(250, 204, 21));
                Text("50%", 50, 42, 27, Dark); Text("OFF", 50, 66, 17, Dark); break;
            case "free": Shape("Pill", ShapeKind.Rectangle, 0, 25, 100, 50, C(22, 163, 74), radius: 14); Text("FREE", 50, 50, 25, White); break;
            case "best": Shape("Tag", ShapeKind.Tag, 0, 10, 100, 80, C(124, 58, 237)); Text("BEST", 50, 50, 20, White); Text("SELLER", 50, 66, 11, White); break;
            case "limited": Shape("Frame", ShapeKind.Rectangle, 0, 30, 100, 40, White, Dark, 3); Text("LIMITED", 50, 50, 17, Dark); break;
            case "thanks": Shape("Heart", ShapeKind.Heart, 0, 5, 100, 90, C(239, 68, 68)); Text("THANKS", 50, 44, 14, White); break;
            case "verified": Shape("Circle", ShapeKind.Ellipse, 0, 0, 100, 100, C(34, 197, 94)); Shape("Check", ShapeKind.Check, 25, 28, 50, 44, White); break;
            case "stars":
                for (int i = 0; i < 5; i++) Shape("Star " + (i + 1), ShapeKind.Star, i * 20, 40, 20, 20, C(250, 204, 21));
                break;
            case "hello": Shape("Bubble", ShapeKind.SpeechBubble, 0, 10, 100, 70, White, Dark, 3, 10); Text("Hello!", 50, 42, 19, Dark); break;
            case "price": Shape("Pill", ShapeKind.Rectangle, 0, 25, 100, 50, C(14, 165, 233), radius: 25); Text("$9.99", 50, 50, 24, White); break;
            case "swipe": Shape("Pill", ShapeKind.Rectangle, 0, 30, 100, 40, Dark, radius: 20); Text("SWIPE", 40, 50, 15, White); Shape("Arrow", ShapeKind.Chevron, 68, 40, 20, 20, White); break;
            case "shield": Shape("Shield", ShapeKind.Shield, 10, 0, 80, 100, C(37, 99, 235)); Shape("Check", ShapeKind.Check, 30, 28, 40, 38, White); break;
            case "hundred": Shape("Ring", ShapeKind.Ellipse, 4, 4, 92, 92, null, C(225, 29, 72), 7); Text("100%", 50, 50, 23, C(225, 29, 72)); break;
            case "lightning": Shape("Circle", ShapeKind.Ellipse, 0, 0, 100, 100, C(250, 204, 21)); Shape("Bolt", ShapeKind.Lightning, 28, 14, 44, 72, Dark); break;
            case "arrival": Shape("Banner", ShapeKind.Rectangle, 0, 35, 100, 30, C(225, 29, 72)); Text("NEW ARRIVAL", 50, 50, 11.5, White); break;
            default: throw new ArgumentException("Unknown badge: " + id);
        }
        return GroupObject.Create(parts);
    }
}
