using System.Text;
using FlashEditor.Core.Documents;
using FlashEditor.Core.Imaging;
using FlashEditor.Core.Painting;

namespace FlashEditor.Core.IO.Psd;

/// <summary>
/// Photoshop files (.psd) with 8-bit RGB color: opening keeps the layers (name, picture, position, opacity, visibility, blend mode and,
/// as see-through areas, the layer masks); saving writes every layer plus the flattened picture, so Photoshop and other editors can open it.
/// Layer effects, text layers, smart objects and adjustment layers of Photoshop are not understood: they stay as the pictures Photoshop
/// saved with them. Files in other color modes (CMYK, 16 bit...) are opened as their flattened picture when possible.
/// </summary>
public static class PsdFile
{
    public const string Extension = ".psd";

    /// <summary>True for a path that ends in .psd.</summary>
    public static bool IsPsd(string path) => path.EndsWith(Extension, StringComparison.OrdinalIgnoreCase);

    // ---- blend modes: the four-letter keys Photoshop uses
    private static readonly (string Key, BlendMode Mode)[] BlendKeys =
    {
        ("norm", BlendMode.Normal), ("mul ", BlendMode.Multiply), ("scrn", BlendMode.Screen), ("over", BlendMode.Overlay),
        ("sLit", BlendMode.SoftLight), ("hLit", BlendMode.HardLight), ("dark", BlendMode.Darken), ("lite", BlendMode.Lighten), ("diff", BlendMode.Difference),
    };

    private static BlendMode ModeOf(string key) => BlendKeys.FirstOrDefault(k => k.Key == key).Mode;
    private static string KeyOf(BlendMode mode) => BlendKeys.First(k => k.Mode == mode).Key;

    // =============================================================== reading

    /// <summary>Opens a .psd file as a document with its layers.</summary>
    public static Document Open(string path)
    {
        using var stream = File.OpenRead(path);
        return Read(stream);
    }

    public static Document Read(Stream stream)
    {
        var r = new BigEndianReader(stream);
        if (r.ReadAscii(4) != "8BPS") throw new InvalidDataException("This is not a Photoshop file.");
        int version = r.ReadUInt16();
        if (version != 1) throw new InvalidDataException("Large Photoshop files (.psb) are not supported.");
        r.Skip(6);
        int channels = r.ReadUInt16();
        int height = (int)r.ReadUInt32(), width = (int)r.ReadUInt32();
        int depth = r.ReadUInt16();
        int mode = r.ReadUInt16();
        ImageDecoder.CheckSize(width, height);

        long colorModeLength = r.ReadUInt32(); r.Skip(colorModeLength);
        long resourcesLength = r.ReadUInt32(); r.Skip(resourcesLength);

        bool rgb8 = depth == 8 && mode == 3;
        long layerSectionLength = r.ReadUInt32();
        long layerSectionEnd = r.Position + layerSectionLength;
        var layers = new List<PsdLayer>();
        if (layerSectionLength > 0 && rgb8) layers = ReadLayers(r, layerSectionEnd);
        r.Seek(layerSectionEnd);

        var document = new Document(width, height);
        if (layers.Count > 0)
        {
            var built = layers.Select(l => BuildLayer(l, width, height)).Where(l => l != null).Select(l => l!).ToList();
            if (built.Count > 0)
            {
                document.SetLayers(built, built[^1]);
                return document;
            }
        }

        // No usable layers: use the flattened picture at the end of the file.
        if (!(depth == 8 && (mode == 3 || mode == 1)))
            throw new InvalidDataException("This Photoshop file uses a color mode that is not supported (only 8-bit RGB and grey).");
        var merged = ReadMerged(r, width, height, channels, mode);
        document.SetLayers(new[] { new Layer("Background", merged) }, null);
        return document;
    }

    private sealed class PsdLayer
    {
        public int Top, Left, Bottom, Right;
        public string Name = "Layer";
        public byte Opacity = 255;
        public bool Visible = true;
        public string BlendKey = "norm";
        public List<(int Id, byte[] Data)> Channels = new();
        public int Width => Math.Max(0, Right - Left);
        public int Height => Math.Max(0, Bottom - Top);
        public int MaskTop, MaskLeft, MaskBottom, MaskRight;
    }

    private static List<PsdLayer> ReadLayers(BigEndianReader r, long sectionEnd)
    {
        long layerInfoLength = r.ReadUInt32();
        if (layerInfoLength == 0) return new();
        long infoEnd = r.Position + layerInfoLength;
        int count = Math.Abs(r.ReadInt16());
        var layers = new List<PsdLayer>();
        var channelLengths = new List<List<(int Id, long Length)>>();
        for (int i = 0; i < count; i++)
        {
            var layer = new PsdLayer { Top = r.ReadInt32(), Left = r.ReadInt32(), Bottom = r.ReadInt32(), Right = r.ReadInt32() };
            int channelCount = r.ReadUInt16();
            var lengths = new List<(int, long)>();
            for (int c = 0; c < channelCount; c++) lengths.Add((r.ReadInt16(), r.ReadUInt32()));
            if (r.ReadAscii(4) != "8BIM") throw new InvalidDataException("A layer in the Photoshop file is damaged.");
            layer.BlendKey = r.ReadAscii(4);
            layer.Opacity = r.ReadByte();
            r.ReadByte();                                         // clipping
            byte flags = r.ReadByte();
            layer.Visible = (flags & 2) == 0;
            r.ReadByte();                                         // filler
            long extraLength = r.ReadUInt32();
            long extraEnd = r.Position + extraLength;
            long maskLength = r.ReadUInt32();
            if (maskLength >= 16)
            {
                long maskStart = r.Position;
                layer.MaskTop = r.ReadInt32(); layer.MaskLeft = r.ReadInt32(); layer.MaskBottom = r.ReadInt32(); layer.MaskRight = r.ReadInt32();
                r.Seek(maskStart + maskLength);
            }
            else r.Skip(maskLength);
            long rangesLength = r.ReadUInt32(); r.Skip(rangesLength);
            int nameLength = r.ReadByte();
            layer.Name = Encoding.Latin1.GetString(r.ReadBytes(nameLength));
            r.Seek(extraEnd);                                     // (the name and extra blocks: only the plain name is used)
            layers.Add(layer);
            channelLengths.Add(lengths);
        }

        for (int i = 0; i < layers.Count; i++)
        {
            var layer = layers[i];
            foreach (var (id, length) in channelLengths[i])
            {
                long end = r.Position + length;
                int w = id == -2 ? Math.Max(0, layer.MaskRight - layer.MaskLeft) : layer.Width;
                int h = id == -2 ? Math.Max(0, layer.MaskBottom - layer.MaskTop) : layer.Height;
                var data = length >= 2 ? ReadChannel(r, w, h, end) : Array.Empty<byte>();
                layer.Channels.Add((id, data));
                r.Seek(end);
            }
        }
        r.Seek(infoEnd);
        return layers;
    }

    private static byte[] ReadChannel(BigEndianReader r, int width, int height, long end)
    {
        int compression = r.ReadUInt16();
        var data = new byte[width * height];
        if (width == 0 || height == 0) return data;
        if (compression == 0)
        {
            var raw = r.ReadBytes(data.Length);
            Array.Copy(raw, data, Math.Min(raw.Length, data.Length));
        }
        else if (compression == 1)
        {
            var counts = new int[height];
            for (int y = 0; y < height; y++) counts[y] = r.ReadUInt16();
            for (int y = 0; y < height; y++) UnpackBits(r.ReadBytes(counts[y]), data, y * width, width);
        }
        else throw new InvalidDataException("This Photoshop file uses a compression that is not supported.");
        return data;
    }

    private static void UnpackBits(byte[] source, byte[] target, int offset, int length)
    {
        int s = 0, t = 0;
        while (s < source.Length && t < length)
        {
            sbyte n = (sbyte)source[s++];
            if (n >= 0)
            {
                int run = n + 1;
                for (int i = 0; i < run && s < source.Length && t < length; i++) target[offset + t++] = source[s++];
            }
            else if (n != -128)
            {
                int run = 1 - n;
                if (s >= source.Length) break;
                byte value = source[s++];
                for (int i = 0; i < run && t < length; i++) target[offset + t++] = value;
            }
        }
    }

    private static Layer? BuildLayer(PsdLayer p, int canvasWidth, int canvasHeight)
    {
        if (p.Width == 0 || p.Height == 0)
        {
            // Empty layers are group markers in Photoshop ("Group 1" / "</Layer group>"): they are not pictures, so they are left out.
            if (p.Name.StartsWith("</", StringComparison.Ordinal) || p.Channels.Count == 0 || p.Name.Contains("Group", StringComparison.OrdinalIgnoreCase)) return null;
        }

        byte[]? Find(int id) => p.Channels.FirstOrDefault(c => c.Id == id).Data;
        var red = Find(0); var green = Find(1); var blue = Find(2); var alpha = Find(-1); var mask = Find(-2);
        var content = new PixelBuffer(Math.Max(1, p.Width), Math.Max(1, p.Height));
        var d = content.Data;
        for (int i = 0; i < p.Width * p.Height; i++)
        {
            d[i * 4] = blue?[i] ?? 0;
            d[i * 4 + 1] = green?[i] ?? 0;
            d[i * 4 + 2] = red?[i] ?? 0;
            d[i * 4 + 3] = p.Channels.Any(c => c.Id == -1) ? alpha![i] : (byte)255;
        }

        // A layer mask hides parts of the layer: here it is applied to the see-through-ness of the layer's pixels.
        if (mask != null && mask.Length > 0)
        {
            int mw = p.MaskRight - p.MaskLeft, mh = p.MaskBottom - p.MaskTop;
            for (int y = 0; y < p.Height; y++)
                for (int x = 0; x < p.Width; x++)
                {
                    int mx = x + p.Left - p.MaskLeft, my = y + p.Top - p.MaskTop;
                    byte m = mx >= 0 && my >= 0 && mx < mw && my < mh ? mask[my * mw + mx] : (byte)255;
                    if (m < 255) d[(y * p.Width + x) * 4 + 3] = (byte)(d[(y * p.Width + x) * 4 + 3] * m / 255);
                }
        }

        var layer = new Layer(p.Name, canvasWidth, canvasHeight)
        {
            Opacity = p.Opacity / 255.0,
            IsVisible = p.Visible,
            BlendMode = ModeOf(p.BlendKey),
        };
        layer.RestoreState(LayerState.Place(content, p.Left, p.Top, canvasWidth, canvasHeight));
        return layer;
    }

    private static PixelBuffer ReadMerged(BigEndianReader r, int width, int height, int channels, int mode)
    {
        int compression = r.ReadUInt16();
        int planes = Math.Min(channels, mode == 3 ? 4 : 2);
        var plane = new byte[planes][];
        for (int c = 0; c < planes; c++) plane[c] = new byte[width * height];
        if (compression == 0)
        {
            for (int c = 0; c < planes; c++) Array.Copy(r.ReadBytes(width * height), plane[c], width * height);
        }
        else if (compression == 1)
        {
            // Row lengths of every row of every channel come first, then the packed rows.
            var counts = new int[channels * height];
            for (int i = 0; i < counts.Length; i++) counts[i] = r.ReadUInt16();
            for (int c = 0; c < channels; c++)
                for (int y = 0; y < height; y++)
                {
                    var packed = r.ReadBytes(counts[c * height + y]);
                    if (c < planes) UnpackBits(packed, plane[c], y * width, width);
                }
        }
        else throw new InvalidDataException("This Photoshop file uses a compression that is not supported.");

        var result = new PixelBuffer(width, height);
        for (int i = 0; i < width * height; i++)
        {
            if (mode == 3)
            {
                result.Data[i * 4] = plane[Math.Min(2, planes - 1)][i];
                result.Data[i * 4 + 1] = plane[Math.Min(1, planes - 1)][i];
                result.Data[i * 4 + 2] = plane[0][i];
                result.Data[i * 4 + 3] = planes >= 4 ? plane[3][i] : (byte)255;
            }
            else
            {
                byte g = plane[0][i];
                result.Data[i * 4] = g; result.Data[i * 4 + 1] = g; result.Data[i * 4 + 2] = g;
                result.Data[i * 4 + 3] = planes >= 2 ? plane[1][i] : (byte)255;
            }
        }
        return result;
    }

    // =============================================================== writing

    /// <summary>Saves the document as a .psd file with all layers (adjustment layers have no picture and are left out; masks are applied).</summary>
    public static void Save(Document document, string path)
    {
        using var stream = File.Create(path);
        Write(document, stream);
    }

    public static void Write(Document document, Stream stream)
    {
        var w = new BigEndianWriter(stream);
        int width = document.Width, height = document.Height;
        w.WriteAscii("8BPS"); w.WriteUInt16(1); w.WriteBytes(new byte[6]);
        w.WriteUInt16(4); w.WriteUInt32((uint)height); w.WriteUInt32((uint)width); w.WriteUInt16(8); w.WriteUInt16(3);
        w.WriteUInt32(0);                                                   // color mode data
        w.WriteUInt32(0);                                                   // image resources

        // Layer and mask information
        var layers = document.Layers.Where(l => !l.IsAdjustment).ToList();
        var info = new MemoryStream();
        var iw = new BigEndianWriter(info);
        iw.WriteInt16((short)-layers.Count);                                // negative: the merged picture has transparency
        var channelData = new List<byte[][]>();
        foreach (var layer in layers)
        {
            var pixels = layer.MaskedPixels();
            var planes = SplitChannels(pixels);
            channelData.Add(planes);
            iw.WriteInt32(0); iw.WriteInt32(0); iw.WriteInt32(height); iw.WriteInt32(width);
            iw.WriteUInt16(4);
            int[] ids = { -1, 0, 1, 2 };
            for (int c = 0; c < 4; c++) { iw.WriteInt16((short)ids[c]); iw.WriteUInt32((uint)(2 + width * height)); }
            iw.WriteAscii("8BIM"); iw.WriteAscii(KeyOf(layer.BlendMode));
            iw.WriteByte((byte)Math.Round(Math.Clamp(layer.Opacity, 0, 1) * 255));
            iw.WriteByte(0);
            iw.WriteByte((byte)(layer.IsVisible ? 0 : 2));
            iw.WriteByte(0);
            var extra = new MemoryStream();
            var ew = new BigEndianWriter(extra);
            ew.WriteUInt32(0);                                              // layer mask data
            ew.WriteUInt32(0);                                              // blending ranges
            string name = new string(layer.Name.Select(ch => ch < 128 ? ch : '?').ToArray());
            if (name.Length > 255) name = name[..255];
            ew.WriteByte((byte)name.Length);
            ew.WriteBytes(Encoding.ASCII.GetBytes(name));
            int padded = ((1 + name.Length) + 3) / 4 * 4;                   // the name takes a multiple of four bytes
            ew.WriteBytes(new byte[padded - (1 + name.Length)]);
            iw.WriteUInt32((uint)extra.Length);
            iw.WriteBytes(extra.ToArray());
        }
        // Channel pixels, in the same order: alpha, red, green, blue (stored raw)
        foreach (var planes in channelData)
            for (int c = 0; c < 4; c++) { iw.WriteUInt16(0); iw.WriteBytes(planes[c]); }
        if (info.Length % 2 != 0) iw.WriteByte(0);

        var section = new MemoryStream();
        var sw = new BigEndianWriter(section);
        sw.WriteUInt32((uint)info.Length);
        sw.WriteBytes(info.ToArray());
        sw.WriteUInt32(0);                                                  // global layer mask info
        w.WriteUInt32((uint)section.Length);
        w.WriteBytes(section.ToArray());

        // The flattened picture (raw, planar: red, green, blue, alpha)
        var flat = Compositor.Flatten(document);
        var flatPlanes = SplitChannels(flat);
        w.WriteUInt16(0);
        for (int c = 1; c < 4; c++) w.WriteBytes(flatPlanes[c]);
        w.WriteBytes(flatPlanes[0]);
    }

    /// <summary>The picture as four planes: alpha, red, green, blue.</summary>
    private static byte[][] SplitChannels(PixelBuffer pixels)
    {
        int n = pixels.Width * pixels.Height;
        var a = new byte[n]; var r = new byte[n]; var g = new byte[n]; var b = new byte[n];
        var d = pixels.Data;
        for (int i = 0; i < n; i++) { b[i] = d[i * 4]; g[i] = d[i * 4 + 1]; r[i] = d[i * 4 + 2]; a[i] = d[i * 4 + 3]; }
        return new[] { a, r, g, b };
    }

    // =============================================================== byte helpers

    private sealed class BigEndianReader
    {
        private readonly Stream _s;
        public BigEndianReader(Stream s) => _s = s;
        public long Position => _s.Position;
        public void Seek(long position) => _s.Position = position;
        public void Skip(long count) => _s.Position += count;

        public byte ReadByte()
        {
            int b = _s.ReadByte();
            if (b < 0) throw new EndOfStreamException();
            return (byte)b;
        }

        public byte[] ReadBytes(int count)
        {
            var buffer = new byte[count];
            int read = 0;
            while (read < count)
            {
                int n = _s.Read(buffer, read, count - read);
                if (n <= 0) throw new EndOfStreamException("The Photoshop file ends too early.");
                read += n;
            }
            return buffer;
        }

        public string ReadAscii(int count) => Encoding.ASCII.GetString(ReadBytes(count));
        public ushort ReadUInt16() { var b = ReadBytes(2); return (ushort)(b[0] << 8 | b[1]); }
        public short ReadInt16() => (short)ReadUInt16();
        public uint ReadUInt32() { var b = ReadBytes(4); return (uint)(b[0] << 24 | b[1] << 16 | b[2] << 8 | b[3]); }
        public int ReadInt32() => (int)ReadUInt32();
    }

    private sealed class BigEndianWriter
    {
        private readonly Stream _s;
        public BigEndianWriter(Stream s) => _s = s;
        public void WriteByte(byte b) => _s.WriteByte(b);
        public void WriteBytes(byte[] b) => _s.Write(b, 0, b.Length);
        public void WriteAscii(string s) => WriteBytes(Encoding.ASCII.GetBytes(s));
        public void WriteUInt16(ushort v) { _s.WriteByte((byte)(v >> 8)); _s.WriteByte((byte)v); }
        public void WriteInt16(short v) => WriteUInt16((ushort)v);
        public void WriteUInt32(uint v) { _s.WriteByte((byte)(v >> 24)); _s.WriteByte((byte)(v >> 16)); _s.WriteByte((byte)(v >> 8)); _s.WriteByte((byte)v); }
        public void WriteInt32(int v) => WriteUInt32((uint)v);
    }
}
