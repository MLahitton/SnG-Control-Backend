using SkiaSharp;
using SnG.Control.Application.Features.Measurements;
using SnG.Control.Infrastructure.Measurements;
namespace SnG.Control.Tests.Application;
public sealed class InstallationPdfTests
{
    [Fact]
    public void RendersLongUnicodeTechnicalSheetAndImmutableWebpWithoutChangingInput()
    {
        var id = Guid.NewGuid(); var sketchId = Guid.NewGuid();
        using var bitmap = new SKBitmap(1200, 900); using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White); using var pen = new SKPaint { Color = SKColors.DarkBlue, StrokeWidth = 4, Style = SKPaintStyle.Stroke, IsAntialias = true };
        canvas.DrawRect(220, 100, 700, 600, pen); canvas.DrawLine(570, 100, 570, 700, pen);
        using var font = new SKFont(SKTypeface.Default, 40); pen.Style = SKPaintStyle.Fill; canvas.DrawText("2000 mm", 400, 780, font, pen);
        using var image = SKImage.FromBitmap(bitmap); using var encoded = image.Encode(SKEncodedImageFormat.Webp, 90);
        var header = new MeasurementReportHeader(id, 1, "Proyecto sintético - validación de impresión", "SG-TEST", "Cliente sintético", new(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc), "Instalación sintética", 1);
        var row = new MeasurementStructureView(Guid.NewGuid(), Guid.NewGuid(), 1, 2, "V1", "Sistema cotizado", "Sala original", 2, 2.4m, null, header.SubmittedAt, header.SubmittedAt, null, 2.101m, 2.4m, "Sistema actualizado", Technical: new("Confirmed", 1100, "Outside", "HingedDoor", "Outward", "Left", true, 160), SketchVersionId: sketchId, TechnicalComplete: true, MeasurementType: "Agreed", ActualLocation: "Sala - costado norte", InstallationObservations: string.Join(" ", Enumerable.Repeat("Observación técnica extensa con acentos: ubicación, alfajía y revisión.", 50)), ComparisonStatus: "OverThreshold", WidthDifferenceMm: 101, HeightDifferenceMm: 0, Modification: new(Guid.NewGuid(), "Dimensional", "Cambio de vano", "Ajuste confirmado en visita técnica", sketchId, "evidence"), ModificationComplete: true);
        var snapshot = new MeasurementSnapshot(Guid.NewGuid(), 1, new(1, 0, 0, 1, 1, 0, 100), [row], [], header);
        var before = System.Text.Json.JsonSerializer.Serialize(snapshot);
        var bytes = new InstallationPdfRenderer().Render(snapshot, new Dictionary<Guid, byte[]> { [sketchId] = encoded.ToArray() }, default);
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4)); Assert.True(bytes.Length > 10000);
        Assert.Equal(before, System.Text.Json.JsonSerializer.Serialize(snapshot));
        Assert.Throws<InvalidOperationException>(() => new InstallationPdfRenderer().Render(snapshot, new Dictionary<Guid, byte[]>(), default));
        if (Environment.GetEnvironmentVariable("SNG_SYNTHETIC_PDF_OUTPUT") is { } output) File.WriteAllBytes(Path.ChangeExtension(output, ".layout.pdf"), bytes);
    }
}
