using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Microsoft.Net.Http.Headers;

namespace ICanHelp.Api.Frontend;

/// <summary>
/// Krok F10: API serwuje zbudowany frontend (PWA z <c>ICanHelp.Web/www</c>) – jedna domena, bez CORS, ten sam HTTPS
/// dla API, huba SignalR i service workera. Katalog: <c>Frontend:Path</c> (domyślnie <c>wwwroot</c> projektu Api);
/// bez <c>index.html</c> w katalogu nic się nie zmienia (praca na <c>ng serve</c> + proxy).
/// Adresy aplikacji (np. <c>/announcements/…</c>) dostają <c>index.html</c>; <c>/api</c>, <c>/hubs</c>, <c>/swagger</c>,
/// <c>/health</c> nigdy – nieznany adres API to dalej 404 jako ProblemDetails.
/// </summary>
public static class FrontendExtensions
{
    public const string PathKey = "Frontend:Path";

    private static readonly string[] ReservedPrefixes = ["/api", "/hubs", "/swagger", "/health"];

    // Pliki, które muszą być zawsze świeże – inaczej przeglądarka trzyma starą wersję aplikacji / workera.
    private static readonly HashSet<string> NoCacheFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "/index.html", "/ngsw.json", "/app-sw.js", "/ngsw-worker.js", "/safety-worker.js", "/worker-basic.min.js",
        "/manifest.webmanifest"
    };

    /// <summary>Fizyczny katalog frontendu albo <c>null</c>, gdy nie ma w nim zbudowanej aplikacji.</summary>
    public static PhysicalFileProvider? FindFrontend(IConfiguration configuration, IWebHostEnvironment environment)
    {
        var path = configuration[PathKey];
        path = string.IsNullOrWhiteSpace(path) ? "wwwroot" : path;
        var fullPath = Path.GetFullPath(Path.Combine(environment.ContentRootPath, path));

        return File.Exists(Path.Combine(fullPath, "index.html")) ? new PhysicalFileProvider(fullPath) : null;
    }

    /// <summary>Pliki statyczne frontendu (przed routingiem).</summary>
    public static WebApplication UseFrontendFiles(this WebApplication app, PhysicalFileProvider frontend)
    {
        var contentTypes = new FileExtensionContentTypeProvider();
        contentTypes.Mappings[".webmanifest"] = "application/manifest+json";

        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = frontend,
            ContentTypeProvider = contentTypes,
            OnPrepareResponse = ctx => SetCacheHeaders(ctx.Context)
        });

        return app;
    }

    /// <summary>Fallback na <c>index.html</c> dla adresów aplikacji (po kontrolerach – ma najniższy priorytet).</summary>
    public static WebApplication MapFrontendFallback(this WebApplication app, PhysicalFileProvider frontend)
    {
        // {*path:nonfile} – adresy bez rozszerzenia; brakujący plik (np. /missing.js) to zwykłe 404.
        app.MapFallback("{*path:nonfile}", async context =>
        {
            if (IsReserved(context.Request.Path))
            {
                // Puste 404 – UseStatusCodePages dopisze ProblemDetails.
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            var index = frontend.GetFileInfo("index.html");
            context.Response.ContentType = "text/html; charset=utf-8";
            SetCacheHeaders(context, "/index.html");
            await context.Response.SendFileAsync(index, context.RequestAborted);
        }).ExcludeFromDescription();

        return app;
    }

    public static bool IsReserved(PathString path) =>
        ReservedPrefixes.Any(prefix => path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase));

    private static void SetCacheHeaders(HttpContext context, string? path = null)
    {
        path ??= context.Request.Path.Value ?? string.Empty;
        context.Response.Headers[HeaderNames.CacheControl] = NoCacheFiles.Contains(path)
            ? "no-cache"
            // Pliki z buildu mają hash w nazwie (main-ABC123.js), ikony zmieniają się rzadko – ngsw i tak pilnuje wersji.
            : "public, max-age=86400";
    }
}
