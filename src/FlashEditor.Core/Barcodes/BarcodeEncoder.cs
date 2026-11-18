namespace FlashEditor.Core.Barcodes;

/// <summary>The kinds of barcode that can be made.</summary>
public enum BarcodeKind { Code128, Ean13, UpcA, Ean8, Code39 }

/// <summary>A barcode as a row of modules (true = a dark bar, one entry per module) and the numbers or letters printed under it.</summary>
/// <param name="Labels">Printed text with the middle of each piece, counted in modules from the left end of the bars (can be negative: outside the bars).</param>
/// <param name="Guards">Modules of the long guard bars of EAN / UPC codes (they reach down into the text); empty for other kinds.</param>
public sealed record BarcodeData(bool[] Modules, IReadOnlyList<(string Text, double Center)> Labels, IReadOnlyList<(int From, int To)> Guards)
{
    public int Width => Modules.Length;
}

/// <summary>
/// Makes barcodes: Code 128 (any text), EAN-13, UPC-A and EAN-8 (shop codes, digits with a check digit) and Code 39 (capital letters, digits
/// and a few marks). Written from the standards: no library.
/// </summary>
public static class BarcodeEncoder
{
    /// <summary>What a kind of barcode accepts, in plain words (shown under the text box).</summary>
    public static string Describe(BarcodeKind kind) => kind switch
    {
        BarcodeKind.Ean13 => "12 digits (the 13th, the check digit, is added) or all 13 digits.",
        BarcodeKind.UpcA => "11 digits (the 12th, the check digit, is added) or all 12 digits.",
        BarcodeKind.Ean8 => "7 digits (the 8th, the check digit, is added) or all 8 digits.",
        BarcodeKind.Code39 => "Capital letters, digits, space and - . $ / + %",
        _ => "Any text with letters, digits and common marks (no accents). Only digits make a shorter code.",
    };

    /// <summary>Null when <paramref name="content"/> can be made into this kind of barcode, otherwise what is wrong.</summary>
    public static string? Validate(string content, BarcodeKind kind)
    {
        try { Encode(content, kind); return null; }
        catch (FormatException ex) { return ex.Message; }
    }

    /// <summary>The barcode for <paramref name="content"/>. Throws <see cref="FormatException"/> with a plain message when it does not fit the kind.</summary>
    public static BarcodeData Encode(string content, BarcodeKind kind)
    {
        content ??= "";
        return kind switch
        {
            BarcodeKind.Ean13 => EanUpc(content, 13, upc: false),
            BarcodeKind.UpcA => EanUpc(content, 12, upc: true),
            BarcodeKind.Ean8 => Ean8(content),
            BarcodeKind.Code39 => Code39(content),
            _ => Code128(content),
        };
    }

    // ---------------------------------------------------------------- EAN / UPC

    private static readonly string[] L = { "0001101", "0011001", "0010011", "0111101", "0100011", "0110001", "0101111", "0111011", "0110111", "0001011" };
    private static readonly string[] G = { "0100111", "0110011", "0011011", "0100001", "0011101", "0111001", "0000101", "0010001", "0001001", "0010111" };
    private static readonly string[] R = { "1110010", "1100110", "1101100", "1000010", "1011100", "1001110", "1010000", "1000100", "1001000", "1110100" };
    private static readonly string[] Parity = { "LLLLLL", "LLGLGG", "LLGGLG", "LLGGGL", "LGLLGG", "LGGLLG", "LGGGLL", "LGLGLG", "LGLGGL", "LGGLGL" };

    /// <summary>The check digit for the digits before it (EAN / UPC: weights 3 and 1 counted from the right).</summary>
    public static int CheckDigit(string digits)
    {
        int sum = 0;
        for (int i = 0; i < digits.Length; i++)
        {
            int digit = digits[digits.Length - 1 - i] - '0';
            sum += i % 2 == 0 ? digit * 3 : digit;
        }
        return (10 - sum % 10) % 10;
    }

    private static string Digits(string content, int length, string name)
    {
        string text = content.Trim().Replace(" ", "");
        if (text.Length == 0 || !text.All(char.IsAsciiDigit)) throw new FormatException($"{name} needs digits only.");
        if (text.Length != length && text.Length != length - 1) throw new FormatException($"{name} needs {length - 1} or {length} digits (you typed {text.Length}).");
        string body = text[..(length - 1)];
        int check = CheckDigit(body);
        if (text.Length == length && text[^1] - '0' != check) throw new FormatException($"The check digit of {name} should be {check}, not {text[^1]}. Leave it out and it is added for you.");
        return body + check;
    }

    private static BarcodeData EanUpc(string content, int length, bool upc)
    {
        string full = Digits(content, length, upc ? "UPC-A" : "EAN-13");
        string ean = upc ? "0" + full : full;                         // a UPC-A is an EAN-13 with a 0 in front

        var modules = new List<bool>();
        void Add(string pattern) { foreach (char c in pattern) modules.Add(c == '1'); }
        Add("101");
        string parity = Parity[ean[0] - '0'];
        for (int i = 0; i < 6; i++) Add((parity[i] == 'L' ? L : G)[ean[1 + i] - '0']);
        Add("01010");
        for (int i = 0; i < 6; i++) Add(R[ean[7 + i] - '0']);
        Add("101");

        var guards = new[] { (0, 3), (45, 50), (92, 95) };
        IReadOnlyList<(string, double)> labels = upc
            ? new[] { (full[..1], -5.0), (full[1..6], 27.5), (full[6..11], 67.5), (full[11..], 100.0) }
            : new[] { (ean[..1], -5.0), (ean[1..7], 24.0), (ean[7..], 71.0) };
        return new BarcodeData(modules.ToArray(), labels, guards);
    }

    private static BarcodeData Ean8(string content)
    {
        string full = Digits(content, 8, "EAN-8");
        var modules = new List<bool>();
        void Add(string pattern) { foreach (char c in pattern) modules.Add(c == '1'); }
        Add("101");
        for (int i = 0; i < 4; i++) Add(L[full[i] - '0']);
        Add("01010");
        for (int i = 4; i < 8; i++) Add(R[full[i] - '0']);
        Add("101");
        return new BarcodeData(modules.ToArray(), new[] { (full[..4], 17.0), (full[4..], 50.0) }, new[] { (0, 3), (31, 36), (64, 67) });
    }

    // ---------------------------------------------------------------- Code 39

    // Each character: nine elements (bar, space, bar...) where 'w' is wide (3 modules) and 'n' narrow (1 module).
    private static readonly Dictionary<char, string> C39 = new()
    {
        ['0'] = "nnnwwnwnn", ['1'] = "wnnwnnnnw", ['2'] = "nnwwnnnnw", ['3'] = "wnwwnnnnn", ['4'] = "nnnwwnnnw", ['5'] = "wnnwwnnnn",
        ['6'] = "nnwwwnnnn", ['7'] = "nnnwnnwnw", ['8'] = "wnnwnnwnn", ['9'] = "nnwwnnwnn", ['A'] = "wnnnnwnnw", ['B'] = "nnwnnwnnw",
        ['C'] = "wnwnnwnnn", ['D'] = "nnnnwwnnw", ['E'] = "wnnnwwnnn", ['F'] = "nnwnwwnnn", ['G'] = "nnnnnwwnw", ['H'] = "wnnnnwwnn",
        ['I'] = "nnwnnwwnn", ['J'] = "nnnnwwwnn", ['K'] = "wnnnnnnww", ['L'] = "nnwnnnnww", ['M'] = "wnwnnnnwn", ['N'] = "nnnnwnnww",
        ['O'] = "wnnnwnnwn", ['P'] = "nnwnwnnwn", ['Q'] = "nnnnnnwww", ['R'] = "wnnnnnwwn", ['S'] = "nnwnnnwwn", ['T'] = "nnnnwnwwn",
        ['U'] = "wwnnnnnnw", ['V'] = "nwwnnnnnw", ['W'] = "wwwnnnnnn", ['X'] = "nwnnwnnnw", ['Y'] = "wwnnwnnnn", ['Z'] = "nwwnwnnnn",
        ['-'] = "nwnnnnwnw", ['.'] = "wwnnnnwnn", [' '] = "nwwnnnwnn", ['$'] = "nwnwnwnnn", ['/'] = "nwnwnnnwn", ['+'] = "nwnnnwnwn",
        ['%'] = "nnnwnwnwn", ['*'] = "nwnnwnwnn",
    };

    /// <summary>The Code 39 characters (without the start / stop mark).</summary>
    public static IReadOnlyCollection<char> Code39Characters => C39.Keys.Where(c => c != '*').ToList();

    private static BarcodeData Code39(string content)
    {
        string text = content.ToUpperInvariant();
        if (text.Length == 0) throw new FormatException("Type something for the barcode.");
        foreach (char c in text)
            if (c == '*' || !C39.ContainsKey(c)) throw new FormatException($"Code 39 cannot show \"{c}\". Use capital letters, digits, space and - . $ / + %");
        if (text.Length > 40) throw new FormatException("Code 39 is limited to 40 characters here.");

        var modules = new List<bool>();
        string all = "*" + text + "*";
        for (int ch = 0; ch < all.Length; ch++)
        {
            string pattern = C39[all[ch]];
            for (int i = 0; i < 9; i++)
            {
                bool bar = i % 2 == 0;
                int width = pattern[i] == 'w' ? 3 : 1;
                for (int k = 0; k < width; k++) modules.Add(bar);
            }
            if (ch < all.Length - 1) modules.Add(false);               // the gap between two characters
        }
        return new BarcodeData(modules.ToArray(), new[] { (text, modules.Count / 2.0) }, Array.Empty<(int, int)>());
    }

    // ---------------------------------------------------------------- Code 128

    // The widths of bar, space, bar, space, bar, space for symbol values 0..105, and the stop pattern (value 106, seven elements).
    public static readonly string[] C128 =
    {
        "212222", "222122", "222221", "121223", "121322", "131222", "122213", "122312", "132212", "221213",
        "221312", "231212", "112232", "122132", "122231", "113222", "123122", "123221", "223211", "221132",
        "221231", "213212", "223112", "312131", "311222", "321122", "321221", "312212", "322112", "322211",
        "212123", "212321", "232121", "111323", "131123", "131321", "112313", "132113", "132311", "211313",
        "231113", "231311", "112133", "112331", "132131", "113123", "113321", "133121", "313121", "211331",
        "231131", "213113", "213311", "213131", "311123", "311321", "331121", "312113", "312311", "332111",
        "314111", "221411", "431111", "111224", "111422", "121124", "121421", "141122", "141221", "112214",
        "112412", "122114", "122411", "142112", "142211", "241211", "221114", "413111", "241112", "134111",
        "111242", "121142", "121241", "114212", "124112", "124211", "411212", "421112", "421211", "212141",
        "214121", "412121", "111143", "111341", "131141", "114113", "114311", "411113", "411311", "113141",
        "114131", "311141", "411131", "211412", "211214", "211232",
        "2331112",
    };

    private static BarcodeData Code128(string content)
    {
        if (content.Length == 0) throw new FormatException("Type something for the barcode.");
        if (content.Length > 80) throw new FormatException("Code 128 is limited to 80 characters here.");

        var values = new List<int>();
        bool digitsOnly = content.All(char.IsAsciiDigit) && content.Length >= 4 && content.Length % 2 == 0;
        if (digitsOnly)
        {
            values.Add(105);                                                         // start C: two digits per symbol
            for (int i = 0; i < content.Length; i += 2) values.Add((content[i] - '0') * 10 + content[i + 1] - '0');
        }
        else
        {
            values.Add(104);                                                         // start B: the characters from space to ~
            foreach (char c in content)
            {
                if (c < 32 || c > 126) throw new FormatException($"Code 128 cannot show \"{c}\". Use letters without accents, digits and common marks.");
                values.Add(c - 32);
            }
        }
        long sum = values[0];
        for (int i = 1; i < values.Count; i++) sum += (long)values[i] * i;
        values.Add((int)(sum % 103));
        values.Add(106);                                                             // stop

        var modules = new List<bool>();
        foreach (int v in values)
        {
            bool bar = true;
            foreach (char w in C128[v])
            {
                for (int k = 0; k < w - '0'; k++) modules.Add(bar);
                bar = !bar;
            }
        }
        return new BarcodeData(modules.ToArray(), new[] { (content, modules.Count / 2.0) }, Array.Empty<(int, int)>());
    }
}
