using System.Globalization;
using SkiaSharp;
using SnG.Control.Application.Features.Measurements;
namespace SnG.Control.Infrastructure.Measurements;

public sealed record InstallationReportRenderData(MeasurementSnapshot Snapshot,
    IReadOnlyDictionary<Guid, byte[]> Sketches, IReadOnlyDictionary<Guid, byte[]> QuotationImages);

// All assets are embedded from validated historical bytes. No database or external URL access.
public sealed class InstallationPdfRenderer
{
    private const float PageWidth = 1190.55f, PageHeight = 841.89f, Margin = 28, Bottom = 805;
    private const float LeftWidth = 310, CenterWidth = 310, LineHeight = 11;
    public const int MaxImageBytes = 16 * 1024 * 1024;
    private const long MaxImagePixels = 24_000_000;

    public byte[] Render(MeasurementSnapshot snapshot, IReadOnlyDictionary<Guid, byte[]> sketches, CancellationToken token) =>
        Render(new(snapshot, sketches, new Dictionary<Guid, byte[]>()), token);

    public byte[] Render(InstallationReportRenderData data, CancellationToken token)
    {
        var snapshot = data.Snapshot;
        var header = snapshot.Report ?? throw new ArgumentException("Report header required.");
        using var fontStream = typeof(InstallationPdfRenderer).Assembly.GetManifestResourceStream("SnG.Control.Infrastructure.Measurements.Fonts.NotoSans-Regular.ttf") ?? throw new InvalidOperationException("Report font missing.");
        using var face = SKTypeface.FromStream(fontStream);
        using var font = new SKFont(face, 9);
        using var title = new SKFont(face, 15);
        using var ink = new SKPaint { Color = new SKColor(30, 40, 50), IsAntialias = true };
        using var accent = new SKPaint { Color = new SKColor(20, 90, 120), IsAntialias = true };
        using var border = new SKPaint { Color = new SKColor(180, 195, 202), Style = SKPaintStyle.Stroke, StrokeWidth = .5f };
        var rightX = Margin + LeftWidth + CenterWidth;
        var rightWidth = PageWidth - Margin - rightX;
        var headerLines = new[] {
            $"Proyecto: {header.ProjectName} | SG: {Text(header.SgCode)} | Cliente: {header.ClientName}",
            $"Radicación: {header.SubmittedAt:yyyy-MM-dd HH:mm} UTC | Responsable: {header.ResponsibleName}",
            $"ID: {header.SubmissionId} | Versión: {header.Version} | Longitudes en mm",
            $"Cotizadas: {snapshot.Summary.Initial} | Vigentes: {snapshot.Summary.Active} | Agregadas: {snapshot.Summary.Added} | Retiradas: {snapshot.Summary.Retired} | Reintegradas: {header.Reinstated}"
        }.SelectMany(s => Wrap(s, font, PageWidth - 2 * Margin)).ToArray();
        var top = 53 + headerLines.Length * LineHeight + 10;
        var rowHeight = (Bottom - top) / 3;
        var capacity = (int)((rowHeight - 40) / LineHeight);
        if (capacity < 8) throw new InvalidOperationException("Report header exceeds available space.");
        // Plan before drawing, so totals and continuation boundaries are exact.
        var pages = new List<List<Row>>();
        var current = new List<Row>();
        foreach (var structure in snapshot.Structures)
        {
            token.ThrowIfCancellationRequested();
            var lines = TechnicalLines(structure).SelectMany(s => Wrap(s, font, rightWidth - 20)).ToArray();
            var versions = new[] { structure.SketchVersionId, structure.Modification?.SketchVersionId }.OfType<Guid>().Distinct().ToArray();
            var offset = 1; var part = 0;
            do
            {
                if (part > 0 && current.Count > 0) { pages.Add(current); current = []; }
                var bodyCapacity = part == 0 ? capacity - 1 : (int)((Bottom - top - 40) / LineHeight) - 1;
                current.Add(new(structure, new[] { lines[0] }.Concat(lines.Skip(offset).Take(bodyCapacity)).ToArray(), part,
                    versions.Length == 0 ? null : versions[Math.Min(part, versions.Length - 1)]));
                offset += bodyCapacity;
                part++;
                if (current.Count == 3 || part > 1 || offset < lines.Length || part < versions.Length) { pages.Add(current); current = []; }
            } while (offset < lines.Length || part < versions.Length);
        }
        if (current.Count > 0) pages.Add(current);
        if (pages.Count == 0) pages.Add([]);
        using var output = new MemoryStream();
        using var document = SKDocument.CreatePdf(output, new SKDocumentPdfMetadata { Title = "Reporte de Toma de Medidas - Steel & Glass", Author = "Steel & Glass", Creation = header.SubmittedAt });
        for (var page = 0; page < pages.Count; page++)
        {
            token.ThrowIfCancellationRequested();
            var canvas = document.BeginPage(PageWidth, PageHeight);
            canvas.DrawText("STEEL & GLASS", Margin, 28, title, accent);
            canvas.DrawText("REPORTE DE TOMA DE MEDIDAS", 760, 28, title, accent);
            var y = 49f;
            foreach (var line in headerLines) { canvas.DrawText(line, Margin, y, font, ink); y += LineHeight; }
            canvas.DrawLine(Margin, top - 5, PageWidth - Margin, top - 5, accent);
            for (var rowIndex = 0; rowIndex < pages[page].Count; rowIndex++)
            {
                var row = pages[page][rowIndex]; var s = row.Structure;
                var rowTop = top + rowIndex * rowHeight;
                var height = row.Part > 0 ? Bottom - top : rowHeight;
                canvas.DrawRect(Margin, rowTop, PageWidth - 2 * Margin, height, border);
                canvas.DrawLine(Margin + LeftWidth, rowTop, Margin + LeftWidth, rowTop + height, border);
                canvas.DrawLine(rightX, rowTop, rightX, rowTop + height, border);
                canvas.DrawText("CROQUIS DE INSTALACIÓN", Margin + 10, rowTop + 14, font, accent);
                canvas.DrawText("IMAGEN ORIGINAL DE COTIZACIÓN", Margin + LeftWidth + 10, rowTop + 14, font, accent);
                canvas.DrawText((row.Part > 0 ? "FICHA TÉCNICA - CONTINUACIÓN" : "FICHA TÉCNICA") + (s.Modification != null ? " | CON MODIFICACIÓN" : ""), rightX + 10, rowTop + 14, font, accent);
                y = rowTop + 29;
                foreach (var line in row.Lines) { canvas.DrawText(line, rightX + 10, y, font, line.Contains(" | ") ? accent : ink); y += LineHeight; }
                canvas.DrawText(s.Id.ToString(), rightX + 10, rowTop + height - 7, font, accent);
                var imageBox = new SKRect(Margin + 10, rowTop + 33, Margin + LeftWidth - 10, rowTop + height - 10);
                if (row.SketchVersion is { } version)
                {
                    if (!data.Sketches.TryGetValue(version, out var bytes)) throw new InvalidOperationException("Snapshot sketch missing.");
                    canvas.DrawText(version.ToString(), Margin + 10, rowTop + 27, font, ink);
                    DrawImage(canvas, bytes, imageBox, true);
                }
                else DrawMessage(canvas, "Sin croquis de obra", imageBox, font, ink);
                imageBox.Offset(LeftWidth, 0);
                if (s.ItemId is { } itemId && data.QuotationImages.TryGetValue(itemId, out var commercial))
                    DrawImage(canvas, commercial, imageBox, false);
                else DrawMessage(canvas, s.ItemId == null ? "Estructura agregada en obra - sin imagen de cotización" : "Imagen histórica de cotización no disponible", imageBox, font, ink);
            }
            canvas.DrawText($"{Text(header.SgCode)} | Radicación {header.SubmissionId} | v{header.Version}", Margin, 826, font, ink);
            canvas.DrawText($"Página {page + 1} de {pages.Count}", PageWidth - 120, 826, font, ink);
            document.EndPage();
        }
        document.Close(); return output.ToArray();
    }

    private sealed record Row(MeasurementStructureView Structure, string[] Lines, int Part, Guid? SketchVersion);

    private static IEnumerable<string> TechnicalLines(MeasurementStructureView s)
    {
        yield return (s.ItemId == null ? "Agregada en obra" : "Ítem " + Text(s.ItemNumber)) + $" | Unidad {s.UnitOrdinal}/{s.UnitCount} | " +
            (s.RetiredAt != null ? "RETIRADA" : s.TechnicalComplete ? "VIGENTE" : "VIGENTE - ficha histórica incompleta") + (s.Modification != null ? " | CON MODIFICACIÓN" : "");
        yield return $"UBICACIÓN | Cotizada: {Text(s.Location)}";
        yield return $"En obra: {Text(s.ActualLocation)}";
        yield return $"SISTEMA | Cotizado: {Text(s.Description)}";
        yield return $"En obra: {Text(s.ActualSystemText)}";
        yield return $"DIMENSIONES (mm) | Cotizadas: ancho {Mm(s.Width)} / alto {Mm(s.Height)}";
        yield return $"Registradas: ancho {Mm(s.ActualWidth)} / alto {Mm(s.ActualHeight)} | Tipo: {Label(s.MeasurementType)}";
        var t = s.Technical;
        yield return $"DETALLES | Manija: {Label(t?.HandleStatus)} | Altura: {N(t?.HandleHeightMm)} mm";
        yield return $"Vista: {Label(t?.ViewSide)} | Apertura: {Label(t?.OpeningType)}";
        yield return $"Dirección: {Label(t?.OpeningDirection)} | Bisagras: {Label(t?.OpeningHand)}";
        yield return $"Alfajía: {(t?.HasAlfajia is true ? N(t.AlfajiaMeasurementMm) + " mm" : t?.HasAlfajia is false ? "No" : "Sin registrar")}";
        yield return s.ComparisonStatus switch { "OverThreshold" => "COMPARACIÓN | Diferencia superior a 100 mm", "NoDimensionalChange" => "COMPARACIÓN | Sin modificación dimensional identificada", _ => "COMPARACIÓN | Pendiente de revisión; no implica ausencia de modificación" };
        if (s.WidthDifferenceMm != null) yield return $"Diferencias absolutas: ancho {N(s.WidthDifferenceMm)} / alto {N(s.HeightDifferenceMm)} mm";
        if (s.Comparison != null) yield return $"Revisión: {Label(s.Comparison.Resolution)} - {s.Comparison.Reason}";
        if (s.Modification is { } m)
        {
            yield return $"MODIFICACIÓN | {Label(m.Type)} | Versión {m.VersionId}";
            yield return "Motivo: " + Text(m.Reason);
            yield return "Descripción: " + Text(m.Description);
            yield return $"Croquis de modificación: {m.SketchVersionId}";
        }
        if (!string.IsNullOrWhiteSpace(s.InstallationObservations)) yield return "OBSERVACIONES | " + s.InstallationObservations;
    }

    private static void DrawImage(SKCanvas canvas, byte[] bytes, SKRect box, bool sketch)
    {
        if (bytes.Length > MaxImageBytes) throw new InvalidOperationException("Report image exceeds byte limit.");
        using var encoded = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(encoded) ?? throw new InvalidOperationException("Report image cannot be decoded.");
        var size = codec.Info;
        if (size.Width <= 0 || size.Height <= 0 || (long)size.Width * size.Height > MaxImagePixels ||
            sketch && (size.Width != 1200 || size.Height != 900)) throw new InvalidOperationException("Invalid report image dimensions.");
        using var image = SKImage.FromEncodedData(encoded) ?? throw new InvalidOperationException("Report image cannot be decoded.");
        var scale = Math.Min(box.Width / size.Width, box.Height / size.Height);
        var width = size.Width * scale; var height = size.Height * scale;
        canvas.DrawImage(image, SKRect.Create(box.MidX - width / 2, box.MidY - height / 2, width, height));
    }
    private static void DrawMessage(SKCanvas canvas, string text, SKRect box, SKFont font, SKPaint paint)
    {
        var lines = Wrap(text, font, box.Width).ToArray(); var y = box.MidY - lines.Length * LineHeight / 2;
        foreach (var line in lines) { canvas.DrawText(line, box.Left, y, font, paint); y += LineHeight; }
    }
    private static IEnumerable<string> Wrap(string value, SKFont font, float width)
    {
        foreach (var paragraph in value.Replace("\r", "").Split('\n'))
        {
            var line = "";
            var elements = StringInfo.GetTextElementEnumerator(paragraph);
            while (elements.MoveNext())
            {
                var element = elements.GetTextElement();
                if (font.MeasureText(line + element) > width && line.Length > 0)
                {
                    var split = line.LastIndexOf(' ');
                    yield return split > 0 ? line[..split] : line;
                    line = split > 0 ? line[(split + 1)..] : "";
                }
                line += element;
            }
            yield return line;
        }
    }
    private static string N(decimal? n) => n?.ToString("0.###", CultureInfo.InvariantCulture) ?? "Sin registrar";
    private static string Mm(decimal? n) => N(n * 1000m);
    private static string Text(string? value) => string.IsNullOrWhiteSpace(value) ? "Sin registrar" : value;
    private static string Label(string? value) => value switch {
        "Agreed" => "Acordada", "Production" => "Producción", "Opening" => "Vano", "NotApplicable" => "No aplica", "Confirmed" => "Confirmada",
        "Outside" => "Por fuera", "Inside" => "Por dentro", "HingedDoor" => "Puerta batiente", "HingedWindow" => "Ventana batiente",
        "Other" => "Otro", "Inward" => "Hacia adentro", "Outward" => "Hacia afuera", "Left" => "Izquierda", "Right" => "Derecha",
        "Comparable" => "Comparable", "NotComparable" => "No comparable", "Dimensional" => "Dimensional", "System" => "Sistema",
        "Typology" => "Tipología", "Configuration" => "Configuración", "Structural" => "Estructural", _ => Text(value) };
}
