using NeoPos.WebAPI.Services;

namespace NeoPos.Tests;

public class MediaPathCollectorTests
{
    [Theory]
    [InlineData("/uploads/products/a.jpg", "/uploads/products/a.jpg")]
    [InlineData("uploads/categories/b.png", "/uploads/categories/b.png")]
    [InlineData("https://cdn.example.com/uploads/products/x.webp", "/uploads/products/x.webp")]
    [InlineData("/uploads/../etc/passwd", null)]
    [InlineData("/images/x.jpg", null)]
    public void NormalizeOne_ValidatesUploadPaths(string input, string? expected)
    {
        Assert.Equal(expected, MediaPathCollector.NormalizeOne(input));
    }

    [Fact]
    public void NormalizeWebBase_AddsHttpsWhenMissing()
    {
        Assert.Equal("https://neopos.runasp.net", MediaSyncService.NormalizeWebBase("neopos.runasp.net"));
        Assert.Equal("http://192.168.1.10:5050", MediaSyncService.NormalizeWebBase("http://192.168.1.10:5050"));
    }
}
