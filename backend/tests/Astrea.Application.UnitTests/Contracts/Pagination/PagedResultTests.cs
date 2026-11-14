using Astrea.Application.Contracts.Pagination;
using AwesomeAssertions;

namespace Astrea.Application.UnitTests.Contracts.Pagination;

public class PagedResultTests
{
    [Fact]
    public void Calcula_total_de_paginas_y_navegacion_en_una_pagina_intermedia()
    {
        var result = PagedResult<int>.From([11, 12, 13, 14, 15, 16, 17, 18, 19, 20], PageRequest.Create(2, 10), totalItems: 25);

        result.TotalPages.Should().Be(3);
        result.HasPrevious.Should().BeTrue();
        result.HasNext.Should().BeTrue();
    }

    [Fact]
    public void En_la_ultima_pagina_no_hay_siguiente()
    {
        var result = PagedResult<int>.From([21, 22, 23, 24, 25], PageRequest.Create(3, 10), totalItems: 25);

        result.HasNext.Should().BeFalse();
        result.HasPrevious.Should().BeTrue();
    }

    [Fact]
    public void Sin_resultados_no_hay_paginas()
    {
        var result = PagedResult<int>.From([], PageRequest.Create(1, 10), totalItems: 0);

        result.TotalPages.Should().Be(0);
        result.HasPrevious.Should().BeFalse();
        result.HasNext.Should().BeFalse();
    }
}
