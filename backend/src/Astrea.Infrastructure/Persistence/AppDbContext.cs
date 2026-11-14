using Astrea.Domain.Academic;
using Astrea.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Astrea.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Usuario> Usuarios => Set<Usuario>();

    public DbSet<Rol> Roles => Set<Rol>();

    public DbSet<UsuarioRol> UsuarioRoles => Set<UsuarioRol>();

    public DbSet<Carrera> Carreras => Set<Carrera>();

    public DbSet<PlanEstudio> PlanesEstudio => Set<PlanEstudio>();

    public DbSet<Materia> Materias => Set<Materia>();

    public DbSet<PlanMateria> PlanMaterias => Set<PlanMateria>();

    public DbSet<Correlatividad> Correlatividades => Set<Correlatividad>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Extensión pgvector para guardar los embeddings del pipeline RAG.
        modelBuilder.HasPostgresExtension("vector");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
