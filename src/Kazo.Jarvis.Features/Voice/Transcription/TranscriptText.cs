using System.Text;
using Kazo.Jarvis.Kernel.Voice;

namespace Kazo.Jarvis.Features.Voice.Transcription;

/// <summary>
/// Turns Whisper's segments into the text of the request: markers such as <c>[BLANK_AUDIO]</c> or
/// <c>(musique)</c> are dropped, so are the segments Whisper itself thinks hold no speech.
/// </summary>
public static class TranscriptText
{
    /// <summary>
    /// Above this no-speech probability a segment is dropped. Not set by an ADR: 0.8 (provisional choice of work
    /// item stage1/10-transcription); Whisper tends to invent a sentence on noise.
    /// </summary>
    public const float NoSpeechThreshold = 0.8f;

    /// <summary>Joins the segments that hold speech, without markers, trimmed.</summary>
    /// <param name="segments">The segments, in order.</param>
    /// <returns>The text; empty when nothing was said.</returns>
    public static string From(IEnumerable<TranscriptSegment> segments)
    {
        ArgumentNullException.ThrowIfNull(segments);
        var text = new StringBuilder();
        foreach (var segment in segments)
        {
            if (segment.NoSpeechProbability > NoSpeechThreshold)
            {
                continue;
            }

            var cleaned = WithoutMarkers(segment.Text);
            if (cleaned.Length > 0)
            {
                text.Append(text.Length > 0 ? " " : string.Empty).Append(cleaned);
            }
        }

        return text.ToString();
    }

    /// <summary>Removes the parts between brackets or parentheses Whisper uses for sounds, and the extra spaces.</summary>
    private static string WithoutMarkers(string text)
    {
        var kept = new StringBuilder(text.Length);
        var depth = 0;
        foreach (var character in text)
        {
            switch (character)
            {
                case '[' or '(':
                    depth++;
                    break;
                case ']' or ')' when depth > 0:
                    depth--;
                    break;
                default:
                    if (depth == 0)
                    {
                        kept.Append(character);
                    }

                    break;
            }
        }

        return string.Join(' ', kept.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }
}
