using Astrea.Domain.Common;
using AwesomeAssertions;

namespace Astrea.Domain.UnitTests.Common;

public class EntityTests
{
    private sealed class EntidadDePrueba(Guid id) : Entity(id);

    private sealed class OtraEntidad(Guid id) : Entity(id);

    [Fact]
    public void Entidades_del_mismo_tipo_con_el_mismo_id_son_iguales()
    {
        var id = Guid.NewGuid();

        new EntidadDePrueba(id).Should().Be(new EntidadDePrueba(id));
    }

    [Fact]
    public void Entidades_de_distinto_tipo_con_el_mismo_id_no_son_iguales()
    {
        var id = Guid.NewGuid();

        new EntidadDePrueba(id).Equals(new OtraEntidad(id)).Should().BeFalse();
    }

    [Fact]
    public void No_se_puede_crear_una_entidad_con_id_vacio()
    {
        var crear = () => new EntidadDePrueba(Guid.Empty);

        crear.Should().Throw<DomainException>();
    }
}
