using System.Text;
using FlashEditor.Core.Documents;
using FlashEditor.Core.Imaging;
using FlashEditor.Core.IO.Psd;

namespace FlashEditor.Core.Tests.IO;

public class PsdFileTests
{
    private static Layer Solid(string name, int w, int h, ColorRgba color)
    {
        var p = new PixelBuffer(w, h);
        p.Fill(color);
        return new Layer(name, p);
    }

    private static byte[] Save(Document doc)
    {
        using var stream = new MemoryStream();
        PsdFile.Write(doc, stream);
        return stream.ToArray();
    }

    private static Document Open(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        return PsdFile.Read(stream);
    }

    [Fact]
    public void Layers_come_back_with_their_names_pictures_and_settings()
    {
        var back = Solid("Background", 6, 4, new ColorRgba(255, 255, 255));
        var shade = Solid("Shadow layer", 6, 4, new ColorRgba(10, 20, 30, 200));
        shade.Opacity = 0.5; shade.IsVisible = false; shade.BlendMode = BlendMode.Multiply;
        var doc = new Document(6, 4);
        doc.SetLayers(new[] { back, shade }, shade);

        var loaded = Open(Save(doc));
        Assert.Equal((6, 4), (loaded.Width, loaded.Height));
        Assert.Equal(2, loaded.Layers.Count);
        Assert.Equal("Background", loaded.Layers[0].Name);
        Assert.Equal(new ColorRgba(255, 255, 255), loaded.Layers[0].Pixels.GetPixel(3, 2));
        var top = loaded.Layers[1];
        Assert.Equal("Shadow layer", top.Name);
        Assert.False(top.IsVisible);
        Assert.Equal(BlendMode.Multiply, top.BlendMode);
        Assert.InRange(top.Opacity, 0.49, 0.51);
        Assert.Equal(new ColorRgba(10, 20, 30, 200), top.Pixels.GetPixel(1, 1));
    }

    [Fact]
    public void Every_blend_mode_survives_saving_and_opening()
    {
        foreach (var mode in BlendModes.All)
        {
            var layer = Solid("L", 2, 2, new ColorRgba(1, 2, 3));
            layer.BlendMode = mode;
            var doc = new Document(2, 2);
            doc.SetLayers(new[] { layer }, layer);
            Assert.Equal(mode, Open(Save(doc)).Layers[0].BlendMode);
        }
    }

    [Fact]
    public void A_layer_mask_is_applied_to_the_saved_pixels()
    {
        var layer = Solid("Masked", 4, 4, new ColorRgba(200, 0, 0));
        var mask = new LayerMask(4, 4);
        for (int y = 0; y < 4; y++) for (int x = 0; x < 2; x++) mask.Alpha[y * 4 + x] = 0;
        layer.SetMask(mask);
        var doc = new Document(4, 4);
        doc.SetLayers(new[] { layer }, layer);
        var loaded = Open(Save(doc)).Layers[0];
        Assert.Equal(0, loaded.Pixels.GetPixel(0, 0).A);
        Assert.Equal(255, loaded.Pixels.GetPixel(3, 0).A);
    }

    [Fact]
    public void Adjustment_layers_have_no_picture_and_are_left_out()
    {
        var picture = Solid("Photo", 3, 3, new ColorRgba(5, 5, 5));
        var adjustment = new Layer("Curves", 3, 3) { Adjustment = AdjustmentStyle.NewCurves };
        var doc = new Document(3, 3);
        doc.SetLayers(new[] { picture, adjustment }, picture);
        Assert.Single(Open(Save(doc)).Layers);
    }

    [Fact]
    public void Non_ascii_names_do_not_break_the_file()
    {
        var layer = Solid("Ñame ✓", 2, 2, new ColorRgba(1, 2, 3));
        var doc = new Document(2, 2);
        doc.SetLayers(new[] { layer }, layer);
        var loaded = Open(Save(doc));
        Assert.Single(loaded.Layers);
        Assert.Equal("?ame ?", loaded.Layers[0].Name);
    }

    [Fact]
    public void A_file_with_a_layer_of_its_own_size_and_place_lands_at_the_right_place()
    {
        // Build a small file by hand: canvas 6 x 4, one 2 x 2 layer at (3, 1), RLE compressed channels.
        byte[] file = HandMadePsd(rle: true, out _);
        var doc = Open(file);
        Assert.Equal((6, 4), (doc.Width, doc.Height));
        var layer = doc.Layers[0];
        Assert.Equal("Hand", layer.Name);
        Assert.Equal(new ColorRgba(255, 0, 0), layer.Pixels.GetPixel(3, 1));
        Assert.Equal(new ColorRgba(255, 0, 0), layer.Pixels.GetPixel(4, 2));
        Assert.Equal(0, layer.Pixels.GetPixel(0, 0).A);
        Assert.Equal(0, layer.Pixels.GetPixel(5, 3).A);
    }

    [Fact]
    public void Raw_and_run_length_files_give_the_same_picture()
    {
        var raw = Open(HandMadePsd(rle: false, out _));
        var packed = Open(HandMadePsd(rle: true, out _));
        Assert.True(raw.Layers[0].Pixels.ContentEquals(packed.Layers[0].Pixels));
    }

    [Fact]
    public void A_file_that_is_not_a_photoshop_file_is_refused()
        => Assert.Throws<InvalidDataException>(() => Open(Encoding.ASCII.GetBytes("this is just text, not a picture at all")));

    [Fact]
    public void A_cut_off_file_is_refused_with_a_clear_error()
    {
        var bytes = Save(new Document(8, 8) is var d && Fill(d) ? d : d);
        Assert.ThrowsAny<Exception>(() => Open(bytes[..(bytes.Length / 2)]));
    }

    private static bool Fill(Document d)
    {
        var layer = Solid("L", 8, 8, new ColorRgba(9, 9, 9));
        d.SetLayers(new[] { layer }, layer);
        return true;
    }

    // ---- a hand made file (so the reader is tested against bytes that did not come from the writer)
    private static byte[] HandMadePsd(bool rle, out int layerCount)
    {
        layerCount = 1;
        var m = new MemoryStream();
        void U16(int v) { m.WriteByte((byte)(v >> 8)); m.WriteByte((byte)v); }
        void U32(long v) { m.WriteByte((byte)(v >> 24)); m.WriteByte((byte)(v >> 16)); m.WriteByte((byte)(v >> 8)); m.WriteByte((byte)v); }
        void Ascii(string s) => m.Write(Encoding.ASCII.GetBytes(s));

        Ascii("8BPS"); U16(1); m.Write(new byte[6]); U16(3); U32(4); U32(6); U16(8); U16(3);
        U32(0); U32(0);

        // one layer: 2 x 2 pixels at left 3, top 1; red = 255, green = 0, blue = 0
        var channels = new List<byte[]>();
        byte[] Channel(byte value)
        {
            var c = new MemoryStream();
            if (!rle) { c.WriteByte(0); c.WriteByte(0); c.Write(new byte[] { value, value, value, value }); }
            else
            {
                c.WriteByte(0); c.WriteByte(1);
                c.Write(new byte[] { 0, 2, 0, 2 });                                  // two rows, two bytes each
                c.Write(new byte[] { (byte)(257 - 2), value, (byte)(257 - 2), value }); // "repeat the next byte twice", per row
            }
            return c.ToArray();
        }
        channels.Add(Channel(255)); channels.Add(Channel(0)); channels.Add(Channel(0));        // R, G, B

        var info = new MemoryStream();
        void IU16(int v) { info.WriteByte((byte)(v >> 8)); info.WriteByte((byte)v); }
        void IU32(long v) { info.WriteByte((byte)(v >> 24)); info.WriteByte((byte)(v >> 16)); info.WriteByte((byte)(v >> 8)); info.WriteByte((byte)v); }
        IU16(1);
        IU32(1); IU32(3); IU32(3); IU32(5);                                              // top, left, bottom, right
        IU16(3);
        for (int c = 0; c < 3; c++) { IU16(c); IU32(channels[c].Length); }
        info.Write(Encoding.ASCII.GetBytes("8BIMnorm")); info.WriteByte(255); info.WriteByte(0); info.WriteByte(0); info.WriteByte(0);
        var extra = new MemoryStream();
        extra.Write(new byte[8]);                                                         // mask data (none), blending ranges (none)
        extra.WriteByte(4); extra.Write(Encoding.ASCII.GetBytes("Hand")); extra.Write(new byte[3]);        // Pascal name "Hand" padded to a multiple of four
        IU32(extra.Length); info.Write(extra.ToArray());
        foreach (var c in channels) info.Write(c);
        if (info.Length % 2 != 0) info.WriteByte(0);

        U32(info.Length + 4 + 4);                                                        // layer section: info length field + info + global mask length
        U32(info.Length); m.Write(info.ToArray());
        U32(0);
        // merged picture (raw, 3 channels of 24 pixels)
        U16(0); m.Write(new byte[3 * 24]);
        return m.ToArray();
    }
}
