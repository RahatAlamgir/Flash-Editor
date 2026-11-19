namespace FlashEditor.App.Services;

/// <summary>One icon of the built-in set: a symbol of the Windows icon font that is inserted as editable text (any color, size, outline, shadow).</summary>
public sealed record IconItem(string Category, string Name, string Glyph);

/// <summary>The icons offered by Insert > Icons. Each one is a character of the Windows icon font, so it stays sharp at any size.</summary>
public static class IconCatalog
{
    /// <summary>The font the icons come from (Windows 11 first, the older Windows 10 font as backup).</summary>
    public const string FontFamily = "Segoe Fluent Icons, Segoe MDL2 Assets";

    public static readonly string[] Categories =
        { "Arrows", "Symbols", "Communication", "Media", "Objects", "Places and travel", "Weather and nature", "Shapes" };

    private static IconItem I(string category, string name, int code) => new(category, name, char.ConvertFromUtf32(code));

    public static readonly IReadOnlyList<IconItem> All = new[]
    {
        I("Arrows", "Right arrow", 0xE72A), I("Arrows", "Left arrow", 0xE72B), I("Arrows", "Up arrow", 0xE74A), I("Arrows", "Down arrow", 0xE74B),
        I("Arrows", "Long left arrow", 0xE7EA), I("Arrows", "Long up arrow", 0xE7EB), I("Arrows", "Up right arrow", 0xE8AD),
        I("Arrows", "Expand", 0xE740), I("Arrows", "Shrink", 0xE73F), I("Arrows", "Chevron left", 0xE76B), I("Arrows", "Chevron right", 0xE76C),
        I("Arrows", "Chevron down", 0xE70D), I("Arrows", "Chevron up", 0xE70E), I("Arrows", "Refresh", 0xE72C), I("Arrows", "Turn back", 0xE777),
        I("Arrows", "Redo", 0xE7A6), I("Arrows", "Undo", 0xE7A7), I("Arrows", "Swap", 0xE8AB), I("Arrows", "Up and down", 0xE8CB),
        I("Arrows", "Enter", 0xE751), I("Arrows", "Return", 0xE845), I("Arrows", "Share", 0xE72D), I("Arrows", "Send", 0xE724),
        I("Arrows", "Download", 0xE896), I("Arrows", "Upload", 0xE898), I("Arrows", "Sync", 0xE895),

        I("Symbols", "Check", 0xE73E), I("Symbols", "Check circle", 0xE930), I("Symbols", "Cross", 0xE711), I("Symbols", "Plus", 0xE710),
        I("Symbols", "Minus", 0xE738), I("Symbols", "Star outline", 0xE734), I("Symbols", "Star", 0xE735), I("Symbols", "Thumbs up", 0xE8E1),
        I("Symbols", "Thumbs down", 0xE8E0), I("Symbols", "Smile", 0xE76E), I("Symbols", "Happy face", 0xE899), I("Symbols", "Warning", 0xE7BA),
        I("Symbols", "Warning filled", 0xE814), I("Symbols", "Info", 0xE946), I("Symbols", "Exclamation", 0xE783), I("Symbols", "Question", 0xE897),
        I("Symbols", "Help circle", 0xE9CE), I("Symbols", "Alert", 0xE8C9), I("Symbols", "Flag", 0xE7C1), I("Symbols", "Pin", 0xE718),
        I("Symbols", "Pushpin", 0xE840), I("Symbols", "Lock", 0xE72E), I("Symbols", "Unlock", 0xE785), I("Symbols", "Key", 0xE8D7),
        I("Symbols", "Eye", 0xE7B3), I("Symbols", "Eye open", 0xE890), I("Symbols", "Tag", 0xE8EC), I("Symbols", "Shield", 0xE730),
        I("Symbols", "Security", 0xE705), I("Symbols", "Not allowed", 0xE733), I("Symbols", "Power", 0xE7E8), I("Symbols", "Lightning", 0xE945),
        I("Symbols", "Fingerprint", 0xE928), I("Symbols", "Light bulb", 0xE82F), I("Symbols", "Settings", 0xE713), I("Symbols", "Wrench", 0xE90F),
        I("Symbols", "Search", 0xE721), I("Symbols", "Zoom", 0xE71E), I("Symbols", "Filter", 0xE71C), I("Symbols", "Link", 0xE71B),
        I("Symbols", "Paperclip", 0xE723), I("Symbols", "At sign", 0xE910), I("Symbols", "Favorites", 0xE728), I("Symbols", "List", 0xE71D),

        I("Communication", "Mail", 0xE715), I("Communication", "Open mail", 0xE8C3), I("Communication", "Phone", 0xE717), I("Communication", "Chat", 0xE8BD),
        I("Communication", "Speech bubble", 0xE90A), I("Communication", "Comments", 0xE8F2), I("Communication", "People", 0xE716),
        I("Communication", "Person", 0xE77B), I("Communication", "Contact card", 0xE8D4), I("Communication", "Globe", 0xE774),
        I("Communication", "World", 0xE909), I("Communication", "Wi-Fi", 0xE701), I("Communication", "Bluetooth", 0xE702), I("Communication", "Signal", 0xE704),
        I("Communication", "Megaphone", 0xE789), I("Communication", "Feedback", 0xE939), I("Communication", "Support", 0xE95B),

        I("Media", "Video", 0xE714), I("Media", "Camera", 0xE722), I("Media", "Play", 0xE768), I("Media", "Pause", 0xE769), I("Media", "Stop", 0xE71A),
        I("Media", "Volume", 0xE767), I("Media", "Mute", 0xE74F), I("Media", "Microphone", 0xE720), I("Media", "Music note", 0xE8D6),
        I("Media", "Playlist", 0xE90B), I("Media", "Headphones", 0xE7F6), I("Media", "Picture", 0xE91B), I("Media", "Photo", 0xE8B9),
        I("Media", "Slideshow", 0xE786), I("Media", "Television", 0xE7F4), I("Media", "Game controller", 0xE7FC), I("Media", "Paint brush", 0xE771),
        I("Media", "Palette", 0xE790), I("Media", "Pencil", 0xE70F), I("Media", "Highlighter", 0xE76D), I("Media", "Eraser", 0xE75C), I("Media", "Edit", 0xE932),

        I("Objects", "Gift", 0xE719), I("Objects", "Shopping cart", 0xE7BF), I("Objects", "Home", 0xE80F), I("Objects", "Briefcase", 0xE821),
        I("Objects", "Bank", 0xE825), I("Objects", "Library", 0xE8F1), I("Objects", "Graduation cap", 0xE7BE), I("Objects", "Folder", 0xE8B7),
        I("Objects", "Open folder", 0xE838), I("Objects", "Document", 0xE8A5), I("Objects", "Page", 0xE7C3), I("Objects", "Calendar", 0xE787),
        I("Objects", "Clock", 0xE823), I("Objects", "Stopwatch", 0xE916), I("Objects", "History", 0xE81C), I("Objects", "Devices", 0xE772),
        I("Objects", "Laptop", 0xE770), I("Objects", "Mobile phone", 0xE8EA), I("Objects", "Keyboard", 0xE765), I("Objects", "Mouse", 0xE962),
        I("Objects", "Speaker", 0xE7F5), I("Objects", "Calculator", 0xE8EF), I("Objects", "Printer", 0xE749), I("Objects", "Save", 0xE74E),
        I("Objects", "Trash", 0xE74D), I("Objects", "Scissors", 0xE8C6), I("Objects", "Copy", 0xE8C8), I("Objects", "Battery", 0xE83F),
        I("Objects", "Package", 0xE7B8), I("Objects", "Credit card", 0xE8C7), I("Objects", "Cloud", 0xE753), I("Objects", "Book", 0xE82D),

        I("Places and travel", "Map", 0xE826), I("Places and travel", "Map pin", 0xE7B7), I("Places and travel", "Location", 0xE819),
        I("Places and travel", "Car", 0xE811), I("Places and travel", "Bus", 0xE806), I("Places and travel", "Train", 0xE7C0), I("Places and travel", "Airplane", 0xE709),
        I("Places and travel", "Ship", 0xE7E3), I("Places and travel", "Walking", 0xE726), I("Places and travel", "Pedestrian", 0xE805),
        I("Places and travel", "Building", 0xE731), I("Places and travel", "Suitcase", 0xE821),

        I("Weather and nature", "Sun", 0xE706), I("Weather and nature", "Moon", 0xE708), I("Weather and nature", "Brightness", 0xE793),
        I("Weather and nature", "Cloud", 0xE753), I("Weather and nature", "Thermometer", 0xE9CA), I("Weather and nature", "Leaf", 0xE8BE),

        I("Shapes", "Square", 0xE739), I("Shapes", "Filled square", 0xE73B), I("Shapes", "Filled circle", 0xE91F), I("Shapes", "Ring", 0xE7C8),
        I("Shapes", "Triangle", 0xE879), I("Shapes", "Filled triangle", 0xE878), I("Shapes", "Diamond", 0xE82C), I("Shapes", "Triangle up", 0xE812),
        I("Shapes", "Triangle down", 0xE813), I("Shapes", "Triangle right", 0xE937), I("Shapes", "Triangle left", 0xE938), I("Shapes", "Dot", 0xE915),
        I("Shapes", "Rounded square", 0xE71A), I("Shapes", "Checkbox", 0xE73A),
    };
}
