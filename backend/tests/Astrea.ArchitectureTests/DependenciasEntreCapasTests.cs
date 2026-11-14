using System.Reflection;
using Astrea.Application.Contracts.Pagination;
using Astrea.Domain.Common;
using Astrea.Infrastructure.Persistence;
using AwesomeAssertions;
using NetArchTest.Rules;

namespace Astrea.ArchitectureTests;

/// <summary>
/// Verifica las reglas de la Arquitectura Hexagonal: las dependencias apuntan hacia el dominio.
/// </summary>
public class DependenciasEntreCapasTests
{
    private static readonly Assembly Domain = typeof(Entity).Assembly;
    private static readonly Assembly Application = typeof(PageRequest).Assembly;
    private static readonly Assembly Infrastructure = typeof(AppDbContext).Assembly;

    private static readonly string[] FrameworksDeInfraestructura =
    [
        "Microsoft.EntityFrameworkCore",
        "Microsoft.AspNetCore",
        "Npgsql",
        "FirebaseAdmin",
    ];

    [Fact]
    public void Domain_no_depende_de_ninguna_otra_capa_ni_de_frameworks()
    {
        var result = Types.InAssembly(Domain)
            .ShouldNot()
            .HaveDependencyOnAny(["Astrea.Application", "Astrea.Infrastructure", "Astrea.Api", .. FrameworksDeInfraestructura])
            .GetResult();

        result.IsSuccessful.Should().BeTrue(Fallas(result));
    }

    [Fact]
    public void Application_no_depende_de_Infrastructure_Api_ni_de_frameworks_de_infraestructura()
    {
        var result = Types.InAssembly(Application)
            .ShouldNot()
            .HaveDependencyOnAny(["Astrea.Infrastructure", "Astrea.Api", .. FrameworksDeInfraestructura])
            .GetResult();

        result.IsSuccessful.Should().BeTrue(Fallas(result));
    }

    [Fact]
    public void Infrastructure_no_depende_de_Api()
    {
        var result = Types.InAssembly(Infrastructure)
            .ShouldNot()
            .HaveDependencyOn("Astrea.Api")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(Fallas(result));
    }

    private static string Fallas(TestResult result) =>
        "tipos que rompen la regla: " + string.Join(", ", result.FailingTypeNames ?? []);
}
