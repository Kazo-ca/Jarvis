using System.Buffers.Binary;
using System.Runtime.InteropServices;
using Kazo.Jarvis.Kernel.Audio;

namespace Kazo.Jarvis.Windows.Audio;

/// <summary>How the samples of a device are written.</summary>
internal enum SampleEncoding
{
    /// <summary>32-bit float, the usual WASAPI shared-mode format.</summary>
    Float32,

    /// <summary>16-bit integer.</summary>
    Pcm16,

    /// <summary>24-bit integer, packed in 3 bytes.</summary>
    Pcm24,

    /// <summary>32-bit integer.</summary>
    Pcm32,
}

/// <summary>
/// Turns what a microphone delivers in its own format (often 48 kHz stereo float) into the format of the
/// kernel, 16 kHz mono float (<see cref="MicrophoneFormat"/>). Keeps its state between blocks, so a stream
/// converts without seams. Plain .NET with no NAudio type: tested on Linux.
/// </summary>
/// <remarks>
/// Mono is the mean of the channels. Downsampling first goes through a low-pass filter at 7 kHz, under the
/// 8 kHz limit of 16 kHz audio, so high sounds do not fold back into the voice band; then linear interpolation.
/// </remarks>
internal sealed class CaptureConverter
{
    private const double CutoffHertz = 7_000;

    private readonly int channels;
    private readonly SampleEncoding encoding;
    private readonly int bytesPerFrame;
    private readonly double step;
    private readonly bool filtered;

    // Low-pass biquad (Butterworth, RBJ cookbook), direct form I.
    private readonly double b0, b1, b2, a1, a2;
    private double x1, x2, y1, y2;

    private float previous;
    private double position;
    private float[] mono = [];

    /// <summary>Creates a converter for one stream.</summary>
    /// <param name="sampleRate">Samples per second of the device.</param>
    /// <param name="channels">Channels of the device, interleaved.</param>
    /// <param name="encoding">How each sample is written.</param>
    public CaptureConverter(int sampleRate, int channels, SampleEncoding encoding)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(sampleRate, 8_000);
        ArgumentOutOfRangeException.ThrowIfLessThan(channels, 1);
        this.channels = channels;
        this.encoding = encoding;
        bytesPerFrame = channels * BytesPerSample(encoding);
        step = (double)sampleRate / MicrophoneFormat.SampleRate;
        filtered = sampleRate > MicrophoneFormat.SampleRate;

        var omega = 2 * Math.PI * CutoffHertz / sampleRate;
        var alpha = Math.Sin(omega) / (2 * Math.Sqrt(0.5));
        var cos = Math.Cos(omega);
        var a0 = 1 + alpha;
        b0 = (1 - cos) / 2 / a0;
        b1 = (1 - cos) / a0;
        b2 = b0;
        a1 = -2 * cos / a0;
        a2 = (1 - alpha) / a0;
    }

    /// <summary>Bytes in one frame (one sample of every channel).</summary>
    public int BytesPerFrame => bytesPerFrame;

    /// <summary>Converts one block. A partial frame at the end is ignored (WASAPI delivers whole frames).</summary>
    /// <param name="input">Bytes in the device format.</param>
    /// <param name="output">Receives the converted samples; cleared first.</param>
    public void Convert(ReadOnlySpan<byte> input, List<float> output)
    {
        output.Clear();
        var frames = input.Length / bytesPerFrame;
        if (frames == 0)
        {
            return;
        }

        if (mono.Length < frames)
        {
            mono = new float[frames];
        }

        for (var frame = 0; frame < frames; frame++)
        {
            var bytes = input.Slice(frame * bytesPerFrame, bytesPerFrame);
            double sum = 0;
            for (var channel = 0; channel < channels; channel++)
            {
                sum += Read(bytes, channel);
            }

            var sample = sum / channels;
            mono[frame] = (float)(filtered ? Filter(sample) : sample);
        }

        // position runs in input frames from the start of this block; -1 is the last frame of the previous one.
        while (position <= frames - 1)
        {
            var index = (int)Math.Floor(position);
            var fraction = (float)(position - index);
            var a = index < 0 ? previous : mono[index];
            var b = index + 1 < frames ? mono[index + 1] : a;
            output.Add(a + ((b - a) * fraction));
            position += step;
        }

        position -= frames;
        previous = mono[frames - 1];
    }

    private static int BytesPerSample(SampleEncoding encoding) => encoding switch
    {
        SampleEncoding.Float32 or SampleEncoding.Pcm32 => 4,
        SampleEncoding.Pcm16 => 2,
        SampleEncoding.Pcm24 => 3,
        _ => throw new ArgumentOutOfRangeException(nameof(encoding)),
    };

    private double Read(ReadOnlySpan<byte> frame, int channel) => encoding switch
    {
        SampleEncoding.Float32 => MemoryMarshal.Read<float>(frame[(channel * 4)..]),
        SampleEncoding.Pcm16 => BinaryPrimitives.ReadInt16LittleEndian(frame[(channel * 2)..]) / 32768.0,
        SampleEncoding.Pcm24 => ReadInt24(frame[(channel * 3)..]) / 8388608.0,
        SampleEncoding.Pcm32 => BinaryPrimitives.ReadInt32LittleEndian(frame[(channel * 4)..]) / 2147483648.0,
        _ => 0,
    };

    private static int ReadInt24(ReadOnlySpan<byte> bytes) => (bytes[0] | (bytes[1] << 8) | (bytes[2] << 16)) << 8 >> 8;

    private double Filter(double x)
    {
        var y = (b0 * x) + (b1 * x1) + (b2 * x2) - (a1 * y1) - (a2 * y2);
        x2 = x1;
        x1 = x;
        y2 = y1;
        y1 = y;
        return y;
    }
}
