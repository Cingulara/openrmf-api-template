using System;
using openrmf_templates_api.Classes;
using Xunit;

namespace tests.Classes;

public class CompressionTests
{
    [Fact]
    public void CompressAndDecompress_RoundTripsText()
    {
        const string raw = "OpenRMF test content with symbols: <xml>alpha</xml>";

        var compressed = Compression.CompressString(raw);
        var decompressed = Compression.DecompressString(compressed);

        Assert.NotEqual(raw, compressed);
        Assert.Equal(raw, decompressed);
    }

    [Fact]
    public void DecompressString_ThrowsForInvalidBase64()
    {
        var ex = Record.Exception(() => Compression.DecompressString("not-base64"));

        Assert.NotNull(ex);
        Assert.IsType<FormatException>(ex);
    }
}
