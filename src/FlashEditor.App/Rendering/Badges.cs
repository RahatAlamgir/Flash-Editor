using FlashEditor.Core.Imaging;
using FlashEditor.Core.Objects;

namespace FlashEditor.App.Rendering;

/// <summary>Ready-made labels and badges (SALE, NEW, 5 stars, speech bubble...) made of shapes and text. Each one is a group, so it can be ungrouped and changed.</summary>
public static class Badges
{
    public sealed record BadgeInfo(string Id, string Name, string Category);

    public static readonly string[] Categories = { "Sale and deals", "Awards and quality", "Social", "Info and steps" };

    public static readonly IReadOnlyList<BadgeInfo> All = new BadgeInfo[]
    {
        // sale and deals
        new("sale", "SALE", "Sale and deals"), new("off", "50% OFF", "Sale and deals"), new("hot", "HOT", "Sale and deals"), new("free", "FREE", "Sale and deals"),
        new("price", "Price", "Sale and deals"), new("arrival", "New arrival", "Sale and deals"), new("limited", "Limited", "Sale and deals"),
        new("bigsale", "Big sale", "Sale and deals"), new("clearance", "Clearance", "Sale and deals"), new("bogo", "Buy 1 get 1", "Sale and deals"),
        new("freeship", "Free shipping", "Sale and deals"), new("ordernow", "Order now", "Sale and deals"), new("hotdeal", "Hot deal", "Sale and deals"),
        new("soldout", "Sold out", "Sale and deals"), new("coming", "Coming soon", "Sale and deals"), new("pct", "-30%", "Sale and deals"),
        // awards and quality
        new("best", "Best seller", "Awards and quality"), new("verified", "Verified", "Awards and quality"), new("stars", "5 stars", "Awards and quality"),
        new("shield", "Shield", "Awards and quality"), new("hundred", "100%", "Awards and quality"), new("lightning", "Lightning", "Awards and quality"),
        new("bestchoice", "Best choice", "Awards and quality"), new("toprated", "Top rated", "Awards and quality"), new("premium", "Premium", "Awards and quality"),
        new("number1", "Number 1", "Awards and quality"), new("guarantee", "Guarantee", "Awards and quality"), new("winner", "Winner", "Awards and quality"),
        new("certified", "Certified", "Awards and quality"), new("eco", "Eco", "Awards and quality"), new("organic", "Organic", "Awards and quality"),
        new("handmade", "Handmade", "Awards and quality"), new("rated", "Rating 4.9", "Awards and quality"),
        // social
        new("new", "NEW", "Social"), new("thanks", "Thank you", "Social"), new("hello", "Speech bubble", "Social"), new("swipe", "Swipe", "Social"),
        new("like", "Likes", "Social"), new("subscribe", "Subscribe", "Social"), new("follow", "Follow", "Social"), new("live", "Live", "Social"),
        new("newpost", "New post", "Social"), new("trending", "Trending", "Social"),
        // info and steps
        new("tip", "Tip", "Info and steps"), new("note", "Note", "Info and steps"), new("important", "Important", "Info and steps"), new("info", "Info", "Info and steps"),
        new("question", "Question", "Info and steps"), new("step1", "Step 1", "Info and steps"), new("step2", "Step 2", "Info and steps"), new("step3", "Step 3", "Info and steps"),
        new("done", "Done", "Info and steps"),
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
            case "bigsale": Shape("Burst", ShapeKind.Burst, 0, 0, 100, 100, C(190, 18, 60)); Text("BIG", 50, 40, 20, White); Text("SALE", 50, 62, 22, C(253, 224, 71)); break;
            case "clearance": Shape("Banner", ShapeKind.Rectangle, 0, 28, 100, 44, C(220, 38, 38), radius: 6); Text("CLEARANCE", 50, 50, 13, White); break;
            case "bogo": Shape("Burst", ShapeKind.Burst, 0, 0, 100, 100, C(124, 58, 237)); Text("BUY 1", 50, 40, 17, White); Text("GET 1", 50, 62, 17, C(253, 224, 71)); break;
            case "freeship": Shape("Pill", ShapeKind.Rectangle, 0, 28, 100, 44, C(22, 163, 74), radius: 22); Text("FREE SHIPPING", 50, 50, 10.5, White); break;
            case "ordernow": Shape("Pill", ShapeKind.Rectangle, 0, 28, 100, 44, C(37, 99, 235), radius: 22); Text("ORDER NOW", 42, 50, 12, White); Shape("Chevron", ShapeKind.Chevron, 76, 41, 14, 18, White); break;
            case "hotdeal": Shape("Tag", ShapeKind.Tag, 0, 12, 100, 76, C(249, 115, 22)); Text("HOT", 52, 42, 22, White); Text("DEAL", 52, 62, 16, C(120, 53, 15)); break;
            case "soldout": Shape("Frame", ShapeKind.Rectangle, 0, 28, 100, 44, White, C(220, 38, 38), 4, 6); Text("SOLD OUT", 50, 50, 15, C(220, 38, 38)); break;
            case "coming": Shape("Frame", ShapeKind.Rectangle, 0, 28, 100, 44, Dark, null, 0, 10); Text("COMING", 50, 42, 13, White); Text("SOON", 50, 60, 13, C(250, 204, 21)); break;
            case "pct": Shape("Circle", ShapeKind.Ellipse, 0, 0, 100, 100, C(225, 29, 72)); Text("-30%", 50, 50, 26, White); break;

            case "bestchoice": Shape("Burst", ShapeKind.Burst, 0, 0, 100, 100, C(234, 179, 8)); Shape("Star", ShapeKind.Star, 30, 14, 40, 40, White); Text("BEST", 50, 66, 15, White); Text("CHOICE", 50, 82, 9, White); break;
            case "toprated": Shape("Circle", ShapeKind.Ellipse, 0, 0, 100, 100, C(217, 119, 6)); Shape("Ring", ShapeKind.Ellipse, 8, 8, 84, 84, null, C(254, 243, 199), 3); Shape("Star", ShapeKind.Star, 36, 14, 28, 28, C(254, 243, 199)); Text("TOP", 50, 56, 17, White); Text("RATED", 50, 75, 11, White); break;
            case "premium": Shape("Shield", ShapeKind.Shield, 10, 0, 80, 100, C(88, 28, 135)); Shape("Star", ShapeKind.Star, 36, 14, 28, 28, C(250, 204, 21)); Text("PREMIUM", 50, 62, 10.5, White); break;
            case "number1": Shape("Circle", ShapeKind.Ellipse, 0, 0, 100, 100, C(250, 204, 21)); Shape("Ring", ShapeKind.Ellipse, 7, 7, 86, 86, null, C(161, 98, 7), 3); Text("#1", 50, 50, 38, C(113, 63, 18)); break;
            case "guarantee": Shape("Circle", ShapeKind.Ellipse, 0, 0, 100, 100, C(21, 128, 61)); Shape("Ring", ShapeKind.Ellipse, 7, 7, 86, 86, null, White, 2); Text("100%", 50, 40, 22, White); Text("GUARANTEE", 50, 64, 8.5, White); break;
            case "winner": Shape("Star", ShapeKind.Star, 0, 0, 100, 100, C(250, 204, 21)); Text("WINNER", 50, 56, 11, C(113, 63, 18)); break;
            case "certified": Shape("Burst", ShapeKind.Burst, 0, 0, 100, 100, C(37, 99, 235)); Shape("Check", ShapeKind.Check, 28, 30, 44, 40, White); break;
            case "eco": Shape("Drop", ShapeKind.Teardrop, 10, 6, 80, 88, C(34, 197, 94)); Text("ECO", 50, 58, 20, White); break;
            case "organic": Shape("Circle", ShapeKind.Ellipse, 0, 0, 100, 100, C(101, 163, 13)); Text("100%", 50, 40, 18, White); Text("ORGANIC", 50, 62, 11, White); break;
            case "handmade": Shape("Heart", ShapeKind.Heart, 0, 4, 100, 92, C(236, 72, 153)); Text("HAND", 50, 40, 13, White); Text("MADE", 50, 56, 13, White); break;
            case "rated": Shape("Pill", ShapeKind.Rectangle, 0, 26, 100, 48, Dark, radius: 24); Shape("Star", ShapeKind.Star, 10, 36, 28, 28, C(250, 204, 21)); Text("4.9", 66, 50, 20, White); break;

            case "like": Shape("Pill", ShapeKind.Rectangle, 0, 26, 100, 48, C(254, 226, 226), radius: 24); Shape("Heart", ShapeKind.Heart, 10, 36, 28, 26, C(239, 68, 68)); Text("1.2K", 66, 50, 18, C(153, 27, 27)); break;
            case "subscribe": Shape("Pill", ShapeKind.Rectangle, 0, 28, 100, 44, C(220, 38, 38), radius: 8); Shape("Play", ShapeKind.RightTriangle, 9, 38, 14, 24, White); Text("SUBSCRIBE", 60, 50, 11, White); break;
            case "follow": Shape("Pill", ShapeKind.Rectangle, 0, 28, 100, 44, C(59, 130, 246), radius: 22); Text("+ FOLLOW", 50, 50, 14, White); break;
            case "live": Shape("Pill", ShapeKind.Rectangle, 0, 28, 100, 44, C(220, 38, 38), radius: 8); Shape("Dot", ShapeKind.Ellipse, 12, 42, 16, 16, White); Text("LIVE", 62, 50, 20, White); break;
            case "newpost": Shape("Bubble", ShapeKind.SpeechBubble, 0, 8, 100, 72, C(37, 99, 235), null, 0, 10); Text("NEW", 50, 34, 16, White); Text("POST", 50, 54, 16, White); break;
            case "trending": Shape("Pill", ShapeKind.Rectangle, 0, 28, 100, 44, C(249, 115, 22), radius: 22); Shape("Bolt", ShapeKind.Lightning, 8, 36, 16, 28, C(254, 240, 138)); Text("TRENDING", 58, 50, 10.5, White); break;

            case "tip": Shape("Bubble", ShapeKind.SpeechBubble, 0, 8, 100, 72, C(250, 204, 21), null, 0, 10); Text("TIP", 50, 42, 26, C(113, 63, 18)); break;
            case "note": Shape("Note", ShapeKind.Rectangle, 4, 4, 92, 92, C(254, 240, 138), null, 0, 4); Text("NOTE", 50, 50, 22, C(113, 63, 18)); break;
            case "important": Shape("Warn", ShapeKind.Triangle, 4, 6, 92, 84, C(220, 38, 38)); Text("!", 50, 66, 40, White); break;
            case "info": Shape("Circle", ShapeKind.Ellipse, 0, 0, 100, 100, C(14, 165, 233)); Text("i", 50, 50, 56, White); break;
            case "question": Shape("Circle", ShapeKind.Ellipse, 0, 0, 100, 100, C(124, 58, 237)); Text("?", 50, 50, 56, White); break;
            case "step1": Shape("Circle", ShapeKind.Ellipse, 0, 0, 100, 100, C(37, 99, 235)); Text("1", 50, 50, 56, White); break;
            case "step2": Shape("Circle", ShapeKind.Ellipse, 0, 0, 100, 100, C(22, 163, 74)); Text("2", 50, 50, 56, White); break;
            case "step3": Shape("Circle", ShapeKind.Ellipse, 0, 0, 100, 100, C(249, 115, 22)); Text("3", 50, 50, 56, White); break;
            case "done": Shape("Box", ShapeKind.Rectangle, 4, 4, 92, 92, C(22, 163, 74), null, 0, 18); Shape("Check", ShapeKind.Check, 24, 28, 52, 44, White); break;
            default: throw new ArgumentException("Unknown badge: " + id);
        }
        return GroupObject.Create(parts);
    }
}
