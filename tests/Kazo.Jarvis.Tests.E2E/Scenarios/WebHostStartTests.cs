using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace Kazo.Jarvis.Tests.E2E.Scenarios;

/// <summary>The Web test host starts and serves its empty page (work item stage1/1-solution).</summary>
[TestFixture]
public sealed class WebHostStartTests : PageTest
{
    [Test]
    public async Task Home_page_opens_with_the_empty_root()
    {
        var response = await Page.GotoAsync(WebHost.BaseAddress.ToString());

        Assert.That(response?.Ok, Is.True, $"HTTP {response?.Status} from {WebHost.BaseAddress}");
        await Expect(Page).ToHaveTitleAsync("Kazo.Jarvis");
        await Expect(Page.Locator("#jarvis-root")).ToHaveAttributeAsync("data-host", "web");
    }
}
