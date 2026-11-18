namespace FlashEditor.Core.Qr;

/// <summary>How much damage a QR code survives (more protection = a bigger, denser code).</summary>
public enum QrLevel { Low, Medium, Quartile, High }

/// <summary>
/// Makes QR codes (ISO 18004, byte mode, versions 1 to 40) from text. Written from the standard: no library and no web service.
/// The result is a square grid of dark / light modules without the empty border (the quiet zone is added when drawing).
/// </summary>
public static class QrEncoder
{
    // Error correction codewords per block, and number of blocks, for versions 0 (unused) to 40, per level (Low, Medium, Quartile, High).
    private static readonly int[][] EccPerBlock =
    {
        new[] { -1, 7, 10, 15, 20, 26, 18, 20, 24, 30, 18, 20, 24, 26, 30, 22, 24, 28, 30, 28, 28, 28, 28, 30, 30, 26, 28, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30 },
        new[] { -1, 10, 16, 26, 18, 24, 16, 18, 22, 22, 26, 30, 22, 22, 24, 24, 28, 28, 26, 26, 26, 26, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28 },
        new[] { -1, 13, 22, 18, 26, 18, 24, 18, 22, 20, 24, 28, 26, 24, 20, 30, 24, 28, 28, 26, 30, 28, 30, 30, 30, 30, 28, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30 },
        new[] { -1, 17, 28, 22, 16, 22, 28, 26, 26, 24, 28, 24, 28, 22, 24, 24, 30, 28, 28, 26, 28, 30, 24, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30 },
    };

    private static readonly int[][] BlockCount =
    {
        new[] { -1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 4, 4, 4, 4, 4, 6, 6, 6, 6, 7, 8, 8, 9, 9, 10, 12, 12, 12, 13, 14, 15, 16, 17, 18, 19, 19, 20, 21, 22, 24, 25 },
        new[] { -1, 1, 1, 1, 2, 2, 4, 4, 4, 5, 5, 5, 8, 9, 9, 10, 10, 11, 13, 14, 16, 17, 17, 18, 20, 21, 23, 25, 26, 28, 29, 31, 33, 35, 37, 38, 40, 43, 45, 47, 49 },
        new[] { -1, 1, 1, 2, 2, 4, 4, 6, 6, 8, 8, 8, 10, 12, 16, 12, 17, 16, 18, 21, 20, 23, 23, 25, 27, 29, 34, 34, 35, 38, 40, 43, 45, 48, 51, 53, 56, 59, 62, 65, 68 },
        new[] { -1, 1, 1, 2, 4, 4, 4, 5, 6, 8, 8, 11, 11, 16, 16, 18, 16, 19, 21, 25, 25, 25, 34, 30, 32, 35, 37, 40, 42, 45, 48, 51, 54, 57, 60, 63, 66, 70, 74, 77, 81 },
    };

    // The two bits that say the level inside the format information.
    private static readonly int[] FormatBits = { 1, 0, 3, 2 };

    /// <summary>The largest text (in bytes) that fits at the given level.</summary>
    public static int MaxBytes(QrLevel level) => DataCodewords(40, level) - 3;

    /// <summary>The modules of the QR code for <paramref name="text"/> (UTF-8). Throws <see cref="ArgumentException"/> when the text is too long.</summary>
    public static bool[,] Encode(string text, QrLevel level = QrLevel.Medium, int? forceMask = null)
    {
        byte[] data = System.Text.Encoding.UTF8.GetBytes(text ?? "");
        int version = 0;
        for (int v = 1; v <= 40; v++)
        {
            int countBits = v < 10 ? 8 : 16;
            if (data.Length < (1 << countBits) && 4 + countBits + 8 * data.Length <= DataCodewords(v, level) * 8) { version = v; break; }
        }
        if (version == 0) throw new ArgumentException("The text is too long for a QR code. Shorten it or lower the error correction.", nameof(text));

        // Bits: mode (byte = 0100), length, the bytes, an end mark, then padding to the capacity.
        var bits = new List<bool>();
        void Put(int value, int count) { for (int i = count - 1; i >= 0; i--) bits.Add(((value >> i) & 1) != 0); }
        Put(0b0100, 4);
        Put(data.Length, version < 10 ? 8 : 16);
        foreach (byte b in data) Put(b, 8);
        int capacity = DataCodewords(version, level) * 8;
        Put(0, Math.Min(4, capacity - bits.Count));
        Put(0, (8 - bits.Count % 8) % 8);
        for (int pad = 0xEC; bits.Count < capacity; pad ^= 0xEC ^ 0x11) Put(pad, 8);

        var codewords = new byte[bits.Count / 8];
        for (int i = 0; i < bits.Count; i++) if (bits[i]) codewords[i >> 3] |= (byte)(0x80 >> (i & 7));

        return new Builder(version, level, AddErrorCorrection(codewords, version, level)).Build(forceMask);
    }

    // ---------------------------------------------------------------- sizes

    private static int RawModules(int version)
    {
        int result = (16 * version + 128) * version + 64;
        if (version >= 2)
        {
            int align = version / 7 + 2;
            result -= (25 * align - 10) * align - 55;
            if (version >= 7) result -= 36;
        }
        return result;
    }

    private static int DataCodewords(int version, QrLevel level)
        => RawModules(version) / 8 - EccPerBlock[(int)level][version] * BlockCount[(int)level][version];

    // ---------------------------------------------------------------- Reed-Solomon

    private static byte[] AddErrorCorrection(byte[] data, int version, QrLevel level)
    {
        int blocks = BlockCount[(int)level][version], eccLength = EccPerBlock[(int)level][version];
        int raw = RawModules(version) / 8, shortBlocks = blocks - raw % blocks, shortLength = raw / blocks;
        var divisor = Divisor(eccLength);

        var dataBlocks = new List<byte[]>();
        var eccBlocks = new List<byte[]>();
        for (int i = 0, k = 0; i < blocks; i++)
        {
            int length = shortLength - eccLength + (i < shortBlocks ? 0 : 1);
            var block = new byte[length];
            Array.Copy(data, k, block, 0, length);
            k += length;
            dataBlocks.Add(block);
            eccBlocks.Add(Remainder(block, divisor));
        }

        var result = new List<byte>();
        for (int i = 0; i < dataBlocks[^1].Length; i++)                       // the long blocks have one more data byte
            foreach (var block in dataBlocks)
                if (i < block.Length) result.Add(block[i]);
        for (int i = 0; i < eccLength; i++)
            foreach (var block in eccBlocks) result.Add(block[i]);
        return result.ToArray();
    }

    private static byte[] Divisor(int degree)
    {
        var result = new byte[degree];
        result[degree - 1] = 1;
        int root = 1;
        for (int i = 0; i < degree; i++)
        {
            for (int j = 0; j < degree; j++)
            {
                result[j] = Multiply(result[j], root);
                if (j + 1 < degree) result[j] ^= result[j + 1];
            }
            root = Multiply(root, 2);
        }
        return result;
    }

    private static byte[] Remainder(byte[] data, byte[] divisor)
    {
        var result = new byte[divisor.Length];
        foreach (byte b in data)
        {
            int factor = b ^ result[0];
            Array.Copy(result, 1, result, 0, result.Length - 1);
            result[^1] = 0;
            for (int i = 0; i < result.Length; i++) result[i] ^= Multiply(divisor[i], factor);
        }
        return result;
    }

    // Multiplication in GF(2^8) with the polynomial x^8 + x^4 + x^3 + x^2 + 1.
    private static byte Multiply(int x, int y)
    {
        int z = 0;
        for (int i = 7; i >= 0; i--)
        {
            z = (z << 1) ^ ((z >> 7) * 0x11D);
            z ^= ((y >> i) & 1) * x;
        }
        return (byte)z;
    }

    // ---------------------------------------------------------------- the grid

    private sealed class Builder
    {
        private readonly int _version, _size;
        private readonly QrLevel _level;
        private readonly byte[] _codewords;
        private readonly bool[,] _modules;
        private readonly bool[,] _function;       // true for finder / timing / alignment / format modules (not data)

        public Builder(int version, QrLevel level, byte[] codewords)
        {
            _version = version;
            _level = level;
            _codewords = codewords;
            _size = version * 4 + 17;
            _modules = new bool[_size, _size];
            _function = new bool[_size, _size];
        }

        public bool[,] Build(int? forceMask = null)
        {
            DrawFunctionPatterns();
            DrawCodewords();

            int best = 0, lowest = int.MaxValue;
            for (int mask = 0; mask < 8; mask++)
            {
                if (forceMask != null && mask != forceMask) continue;
                ApplyMask(mask);
                DrawFormat(mask);
                int penalty = Penalty();
                if (penalty < lowest) { lowest = penalty; best = mask; }
                ApplyMask(mask);                  // applying twice undoes it
            }
            ApplyMask(best);
            DrawFormat(best);
            return _modules;
        }

        private void Set(int x, int y, bool dark)
        {
            _modules[y, x] = dark;
            _function[y, x] = true;
        }

        private static bool Bit(int value, int index) => ((value >> index) & 1) != 0;

        private void DrawFunctionPatterns()
        {
            for (int i = 0; i < _size; i++)
            {
                Set(6, i, i % 2 == 0);
                Set(i, 6, i % 2 == 0);
            }
            Finder(3, 3);
            Finder(_size - 4, 3);
            Finder(3, _size - 4);

            var positions = AlignmentPositions();
            int n = positions.Length;
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                {
                    if ((i == 0 && j == 0) || (i == 0 && j == n - 1) || (i == n - 1 && j == 0)) continue;   // where the finders are
                    Alignment(positions[i], positions[j]);
                }

            DrawFormat(0);                        // reserves the format area (the real bits are written after the mask is chosen)
            DrawVersion();
        }

        private void Finder(int cx, int cy)
        {
            for (int dy = -4; dy <= 4; dy++)
                for (int dx = -4; dx <= 4; dx++)
                {
                    int dist = Math.Max(Math.Abs(dx), Math.Abs(dy)), x = cx + dx, y = cy + dy;
                    if (x >= 0 && x < _size && y >= 0 && y < _size) Set(x, y, dist != 2 && dist != 4);
                }
        }

        private void Alignment(int cx, int cy)
        {
            for (int dy = -2; dy <= 2; dy++)
                for (int dx = -2; dx <= 2; dx++)
                    Set(cx + dx, cy + dy, Math.Max(Math.Abs(dx), Math.Abs(dy)) != 1);
        }

        private int[] AlignmentPositions()
        {
            if (_version == 1) return Array.Empty<int>();
            int count = _version / 7 + 2;
            int step = _version == 32 ? 26 : (_version * 4 + count * 2 + 1) / (count * 2 - 2) * 2;
            var result = new int[count];
            result[0] = 6;
            for (int i = count - 1, pos = _size - 7; i >= 1; i--, pos -= step) result[i] = pos;
            return result;
        }

        private void DrawFormat(int mask)
        {
            int data = FormatBits[(int)_level] << 3 | mask;
            int rem = data;
            for (int i = 0; i < 10; i++) rem = (rem << 1) ^ ((rem >> 9) * 0x537);
            int bits = (data << 10 | rem) ^ 0x5412;

            for (int i = 0; i <= 5; i++) Set(8, i, Bit(bits, i));
            Set(8, 7, Bit(bits, 6));
            Set(8, 8, Bit(bits, 7));
            Set(7, 8, Bit(bits, 8));
            for (int i = 9; i < 15; i++) Set(14 - i, 8, Bit(bits, i));

            for (int i = 0; i < 8; i++) Set(_size - 1 - i, 8, Bit(bits, i));
            for (int i = 8; i < 15; i++) Set(8, _size - 15 + i, Bit(bits, i));
            Set(8, _size - 8, true);              // the one module that is always dark
        }

        private void DrawVersion()
        {
            if (_version < 7) return;
            int rem = _version;
            for (int i = 0; i < 12; i++) rem = (rem << 1) ^ ((rem >> 11) * 0x1F25);
            int bits = _version << 12 | rem;
            for (int i = 0; i < 18; i++)
            {
                bool dark = Bit(bits, i);
                int a = _size - 11 + i % 3, b = i / 3;
                Set(a, b, dark);
                Set(b, a, dark);
            }
        }

        private void DrawCodewords()
        {
            int i = 0;
            for (int right = _size - 1; right >= 1; right -= 2)
            {
                if (right == 6) right = 5;                           // the vertical timing line is skipped
                for (int vert = 0; vert < _size; vert++)
                    for (int j = 0; j < 2; j++)
                    {
                        int x = right - j;
                        bool upward = ((right + 1) & 2) == 0;
                        int y = upward ? _size - 1 - vert : vert;
                        if (!_function[y, x] && i < _codewords.Length * 8)
                        {
                            _modules[y, x] = Bit(_codewords[i >> 3], 7 - (i & 7));
                            i++;
                        }
                    }
            }
        }

        private void ApplyMask(int mask)
        {
            for (int y = 0; y < _size; y++)
                for (int x = 0; x < _size; x++)
                {
                    bool invert = mask switch
                    {
                        0 => (x + y) % 2 == 0,
                        1 => y % 2 == 0,
                        2 => x % 3 == 0,
                        3 => (x + y) % 3 == 0,
                        4 => (x / 3 + y / 2) % 2 == 0,
                        5 => x * y % 2 + x * y % 3 == 0,
                        6 => (x * y % 2 + x * y % 3) % 2 == 0,
                        _ => ((x + y) % 2 + x * y % 3) % 2 == 0,
                    };
                    if (!_function[y, x] && invert) _modules[y, x] = !_modules[y, x];
                }
        }

        // The penalty rules of the standard: long runs, 2 x 2 blocks, finder-like patterns and an unbalanced mix of dark and light.
        private int Penalty()
        {
            int result = 0;
            for (int pass = 0; pass < 2; pass++)
                for (int a = 0; a < _size; a++)
                {
                    bool Get(int b) => pass == 0 ? _modules[a, b] : _modules[b, a];
                    int run = 1;
                    for (int b = 1; b < _size; b++)
                    {
                        if (Get(b) == Get(b - 1)) { run++; if (run == 5) result += 3; else if (run > 5) result++; }
                        else run = 1;
                    }
                    for (int b = 0; b + 10 < _size; b++)
                    {
                        if (Get(b) && !Get(b + 1) && Get(b + 2) && Get(b + 3) && Get(b + 4) && !Get(b + 5) && Get(b + 6) && !Get(b + 7) && !Get(b + 8) && !Get(b + 9) && !Get(b + 10)) result += 40;
                        if (!Get(b) && !Get(b + 1) && !Get(b + 2) && !Get(b + 3) && Get(b + 4) && !Get(b + 5) && Get(b + 6) && Get(b + 7) && Get(b + 8) && !Get(b + 9) && Get(b + 10)) result += 40;
                    }
                }
            for (int y = 0; y + 1 < _size; y++)
                for (int x = 0; x + 1 < _size; x++)
                {
                    bool c = _modules[y, x];
                    if (c == _modules[y, x + 1] && c == _modules[y + 1, x] && c == _modules[y + 1, x + 1]) result += 3;
                }
            int dark = 0;
            foreach (bool m in _modules) if (m) dark++;
            int total = _size * _size;
            result += (int)Math.Ceiling(Math.Abs(dark * 20 - total * 10) / (double)total) - 1 >= 0 ? ((int)Math.Ceiling(Math.Abs(dark * 20 - total * 10) / (double)total) - 1) * 10 : 0;
            return result;
        }
    }
}
