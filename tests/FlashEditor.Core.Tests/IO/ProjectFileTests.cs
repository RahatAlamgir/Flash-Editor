using System.IO.Compression;
using FlashEditor.Core.Documents;
using FlashEditor.Core.Imaging;
using FlashEditor.Core.IO.Project;
using FlashEditor.Core.Objects;
using Xunit;

namespace FlashEditor.Core.Tests.IO;

public class ProjectFileTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "flash-project-tests-" + Guid.NewGuid().ToString("N"));

    public ProjectFileTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        try { Directory.Delete(_folder, true); } catch { /* temp folder, the system cleans it up */ }
    }

    private string PathFor(string name) => Path.Combine(_folder, name);

    private static PixelBuffer Solid(int w, int h, ColorRgba c)
    {
        var p = new PixelBuffer(w, h);
        p.Fill(c);
        return p;
    }

    private static Layer ObjectLayer(string name, LayerObject obj, int w, int h)
    {
        var layer = new Layer(name, w, h);
        layer.RestoreState(new LayerState(Solid(w, h, new ColorRgba(1, 2, 3, 4)), null, 0, 0, obj));
        return layer;
    }

    private static Document Sample()
    {
        var doc = new Document(80, 60);
        var background = new Layer("Background", Solid(80, 60, new ColorRgba(255, 255, 255)));
        var pixels = new Layer("Painted", Solid(80, 60, new ColorRgba(10, 20, 30, 200))) { Opacity = 0.5, IsVisible = false, IsLocked = true };

        var shape = new ShapeObject(new ObjectBox(5, 6, 30, 20, 15), ShapeKind.Star, new ColorRgba(255, 0, 0), new ColorRgba(0, 0, 255), 3, Points: 7, FlipX: true)
        {
            Shadow = new ShadowStyle(new ColorRgba(0, 0, 0, 90), 5, 30, 6, 1.5),
            ThreeD = new ThreeDStyle(12, 60, new ColorRgba(9, 9, 9), 0.4),
            Warp = new[] { new PointD(-0.1, 0), new PointD(1.2, 0.1), new PointD(1, 1), new PointD(0, 1.3) },
        };
        var text = new TextObject(new ObjectBox(1, 2, 40, 12), "Hello\nthere", "Segoe UI", 18, new ColorRgba(0, 128, 0), Bold: true, Italic: true, Align: TextAlign.Center);
        var picture = new ImageObject(new ObjectBox(0, 0, 20, 20), Solid(8, 8, new ColorRgba(200, 100, 50)), "Photo", Original: Solid(8, 8, new ColorRgba(1, 1, 1)), Revision: 3);
        var group = GroupObject.Create(new[] { new GroupMember(shape, "a", 0.5), new GroupMember(text, "b", 1.0) });

        doc.SetLayers(new List<Layer>
        {
            background, pixels,
            ObjectLayer("Star", shape, 80, 60),
            ObjectLayer("Words", text, 80, 60),
            ObjectLayer("Photo", picture, 80, 60),
            ObjectLayer("Group", group, 80, 60),
        }, null);
        doc.ActiveLayer = doc.Layers[3];
        return doc;
    }

    [Fact]
    public void A_project_comes_back_the_way_it_was_saved()
    {
        var doc = Sample();
        var path = PathFor("a.flash");
        ProjectWriter.Save(doc, path);
        var back = ProjectReader.Open(path);

        Assert.Equal((80, 60), (back.Width, back.Height));
        Assert.Equal(doc.Layers.Count, back.Layers.Count);
        Assert.Equal(3, back.ActiveLayerIndex);

        var painted = back.Layers[1];
        Assert.Equal("Painted", painted.Name);
        Assert.Equal(0.5, painted.Opacity);
        Assert.False(painted.IsVisible);
        Assert.True(painted.IsLocked);
        Assert.Null(painted.Object);
        Assert.Equal(new ColorRgba(10, 20, 30, 200), painted.Pixels.GetPixel(40, 30));

        for (int i = 0; i < doc.Layers.Count; i++)
            Assert.True(doc.Layers[i].Pixels.ContentEquals(back.Layers[i].Pixels), $"layer {i} pixels differ");
    }

    [Fact]
    public void Shapes_keep_their_look_effects_and_reshape()
    {
        var path = PathFor("shape.flash");
        ProjectWriter.Save(Sample(), path);
        var shape = Assert.IsType<ShapeObject>(ProjectReader.Open(path).Layers[2].Object);

        Assert.Equal(ShapeKind.Star, shape.Shape);
        Assert.Equal(new ObjectBox(5, 6, 30, 20, 15), shape.Box);
        Assert.Equal(new ColorRgba(255, 0, 0), shape.Fill);
        Assert.Equal(new ColorRgba(0, 0, 255), shape.Stroke);
        Assert.Equal(7, shape.Points);
        Assert.True(shape.FlipX);
        Assert.False(shape.FlipY);
        Assert.Equal(new ShadowStyle(new ColorRgba(0, 0, 0, 90), 5, 30, 6, 1.5), shape.Shadow);
        Assert.Equal(new ThreeDStyle(12, 60, new ColorRgba(9, 9, 9), 0.4), shape.ThreeD);
        Assert.Equal(4, shape.Warp!.Count);
        Assert.Equal(new PointD(1.2, 0.1), shape.Warp[1]);
    }

    [Fact]
    public void Text_pictures_and_groups_come_back_as_editable_objects()
    {
        var path = PathFor("objects.flash");
        ProjectWriter.Save(Sample(), path);
        var back = ProjectReader.Open(path);

        var text = Assert.IsType<TextObject>(back.Layers[3].Object);
        Assert.Equal("Hello\nthere", text.Text);
        Assert.Equal("Segoe UI", text.FontFamily);
        Assert.Equal(18, text.FontSize);
        Assert.True(text.Bold && text.Italic);
        Assert.Equal(TextAlign.Center, text.Align);

        var picture = Assert.IsType<ImageObject>(back.Layers[4].Object);
        Assert.Equal("Photo", picture.Name);
        Assert.Equal(3, picture.Revision);
        Assert.Equal(new ColorRgba(200, 100, 50), picture.Source.GetPixel(4, 4));
        Assert.Equal(new ColorRgba(1, 1, 1), picture.Original!.GetPixel(4, 4));

        var group = Assert.IsType<GroupObject>(back.Layers[5].Object);
        Assert.Equal(2, group.Members.Count);
        Assert.IsType<ShapeObject>(group.Members[0].Object);
        Assert.Equal(0.5, group.Members[0].Opacity);
        Assert.Equal("b", group.Members[1].Name);
        Assert.IsType<TextObject>(group.Members[1].Object);
    }

    [Fact]
    public void Pixels_moved_past_the_canvas_edge_are_kept()
    {
        var doc = new Document(20, 20);
        var layer = new Layer("Moved", 20, 20);
        var content = Solid(10, 10, new ColorRgba(5, 6, 7));
        layer.RestoreState(LayerState.Place(content, -4, 15, 20, 20));
        Assert.True(layer.HasHiddenPixels);
        doc.SetLayers(new List<Layer> { layer }, layer);

        var path = PathFor("edge.flash");
        ProjectWriter.Save(doc, path);
        var back = ProjectReader.Open(path).Layers[0];

        Assert.True(back.HasHiddenPixels);
        var (full, x, y) = back.GetFullContent();
        var (fullOld, xOld, yOld) = layer.GetFullContent();
        Assert.Equal((xOld, yOld), (x, y));
        Assert.True(fullOld.ContentEquals(full));
    }

    [Fact]
    public void A_picture_used_twice_is_stored_once()
    {
        var shared = Solid(8, 8, new ColorRgba(1, 2, 3));
        var one = new ImageObject(new ObjectBox(0, 0, 8, 8), shared);
        var two = new ImageObject(new ObjectBox(10, 0, 8, 8), shared);
        var doc = new Document(30, 10);
        doc.SetLayers(new List<Layer> { ObjectLayer("One", one, 30, 10), ObjectLayer("Two", two, 30, 10) }, null);

        var path = PathFor("shared.flash");
        ProjectWriter.Save(doc, path);
        using var zip = ZipFile.OpenRead(path);
        // two layer pictures + one shared source
        Assert.Equal(3, zip.Entries.Count(e => e.FullName.StartsWith("images/")));
    }

    [Fact]
    public void Saving_again_replaces_the_file_and_leaves_no_leftovers()
    {
        var path = PathFor("twice.flash");
        ProjectWriter.Save(Sample(), path);
        var small = new Document(10, 10);
        var layer = new Layer("Background", 10, 10);
        small.SetLayers(new List<Layer> { layer }, layer);
        ProjectWriter.Save(small, path);
        Assert.Equal(10, ProjectReader.Open(path).Width);
        Assert.Equal(new[] { "twice.flash" }, Directory.GetFiles(_folder).Select(Path.GetFileName).Where(n => n!.StartsWith("twice")).ToArray());
    }

    [Fact]
    public void Files_that_are_not_projects_give_a_clear_error()
    {
        var notZip = PathFor("text.flash");
        File.WriteAllText(notZip, "hello");
        Assert.Throws<InvalidDataException>(() => ProjectReader.Open(notZip));

        var emptyZip = PathFor("empty.flash");
        using (var stream = File.Create(emptyZip)) using (new ZipArchive(stream, ZipArchiveMode.Create)) { }
        var e = Assert.Throws<InvalidDataException>(() => ProjectReader.Open(emptyZip));
        Assert.Contains("not a Flash Editor project", e.Message);
    }

    [Fact]
    public void A_project_from_a_newer_version_is_refused()
    {
        var path = PathFor("future.flash");
        using (var stream = File.Create(path))
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
        using (var text = new StreamWriter(zip.CreateEntry("project.json").Open()))
            text.Write("{\"format\":\"flash-project\",\"version\":99,\"width\":1,\"height\":1,\"layers\":[]}");
        var e = Assert.Throws<InvalidDataException>(() => ProjectReader.Open(path));
        Assert.Contains("newer version", e.Message);
    }

    [Fact]
    public void Only_flash_files_count_as_projects()
    {
        Assert.True(ProjectFile.IsProject(@"C:\x\poster.FLASH"));
        Assert.False(ProjectFile.IsProject(@"C:\x\poster.png"));
    }
}
