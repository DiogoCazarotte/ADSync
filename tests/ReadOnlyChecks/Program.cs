using ADSync.Domain.Exceptions;
using ADSync.Infrastructure.Data;
using ADSync.Infrastructure.Identity;
using ADSync.Infrastructure.Safety;
using Microsoft.EntityFrameworkCore;

// No running database is used. If interception fails, the unusable loopback
// connection fails the test; no existing database or data is addressed.
var options = new DbContextOptionsBuilder<AdSyncDbContext>()
    .UseNpgsql("Host=127.0.0.1;Port=1;Database=unused;Username=unused;Timeout=1")
    .AddInterceptors(new ReadOnlySaveChangesInterceptor(true)).Options;
foreach (var asynchronous in new[] { false, true })
{
    await using var db = new AdSyncDbContext(options);
    db.Users.Add(new ApplicationUser { Id = Guid.NewGuid(), UserName = "in-memory-check" });
    try
    {
        if (asynchronous) await db.SaveChangesAsync(); else db.SaveChanges();
        throw new Exception("Gravação deveria ter sido bloqueada.");
    }
    catch (AcessoNegadoException)
    {
        Console.WriteLine($"PASS: SaveChanges{(asynchronous ? "Async" : "")} bloqueado antes de conectar ao banco.");
    }
}
