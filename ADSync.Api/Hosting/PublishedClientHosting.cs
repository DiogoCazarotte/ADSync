using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;

namespace ADSync.Api.Hosting;

public static class PublishedClientHosting
{
    public static void UsePublishedClient(this WebApplication app)
    {
        var clientRoot = Path.Combine(app.Environment.ContentRootPath, "client");
        if (!File.Exists(Path.Combine(clientRoot, "index.html")))
            throw new InvalidOperationException("Cliente publicado ausente. Use o Dockerfile da raiz para publicar API e cliente juntos.");

        var files = new PhysicalFileProvider(clientRoot);
        app.Lifetime.ApplicationStopped.Register(files.Dispose);

        var types = new FileExtensionContentTypeProvider();
        types.Mappings[".wasm"] = "application/wasm";
        types.Mappings[".dat"] = "application/octet-stream";
        types.Mappings[".blat"] = "application/octet-stream";
        types.Mappings[".webmanifest"] = "application/manifest+json";
        app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = files });
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = files,
            ContentTypeProvider = types,
            OnPrepareResponse = context =>
            {
                // Permite validar novas versões do HTML, configuração e service worker.
                // O service worker publicado continua responsável pelo cache offline.
                context.Context.Response.Headers.CacheControl = "no-cache";
                context.Context.Response.Headers["Blazor-Environment"] = app.Environment.EnvironmentName;
            }
        });
    }

    public static void MapPublishedClientFallback(this WebApplication app)
    {
        app.MapFallback(async context =>
        {
            // Um endereço de API inexistente precisa ser 404, inclusive sem extensão.
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }
            if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
            {
                context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
                return;
            }
            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.Headers.CacheControl = "no-cache";
            await context.Response.SendFileAsync(Path.Combine(app.Environment.ContentRootPath, "client", "index.html"));
        });
    }
}
