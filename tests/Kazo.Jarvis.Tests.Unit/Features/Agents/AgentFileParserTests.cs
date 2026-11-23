using Kazo.Jarvis.Features.Agents.Catalog;
using Kazo.Jarvis.Kernel.Agents;

namespace Kazo.Jarvis.Tests.Unit.Features.Agents;

/// <summary>The file of an agent (ADR 0025, section 2): a header like an agent of Claude Code, then the instructions.</summary>
[TestFixture]
public sealed class AgentFileParserTests
{
    [Test]
    public void A_file_gives_its_name_description_tools_and_instructions()
    {
        const string Text = """
            ---
            name: Domotique
            description: Les lumières et les prises de la maison.
            tools: HassTurnOn, HassTurnOff
            ---

            Lis les entités avant d'agir.
            Ne devine jamais une serrure.
            """;

        var problem = AgentFileParser.Parse("home-automation", Text, out var agent);

        Assert.Multiple(() =>
        {
            Assert.That(problem, Is.Null);
            Assert.That(agent!.Id, Is.EqualTo("home-automation"));
            Assert.That(agent.Name, Is.EqualTo("Domotique"));
            Assert.That(agent.Description, Is.EqualTo("Les lumières et les prises de la maison."));
            Assert.That(agent.Tools, Is.EqualTo(new[] { "HassTurnOn", "HassTurnOff" }));
            Assert.That(agent.Instructions, Is.EqualTo("Lis les entités avant d'agir.\nNe devine jamais une serrure."));
        });
    }

    [Test]
    public void The_tools_may_be_one_per_line_quoted_or_absent()
    {
        var listed = AgentFileParser.Parse("web", "---\nname: \"Web\"\ndescription: 'Chercher.'\ntools:\n  - web_search\n  - \"fetch_url\"\n  - web_search\n---\nCite la source.", out var web);
        var none = AgentFileParser.Parse("talk", "﻿\r\n---\r\nname: Parole\r\ndescription: Parler.\r\n---\r\n", out var talk);

        Assert.Multiple(() =>
        {
            Assert.That(listed, Is.Null);
            Assert.That(web!.Name, Is.EqualTo("Web"));
            Assert.That(web.Description, Is.EqualTo("Chercher."));
            Assert.That(web.Tools, Is.EqualTo(new[] { "web_search", "fetch_url" }), "A tool named twice counts once.");
            Assert.That(none, Is.Null);
            Assert.That(talk!.Tools, Is.Empty);
            Assert.That(talk.Instructions, Is.Empty);
        });
    }

    [TestCase(null, AgentFileProblem.Unreadable)]
    [TestCase("", AgentFileProblem.NoHeader)]
    [TestCase("Juste des consignes.", AgentFileProblem.NoHeader)]
    [TestCase("---\nname: Web\ndescription: Chercher.\nPas de fin d'en-tête.", AgentFileProblem.NoHeader)]
    [TestCase("---\ndescription: Chercher.\n---\n", AgentFileProblem.NoName)]
    [TestCase("---\nname: Web\ndescription:\n---\n", AgentFileProblem.NoDescription)]
    public void A_file_that_does_not_read_says_why(string? text, AgentFileProblem expected)
    {
        var problem = AgentFileParser.Parse("web", text, out var agent);

        Assert.Multiple(() =>
        {
            Assert.That(problem, Is.EqualTo(expected));
            Assert.That(agent, Is.Null);
        });
    }

    [Test]
    public void What_is_written_reads_back_the_same()
    {
        var agent = new AgentDefinition("files", "Fichiers", "Lister un dossier : et lire un fichier.", ["list_directory", "read_file"], "Reste dans les dossiers autorisés.\nRésume.");

        var problem = AgentFileParser.Parse("files", AgentFileParser.Write(agent), out var read);

        Assert.Multiple(() =>
        {
            Assert.That(problem, Is.Null);
            Assert.That(read!.Name, Is.EqualTo(agent.Name));
            Assert.That(read.Description, Is.EqualTo(agent.Description), "A colon in the description stays in it.");
            Assert.That(read.Tools, Is.EqualTo(agent.Tools));
            Assert.That(read.Instructions, Is.EqualTo(agent.Instructions));
        });
    }
}
