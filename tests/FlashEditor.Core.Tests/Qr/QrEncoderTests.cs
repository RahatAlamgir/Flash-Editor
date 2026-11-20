using FlashEditor.Core.Qr;

namespace FlashEditor.Core.Tests.Qr;

public class QrEncoderTests
{
    [Theory]
    [InlineData(QrLevel.Low, 2953)]
    [InlineData(QrLevel.Medium, 2331)]
    [InlineData(QrLevel.Quartile, 1663)]
    [InlineData(QrLevel.High, 1273)]
    public void The_largest_text_matches_the_standard(QrLevel level, int bytes) => Assert.Equal(bytes, QrEncoder.MaxBytes(level));

    [Fact]
    public void A_short_text_makes_a_version_1_code_and_longer_texts_make_bigger_ones()
    {
        Assert.Equal(21, QrEncoder.Encode("HELLO", QrLevel.Low).GetLength(0));
        int a = QrEncoder.Encode(new string('x', 40), QrLevel.Medium).GetLength(0);
        int b = QrEncoder.Encode(new string('x', 400), QrLevel.Medium).GetLength(0);
        Assert.True(b > a);
        Assert.Equal(0, (a - 17) % 4);
    }

    [Fact]
    public void The_three_finder_patterns_and_the_timing_lines_are_in_place()
    {
        var m = QrEncoder.Encode("https://example.com", QrLevel.Quartile);
        int n = m.GetLength(0);
        foreach (var (ox, oy) in new[] { (0, 0), (n - 7, 0), (0, n - 7) })
        {
            for (int i = 0; i < 7; i++)
            {
                Assert.True(m[oy, ox + i]); Assert.True(m[oy + 6, ox + i]);     // outer ring
                Assert.True(m[oy + i, ox]); Assert.True(m[oy + i, ox + 6]);
            }
            Assert.True(m[oy + 3, ox + 3]);                                       // middle
            Assert.False(m[oy + 1, ox + 1]);                                      // light ring
        }
        for (int i = 8; i < n - 8; i++) Assert.Equal(i % 2 == 0, m[6, i]);       // timing
    }

    [Fact]
    public void The_same_text_always_gives_the_same_code_and_different_levels_differ()
    {
        var a = QrEncoder.Encode("flash", QrLevel.Medium);
        var b = QrEncoder.Encode("flash", QrLevel.Medium);
        var c = QrEncoder.Encode("flash", QrLevel.High);
        Assert.Equal(a.Cast<bool>(), b.Cast<bool>());
        Assert.NotEqual(a.Cast<bool>(), c.Cast<bool>());
    }

    [Fact]
    public void A_text_that_is_too_long_is_refused()
        => Assert.Throws<ArgumentException>(() => QrEncoder.Encode(new string('a', QrEncoder.MaxBytes(QrLevel.Low) + 1), QrLevel.Low));

    [Fact]
    public void Letters_outside_ascii_are_stored_as_utf8()
    {
        var a = QrEncoder.Encode("é€é€é€é€é€é€é€é€é€é€é€é€é€é€é€é€é€é€é€é€", QrLevel.Low);    // 100 bytes in 40 letters
        var b = QrEncoder.Encode(new string('e', 40), QrLevel.Low);
        Assert.True(a.GetLength(0) > b.GetLength(0));
    }
}
