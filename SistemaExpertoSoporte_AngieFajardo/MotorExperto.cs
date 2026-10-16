namespace SistemaExpertoSoporte;

// ============================================================
//  SISTEMA EXPERTO: DIAGNÓSTICO DE FALLAS DE COMPUTADORAS
//  Base de conocimiento obtenida de la entrevista con el
//  encargado de soporte técnico.
//  Técnica: reglas de producción (SI ... ENTONCES ...)
//  con encadenamiento hacia adelante y resolución de
//  conflictos por prioridad (disco > calor > RAM).
// ============================================================

/// <summary>Regla de producción: SI condición ENTONCES se deduce un hecho.</summary>
public record Regla(string Id, string Descripcion, Func<MotorInferencia, Task<bool>> Condicion, string Conclusion);

/// <summary>Falla final con la acción recomendada y su prioridad (menor número = se atiende primero).</summary>
public record Falla(string Nombre, string Accion, int Prioridad);

// ------------------------------------------------------------
//  BASE DE CONOCIMIENTO
// ------------------------------------------------------------
public static class BaseConocimiento
{
    // Preguntas al usuario (síntomas observables). La clave es el nombre del hecho.
    public static readonly Dictionary<string, string> Preguntas = new()
    {
        ["enciende"]            = "¿La computadora enciende?",
        ["luz"]                 = "¿Hay alguna luz encendida en el equipo?",
        ["ventiladores"]        = "¿Los ventiladores giran, aunque sea por un instante?",
        ["ventilador_1s"]       = "¿Los ventiladores giran solo un segundo y se apagan?",
        ["cable_regleta_ok"]    = "¿El cable de poder y la regleta están bien?",
        ["imagen"]              = "¿Hay imagen en el monitor?",
        ["pitidos"]             = "¿La computadora emite pitidos?",
        ["monitor_con_luz"]     = "¿El monitor tiene alguna luz encendida?",
        ["monitor_sin_senal"]   = "¿La luz del monitor indica «sin señal»?",
        ["imagen_salida_placa"] = "Al conectar el cable de video a la salida de la placa madre, ¿aparece imagen?",
        ["reinicia_uso"]        = "¿Se reinicia después de un rato de uso o al abrir programas pesados?",
        ["reinicia_azar"]       = "¿Se reinicia al azar, incluso recién encendida?",
        ["lenta"]               = "¿La computadora está lenta?",
        ["caliente"]            = "¿La computadora está caliente?",
        ["disco_sintomas"]      = "¿El disco hace ruidos de clic o hay archivos que no abren?",
    };

    // Fallas posibles, acción recomendada y prioridad para resolver conflictos.
    // Orden de la entrevista: disco con clics (1), calor (2), RAM (3).
    public static readonly Dictionary<string, Falla> Fallas = new()
    {
        ["falla_fuente"] = new("Fuente de poder",
            "Probarla con otra fuente que funcione.", 10),
        ["falla_placa_madre"] = new("Placa madre",
            "Revisarla en persona.", 10),
        ["falla_ram"] = new("Memoria RAM",
            "Sacar los módulos, limpiar los contactos y volver a colocarlos.", 3),
        ["falla_monitor"] = new("Monitor",
            "Reemplazar o reparar el monitor.", 10),
        ["falla_tarjeta_video"] = new("Tarjeta de video dedicada",
            "Reemplazar la tarjeta de video.", 10),
        ["falla_cable_video"] = new("Cable de video",
            "Cambiar el cable de video.", 10),
        ["falla_sobrecalentamiento"] = new("Sobrecalentamiento",
            "Limpiar ventiladores y disipador, y cambiar la pasta térmica.", 2),
        ["falla_disco"] = new("Disco duro",
            "Respaldar la información de inmediato y después reemplazar el disco, de preferencia por uno de estado sólido (SSD).", 1),
    };

    // Reglas de producción (en el orden del flujo de la entrevista).
    // m.H("x") consulta el hecho x; si es un síntoma desconocido, se le pregunta al usuario.
    public static List<Regla> CrearReglas() => new()
    {
        // ---------- NO ENCIENDE: problemas de energía ----------
        new("R1", "SI no enciende, no hay luces y los ventiladores no giran ENTONCES hay un problema de energía (sin señales)",
            async m => !await m.H("enciende") && !await m.H("luz") && !await m.H("ventiladores"),
            "energia_sin_senales"),

        new("R2", "SI problema de energía sin señales Y cable y regleta están bien ENTONCES la falla es la fuente de poder",
            async m => await m.H("energia_sin_senales") && await m.H("cable_regleta_ok"),
            "falla_fuente"),

        new("R3", "SI no enciende Y los ventiladores giran un segundo y se apagan ENTONCES hay un problema de energía (ciclo corto)",
            async m => !await m.H("enciende") && await m.H("ventiladores") && await m.H("ventilador_1s"),
            "energia_ciclo_corto"),

        new("R4", "SI problema de energía con ciclo corto de ventiladores ENTONCES la falla es la placa madre",
            async m => await m.H("energia_ciclo_corto"),
            "falla_placa_madre"),

        // ---------- ENCIENDE PERO SIN IMAGEN ----------
        new("R5", "SI enciende, no hay imagen y hay pitidos ENTONCES es un problema de memoria",
            async m => await m.H("enciende") && !await m.H("imagen") && await m.H("pitidos"),
            "problema_memoria"),

        new("R6", "SI es un problema de memoria ENTONCES la falla es la RAM",
            async m => await m.H("problema_memoria"),
            "falla_ram"),

        new("R7", "SI enciende, no hay imagen y no hay pitidos ENTONCES es un problema de video",
            async m => await m.H("enciende") && !await m.H("imagen") && !await m.H("pitidos"),
            "problema_video"),

        new("R8", "SI problema de video Y el monitor no tiene ninguna luz ENTONCES la falla es el monitor",
            async m => await m.H("problema_video") && !await m.H("monitor_con_luz"),
            "falla_monitor"),

        new("R9", "SI problema de video Y el monitor tiene luz de «sin señal» ENTONCES el monitor funciona",
            async m => await m.H("problema_video") && await m.H("monitor_con_luz") && await m.H("monitor_sin_senal"),
            "monitor_funciona"),

        new("R10", "SI el monitor funciona Y al conectar a la salida de la placa madre aparece imagen ENTONCES la falla es la tarjeta de video dedicada",
            async m => await m.H("monitor_funciona") && await m.H("imagen_salida_placa"),
            "falla_tarjeta_video"),

        new("R11", "SI el monitor funciona Y al conectar a la salida de la placa madre NO aparece imagen ENTONCES la falla es el cable de video",
            async m => await m.H("monitor_funciona") && !await m.H("imagen_salida_placa"),
            "falla_cable_video"),

        // ---------- ENCIENDE Y HAY IMAGEN: comportamiento ----------
        new("R12", "SI enciende, hay imagen y se reinicia tras un rato de uso o con programas pesados ENTONCES la falla es sobrecalentamiento",
            async m => await m.H("enciende") && await m.H("imagen") && await m.H("reinicia_uso"),
            "falla_sobrecalentamiento"),

        new("R13", "SI enciende, hay imagen y se reinicia al azar (incluso recién encendida) ENTONCES la falla es la fuente de poder",
            async m => await m.H("enciende") && await m.H("imagen") && await m.H("reinicia_azar"),
            "falla_fuente"),

        new("R14", "SI enciende, hay imagen, está lenta y caliente ENTONCES la falla es sobrecalentamiento (la PC se ralentiza a propósito)",
            async m => await m.H("enciende") && await m.H("imagen") && await m.H("lenta") && await m.H("caliente"),
            "falla_sobrecalentamiento"),

        new("R15", "SI enciende, hay imagen, está lenta y el disco hace clics o hay archivos que no abren ENTONCES la falla es el disco duro",
            async m => await m.H("enciende") && await m.H("imagen") && await m.H("lenta") && await m.H("disco_sintomas"),
            "falla_disco"),

        new("R16", "SI enciende, hay imagen, está lenta, sin ruidos y sin otros síntomas ENTONCES no es una falla de hardware",
            async m => await m.H("enciende") && await m.H("imagen") && await m.H("lenta")
                       && !await m.H("disco_sintomas") && !await m.H("caliente")
                       && !await m.H("reinicia_uso") && !await m.H("reinicia_azar"),
            "sin_falla_hardware"),
    };
}

// ------------------------------------------------------------
//  MOTOR DE INFERENCIA (encadenamiento hacia adelante)
// ------------------------------------------------------------
public class MotorInferencia
{
    private readonly Dictionary<string, bool> _hechos = new();
    private readonly List<Regla> _reglas = BaseConocimiento.CrearReglas();
    private readonly Func<string, Task<bool>> _preguntar;

    /// <param name="preguntar">Función que muestra una pregunta al usuario (en el formulario) y devuelve su respuesta.</param>
    public MotorInferencia(Func<string, Task<bool>> preguntar) => _preguntar = preguntar;

    /// <summary>Reglas que se activaron, en orden (sirve para explicar el razonamiento).</summary>
    public List<Regla> Disparadas { get; } = new();

    /// <summary>
    /// Devuelve el valor de un hecho. Si es un síntoma que aún no se conoce, se lo pregunta al usuario
    /// (solo se pregunta lo necesario). Un hecho derivado que no se ha deducido vale falso.
    /// </summary>
    public async Task<bool> H(string hecho)
    {
        if (_hechos.TryGetValue(hecho, out bool valor))
            return valor;

        if (BaseConocimiento.Preguntas.TryGetValue(hecho, out string? pregunta))
        {
            bool respuesta = await _preguntar(pregunta);
            _hechos[hecho] = respuesta;
            return respuesta;
        }

        return false;
    }

    public bool Deducido(string hecho) => _hechos.TryGetValue(hecho, out bool v) && v;

    /// <summary>Ejecuta el ciclo: evaluar reglas hasta que no se deduzcan hechos nuevos.</summary>
    public async Task Inferir()
    {
        bool hayCambios;
        do
        {
            hayCambios = false;
            foreach (var regla in _reglas)
            {
                if (Disparadas.Contains(regla)) continue;

                if (await regla.Condicion(this))
                {
                    _hechos[regla.Conclusion] = true;
                    Disparadas.Add(regla);
                    hayCambios = true;
                }
            }
        } while (hayCambios);
    }

    /// <summary>Resolución de conflictos: ordena las fallas encontradas por prioridad.</summary>
    public List<Falla> FallasEncontradas()
    {
        return Disparadas
            .Where(r => BaseConocimiento.Fallas.ContainsKey(r.Conclusion))
            .Select(r => BaseConocimiento.Fallas[r.Conclusion])
            .Distinct()
            .OrderBy(f => f.Prioridad) // OrderBy es estable: empata por orden de activación
            .ToList();
    }
}
