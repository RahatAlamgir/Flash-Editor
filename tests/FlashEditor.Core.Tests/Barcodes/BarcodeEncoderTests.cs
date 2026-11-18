using FlashEditor.Core.Barcodes;
using Xunit;

namespace FlashEditor.Core.Tests.Barcodes;

public class BarcodeEncoderTests
{
    [Theory]
    [InlineData("5901234123457", 7)]
    [InlineData("4006381333931", 1)]
    [InlineData("9780201379624", 4)]
    public void Known_EAN13_numbers_have_the_right_check_digit(string number, int check)
        => Assert.Equal(check, BarcodeEncoder.CheckDigit(number[..12]));

    [Fact]
    public void An_EAN13_is_95_modules_wide_with_guards_and_a_check_digit_added()
    {
        var data = BarcodeEncoder.Encode("590123412345", BarcodeKind.Ean13);
        Assert.Equal(95, data.Width);
        Assert.True(data.Modules[0] && !data.Modules[1] && data.Modules[2]);                 // start guard 101
        Assert.True(!data.Modules[45] && data.Modules[46] && !data.Modules[47] && data.Modules[48] && !data.Modules[49]);   // middle guard 01010
        Assert.True(data.Modules[92] && !data.Modules[93] && data.Modules[94]);              // end guard
        Assert.Equal("5", data.Labels[0].Text);
        Assert.Equal("123457", data.Labels[2].Text);                                         // ends with the added check digit 7
    }

    [Fact]
    public void A_wrong_check_digit_is_refused_with_the_right_one_named()
    {
        string? message = BarcodeEncoder.Validate("5901234123458", BarcodeKind.Ean13);
        Assert.NotNull(message);
        Assert.Contains("7", message);
        Assert.Null(BarcodeEncoder.Validate("5901234123457", BarcodeKind.Ean13));
    }

    [Fact]
    public void The_number_of_digits_and_the_kind_of_characters_are_checked()
    {
        Assert.NotNull(BarcodeEncoder.Validate("12345", BarcodeKind.Ean13));
        Assert.NotNull(BarcodeEncoder.Validate("12345678901A", BarcodeKind.Ean13));
        Assert.NotNull(BarcodeEncoder.Validate("", BarcodeKind.Code128));
        Assert.NotNull(BarcodeEncoder.Validate("café", BarcodeKind.Code128));
        Assert.NotNull(BarcodeEncoder.Validate("abc*", BarcodeKind.Code39));
        Assert.Null(BarcodeEncoder.Validate("036000291452", BarcodeKind.UpcA));
        Assert.Null(BarcodeEncoder.Validate("9638507", BarcodeKind.Ean8));
    }

    [Fact]
    public void A_UPC_A_is_an_EAN13_with_a_zero_in_front_and_shows_twelve_digits()
    {
        var upc = BarcodeEncoder.Encode("03600029145", BarcodeKind.UpcA);
        var ean = BarcodeEncoder.Encode("003600029145", BarcodeKind.Ean13);
        Assert.Equal(ean.Modules, upc.Modules);
        Assert.Equal("036000291452", string.Concat(upc.Labels.Select(l => l.Text)));
    }

    [Fact]
    public void Every_Code128_symbol_follows_the_rules_of_the_standard()
    {
        Assert.Equal(107, BarcodeEncoder.C128.Length);
        var seen = new HashSet<string>();
        for (int v = 0; v < 106; v++)
        {
            string p = BarcodeEncoder.C128[v];
            Assert.Equal(6, p.Length);
            Assert.Equal(11, p.Sum(c => c - '0'));                                   // 11 modules wide
            Assert.Equal(0, (p[0] - '0' + p[2] - '0' + p[4] - '0') % 2);             // the bars add up to an even width
            Assert.True(p.All(c => c is >= '1' and <= '4'));
            Assert.True(seen.Add(p), $"symbol {v} repeats another");
        }
        Assert.Equal(13, BarcodeEncoder.C128[106].Sum(c => c - '0'));                // the stop mark
    }

    [Fact]
    public void Code128_adds_a_check_symbol_and_a_stop_mark()
    {
        var data = BarcodeEncoder.Encode("A", BarcodeKind.Code128);
        // start B (11) + "A" (11) + check (11) + stop (13) modules
        Assert.Equal(11 + 11 + 11 + 13, data.Width);
        Assert.True(data.Modules[0]);                                                  // starts with a bar
        Assert.True(data.Modules[^1]);                                                 // the stop mark ends with a bar
    }

    [Fact]
    public void Only_digits_in_pairs_make_the_shorter_Code128_C()
    {
        int digits = BarcodeEncoder.Encode("1234567890", BarcodeKind.Code128).Width;
        int text = BarcodeEncoder.Encode("12345678901", BarcodeKind.Code128).Width;
        Assert.True(digits < text);
        Assert.Equal(11 * (1 + 5 + 1) + 13, digits);
    }

    [Fact]
    public void Every_Code39_character_has_three_wide_elements_out_of_nine()
    {
        var chars = BarcodeEncoder.Code39Characters;
        Assert.Equal(43, chars.Count);
        // *X* = three characters of 15 modules (3 wide x 3 + 6 narrow) with two gaps: 47 modules only when each has exactly three wide elements.
        foreach (char c in chars)
            Assert.Equal(3 * 15 + 2, BarcodeEncoder.Encode(c.ToString(), BarcodeKind.Code39).Width);
        var patterns = chars.Select(c => string.Concat(BarcodeEncoder.Encode(c.ToString(), BarcodeKind.Code39).Modules.Select(m => m ? '1' : '0'))).ToList();
        Assert.Equal(patterns.Count, patterns.Distinct().Count());                    // no two characters look the same
    }

    [Fact]
    public void Lowercase_letters_are_made_capital_for_Code39()
        => Assert.Equal(BarcodeEncoder.Encode("HELLO 1", BarcodeKind.Code39).Modules, BarcodeEncoder.Encode("hello 1", BarcodeKind.Code39).Modules);
}
