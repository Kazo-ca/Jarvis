using System.Text.Json;
using Kazo.Jarvis.Features.ClaudeCode.Launch;

namespace Kazo.Jarvis.Tests.Unit.Features.ClaudeCode;

/// <summary>
/// What the log of a task keeps of a tool (ADR 0026, section 5): the tool and what it works on, never the
/// content, and the file only for a tool that writes one.
/// </summary>
[TestFixture]
public sealed class ToolNoteTests
{
    [TestCase("Write", """{"file_path":"/src/bar/a.md","content":"secret text"}""", "Write /src/bar/a.md", "/src/bar/a.md")]
    [TestCase("Edit", """{"file_path":"a.md","old_string":"x","new_string":"y"}""", "Edit a.md", "a.md")]
    [TestCase("NotebookEdit", """{"notebook_path":"n.ipynb"}""", "NotebookEdit n.ipynb", "n.ipynb")]
    [TestCase("Read", """{"file_path":"/src/bar/a.md"}""", "Read /src/bar/a.md", null)]
    [TestCase("Bash", """{"command":"dotnet test","description":"Run the tests"}""", "Bash dotnet test", null)]
    [TestCase("Grep", """{"pattern":"TODO","path":"src"}""", "Grep src", null)]
    [TestCase("Glob", """{"pattern":"**/*.cs"}""", "Glob **/*.cs", null)]
    [TestCase("TodoWrite", """{"todos":[]}""", "TodoWrite", null)]
    [TestCase("Write", """{"file_path":12}""", "Write", null)]
    [TestCase("Write", "[]", "Write", null)]
    public void A_tool_is_noted_with_what_it_works_on(string tool, string input, string about, string? path)
    {
        using var document = JsonDocument.Parse(input);

        Assert.That(ToolNote.Of(tool, document.RootElement), Is.EqualTo((about, path)));
    }
}
