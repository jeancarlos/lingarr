using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Lingarr.Contracts.Translation;
using Lingarr.Server.Interfaces.Services;
using Lingarr.Server.Interfaces.Services.Translation;
using Lingarr.Server.Services;
using Lingarr.Server.Services.Translation;
using Lingarr.Server.Services.Translation.Base;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Lingarr.Server.Tests.Services.Translation;

public class BaseLanguageServicePromptContextTests
{
    private const string Prompt = "Translate from {sourceLanguage} to {targetLanguage}";

    private const string GlossaryText =
        "# context: Ninja anime.\nLeaf Village = Aldeia da Folha\n";

    private const string RenderedGlossary =
        "Series context: Ninja anime.\n" +
        "Always use these established Brazilian Portuguese renderings:\n" +
        "- Leaf Village → Aldeia da Folha\n";

    internal sealed class TestLanguageService : BaseLanguageService
    {
        public TestLanguageService() : base(
            new Mock<ISettingService>().Object,
            new Mock<ILogger>().Object,
            new LanguageCodeService())
        {
            _prompt = Prompt;
            _replacements = new Dictionary<string, string>
            {
                ["sourceLanguage"] = "English",
                ["targetLanguage"] = "Brazilian Portuguese"
            };
        }

        public void SetPrompt(string prompt) => _prompt = prompt;

        public Dictionary<string, string> Replacements() => GetReplacements("model", "line", null, null);

        public Dictionary<string, string> BatchReplacements() => GetBatchReplacements("model", "[]");

        public override string? ModelName => "model";

        public override Task<string> TranslateAsync(
            string text,
            string sourceLanguage,
            string targetLanguage,
            List<string>? contextLinesBefore,
            List<string>? contextLinesAfter,
            CancellationToken cancellationToken) => Task.FromResult(text);
    }

    [Fact]
    public void GetReplacements_ShouldBeUnchanged_WithoutPromptContext()
    {
        var service = new TestLanguageService();

        var replacements = service.Replacements();

        Assert.Equal("Translate from English to Brazilian Portuguese", replacements["systemPrompt"]);
        Assert.False(replacements.ContainsKey("title"));
        Assert.False(replacements.ContainsKey("glossary"));
    }

    [Fact]
    public void GetReplacements_ShouldLeavePlaceholdersLiteral_WithoutPromptContext()
    {
        var service = new TestLanguageService();
        service.SetPrompt("{title} {glossary} from {sourceLanguage}");

        Assert.Equal("{title} {glossary} from English", service.Replacements()["systemPrompt"]);
    }

    [Fact]
    public void GetReplacements_ShouldBeUnchanged_WithTitleButNoGlossary()
    {
        var service = new TestLanguageService();
        service.SetPromptContext(new PromptContext("Naruto Kai", null));

        Assert.Equal("Translate from English to Brazilian Portuguese",
            service.Replacements()["systemPrompt"]);
    }

    [Fact]
    public void GetReplacements_ShouldReplaceTitleAndGlossaryPlaceholders()
    {
        var service = new TestLanguageService();
        service.SetPrompt("Translating {title}.\n{glossary}");
        service.SetPromptContext(new PromptContext("Naruto Kai", ShowGlossary.Parse(GlossaryText)));

        Assert.Equal("Translating Naruto Kai.\n" + RenderedGlossary,
            service.Replacements()["systemPrompt"]);
    }

    [Fact]
    public void GetReplacements_ShouldAppendGlossary_WhenPromptHasNoPlaceholder()
    {
        var service = new TestLanguageService();
        service.SetPromptContext(new PromptContext("Naruto Kai", ShowGlossary.Parse(GlossaryText)));

        Assert.Equal("Translate from English to Brazilian Portuguese\n" + RenderedGlossary,
            service.Replacements()["systemPrompt"]);
    }

    [Fact]
    public void GetReplacements_ShouldNotAppend_WhenGlossaryRendersEmpty()
    {
        var service = new TestLanguageService();
        service.SetPromptContext(new PromptContext("Naruto Kai", ShowGlossary.Parse("# only a comment\n")));

        Assert.Equal("Translate from English to Brazilian Portuguese",
            service.Replacements()["systemPrompt"]);
    }

    [Fact]
    public void GetBatchReplacements_ShouldAppendGlossary_WhenPromptHasNoPlaceholder()
    {
        var service = new TestLanguageService();
        service.SetPromptContext(new PromptContext("Naruto Kai", ShowGlossary.Parse(GlossaryText)));

        var replacements = service.BatchReplacements();

        Assert.Equal("Translate from English to Brazilian Portuguese\n" + RenderedGlossary,
            replacements["systemPrompt"]);
        Assert.Equal("Naruto Kai", replacements["title"]);
    }

    [Fact]
    public void GetBatchReplacements_ShouldBeUnchanged_WithoutPromptContext()
    {
        var replacements = new TestLanguageService().BatchReplacements();

        Assert.Equal("Translate from English to Brazilian Portuguese", replacements["systemPrompt"]);
        Assert.False(replacements.ContainsKey("title"));
        Assert.False(replacements.ContainsKey("glossary"));
    }

    [Fact]
    public void Apply_ShouldSetContextOnLanguageServicesOnly()
    {
        var languageService = new TestLanguageService();
        var plainService = new Mock<ITranslationService>().Object;
        var services = new List<TranslationServiceEntry>
        {
            new("localai", languageService, null),
            new("google", plainService, null)
        };

        PromptContextService.Apply(services, new PromptContext("Naruto Kai", null));

        Assert.Equal("Naruto Kai", languageService.Replacements()["title"]);
    }

    [Fact]
    public void Apply_ShouldDoNothing_WithNullContext()
    {
        var languageService = new TestLanguageService();

        PromptContextService.Apply(
            new List<TranslationServiceEntry> { new("localai", languageService, null) },
            null);

        Assert.False(languageService.Replacements().ContainsKey("title"));
    }
}
