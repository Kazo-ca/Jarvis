namespace Kazo.Jarvis.Kernel.Models;

/// <summary>
/// One file to download into the models folder (ADR 0006, section 7): where it comes from, where it goes, its
/// size and its published checksum. Nothing is kept unless the checksum matches.
/// </summary>
/// <param name="Source">The address of the file, an <c>https</c> one.</param>
/// <param name="Target">
/// Where it goes, relative to the models folder, with <c>/</c> as separator. For a file, its path
/// (<c>whisper/ggml-base.bin</c>); for an archive, the folder it unpacks to (<c>vits-piper-fr_FR-tom-medium</c>).
/// </param>
/// <param name="Size">Size of the download in bytes, shown before it starts and checked at the end.</param>
/// <param name="Checksum">The expected hash, <c>sha256:&lt;hex&gt;</c> or <c>sha1:&lt;hex&gt;</c> (the one the publisher gives).</param>
/// <param name="Format">A plain file, or an archive to unpack.</param>
public sealed record ModelDownload(Uri Source, string Target, long Size, string Checksum, ModelDownloadFormat Format = ModelDownloadFormat.File);

/// <summary>What a download is.</summary>
public enum ModelDownloadFormat
{
    /// <summary>A file kept as it is, at <see cref="ModelDownload.Target"/>.</summary>
    File,

    /// <summary>
    /// A <c>.tar.bz2</c> archive whose single top folder is <see cref="ModelDownload.Target"/>, as sherpa-onnx
    /// publishes its voices. The archive is deleted once unpacked.
    /// </summary>
    TarBz2,
}
