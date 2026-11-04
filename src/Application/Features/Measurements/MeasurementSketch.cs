using System.Buffers.Binary;
using System.Text.Json;
using SnG.Control.Application.Features.ProjectDocuments;

namespace SnG.Control.Application.Features.Measurements;

public sealed record SketchPoint(double X, double Y);
public sealed record SketchStroke(string Tool, string Color, double Width, SketchPoint[] Points);
public sealed record SketchDrawing(int Version, int Width, int Height, SketchStroke[] Strokes);
public sealed record SketchUpload(SketchDrawing Drawing, byte[] Webp);
public sealed record SketchRead(Guid Id, Guid StructureId, long Revision, SketchDrawing Drawing, string WebpSha256);
public static class MeasurementSketch
{
    public const int MaxRequestBytes = 2_000_000;
    public static void Validate(SketchUpload upload)
    {
        var d = upload.Drawing; var bytes = upload.Webp;
        if (d is null || d.Version != 1 || d.Width != 1200 || d.Height != 900 || d.Strokes is null || d.Strokes.Length > 200) Invalid();
        var count = 0;
        foreach (var stroke in d!.Strokes)
        {
            if (stroke is null || stroke.Tool is not ("Pen" or "Eraser") || stroke.Color is not ("#111111" or "#0066cc" or "#cc2222")
                || !double.IsFinite(stroke.Width) || stroke.Width < 1 || stroke.Width > 20 || stroke.Points is null || stroke.Points.Length is < 1 or > 2000) Invalid();
            count += stroke!.Points.Length;
            if (count > 20000 || stroke.Points.Any(p => p is null || !double.IsFinite(p.X) || !double.IsFinite(p.Y) || p.X < 0 || p.X > 1 || p.Y < 0 || p.Y > 1)) Invalid();
        }
        if (JsonSerializer.SerializeToUtf8Bytes(d).Length > 1_000_000 || bytes is null || bytes.Length is < 30 or > 512_000) Invalid();
        // The editor exports opaque, nonanimated, metadata-free lossy WEBP. Reject other containers/chunks.
        var b = bytes!.AsSpan();
        if (!b[..4].SequenceEqual("RIFF"u8) || !b.Slice(8, 4).SequenceEqual("WEBP"u8) || !b.Slice(12, 4).SequenceEqual("VP8 "u8)
            || BinaryPrimitives.ReadUInt32LittleEndian(b[4..]) != b.Length - 8) Invalid();
        var length = BinaryPrimitives.ReadUInt32LittleEndian(b[16..]);
        if (length < 10 || (long)length + (length % 2) + 20 != b.Length || (b[20] & 1) != 0
            || !b.Slice(23, 3).SequenceEqual(new byte[] { 0x9d, 0x01, 0x2a })
            || (BinaryPrimitives.ReadUInt16LittleEndian(b[26..]) & 0x3fff) != d.Width
            || (BinaryPrimitives.ReadUInt16LittleEndian(b[28..]) & 0x3fff) != d.Height) Invalid();
    }
    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    private static void Invalid() => throw new DocumentException(400, "InvalidMeasurementSketch");
}
