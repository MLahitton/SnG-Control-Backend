using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;

namespace SnG.Control.Api.Contracts;

internal sealed record QuotationUpload(byte[] File, string FileName, byte[]? Confirmation);

internal static class QuotationMultipartReader
{
    public static async Task<QuotationUpload?> ReadAsync(HttpRequest request, bool withConfirmation, long limit, CancellationToken cancellationToken)
    {
        if (!MediaTypeHeaderValue.TryParse(request.ContentType, out var contentType)
            || !contentType.MediaType.Equals("multipart/form-data", StringComparison.OrdinalIgnoreCase)) return null;
        var boundary = HeaderUtilities.RemoveQuotes(contentType.Boundary).Value;
        if (string.IsNullOrEmpty(boundary) || boundary.Length > 128) return null;
        byte[]? file = null, confirmation = null;
        string? fileName = null;
        long total = 0;
        try
        {
            var reader = new MultipartReader(boundary, request.Body) { BodyLengthLimit = limit };
            MultipartSection? part;
            while ((part = await reader.ReadNextSectionAsync(cancellationToken)) is not null)
            {
                if (!ContentDispositionHeaderValue.TryParse(part.ContentDisposition, out var disposition)
                    || !disposition.DispositionType.Equals("form-data", StringComparison.OrdinalIgnoreCase)) return null;
                var name = HeaderUtilities.RemoveQuotes(disposition.Name).Value;
                if (name == "file" && file is null)
                {
                    fileName = HeaderUtilities.RemoveQuotes(disposition.FileNameStar.HasValue ? disposition.FileNameStar : disposition.FileName).Value;
                    if (string.IsNullOrWhiteSpace(fileName)) return null;
                }
                else if (name == "confirmation" && withConfirmation && confirmation is null)
                {
                    if (!MediaTypeHeaderValue.TryParse(part.ContentType, out var jsonType)
                        || !jsonType.MediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase)
                        || (jsonType.Charset.HasValue && !HeaderUtilities.RemoveQuotes(jsonType.Charset).Equals("utf-8", StringComparison.OrdinalIgnoreCase))) return null;
                }
                else return null;
                using var buffer = new MemoryStream();
                var bytes = new byte[81920];
                int read;
                while ((read = await part.Body.ReadAsync(bytes, cancellationToken)) > 0)
                {
                    total += read;
                    if (total > limit) return null;
                    await buffer.WriteAsync(bytes.AsMemory(0, read), cancellationToken);
                }
                if (buffer.Length == 0) return null;
                if (name == "file") file = buffer.ToArray(); else confirmation = buffer.ToArray();
            }
            return file is not null && (!withConfirmation || confirmation is not null) ? new(file, fileName!, confirmation) : null;
        }
        catch (InvalidDataException) { return null; }
        catch (IOException) { return null; }
    }
}
