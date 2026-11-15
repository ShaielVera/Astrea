using Astrea.Domain.Academic;
using Astrea.Domain.Common;
using AwesomeAssertions;

namespace Astrea.Domain.UnitTests.Academic;

public class CorrelatividadTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 14, 12, 0, 0, TimeSpan.Zero);

    private readonly PlanEstudio _plan;
    private readonly Guid _programacionI;
    private readonly Guid _programacionII;
    private readonly Guid _logica;
    private readonly Guid _sistemasOperativos;
    private readonly Guid _baseDeDatos;

    public CorrelatividadTests()
    {
        var carrera = Carrera.Crear(Guid.NewGuid(), "TSDS", "Tecnicatura Superior en Desarrollo de Software", 3, Ahora);
        _plan = PlanEstudio.Crear(Guid.NewGuid(), carrera, "2120/16", "Plan 2120/16", null, null);
        _logica = Agregar("Lógica y Estructura de Datos", 1);
        _sistemasOperativos = Agregar("Sistemas Operativos", 1);
        _programacionI = Agregar("Programación I", 2);
        _baseDeDatos = Agregar("Base de Datos I", 2);
        _programacionII = Agregar("Programación II", 3);
    }

    [Fact]
    public void Agrega_un_requisito_para_cursar_y_otro_para_rendir_de_la_misma_materia()
    {
        _plan.AgregarCorrelatividad(Guid.NewGuid(), _programacionII, TipoCorrelatividad.Cursar, _programacionI, CondicionRequisito.Regularizada, null, null);
        _plan.AgregarCorrelatividad(Guid.NewGuid(), _programacionII, TipoCorrelatividad.Rendir, _programacionI, CondicionRequisito.Aprobada, null, null);

        _plan.CorrelatividadesDe(_programacionII, TipoCorrelatividad.Cursar).Should().ContainSingle()
            .Which.Condicion.Should().Be(CondicionRequisito.Regularizada);
        _plan.CorrelatividadesDe(_programacionII, TipoCorrelatividad.Rendir).Should().ContainSingle()
            .Which.Condicion.Should().Be(CondicionRequisito.Aprobada);
    }

    [Fact]
    public void Una_materia_no_puede_ser_correlativa_de_si_misma()
    {
        var agregar = () => _plan.AgregarCorrelatividad(Guid.NewGuid(), _programacionI, TipoCorrelatividad.Rendir, _programacionI, CondicionRequisito.Aprobada, null, null);

        agregar.Should().Throw<DomainException>().WithMessage("*sí misma*");
    }

    [Fact]
    public void No_se_repite_el_mismo_requisito_para_el_mismo_destino_y_tipo()
    {
        _plan.AgregarCorrelatividad(Guid.NewGuid(), _programacionI, TipoCorrelatividad.Rendir, _logica, CondicionRequisito.Aprobada, null, null);

        var repetir = () => _plan.AgregarCorrelatividad(Guid.NewGuid(), _programacionI, TipoCorrelatividad.Rendir, _logica, CondicionRequisito.Regularizada, null, null);

        repetir.Should().Throw<DomainException>().WithMessage("*ya es requisito*");
    }

    [Fact]
    public void Rechaza_una_cadena_circular_indirecta()
    {
        _plan.AgregarCorrelatividad(Guid.NewGuid(), _programacionII, TipoCorrelatividad.Rendir, _programacionI, CondicionRequisito.Aprobada, null, null);
        _plan.AgregarCorrelatividad(Guid.NewGuid(), _programacionI, TipoCorrelatividad.Rendir, _logica, CondicionRequisito.Aprobada, null, null);

        var circular = () => _plan.AgregarCorrelatividad(Guid.NewGuid(), _logica, TipoCorrelatividad.Cursar, _programacionII, CondicionRequisito.Regularizada, null, null);

        circular.Should().Throw<DomainException>().WithMessage("*circular*");
    }

    [Fact]
    public void Destino_y_requisito_deben_ser_materias_del_plan()
    {
        var ajena = () => _plan.AgregarCorrelatividad(Guid.NewGuid(), _programacionI, TipoCorrelatividad.Rendir, Guid.NewGuid(), CondicionRequisito.Aprobada, null, null);

        ajena.Should().Throw<DomainException>().WithMessage("*no forma parte*");
    }

    [Fact]
    public void Un_requisito_sin_materia_necesita_texto()
    {
        var sinTexto = () => _plan.AgregarCorrelatividad(Guid.NewGuid(), _programacionII, TipoCorrelatividad.Cursar, null, CondicionRequisito.Aprobada, null, "  ");

        sinTexto.Should().Throw<DomainException>().WithMessage("*texto del requisito*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(21)]
    public void El_grupo_de_alternativas_tiene_rango(int grupo)
    {
        var agregar = () => _plan.AgregarCorrelatividad(Guid.NewGuid(), _programacionII, TipoCorrelatividad.Cursar, _logica, CondicionRequisito.Regularizada, grupo, null);

        agregar.Should().Throw<DomainException>().WithMessage("*grupo*");
    }

    [Fact]
    public void Quitar_elimina_la_correlatividad()
    {
        var correlatividad = _plan.AgregarCorrelatividad(Guid.NewGuid(), _programacionI, TipoCorrelatividad.Rendir, _logica, CondicionRequisito.Aprobada, null, null);

        _plan.QuitarCorrelatividad(correlatividad.Id);

        _plan.Correlatividades.Should().BeEmpty();
    }

    [Theory]
    [InlineData(CondicionRequisito.Regularizada, EstadoMateria.SinCursar, false)]
    [InlineData(CondicionRequisito.Regularizada, EstadoMateria.Regularizada, true)]
    [InlineData(CondicionRequisito.Regularizada, EstadoMateria.Aprobada, true)]
    [InlineData(CondicionRequisito.Aprobada, EstadoMateria.SinCursar, false)]
    [InlineData(CondicionRequisito.Aprobada, EstadoMateria.Regularizada, false)]
    [InlineData(CondicionRequisito.Aprobada, EstadoMateria.Aprobada, true)]
    public void Aprobada_cumple_regularizada_pero_regularizada_no_cumple_aprobada(CondicionRequisito condicion, EstadoMateria estado, bool cumple)
    {
        _plan.AgregarCorrelatividad(Guid.NewGuid(), _programacionII, TipoCorrelatividad.Cursar, _programacionI, condicion, null, null);

        var verificacion = _plan.VerificarCorrelatividades(_programacionII, TipoCorrelatividad.Cursar, Estados((_programacionI, estado)));

        verificacion.Resultado.Should().Be(cumple ? ResultadoVerificacion.Cumple : ResultadoVerificacion.NoCumple);
    }

    [Fact]
    public void Todos_los_requisitos_obligatorios_deben_cumplirse_e_informa_los_faltantes()
    {
        _plan.AgregarCorrelatividad(Guid.NewGuid(), _baseDeDatos, TipoCorrelatividad.Rendir, _logica, CondicionRequisito.Aprobada, null, null);
        var sistemas = _plan.AgregarCorrelatividad(Guid.NewGuid(), _baseDeDatos, TipoCorrelatividad.Rendir, _sistemasOperativos, CondicionRequisito.Aprobada, null, null);

        var verificacion = _plan.VerificarCorrelatividades(
            _baseDeDatos,
            TipoCorrelatividad.Rendir,
            Estados((_logica, EstadoMateria.Aprobada), (_sistemasOperativos, EstadoMateria.Regularizada)));

        verificacion.Resultado.Should().Be(ResultadoVerificacion.NoCumple);
        verificacion.Faltantes.Should().ContainSingle().Which.Should().BeSameAs(sistemas);
    }

    [Fact]
    public void En_un_grupo_de_alternativas_alcanza_con_una()
    {
        // "Procesos Productivos y/o Diseño y Producción Tecnológica II" (Profesorado de Educación Tecnológica).
        _plan.AgregarCorrelatividad(Guid.NewGuid(), _programacionII, TipoCorrelatividad.Cursar, _logica, CondicionRequisito.Regularizada, null, null);
        _plan.AgregarCorrelatividad(Guid.NewGuid(), _programacionII, TipoCorrelatividad.Cursar, _programacionI, CondicionRequisito.Regularizada, 1, null);
        _plan.AgregarCorrelatividad(Guid.NewGuid(), _programacionII, TipoCorrelatividad.Cursar, _baseDeDatos, CondicionRequisito.Regularizada, 1, null);

        var conUna = _plan.VerificarCorrelatividades(
            _programacionII,
            TipoCorrelatividad.Cursar,
            Estados((_logica, EstadoMateria.Aprobada), (_baseDeDatos, EstadoMateria.Regularizada)));
        var sinNinguna = _plan.VerificarCorrelatividades(
            _programacionII,
            TipoCorrelatividad.Cursar,
            Estados((_logica, EstadoMateria.Aprobada)));

        conUna.Resultado.Should().Be(ResultadoVerificacion.Cumple);
        sinNinguna.Resultado.Should().Be(ResultadoVerificacion.NoCumple);
        sinNinguna.Faltantes.Should().HaveCount(2);
    }

    [Fact]
    public void El_grupo_de_alternativas_no_reemplaza_a_los_obligatorios()
    {
        _plan.AgregarCorrelatividad(Guid.NewGuid(), _programacionII, TipoCorrelatividad.Cursar, _logica, CondicionRequisito.Aprobada, null, null);
        _plan.AgregarCorrelatividad(Guid.NewGuid(), _programacionII, TipoCorrelatividad.Cursar, _programacionI, CondicionRequisito.Regularizada, 1, null);
        _plan.AgregarCorrelatividad(Guid.NewGuid(), _programacionII, TipoCorrelatividad.Cursar, _baseDeDatos, CondicionRequisito.Regularizada, 1, null);

        var verificacion = _plan.VerificarCorrelatividades(
            _programacionII,
            TipoCorrelatividad.Cursar,
            Estados((_logica, EstadoMateria.Regularizada), (_programacionI, EstadoMateria.Aprobada)));

        verificacion.Resultado.Should().Be(ResultadoVerificacion.NoCumple);
        verificacion.Faltantes.Should().ContainSingle().Which.PlanMateriaRequisitoId.Should().Be(_logica);
    }

    [Fact]
    public void Un_requisito_en_texto_queda_para_verificar_por_secretaria()
    {
        _plan.AgregarCorrelatividad(Guid.NewGuid(), _programacionII, TipoCorrelatividad.Cursar, _programacionI, CondicionRequisito.Regularizada, null, null);
        _plan.AgregarCorrelatividad(Guid.NewGuid(), _programacionII, TipoCorrelatividad.Cursar, null, CondicionRequisito.Aprobada, null, "Todas las materias de primer año aprobadas");

        var verificacion = _plan.VerificarCorrelatividades(_programacionII, TipoCorrelatividad.Cursar, Estados((_programacionI, EstadoMateria.Regularizada)));

        verificacion.Resultado.Should().Be(ResultadoVerificacion.RequiereVerificacion);
        verificacion.AVerificar.Should().ContainSingle().Which.Descripcion.Should().Be("Todas las materias de primer año aprobadas");
    }

    [Fact]
    public void Sin_correlatividades_se_cumple()
    {
        _plan.VerificarCorrelatividades(_logica, TipoCorrelatividad.Rendir, Estados()).Resultado.Should().Be(ResultadoVerificacion.Cumple);
    }

    private Guid Agregar(string nombre, int anio) =>
        _plan.AgregarMateria(Guid.NewGuid(), Materia.Crear(Guid.NewGuid(), $"TSDS-{Guid.NewGuid():N}"[..20], nombre), anio, Cuatrimestre.Anual, null, true).Id;

    private static Dictionary<Guid, EstadoMateria> Estados(params (Guid PlanMateriaId, EstadoMateria Estado)[] estados) =>
        estados.ToDictionary(estado => estado.PlanMateriaId, estado => estado.Estado);
}
