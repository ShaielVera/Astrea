using Astrea.Application.Contracts.Pagination;
using AwesomeAssertions;

namespace Astrea.Application.UnitTests.Contracts.Pagination;

public class PageRequestTests
{
    [Fact]
    public void Sin_parametros_usa_la_primera_pagina_y_el_tamanio_por_defecto()
    {
        var request = PageRequest.Create(null, null);

        request.Page.Should().Be(1);
        request.PageSize.Should().Be(PageRequest.DefaultPageSize);
        request.Skip.Should().Be(0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void Una_pagina_menor_a_uno_se_normaliza_a_uno(int page)
    {
        PageRequest.Create(page, 10).Page.Should().Be(1);
    }

    [Fact]
    public void Un_pageSize_mayor_al_maximo_se_recorta()
    {
        PageRequest.Create(1, 500).PageSize.Should().Be(PageRequest.MaxPageSize);
    }

    [Fact]
    public void Skip_se_calcula_segun_pagina_y_tamanio()
    {
        PageRequest.Create(3, 10).Skip.Should().Be(20);
    }
}
