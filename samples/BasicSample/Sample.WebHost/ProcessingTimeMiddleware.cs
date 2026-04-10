using Microsoft.AspNetCore.Http;
using System.Diagnostics;
using System.Globalization;

namespace Sample.WebHost;

/// <summary>
/// Middleware that measures the time spent processing each HTTP request and
/// appends the result in the <c>X-Processing-Time</c> response header.
///
/// Header format: "X-Processing-Time: 42.31 ms"
/// </summary>
public class ProcessingTimeMiddleware
{
    private readonly RequestDelegate _next;

    public ProcessingTimeMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Swap the real response body for a buffer so nothing is sent yet
        var originalBody = context.Response.Body;
        using var buffer = new MemoryStream();
        context.Response.Body = buffer;

        var sw = Stopwatch.StartNew();

        try
        {
            await _next(context);
        }
        finally
        {
            sw.Stop();

            // At this point nothing has reached the client yet —
            // headers are still modifiable
            context.Response.Headers["X-Processing-Time"] = $"{sw.Elapsed.TotalMilliseconds.ToString("F2", CultureInfo.InvariantCulture)} ms";

            // Flush the buffer to the real stream
            buffer.Seek(0, SeekOrigin.Begin);
            await buffer.CopyToAsync(originalBody);
            context.Response.Body = originalBody;
        }
    }
}