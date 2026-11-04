using System.Globalization;
using SkiaSharp;
using SnG.Control.Application.Features.Measurements;
namespace SnG.Control.Infrastructure.Measurements;

// Server-owned layout; immutable WEBP bytes are decoded in memory, never converted to public assets.
public sealed class InstallationPdfRenderer
{
    public byte[] Render(MeasurementSnapshot snapshot, IReadOnlyDictionary<Guid, byte[]> sketches, CancellationToken token)
    {
        var header = snapshot.Report ?? throw new ArgumentException("Report header required.");
        using var fontStream = typeof(InstallationPdfRenderer).Assembly.GetManifestResourceStream("SnG.Control.Infrastructure.Measurements.Fonts.NotoSans-Regular.ttf") ?? throw new InvalidOperationException("Report font missing.");
        using var face = SKTypeface.FromStream(fontStream);
        using var font = new SKFont(face, 10); using var title = new SKFont(face, 16);
        using var ink = new SKPaint { Color = new SKColor(30, 40, 50), IsAntialias = true };
        using var accent = new SKPaint { Color = new SKColor(20, 90, 120), IsAntialias = true };
        using var output = new MemoryStream();
        using var document = SKDocument.CreatePdf(output, new SKDocumentPdfMetadata { Title = "Reporte de Toma de Medidas - Steel & Glass", Author = "Steel & Glass", Creation = header.SubmittedAt });
        SKCanvas? canvas = null; float y = 0; int page = 0; string? structureContext = null;
        void NewPage()
        {
            if (canvas != null) document.EndPage();
            canvas = document.BeginPage(595, 842); page++; y = 82;
            canvas.DrawText("STEEL & GLASS", 42, 37, title, accent);
            canvas.DrawText("Reporte de Toma de Medidas", 42, 55, font, ink);
            canvas.DrawLine(42, 64, 553, 64, accent);
            if (structureContext != null) { canvas.DrawText(structureContext, 42, 78, font, ink); y = 100; }
            canvas.DrawText($"Radicación {header.SubmissionId} | v{header.Version} | Página {page}", 42, 816, font, ink);
        }
        void Line(string value, bool heading = false)
        {
            token.ThrowIfCancellationRequested();
            var f = heading ? title : font; var max = 511f;
            foreach (var paragraph in value.Replace("\r", "").Split('\n'))
            {
                var line = "";
                foreach (var element in StringInfo.GetTextElementEnumerator(paragraph).AsEnumerable())
                {
                    if (f.MeasureText(line + element) > max && line.Length > 0)
                    {
                        var split = line.LastIndexOf(' '); var outputLine = split > 0 ? line[..split] : line;
                        var remainder = split > 0 ? line[(split + 1)..] : "";
                        if (y > 785) NewPage(); canvas!.DrawText(outputLine, 42, y, f, heading ? accent : ink); y += heading ? 22 : 15; line = remainder;
                    }
                    line += element;
                }
                if (y > 785) NewPage(); canvas!.DrawText(line, 42, y, f, heading ? accent : ink); y += heading ? 24 : 15;
            }
        }
        string N(decimal? n) => n?.ToString("0.###", CultureInfo.InvariantCulture) ?? "Sin registrar";
        string Mm(decimal? n) => N(n * 1000m);
        string Text(string? v) => string.IsNullOrWhiteSpace(v) ? "Sin registrar" : v;
        string Label(string? value) => value switch { "Agreed" => "Acordada", "Production" => "Producción", "Opening" => "Vano", "NotApplicable" => "No aplica", "Confirmed" => "Confirmada", "Outside" => "Por fuera", "Inside" => "Por dentro", "HingedDoor" => "Puerta batiente", "HingedWindow" => "Ventana batiente", "Other" => "Otro", "Inward" => "Hacia adentro", "Outward" => "Hacia afuera", "Left" => "Izquierda", "Right" => "Derecha", _ => Text(value) };
        NewPage(); Line(header.ProjectName, true); Line($"Código: {Text(header.SgCode)} | Cliente: {header.ClientName}");
        Line($"Radicación: {header.SubmittedAt:yyyy-MM-dd HH:mm} UTC | Responsable: {header.ResponsibleName}");
        Line("Estado: Radicada - expediente inmutable"); y += 12;
        Line("Resumen del levantamiento", true);
        var summary = snapshot.Summary;
        Line($"Cotizadas: {summary.Initial} | Vigentes: {summary.Active} | Agregadas: {summary.Added}");
        Line($"Retiradas: {summary.Retired} | Reintegradas: {header.Reinstated} | Con modificación: {snapshot.Structures.Count(s => s.Modification != null)}");
        Line("Todas las longitudes se expresan en mm. Sin ajustes de fabricación.");
        foreach (var s in snapshot.Structures)
        {
            structureContext = $"Estructura {s.Id}"; NewPage(); Line($"{(s.ItemId == null ? "Agregada en obra" : "Ítem " + s.ItemNumber)} - Unidad {s.UnitOrdinal}/{s.UnitCount}", true);
            Line($"ID: {s.Id}"); Line(s.RetiredAt != null ? "RETIRADA - evidencia histórica" : s.TechnicalComplete ? "VIGENTE - ficha validada" : "VIGENTE - confirmación histórica; ficha incompleta según requisitos actuales");
            if (s.Modification != null) Line("ESTRUCTURA CON MODIFICACIÓN", true);
            Line($"Ubicación cotizada: {Text(s.Location)}"); Line($"Ubicación real: {Text(s.ActualLocation)}");
            Line($"Sistema cotizado: {s.Description}"); Line($"Sistema en obra: {Text(s.ActualSystemText)}");
            if (y + 80 > 785) NewPage();
            using (var border = new SKPaint { Color = new SKColor(190, 205, 215), Style = SKPaintStyle.Stroke, StrokeWidth = .5f })
            {
                string[][] table = [["Dimensiones (mm)", "Ancho", "Alto"], ["Cotizadas", Mm(s.Width), Mm(s.Height)], ["Registradas", Mm(s.ActualWidth), Mm(s.ActualHeight)]];
                foreach (var cells in table) { for (var column = 0; column < 3; column++) { var x = 42 + column * 170.33f; canvas!.DrawRect(x, y, 170.33f, 22, border); canvas.DrawText(cells[column], x + 6, y + 15, font, ink); } y += 22; }
                y += 16;
            }
            Line($"Tipo de medida: {Label(s.MeasurementType)}");
            var t = s.Technical;
            Line($"Manija: {Label(t?.HandleStatus)} | altura: {N(t?.HandleHeightMm)} mm");
            Line($"Vista: {Label(t?.ViewSide)} | Apertura: {Label(t?.OpeningType)}");
            Line($"Dirección: {Label(t?.OpeningDirection)} | Bisagras: {Label(t?.OpeningHand)}");
            Line($"Alfajía: {(t?.HasAlfajia is true ? "Sí - " + N(t.AlfajiaMeasurementMm) + " mm" : t?.HasAlfajia is false ? "No" : "Sin registrar")}");
            if (!string.IsNullOrWhiteSpace(s.InstallationObservations)) Line("Observaciones: " + s.InstallationObservations);
            Line(s.ComparisonStatus switch { "OverThreshold" => "Diferencia dimensional superior a 100 mm", "NoDimensionalChange" => "Sin modificación dimensional identificada", _ => "Comparación pendiente de revisión (no equivale a ausencia de modificación)" });
            if (s.WidthDifferenceMm != null) Line($"Diferencias absolutas: ancho {N(s.WidthDifferenceMm)} mm | alto {N(s.HeightDifferenceMm)} mm");
            if (s.Comparison != null) Line($"Revisión: {s.Comparison.Resolution} - {s.Comparison.Reason}");
            if (s.Modification is { } m) { Line($"Modificación {m.VersionId} - {m.Type}"); Line("Motivo: " + Text(m.Reason)); Line("Descripción: " + Text(m.Description)); Line($"Croquis asociado: {m.SketchVersionId}"); }
            var versionIds = new[] { s.SketchVersionId, s.Modification?.SketchVersionId }.Where(v => v != null).Select(v => v!.Value).Distinct();
            foreach (var versionId in versionIds)
            {
                if (!sketches.TryGetValue(versionId, out var bytes)) throw new InvalidOperationException("Snapshot sketch missing.");
                using var image = SKImage.FromEncodedData(bytes) ?? throw new InvalidOperationException("Snapshot sketch cannot be decoded.");
                if (image.Width != 1200 || image.Height != 900) throw new InvalidOperationException("Invalid sketch dimensions.");
                if (y + 410 > 785) NewPage(); Line($"Croquis de Instalación - versión {versionId}");
                canvas!.DrawImage(image, new SKRect(42, y, 553, y + 383.25f)); y += 395;
            }
        }
        document.EndPage(); document.Close(); return output.ToArray();
    }
}
internal static class TextElements
{
    internal static IEnumerable<string> AsEnumerable(this TextElementEnumerator elements) { while (elements.MoveNext()) yield return elements.GetTextElement(); }
}
