using Astrea.Domain.Knowledge;
using AwesomeAssertions;

namespace Astrea.Domain.UnitTests.Knowledge;

public class FragmentadorTests
{
    private static string Articulo(int numero, int oraciones) =>
        $"Artículo {numero}°: " + string.Join(' ', Enumerable.Range(1, oraciones).Select(i => $"El estudiante regular deberá cumplir con la condición número {i} del artículo {numero}."));

    [Fact]
    public void Un_texto_corto_es_un_solo_fragmento_con_su_pagina()
    {
        var fragmentos = Fragmentador.Fragmentar([new PaginaTexto(1, "Las inscripciones a mesas cierran 48 horas antes.")]);

        fragmentos.Should().ContainSingle().Which.Should().BeEquivalentTo(new FragmentoTexto(1, "Las inscripciones a mesas cierran 48 horas antes.", null, 1, 1));
    }

    [Fact]
    public void Ningun_fragmento_supera_el_tamanio_maximo_y_se_numeran_en_orden()
    {
        var paginas = Enumerable.Range(1, 5).Select(numero => new PaginaTexto(numero, Articulo(numero, 30))).ToList();

        var fragmentos = Fragmentador.Fragmentar(paginas);

        fragmentos.Count.Should().BeGreaterThan(5);
        fragmentos.Should().OnlyContain(fragmento => fragmento.Contenido.Length <= Fragmentador.TamanioMaximo);
        fragmentos.Select(fragmento => fragmento.Numero).Should().Equal(Enumerable.Range(1, fragmentos.Count));
    }

    [Fact]
    public void Los_fragmentos_consecutivos_se_solapan()
    {
        var fragmentos = Fragmentador.Fragmentar([new PaginaTexto(1, Articulo(1, 60))]);

        fragmentos.Count.Should().BeGreaterThan(1);
        for (var i = 1; i < fragmentos.Count; i++)
        {
            var inicio = fragmentos[i].Contenido.TrimStart('…')[..40];
            fragmentos[i - 1].Contenido.Should().Contain(inicio, "el fragmento {0} repite el final del anterior", i + 1);
        }
    }

    [Fact]
    public void Un_encabezado_abre_un_fragmento_nuevo_y_queda_como_seccion()
    {
        var texto = """
            CAPÍTULO I
            DE LOS ESTUDIANTES
            Son estudiantes regulares quienes cumplan las condiciones de este reglamento.
            CAPÍTULO II
            DE LAS MESAS DE EXAMEN
            Las mesas se constituyen en tres turnos anuales.
            """;

        var fragmentos = Fragmentador.Fragmentar([new PaginaTexto(1, texto)]);

        fragmentos.Should().HaveCount(2);
        fragmentos[0].Seccion.Should().Be("CAPÍTULO I — DE LOS ESTUDIANTES");
        fragmentos[0].Contenido.Should().StartWith("CAPÍTULO I").And.Contain("Son estudiantes regulares").And.NotContain("mesas se constituyen");
        fragmentos[1].Seccion.Should().Be("CAPÍTULO II — DE LAS MESAS DE EXAMEN");
        fragmentos[1].Contenido.Should().Contain("tres turnos");
    }

    [Fact]
    public void Un_fragmento_que_cruza_paginas_registra_desde_y_hasta()
    {
        var fragmentos = Fragmentador.Fragmentar([
            new PaginaTexto(3, "La regularidad se pierde por inasistencias."),
            new PaginaTexto(4, "También se pierde por no aprobar los parciales."),
        ]);

        fragmentos.Should().ContainSingle().Which.Should().BeEquivalentTo(new { PaginaDesde = 3, PaginaHasta = 4 });
    }

    [Fact]
    public void Une_palabras_cortadas_con_guion_y_normaliza_espacios()
    {
        var fragmentos = Fragmentador.Fragmentar([new PaginaTexto(1, "El regla-\nmento   académico rige   desde 2024.")]);

        fragmentos.Single().Contenido.Should().Be("El reglamento académico rige desde 2024.");
    }

    [Fact]
    public void Paginas_vacias_o_solo_encabezados_no_generan_fragmentos()
    {
        Fragmentador.Fragmentar([new PaginaTexto(1, "   \n\n "), new PaginaTexto(2, "ANEXO I")]).Should().BeEmpty();
    }

    [Fact]
    public void Una_tira_sin_espacios_enorme_tambien_se_corta()
    {
        var fragmentos = Fragmentador.Fragmentar([new PaginaTexto(1, new string('x', 5000))]);

        fragmentos.Should().OnlyContain(fragmento => fragmento.Contenido.Length <= Fragmentador.TamanioMaximo);
    }
}
