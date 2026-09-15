using Lingarr.Server.Services.Translation;
using Xunit;

namespace Lingarr.Server.Tests.Services.Translation;

public class StructuredResponseNormalizerTests
{
    [Fact]
    public void Normalize_PlainObject_IsUnchanged()
    {
        const string content = "{\"translations\":[{\"position\":1,\"line\":\"Ola\"}]}";

        Assert.Equal(content, StructuredResponseNormalizer.Normalize(content));
    }

    [Fact]
    public void Normalize_FencedObject_StripsFence()
    {
        var content = "```json\n{\"translations\":[{\"position\":1,\"line\":\"Ola\"}]}\n```";

        Assert.Equal(
            "{\"translations\":[{\"position\":1,\"line\":\"Ola\"}]}",
            StructuredResponseNormalizer.Normalize(content));
    }

    [Fact]
    public void Normalize_ObjectWithProse_KeepsOnlyJson()
    {
        var content = "Aqui esta: {\"translations\":[]} espero que ajude!";

        Assert.Equal("{\"translations\":[]}", StructuredResponseNormalizer.Normalize(content));
    }

    [Fact]
    public void Normalize_BareArray_IsWrapped()
    {
        var content = "```json\n[{\"position\":1,\"line\":\"Ola\"}]\n```";

        Assert.Equal(
            "{\"translations\":[{\"position\":1,\"line\":\"Ola\"}]}",
            StructuredResponseNormalizer.Normalize(content));
    }

    [Fact]
    public void Normalize_BareArrayOfObjects_PrefersTheArrayItStartsWith()
    {
        var content = "[{\"position\":1,\"line\":\"Ola\"},{\"position\":2,\"line\":\"Tudo bem\"}]";

        Assert.Equal(
            "{\"translations\":[{\"position\":1,\"line\":\"Ola\"},{\"position\":2,\"line\":\"Tudo bem\"}]}",
            StructuredResponseNormalizer.Normalize(content));
    }

    [Fact]
    public void Normalize_Prose_IsLeftForTheParserToReject()
    {
        const string content = "Nao consigo traduzir isso.";

        Assert.Equal(content, StructuredResponseNormalizer.Normalize(content));
    }

    [Fact]
    public void Normalize_Empty_IsUnchanged()
    {
        Assert.Equal(string.Empty, StructuredResponseNormalizer.Normalize(string.Empty));
    }
}
