using SkiaSharp;
using SnG.Control.Application.Features.Measurements;
using SnG.Control.Infrastructure.Measurements;
namespace SnG.Control.Tests.Application;
public sealed class InstallationPdfTests
{
    [Theory]
    [InlineData(1, 1)] [InlineData(2, 1)] [InlineData(3, 1)] [InlineData(4, 2)]
    [InlineData(6, 2)] [InlineData(7, 3)] [InlineData(21, 7)]
    public void A3LandscapeHasThreeRowsPerPage(int count, int pages)
    {
        var snapshot = Sample(count);
        var bytes = new InstallationPdfRenderer().Render(snapshot, new Dictionary<Guid, byte[]>(), default);
        Assert.Equal(pages, PageCount(bytes));
        var raw = System.Text.Encoding.Latin1.GetString(bytes);
        var box = System.Text.RegularExpressions.Regex.Match(raw, @"/MediaBox \[0 0 ([0-9.]+) ([0-9.]+)\]");
        Assert.True(box.Success);
        Assert.InRange(float.Parse(box.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture), 1190f, 1191f);
        Assert.InRange(float.Parse(box.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture), 841f, 842f);
        Save($"normal-{count}", bytes);
    }

    [Fact]
    public void EmbedsPortraitLandscapeAndSharedItemImagesAlongsideExactSketchVersions()
    {
        var snapshot = Sample(3);
        var sketch = Guid.NewGuid(); var item = snapshot.Structures[0].ItemId!.Value;
        snapshot = snapshot with { Structures = snapshot.Structures.Select((s, i) => s with { ItemId = i < 2 ? item : s.ItemId, SketchVersionId = sketch }).ToArray() };
        var portrait = Drawing(200, 400); var landscape = Drawing(500, 150); var webp = Drawing(1200, 900);
        var images = new Dictionary<Guid, byte[]> { [item] = portrait, [snapshot.Structures[2].ItemId!.Value] = landscape };
        var before = System.Text.Json.JsonSerializer.Serialize(snapshot);
        var bytes = new InstallationPdfRenderer().Render(new(snapshot, new Dictionary<Guid, byte[]> { [sketch] = webp }, images), default);
        Assert.Equal(1, PageCount(bytes));
        var raw = System.Text.Encoding.Latin1.GetString(bytes);
        Assert.Contains("/Width 200", raw); Assert.Contains("/Height 400", raw);
        Assert.Contains("/Width 500", raw); Assert.Contains("/Height 150", raw);
        Assert.Contains("/Width 1200", raw); Assert.Contains("/Height 900", raw);
        Assert.DoesNotContain("/URI", raw);
        Assert.Equal(before, System.Text.Json.JsonSerializer.Serialize(snapshot));
        Assert.Equal(portrait, images[item]);
        Save("three-drawings", bytes);
    }

    [Fact]
    public void MissingCommercialImagesAndAddedRetiredHistoricalRowsRemainPrintable()
    {
        var snapshot = Sample(3);
        snapshot = snapshot with { Structures = [
            snapshot.Structures[0] with { ItemId = null },
            snapshot.Structures[1] with { RetiredAt = snapshot.Report!.SubmittedAt },
            snapshot.Structures[2] with { Technical = null, ActualWidth = null, ActualHeight = null, ActualSystemText = null, TechnicalComplete = false }
        ] };
        var bytes = new InstallationPdfRenderer().Render(snapshot, new Dictionary<Guid, byte[]>(), default);
        Assert.Equal(1, PageCount(bytes)); Save("historical", bytes);
    }

    [Fact]
    public void RejectsCorruptOrOversizedAssetsAndMissingReferencedSketch()
    {
        var snapshot = Sample(1); var item = snapshot.Structures[0].ItemId!.Value;
        foreach (var invalid in new byte[][] { [1, 2, 3], new byte[InstallationPdfRenderer.MaxImageBytes + 1] })
            Assert.Throws<InvalidOperationException>(() => new InstallationPdfRenderer().Render(new(snapshot, new Dictionary<Guid, byte[]>(), new Dictionary<Guid, byte[]> { [item] = invalid }), default));
        var sketch = Guid.NewGuid();
        snapshot = snapshot with { Structures = [snapshot.Structures[0] with { SketchVersionId = sketch }] };
        Assert.Throws<InvalidOperationException>(() => new InstallationPdfRenderer().Render(snapshot, new Dictionary<Guid, byte[]> { [sketch] = Drawing(30, 30) }, default));
    }

    private static MeasurementSnapshot Sample(int count)
    {
        var at = new DateTime(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);
        var rows = Enumerable.Range(1, count).Select(i => new MeasurementStructureView(Guid.NewGuid(), Guid.NewGuid(), 1, 1, $"V{i}",
            "Ventana corrediza", "Sala", 2, 2.4m, null, at, at, null, 2, 2.4m, "Ventana corrediza",
            Technical: new("Confirmed", 1100, "Outside", "HingedDoor", "Outward", "Left", true, 160),
            TechnicalComplete: true, MeasurementType: "Agreed", ActualLocation: "Sala norte", ComparisonStatus: "NoDimensionalChange")).ToArray();
        return new(Guid.NewGuid(), 1, new(count, 0, 0, count, count, 0, 100), rows, [],
            new(Guid.NewGuid(), 1, "Proyecto sintético", "SG-TEST", "Cliente sintético", at, "Instalación sintética", 0));
    }
    private static byte[] Drawing(int width, int height)
    {
        using var bitmap = new SKBitmap(width, height); using var canvas = new SKCanvas(bitmap); canvas.Clear(SKColors.White);
        using var pen = new SKPaint { Color = SKColors.DarkBlue, StrokeWidth = 3, Style = SKPaintStyle.Stroke };
        canvas.DrawRect(15, 15, width - 30, height - 30, pen);
        canvas.DrawLine(width / 2, 15, width / 2, height - 15, pen);
        using var font = new SKFont(SKTypeface.Default, Math.Max(12, width / 30));
        pen.Style = SKPaintStyle.Fill; canvas.DrawText("2000 mm", 20, height / 2, font, pen);
        using var image = SKImage.FromBitmap(bitmap); using var encoded = image.Encode(SKEncodedImageFormat.Webp, 90); return encoded.ToArray();
    }
    private static int PageCount(byte[] bytes) => System.Text.RegularExpressions.Regex.Matches(System.Text.Encoding.Latin1.GetString(bytes), @"/Type /Page\b").Count;
    private static void Save(string name, byte[] bytes)
    {
        if (Environment.GetEnvironmentVariable("SNG_SYNTHETIC_PDF_OUTPUT") is { } output)
            File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(output)!, $"installation-{name}.pdf"), bytes);
    }
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
        Assert.True(PageCount(bytes) > 1);
        Assert.Throws<InvalidOperationException>(() => new InstallationPdfRenderer().Render(snapshot, new Dictionary<Guid, byte[]>(), default));
        if (Environment.GetEnvironmentVariable("SNG_SYNTHETIC_PDF_OUTPUT") is { } output) File.WriteAllBytes(Path.ChangeExtension(output, ".layout.pdf"), bytes);
    }
}
