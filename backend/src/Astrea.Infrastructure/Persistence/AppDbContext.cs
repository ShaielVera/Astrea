using Astrea.Domain.Academic;
using Astrea.Domain.Exams;
using Astrea.Domain.LostAndFound;
using Astrea.Domain.Procedures;
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

    public DbSet<MesaExamen> MesasExamen => Set<MesaExamen>();

    public DbSet<TramiteTipo> TramiteTipos => Set<TramiteTipo>();

    public DbSet<Tramite> Tramites => Set<Tramite>();

    public DbSet<TramiteEvento> TramiteEventos => Set<TramiteEvento>();

    public DbSet<PlantillaDocumento> PlantillasDocumento => Set<PlantillaDocumento>();

    public DbSet<DocumentoEmitido> DocumentosEmitidos => Set<DocumentoEmitido>();

    public DbSet<ObjetoPerdido> ObjetosPerdidos => Set<ObjetoPerdido>();

    public DbSet<EntregaObjeto> EntregasObjeto => Set<EntregaObjeto>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Extensión pgvector para guardar los embeddings del pipeline RAG.
        modelBuilder.HasPostgresExtension("vector");

        // Búsquedas sin distinguir tildes (ej. "programacion" encuentra "Programación").
        modelBuilder.HasPostgresExtension("unaccent");

        // Números de trámite correlativos (T-000001, T-000002…).
        modelBuilder.HasSequence<int>(Configurations.TramiteConfiguration.SecuenciaNumero);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
