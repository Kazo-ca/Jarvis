using Kazo.Jarvis.Kernel.Audio;

namespace Kazo.Jarvis.Features.Audio.Devices;

/// <summary>The short sound of the "Test the output" button: two soft notes, generated, no file.</summary>
public static class OutputChime
{
    /// <summary>Sample rate of the chime; Windows converts it to the output's.</summary>
    public const int SampleRate = 24_000;

    private const double NoteSeconds = 0.18;
    private const float Volume = 0.35f;

    /// <summary>Creates the chime: A5 then E6, each faded in and out so it never clicks.</summary>
    /// <returns>A clip of about 0.4 s.</returns>
    public static AudioClip Create()
    {
        var noteLength = (int)(NoteSeconds * SampleRate);
        var gap = SampleRate / 50;
        var samples = new float[(noteLength * 2) + gap];
        WriteNote(samples.AsSpan(0, noteLength), 880);
        WriteNote(samples.AsSpan(noteLength + gap, noteLength), 1318.5);
        return new AudioClip(samples, SampleRate);
    }

    private static void WriteNote(Span<float> target, double frequency)
    {
        for (var i = 0; i < target.Length; i++)
        {
            // Raised sine window: no step at either end, hence no click.
            var envelope = Math.Sin(Math.PI * i / target.Length);
            target[i] = (float)(Volume * envelope * envelope * Math.Sin(2 * Math.PI * frequency * i / SampleRate));
        }
    }
}
