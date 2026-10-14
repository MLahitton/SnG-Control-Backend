using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using SnG.Control.Api.Errors;

namespace SnG.Control.Tests.Api;

public class ApiExceptionDiagnosticsTests
{
    [Fact]
    public async Task MissingStoredFile_LogsTechnicalExceptionButReturnsGeneric500()
    {
        var logger = new DiagnosticLogger();
        var context = new DefaultHttpContext(); context.Response.Body = new MemoryStream();
        var exception = new FileNotFoundException("Private storage detail", "private/confirmed.xlsx");
        Assert.True(await new ApiExceptionHandler(logger).TryHandleAsync(context, exception, default));
        Assert.Equal(500, context.Response.StatusCode); Assert.Same(exception, logger.Exception); Assert.Equal(LogLevel.Error, logger.Level);
        var body = Encoding.UTF8.GetString(((MemoryStream)context.Response.Body).ToArray());
        Assert.DoesNotContain("Private", body); Assert.DoesNotContain("private/", body); Assert.Contains("An unexpected error occurred", body);
    }

    private sealed class DiagnosticLogger : ILogger<ApiExceptionHandler>
    {
        public Exception? Exception { get; private set; }
        public LogLevel Level { get; private set; }
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Level = logLevel; Exception = exception;
        }
    }
}
