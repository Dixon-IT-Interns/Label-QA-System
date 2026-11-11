using System.Buffers.Binary;

namespace Dixon.LabelVerification.API.Services;

public static class ImageFileInspector
{
    public const long MaximumFileSizeBytes = 20 * 1024 * 1024;
    private static readonly byte[] PngSignature = [137, 80, 78, 71, 13, 10, 26, 10];

    public static (string ContentType, int Width, int Height) Inspect(
        ReadOnlySpan<byte> bytes,
        string fileName,
        string declaredContentType)
    {
        if (bytes.Length == 0)
        {
            throw new InspectionValidationException("The selected image is empty.");
        }

        if (bytes.Length > MaximumFileSizeBytes)
        {
            throw new InspectionValidationException("Images must be 20 MB or smaller.");
        }

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var (actualType, width, height) = bytes.StartsWith(PngSignature)
            ? InspectPng(bytes)
            : InspectJpeg(bytes);

        if ((actualType, extension, declaredContentType.ToLowerInvariant()) switch
            {
                ("image/png", ".png", "image/png") => false,
                ("image/jpeg", ".jpg" or ".jpeg", "image/jpeg") => false,
                _ => true
            })
        {
            throw new InspectionValidationException(
                "The image content, file extension, and declared MIME type must match JPEG or PNG.");
        }

        if (width < 320 || height < 240 || width > 20000 || height > 20000
            || (long)width * height > 100_000_000)
        {
            throw new InspectionValidationException(
                "Images must be at least 320 × 240 pixels and within supported dimensions.");
        }

        return (actualType, width, height);
    }

    private static (string, int, int) InspectPng(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 24 || !bytes.Slice(12, 4).SequenceEqual("IHDR"u8))
        {
            throw new InspectionValidationException("The PNG header is invalid.");
        }

        var width = BinaryPrimitives.ReadInt32BigEndian(bytes.Slice(16, 4));
        var height = BinaryPrimitives.ReadInt32BigEndian(bytes.Slice(20, 4));
        return ("image/png", width, height);
    }

    private static (string, int, int) InspectJpeg(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 4 || bytes[0] != 0xff || bytes[1] != 0xd8)
        {
            throw new InspectionValidationException("Only JPEG and PNG image contents are supported.");
        }

        var offset = 2;
        while (offset + 4 <= bytes.Length)
        {
            if (bytes[offset] != 0xff)
            {
                offset++;
                continue;
            }

            var markerStart = offset;
            while (offset < bytes.Length && bytes[offset] == 0xff) offset++;
            if (offset >= bytes.Length) break;
            var marker = bytes[offset++];
            if (marker is 0xd8 or 0xd9 or 0x01 or >= 0xd0 and <= 0xd7) continue;
            if (offset + 2 > bytes.Length) break;
            var segmentLength = BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(offset, 2));
            if (segmentLength < 2 || offset + segmentLength > bytes.Length) break;

            if (marker is 0xc0 or 0xc1 or 0xc2 or 0xc3 or 0xc5 or 0xc6 or 0xc7
                or 0xc9 or 0xca or 0xcb or 0xcd or 0xce or 0xcf)
            {
                if (segmentLength < 7)
                {
                    throw new InspectionValidationException("The JPEG dimensions are invalid.");
                }
                var height = BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(offset + 3, 2));
                var width = BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(offset + 5, 2));
                return ("image/jpeg", width, height);
            }

            offset += segmentLength;
            if (offset <= markerStart) break;
        }

        throw new InspectionValidationException("The JPEG image has no valid dimensions header.");
    }
}
