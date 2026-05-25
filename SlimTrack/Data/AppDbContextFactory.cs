using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SlimTrack.Data.Database;

namespace SlimTrack.Data;

/// <summary>
/// Factory para criação do DbContext em tempo de design (geração de migrations via dotnet ef).
/// Necessária porque a string de conexão vem do Aspire em tempo de execução.
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(
            "Host=localhost;Database=slimtrack;Username=postgres;******",
            npgsql => npgsql.MigrationsAssembly("SlimTrack")
        );
        return new AppDbContext(optionsBuilder.Options);
    }
}
