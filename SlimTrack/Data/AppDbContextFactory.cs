using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SlimTrack.Data.Database;

namespace SlimTrack.Data;

/// <summary>
/// Factory para criação do DbContext em tempo de design (geração de migrations via dotnet ef).
/// Necessária porque a string de conexão vem do Aspire em tempo de execução.
/// A connection string pode ser fornecida pela variável de ambiente SLIMTRACK_DB_CONNECTION
/// ou, se ausente, usa o valor padrão para desenvolvimento local.
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("SLIMTRACK_DB_CONNECTION")
            ?? "Host=localhost;Database=slimtrack;Username=postgres;******";

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(
            connectionString,
            npgsql => npgsql.MigrationsAssembly("SlimTrack")
        );
        return new AppDbContext(optionsBuilder.Options);
    }
}
