using Kazo.Jarvis.Adapters.Hardware;

namespace Kazo.Jarvis.Tests.Unit.Adapters.Hardware;

/// <summary>Reading the answer of <c>nvidia-smi</c> (work item 14).</summary>
[TestFixture]
public sealed class NvidiaSmiGraphicsCardProbeTests
{
    [Test]
    public void One_card_is_read()
    {
        var card = NvidiaSmiGraphicsCardProbe.Parse("NVIDIA GeForce RTX 3060, 12288\r\n");

        Assert.Multiple(() =>
        {
            Assert.That(card?.Name, Is.EqualTo("NVIDIA GeForce RTX 3060"));
            Assert.That(card?.MemoryMiB, Is.EqualTo(12288));
        });
    }

    [Test]
    public void The_card_with_the_most_memory_is_kept() =>
        Assert.That(NvidiaSmiGraphicsCardProbe.Parse("NVIDIA T400, 2048\nNVIDIA GeForce RTX 4090, 24564\n")?.Name, Is.EqualTo("NVIDIA GeForce RTX 4090"));

    [TestCase("")]
    [TestCase("No devices were found")]
    [TestCase("NVIDIA GeForce RTX 3060, [N/A]")]
    public void An_answer_without_a_card_gives_none(string output) =>
        Assert.That(NvidiaSmiGraphicsCardProbe.Parse(output), Is.Null);
}
