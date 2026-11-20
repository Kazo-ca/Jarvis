using System.Runtime.InteropServices;
using Kazo.Jarvis.Kernel.Audio;
using Kazo.Jarvis.Windows.Audio;

namespace Kazo.Jarvis.Tests.Unit.Windows.Audio;

/// <summary>
/// The format conversions of the Windows host's audio (work item stage1/8-audio-devices): device format to
/// 16 kHz mono for the capture, mono clip to the output format for the playback.
/// </summary>
[TestFixture]
public sealed class AudioConvertersTests
{
    [Test]
    public void Stereo_float_at_48_kHz_becomes_a_third_as_many_mono_samples()
    {
        var converter = new CaptureConverter(48_000, 2, SampleEncoding.Float32);
        var output = new List<float>();
        var total = 0;

        // One second in blocks of 10 ms, as WASAPI delivers them.
        for (var block = 0; block < 100; block++)
        {
            converter.Convert(StereoFloat(Constant(0.25f, 480), Constant(0.75f, 480)), output);
            total += output.Count;
        }

        Assert.Multiple(() =>
        {
            Assert.That(total, Is.InRange(MicrophoneFormat.SampleRate - 1, MicrophoneFormat.SampleRate + 1));

            // Mean of the two channels, once the low-pass filter has settled.
            Assert.That(output[^1], Is.EqualTo(0.5f).Within(0.001f));
        });
    }

    [Test]
    public void A_voice_band_tone_crosses_the_seams_between_blocks_intact()
    {
        var converter = new CaptureConverter(48_000, 1, SampleEncoding.Float32);
        var tone = Sine(440, 48_000, 48_000);
        var output = new List<float>();
        var result = new List<float>();
        for (var start = 0; start < tone.Length; start += 441)
        {
            converter.Convert(MonoFloat(tone.AsSpan(start, Math.Min(441, tone.Length - start))), output);
            result.AddRange(output);
        }

        var expected = Sine(440, MicrophoneFormat.SampleRate, result.Count);

        // After the filter has settled, the tone matches a 16 kHz tone, give or take the filter's small delay.
        var error = result.Skip(1_000).Zip(expected.Skip(1_000)).Max(pair => Math.Abs(pair.First - pair.Second));
        Assert.That(error, Is.LessThan(0.15f));
    }

    [Test]
    public void A_tone_above_8_kHz_is_filtered_out_instead_of_folding_back()
    {
        var converter = new CaptureConverter(48_000, 1, SampleEncoding.Float32);
        var output = new List<float>();

        converter.Convert(MonoFloat(Sine(12_000, 48_000, 48_000)), output);

        var loudest = output.Skip(1_000).Max(Math.Abs);
        Assert.That(loudest, Is.LessThan(0.2f));
    }

    [Test]
    public void Sixteen_bit_integers_are_read_and_16_kHz_passes_unchanged()
    {
        var converter = new CaptureConverter(16_000, 1, SampleEncoding.Pcm16);
        short[] samples = [0, 16_384, -16_384, 32_767];
        var output = new List<float>();

        converter.Convert(MemoryMarshal.AsBytes(samples.AsSpan()), output);

        Assert.That(output, Is.EqualTo(new[] { 0f, 0.5f, -0.5f, 32_767f / 32_768f }).Within(0.0001f));
    }

    [Test]
    public void Twenty_four_bit_integers_keep_their_sign()
    {
        var converter = new CaptureConverter(16_000, 1, SampleEncoding.Pcm24);
        byte[] bytes = [0x00, 0x00, 0x40, 0x00, 0x00, 0xC0];
        var output = new List<float>();

        converter.Convert(bytes, output);

        Assert.That(output, Is.EqualTo(new[] { 0.5f, -0.5f }).Within(0.0001f));
    }

    [Test]
    public void A_mono_clip_is_resampled_and_copied_to_every_channel()
    {
        float[] clip = [0f, 1f];

        var converted = PlaybackConverter.Convert(clip, 24_000, 48_000, 2);

        Assert.That(converted, Is.EqualTo(new[] { 0f, 0f, 0.5f, 0.5f, 1f, 1f, 1f, 1f }).Within(0.0001f));
    }

    [Test]
    public void A_clip_keeps_its_duration_at_the_output_rate()
    {
        var clip = new float[22_050];

        var converted = PlaybackConverter.Convert(clip, 22_050, 48_000, 2);

        Assert.That(converted, Has.Length.EqualTo(48_000 * 2));
    }

    private static float[] Constant(float value, int count) => [.. Enumerable.Repeat(value, count)];

    private static float[] Sine(double frequency, int rate, int count) =>
        [.. Enumerable.Range(0, count).Select(i => (float)(0.5 * Math.Sin(2 * Math.PI * frequency * i / rate)))];

    private static byte[] MonoFloat(ReadOnlySpan<float> samples) => MemoryMarshal.AsBytes(samples).ToArray();

    private static byte[] StereoFloat(float[] left, float[] right)
    {
        var interleaved = new float[left.Length * 2];
        for (var i = 0; i < left.Length; i++)
        {
            interleaved[i * 2] = left[i];
            interleaved[(i * 2) + 1] = right[i];
        }

        return MonoFloat(interleaved);
    }
}
