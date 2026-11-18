using FlashEditor.Core.Imaging;

namespace FlashEditor.Core.IO;

/// <summary>
/// Writes a single-picture GIF. GIF holds at most 256 colors, so the picture's colors are reduced to the best 256
/// (median cut), with error-diffusion dithering when that is needed to keep gradients smooth. A picture with 256 colors
/// or fewer is stored exactly. Transparency is all or nothing: pixels that are at least half transparent become clear.
/// </summary>
public static class GifEncoder
{
    public static byte[] Encode(PixelBuffer image, bool dither = true)
    {
        var (palette, indices) = Quantize(image, dither);

        // The color table has 2, 4, ... 256 entries.
        int tableBits = 1;
        while ((1 << tableBits) < palette.Count) tableBits++;
        int tableSize = 1 << tableBits;
        bool transparent = HasClearPixels(image);

        using var output = new MemoryStream();
        void Byte(int value) => output.WriteByte((byte)value);
        void Word(int value) { Byte(value & 0xFF); Byte(value >> 8); }

        // Header and "logical screen": size, and a global color table of tableSize entries.
        foreach (char c in "GIF89a") Byte(c);
        Word(image.Width);
        Word(image.Height);
        Byte(0x80 | 0x70 | (tableBits - 1)); // has color table, 8 bits of color resolution, table size
        Byte(0);                             // background color index
        Byte(0);                             // pixel aspect ratio

        for (int i = 0; i < tableSize; i++)
        {
            var c = i < palette.Count ? palette[i] : (R: (byte)0, G: (byte)0, B: (byte)0);
            Byte(c.R); Byte(c.G); Byte(c.B);
        }

        if (transparent)
        {
            // Graphic control extension: color number 0 is clear.
            Byte(0x21); Byte(0xF9); Byte(4);
            Byte(0x01);                      // transparent color flag
            Word(0);                         // delay (unused for one picture)
            Byte(0);                         // the clear color is index 0
            Byte(0);
        }

        // The picture itself.
        Byte(0x2C);
        Word(0); Word(0); Word(image.Width); Word(image.Height);
        Byte(0);                             // no local table, not interlaced

        int minCodeSize = Math.Max(2, tableBits);
        Byte(minCodeSize);
        WriteSubBlocks(output, Lzw.Compress(indices, minCodeSize));
        Byte(0x3B);                          // end of file
        return output.ToArray();
    }

    /// <summary>
    /// An animated GIF: every picture is one frame (all frames take the size of the first one; others are scaled to it).
    /// Each frame has its own best 256 colors.
    /// </summary>
    /// <param name="delayMs">How long each frame is shown, in milliseconds (the GIF format counts in hundredths of a second).</param>
    /// <param name="loop">True: repeats forever. False: plays once.</param>
    public static byte[] EncodeAnimation(IReadOnlyList<PixelBuffer> frames, int delayMs = 1000, bool loop = true, bool dither = true)
    {
        if (frames.Count == 0) throw new ArgumentException("An animation needs at least one frame.", nameof(frames));
        int width = frames[0].Width, height = frames[0].Height;
        int delay = Math.Clamp((int)Math.Round(delayMs / 10.0), 1, 65535);

        using var output = new MemoryStream();
        void Byte(int value) => output.WriteByte((byte)value);
        void Word(int value) { Byte(value & 0xFF); Byte(value >> 8); }

        foreach (char c in "GIF89a") Byte(c);
        Word(width);
        Word(height);
        Byte(0x70);                          // no global color table, 8 bits of color resolution
        Byte(0);
        Byte(0);
        if (loop)
        {
            Byte(0x21); Byte(0xFF); Byte(11);
            foreach (char c in "NETSCAPE2.0") Byte(c);
            Byte(3); Byte(1); Word(0);       // repeat forever
            Byte(0);
        }

        foreach (var original in frames)
        {
            var frame = original.Width == width && original.Height == height
                ? original
                : Transforms.Resampler.Resize(original, width, height, Transforms.ResampleMode.Smooth);
            var (palette, indices) = Quantize(frame, dither);
            int tableBits = 1;
            while ((1 << tableBits) < palette.Count) tableBits++;
            int tableSize = 1 << tableBits;
            bool transparent = HasClearPixels(frame);

            Byte(0x21); Byte(0xF9); Byte(4);
            Byte((2 << 2) | (transparent ? 1 : 0));   // frame is cleared before the next one is drawn; transparent flag
            Word(delay);
            Byte(0);
            Byte(0);

            Byte(0x2C);
            Word(0); Word(0); Word(width); Word(height);
            Byte(0x80 | (tableBits - 1));            // a color table of its own for this frame
            for (int i = 0; i < tableSize; i++)
            {
                var c = i < palette.Count ? palette[i] : (R: (byte)0, G: (byte)0, B: (byte)0);
                Byte(c.R); Byte(c.G); Byte(c.B);
            }
            int minCodeSize = Math.Max(2, tableBits);
            Byte(minCodeSize);
            WriteSubBlocks(output, Lzw.Compress(indices, minCodeSize));
        }
        Byte(0x3B);
        return output.ToArray();
    }

    private static bool HasClearPixels(PixelBuffer image)
    {
        for (int i = 3; i < image.Data.Length; i += 4)
            if (image.Data[i] < 128) return true;
        return false;
    }

    private static void WriteSubBlocks(Stream output, byte[] data)
    {
        for (int pos = 0; pos < data.Length; pos += 255)
        {
            int count = Math.Min(255, data.Length - pos);
            output.WriteByte((byte)count);
            output.Write(data, pos, count);
        }
        output.WriteByte(0);
    }

    // ------------------------------------------------------------------ colors

    private static (List<(byte R, byte G, byte B)> Palette, byte[] Indices) Quantize(PixelBuffer image, bool dither)
    {
        int w = image.Width, h = image.Height;
        var data = image.Data;
        bool clear = HasClearPixels(image);
        int maxColors = clear ? 255 : 256;      // with transparency, entry 0 is reserved for "clear"
        int first = clear ? 1 : 0;

        // One pass over the picture: note whether it has few enough colors to be stored exactly, and build a histogram
        // (6 bits per channel) for the color reduction. A fixed-size histogram keeps this fast for photos with millions of colors.
        var distinct = new HashSet<int>();
        bool exact = true;
        var histogram = new Histogram();
        for (int i = 0; i < data.Length; i += 4)
        {
            if (data[i + 3] < 128) continue;
            int key = data[i + 2] << 16 | data[i + 1] << 8 | data[i];
            if (exact)
            {
                distinct.Add(key);
                if (distinct.Count > maxColors) { exact = false; distinct.Clear(); }
            }
            histogram.Add(data[i + 2], data[i + 1], data[i]);
        }

        var palette = new List<(byte R, byte G, byte B)>();
        if (clear) palette.Add((0, 0, 0));

        var indices = new byte[w * h];
        if (exact)
        {
            var lookup = new Dictionary<int, byte>();
            foreach (int key in distinct)
            {
                lookup[key] = (byte)palette.Count;
                palette.Add(((byte)(key >> 16), (byte)(key >> 8), (byte)key));
            }
            for (int p = 0, i = 0; p < indices.Length; p++, i += 4)
                indices[p] = data[i + 3] < 128 ? (byte)0 : lookup[data[i + 2] << 16 | data[i + 1] << 8 | data[i]];
            if (palette.Count == 0) palette.Add((0, 0, 0));
            return (palette, indices);
        }

        palette.AddRange(MedianCut(histogram.Entries(), maxColors));
        Map(image, palette, first, indices, dither);
        return (palette, indices);
    }

    /// <summary>Median cut: repeatedly split the group of colors with the widest spread at its middle, then average each group.</summary>
    private static List<(byte R, byte G, byte B)> MedianCut(List<(int R, int G, int B, int N)> all, int target)
    {
        var boxes = new List<List<(int R, int G, int B, int N)>> { all };

        while (boxes.Count < target)
        {
            // Split the box that spreads over the largest range and still has more than one color.
            int best = -1, bestRange = 0;
            for (int i = 0; i < boxes.Count; i++)
            {
                if (boxes[i].Count < 2) continue;
                int range = Math.Max(Range(boxes[i], c => c.R), Math.Max(Range(boxes[i], c => c.G), Range(boxes[i], c => c.B)));
                if (range > bestRange) { bestRange = range; best = i; }
            }
            if (best < 0) break;

            var box = boxes[best];
            int rr = Range(box, c => c.R), rg = Range(box, c => c.G), rb = Range(box, c => c.B);
            Func<(int R, int G, int B, int N), int> channel = rr >= rg && rr >= rb ? c => c.R : rg >= rb ? c => c.G : c => c.B;
            box.Sort((a, b) => channel(a).CompareTo(channel(b)));

            // The split is where half of the pixels lie on each side.
            long total = box.Sum(c => (long)c.N), running = 0;
            int cut = 0;
            for (; cut < box.Count - 1; cut++)
            {
                running += box[cut].N;
                if (running * 2 >= total) break;
            }
            cut = Math.Clamp(cut + 1, 1, box.Count - 1);
            boxes[best] = box.GetRange(0, cut);
            boxes.Add(box.GetRange(cut, box.Count - cut));
        }

        return boxes.Select(box =>
        {
            long n = box.Sum(c => (long)c.N);
            return ((byte)(box.Sum(c => (long)c.R * c.N) / n), (byte)(box.Sum(c => (long)c.G * c.N) / n), (byte)(box.Sum(c => (long)c.B * c.N) / n));
        }).ToList();
    }

    /// <summary>Counts pixels per color bucket (6 bits per channel) and remembers the average real color of each bucket.</summary>
    private sealed class Histogram
    {
        private readonly int[] _count = new int[1 << 18];
        private readonly long[] _r = new long[1 << 18], _g = new long[1 << 18], _b = new long[1 << 18];

        public void Add(int r, int g, int b)
        {
            int key = (r >> 2) << 12 | (g >> 2) << 6 | (b >> 2);
            _count[key]++; _r[key] += r; _g[key] += g; _b[key] += b;
        }

        public List<(int R, int G, int B, int N)> Entries()
        {
            var list = new List<(int, int, int, int)>();
            for (int k = 0; k < _count.Length; k++)
                if (_count[k] > 0) list.Add(((int)(_r[k] / _count[k]), (int)(_g[k] / _count[k]), (int)(_b[k] / _count[k]), _count[k]));
            return list;
        }
    }

    private static int Range(List<(int R, int G, int B, int N)> box, Func<(int R, int G, int B, int N), int> channel)
        => box.Max(channel) - box.Min(channel);

    /// <summary>Gives every pixel the number of its nearest palette color, spreading the rounding error to its neighbors when dithering.</summary>
    private static void Map(PixelBuffer image, List<(byte R, byte G, byte B)> palette, int first, byte[] indices, bool dither)
    {
        int w = image.Width, h = image.Height;
        var data = image.Data;
        var cache = new int[1 << 15];                 // nearest color for each 5-bit-per-channel color
        Array.Fill(cache, -1);

        int Nearest(int r, int g, int b)
        {
            int key = (r >> 3) << 10 | (g >> 3) << 5 | (b >> 3);
            if (cache[key] >= 0) return cache[key];
            int best = first, bestDistance = int.MaxValue;
            for (int i = first; i < palette.Count; i++)
            {
                int dr = r - palette[i].R, dg = g - palette[i].G, db = b - palette[i].B;
                int d = dr * dr * 3 + dg * dg * 4 + db * db * 2;   // the eye is most sensitive to green
                if (d < bestDistance) { bestDistance = d; best = i; }
            }
            return cache[key] = best;
        }

        // Error carried to the next pixel / the next row, per color channel.
        var errors = new float[2][];
        errors[0] = new float[(w + 2) * 3];
        errors[1] = new float[(w + 2) * 3];

        for (int y = 0; y < h; y++)
        {
            var current = errors[y & 1];
            var next = errors[(y + 1) & 1];
            Array.Clear(next);

            for (int x = 0; x < w; x++)
            {
                int i = (y * w + x) * 4;
                if (data[i + 3] < 128) { indices[y * w + x] = 0; continue; }

                int e = (x + 1) * 3;
                int r = Math.Clamp((int)Math.Round(data[i + 2] + (dither ? current[e] : 0)), 0, 255);
                int g = Math.Clamp((int)Math.Round(data[i + 1] + (dither ? current[e + 1] : 0)), 0, 255);
                int b = Math.Clamp((int)Math.Round(data[i] + (dither ? current[e + 2] : 0)), 0, 255);

                int chosen = Nearest(r, g, b);
                indices[y * w + x] = (byte)chosen;
                if (!dither) continue;

                // Floyd-Steinberg: 7/16 to the right, 3/16 down-left, 5/16 down, 1/16 down-right.
                float er = r - palette[chosen].R, eg = g - palette[chosen].G, eb = b - palette[chosen].B;
                void Spread(float[] row, int at, float share) { row[at] += er * share; row[at + 1] += eg * share; row[at + 2] += eb * share; }
                Spread(current, e + 3, 7 / 16f);
                Spread(next, e - 3, 3 / 16f);
                Spread(next, e, 5 / 16f);
                Spread(next, e + 3, 1 / 16f);
            }
        }
    }

    // ------------------------------------------------------------------ LZW compression (the GIF flavor)

    private static class Lzw
    {
        private const int MaxBits = 12, TableSize = 1 << MaxBits, HashSize = 1 << 13;

        public static byte[] Compress(byte[] indices, int minCodeSize)
        {
            var writer = new BitWriter();
            int clearCode = 1 << minCodeSize, endCode = clearCode + 1;
            int initialBits = minCodeSize + 1;

            // Dictionary: (prefix code, next pixel) -> code, in a small hash table that is emptied by changing a stamp.
            var keys = new int[HashSize];
            var codes = new int[HashSize];
            var stamps = new int[HashSize];
            int stamp = 1;

            int nextCode = endCode + 1, bits = initialBits;

            int Find(int key)
            {
                uint slot = ((uint)key * 2654435761u) >> (32 - 13);
                while (stamps[slot] == stamp)
                {
                    if (keys[slot] == key) return codes[slot];
                    slot = (slot + 1) & (HashSize - 1);
                }
                return -1;
            }
            void Add(int key, int code)
            {
                uint slot = ((uint)key * 2654435761u) >> (32 - 13);
                while (stamps[slot] == stamp) slot = (slot + 1) & (HashSize - 1);
                stamps[slot] = stamp; keys[slot] = key; codes[slot] = code;
            }

            writer.Write(clearCode, bits);
            int prefix = indices[0];
            for (int i = 1; i < indices.Length; i++)
            {
                int pixel = indices[i];
                int key = prefix << 8 | pixel;
                int found = Find(key);
                if (found >= 0) { prefix = found; continue; }

                writer.Write(prefix, bits);
                if (nextCode < TableSize)
                {
                    Add(key, nextCode++);
                    // The code size grows once the table holds more entries than the current size can number.
                    if (nextCode - 1 == (1 << bits) && bits < MaxBits) bits++;
                }
                else
                {
                    // The table is full: tell the reader to start over.
                    writer.Write(clearCode, bits);
                    stamp++;
                    nextCode = endCode + 1;
                    bits = initialBits;
                }
                prefix = pixel;
            }
            writer.Write(prefix, bits);
            // The reader has added one more entry than the writer by now: it may need a wider code for the end marker.
            if (nextCode < TableSize && nextCode == (1 << bits) && bits < MaxBits) bits++;
            writer.Write(endCode, bits);
            return writer.Finish();
        }
    }

    private sealed class BitWriter
    {
        private readonly List<byte> _bytes = new();
        private int _accumulator, _count;

        public void Write(int code, int bits)
        {
            _accumulator |= code << _count;
            _count += bits;
            while (_count >= 8)
            {
                _bytes.Add((byte)_accumulator);
                _accumulator >>= 8;
                _count -= 8;
            }
        }

        public byte[] Finish()
        {
            if (_count > 0) _bytes.Add((byte)_accumulator);
            return _bytes.ToArray();
        }
    }
}
