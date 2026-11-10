using System.Globalization;
using System.Windows.Media.Imaging;

namespace SylverInk.FileIO;

public static class BitmapUtils
{
    private static int GetGlobalColorTableSize(byte[] gif)
    {
        byte packed = gif[10];
        return (packed & 0x80) != 0
            ? 3 * (1 << ((packed & 0x07) + 1))
            : 0;
    }
    private static bool GetMetadataBool(BitmapMetadata? metadata, string query)
    {
        if (metadata is null)
            return false;

        try
        {
            object value = metadata.GetQuery(query);
            return value is bool b && b;
        }
        catch
        {
            return false;
        }
    }

    private static byte? GetMetadataByte(BitmapMetadata? metadata, string query)
    {
        if (metadata is null)
            return null;

        try
        {
            object value = metadata.GetQuery(query);
            return value is null
                ? null
                : Convert.ToByte(value, CultureInfo.InvariantCulture);
        }
        catch
        {
            return null;
        }
    }

    public static T GetQuery<T>(BitmapMetadata? metadata, string path, T defaultValue) where T : struct
    {
        if (metadata is null)
            return defaultValue;

        try
        {
            object? value = metadata.GetQuery(path);

            if (value is T typed)
                return typed;

            return value is null
                ? defaultValue
                : (T)Convert.ChangeType(value, typeof(T), CultureInfo.CurrentCulture);
        }
        catch (Exception ex)
        {
            App.LogException(ex);
            return defaultValue;
        }
    }

    public static byte[] RewriteGif(byte[] gifData, List<long> FrameTiming, List<BitmapMetadata?> frameMetadata)
    {
        if (gifData == null || gifData.Length < 13)
            throw new InvalidDataException("Not a valid GIF stream.");

        // Header (6 bytes) + Logical Screen Descriptor (7 bytes).
        int globalColorTableSize = GetGlobalColorTableSize(gifData);
        int position = 13 + globalColorTableSize;

        if (position > gifData.Length)
            throw new InvalidDataException("Truncated GIF header.");

        // Approximate capacity: original data + GCE per frame + loop extension.
        var output = new List<byte>(gifData.Length + 19 + FrameTiming.Count * 8);

        // Copy GIF header, logical screen descriptor, and global color table.
        output.AddRange(gifData.AsSpan(0, position));

        // Animated GIFs normally want a Netscape loop extension.
        WriteNetscapeLoopExtension(output);

        int frameIndex = 0;

        while (position < gifData.Length)
        {
            byte introducer = gifData[position];
            switch (introducer)
            {
                case 0x21: // Extension block.
                    if (position + 1 >= gifData.Length)
                        throw new InvalidDataException("Truncated GIF extension.");

                    byte label = gifData[position + 1];

                    if (label == 0xF9) // Graphic Control Extension.
                    {
                        // Discard the old GCE. We write our own before each image.
                        if (position + 7 >= gifData.Length || gifData[position + 2] != 4)
                            throw new InvalidDataException("Invalid GIF Graphic Control Extension.");

                        position += 8;
                        continue;
                    }

                    // Copy other extensions verbatim.
                    int extensionStart = position;
                    position += 2;
                    SkipSubBlocks(gifData, ref position);

                    output.AddRange(gifData.AsSpan(extensionStart, position - extensionStart));
                    continue;
                case 0x2C: // Image descriptor.
                    if (frameIndex >= FrameTiming.Count)
                        throw new InvalidDataException("GIF has more frames than supplied delays.");

                    WriteGraphicControlExtension(output, FrameTiming[frameIndex] / 100_000L, frameMetadata?[frameIndex]);

                    int imageStart = position;

                    if (position + 9 >= gifData.Length)
                        throw new InvalidDataException("Truncated GIF image descriptor.");

                    byte imagePacked = gifData[position + 9];
                    position += 10;

                    // Skip optional local color table.
                    if ((imagePacked & 0x80) != 0)
                        position += 3 * (1 << ((imagePacked & 0x07) + 1));

                    position++; // LZW minimum code size.
                    SkipSubBlocks(gifData, ref position);

                    // Copy image descriptor, local color table, and LZW data.
                    output.AddRange(gifData.AsSpan(imageStart, position - imageStart));

                    frameIndex++;
                    continue;
                case 0x3B: // Trailer.
                    output.Add(introducer);
                    position = gifData.Length;
                    break;
                default:
                    throw new InvalidDataException("Unexpected GIF block introducer.");
            }
        }

        if (frameIndex != FrameTiming.Count)
            throw new InvalidDataException("GIF has fewer frames than supplied delays.");

        return [.. output];
    }

    private static void SkipSubBlocks(byte[] gif, ref int position)
    {
        while (true)
        {
            if (position >= gif.Length)
                throw new InvalidDataException("Truncated GIF sub-block.");

            int count = gif[position++];
            if (count == 0)
                return;

            if (position + count > gif.Length)
                throw new InvalidDataException("Truncated GIF sub-block data.");

            position += count;
        }
    }

    public static BitmapSource Snapshot(BitmapSource source)
    {
        var snapshot = new WriteableBitmap(source);
        snapshot.Freeze();
        return snapshot;
    }

    private static void WriteGraphicControlExtension(List<byte> output, long delay, BitmapMetadata? metadata)
    {
        byte packed = 0;
        byte transparentIndex = 0;

        byte? disposal = GetMetadataByte(metadata, "/grctlext/Disposal");
        if (disposal.HasValue)
            packed |= (byte)((disposal.Value & 0x07) << 2);

        if (GetMetadataBool(metadata, "/grctlext/TransparencyFlag"))
        {
            packed |= 0x01;
            byte? index = GetMetadataByte(metadata, "/grctlext/TransparentColorIndex");
            if (index.HasValue)
                transparentIndex = index.Value;
        }

        output.Add(0x21);              // Extension introducer.
        output.Add(0xF9);              // Graphic Control Extension label.
        output.Add(0x04);              // Block size.
        output.Add(packed);            // Disposal / transparency flags.
        output.Add((byte)(delay & 0xFF)); // Delay, little-endian.
        output.Add((byte)(delay >> 8));
        output.Add(transparentIndex);
        output.Add(0x00);              // Block terminator.
    }

    private static void WriteNetscapeLoopExtension(List<byte> output)
    {
        output.AddRange([
            0x21, 0xFF, 0x0B, // Application extension, 11-byte identifier.
            (byte)'N', (byte)'E', (byte)'T', (byte)'S', (byte)'C', (byte)'A', (byte)'P', (byte)'E',
            (byte)'2', (byte)'.', (byte)'0',
            0x03, 0x01,       // Sub-block size and loop sub-block id.
            0x00, 0x00,       // Loop count: 0 (loop indefinitely).
            0x00              // Terminator.
        ]);
    }
}
