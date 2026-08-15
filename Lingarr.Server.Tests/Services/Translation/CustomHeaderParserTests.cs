using Lingarr.Server.Services.Translation;
using Xunit;

namespace Lingarr.Server.Tests.Services.Translation;

public class CustomHeaderParserTests
{
    [Fact]
    public void Parse_Null_ReturnsEmpty()
    {
        Assert.Empty(CustomHeaderParser.Parse(null));
    }

    [Fact]
    public void Parse_Whitespace_ReturnsEmpty()
    {
        Assert.Empty(CustomHeaderParser.Parse("  \n \r\n "));
    }

    [Fact]
    public void Parse_SingleHeader_ReturnsNameAndValue()
    {
        var result = CustomHeaderParser.Parse("X-Token-Saver: off");

        Assert.Equal([("X-Token-Saver", "off")], result);
    }

    [Fact]
    public void Parse_MultipleLines_ReturnsAllHeaders()
    {
        var result = CustomHeaderParser.Parse("X-One: 1\r\nX-Two: 2\nX-Three: 3");

        Assert.Equal([("X-One", "1"), ("X-Two", "2"), ("X-Three", "3")], result);
    }

    [Fact]
    public void Parse_TrimsSurroundingWhitespace()
    {
        var result = CustomHeaderParser.Parse("   X-One   :   value with spaces   ");

        Assert.Equal([("X-One", "value with spaces")], result);
    }

    [Fact]
    public void Parse_ValueContainingColon_SplitsOnFirstColonOnly()
    {
        var result = CustomHeaderParser.Parse("X-Target: http://example.com:8080/v1");

        Assert.Equal([("X-Target", "http://example.com:8080/v1")], result);
    }

    [Fact]
    public void Parse_SkipsCommentLines()
    {
        var result = CustomHeaderParser.Parse("# a comment\nX-One: 1");

        Assert.Equal([("X-One", "1")], result);
    }

    [Fact]
    public void Parse_SkipsLinesWithoutColon()
    {
        var result = CustomHeaderParser.Parse("nonsense\nX-One: 1");

        Assert.Equal([("X-One", "1")], result);
    }

    [Fact]
    public void Parse_SkipsLinesWithEmptyName()
    {
        var result = CustomHeaderParser.Parse(": orphan\nX-One: 1");

        Assert.Equal([("X-One", "1")], result);
    }

    [Fact]
    public void Parse_AllowsEmptyValue()
    {
        var result = CustomHeaderParser.Parse("X-One:");

        Assert.Equal([("X-One", "")], result);
    }
}
