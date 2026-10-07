using System;
using System.Collections.Generic;
using System.IO;
using Lingarr.Server.Services.Translation;
using Xunit;

namespace Lingarr.Server.Tests.Services.Translation;

public class ShowGlossaryTests
{
    [Fact]
    public void Parse_ShouldReadContextAndEntries()
    {
        var glossary = ShowGlossary.Parse(
            "# context: Teen ninja anime.\n" +
            "# a plain comment\n" +
            "\n" +
            "Believe it! = Tô certo!\n" +
            "Hidden Leaf Village = Aldeia Oculta da Folha\n" +
            "malformed line without separator\n");

        Assert.Equal("Teen ninja anime.", glossary.Context);
        Assert.Equal(2, glossary.Entries.Count);
        Assert.Equal(new KeyValuePair<string, string>("Believe it!", "Tô certo!"), glossary.Entries[0]);
        Assert.Equal(new KeyValuePair<string, string>("Hidden Leaf Village", "Aldeia Oculta da Folha"),
            glossary.Entries[1]);
    }

    [Fact]
    public void Parse_ShouldKeepFirstContextLine()
    {
        var glossary = ShowGlossary.Parse("# context: first\n# context: second\n");

        Assert.Equal("first", glossary.Context);
    }

    [Fact]
    public void Parse_ShouldHandleWindowsLineEndings()
    {
        var glossary = ShowGlossary.Parse("# context: Ninja anime.\r\nLeaf Village = Aldeia da Folha\r\n");

        Assert.Equal("Ninja anime.", glossary.Context);
        Assert.Equal(new KeyValuePair<string, string>("Leaf Village", "Aldeia da Folha"),
            Assert.Single(glossary.Entries));
    }

    [Fact]
    public void Render_ShouldProduceContextAndRenderings()
    {
        var glossary = ShowGlossary.Parse("# context: Ninja anime.\nLeaf Village = Aldeia da Folha\n");

        Assert.Equal(
            "Series context: Ninja anime.\n" +
            "Always use these established Brazilian Portuguese renderings:\n" +
            "- Leaf Village → Aldeia da Folha\n",
            glossary.Render("Brazilian Portuguese"));
    }

    [Fact]
    public void Render_ShouldReturnEmpty_WhenFileHasNoUsableLines()
    {
        Assert.Equal(string.Empty, ShowGlossary.Parse("# just a comment\n\n").Render("Spanish"));
    }

    [Fact]
    public void Render_ShouldSkipHeading_WhenOnlyContextExists()
    {
        Assert.Equal("Series context: Ninja anime.\n",
            ShowGlossary.Parse("# context: Ninja anime.").Render("Spanish"));
    }

    [Fact]
    public void Load_ShouldReturnNull_WhenFolderHasNoGlossary()
    {
        var folder = Directory.CreateTempSubdirectory().FullName;
        try
        {
            Assert.Null(ShowGlossary.Load(folder));
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void Load_ShouldReturnNull_WhenFolderIsMissingOrEmpty()
    {
        Assert.Null(ShowGlossary.Load(null));
        Assert.Null(ShowGlossary.Load(""));
        Assert.Null(ShowGlossary.Load(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())));
    }

    [Fact]
    public void Load_ShouldParseGlossaryFile()
    {
        var folder = Directory.CreateTempSubdirectory().FullName;
        try
        {
            File.WriteAllText(Path.Combine(folder, ".lingarr-glossary.txt"),
                "# context: Ninja anime.\nLeaf Village = Aldeia da Folha\n");

            var glossary = ShowGlossary.Load(folder);

            Assert.NotNull(glossary);
            Assert.Equal("Ninja anime.", glossary!.Context);
            Assert.Single(glossary.Entries);
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }
}
