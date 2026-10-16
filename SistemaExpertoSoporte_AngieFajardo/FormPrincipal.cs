using System.Drawing;
using System.Windows.Forms;

namespace SistemaExpertoSoporte;

/// <summary>
/// Ventana única del sistema experto. Toda la interacción (preguntas, respuestas,
/// diagnóstico y explicación) ocurre dentro de este formulario. La interfaz se construye
/// por código, así que no hace falta el diseñador.
/// </summary>
public class FormPrincipal : Form
{
    private readonly Label _lblTitulo = new();
    private readonly Label _lblPregunta = new();
    private readonly Button _btnSi = new();
    private readonly Button _btnNo = new();
    private readonly Button _btnNuevo = new();
    private readonly ListBox _lstHistorial = new();
    private readonly RichTextBox _rtbResultado = new();

    private TaskCompletionSource<bool>? _respuestaPendiente;
    private CancellationTokenSource? _cts;

    public FormPrincipal()
    {
        ConstruirInterfaz();

        Shown += async (_, _) => await IniciarDiagnostico();
        FormClosing += (_, _) => _cts?.Cancel();
    }

    // ------------------------------------------------------------
    //  Interfaz
    // ------------------------------------------------------------
    private void ConstruirInterfaz()
    {
        Text = "Sistema Experto - Soporte Técnico de PC";
        ClientSize = new Size(900, 620);
        MinimumSize = new Size(760, 520);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10F);

        // Título
        _lblTitulo.Text = "Diagnóstico de fallas de computadoras";
        _lblTitulo.Font = new Font("Segoe UI", 16F, FontStyle.Bold);
        _lblTitulo.ForeColor = Color.FromArgb(30, 64, 120);
        _lblTitulo.Dock = DockStyle.Top;
        _lblTitulo.Height = 50;
        _lblTitulo.Padding = new Padding(12, 10, 0, 0);

        // Panel de pregunta actual
        var grpPregunta = new GroupBox
        {
            Text = "Pregunta actual",
            Dock = DockStyle.Top,
            Height = 130,
            Padding = new Padding(12),
        };

        _lblPregunta.Dock = DockStyle.Top;
        _lblPregunta.Height = 50;
        _lblPregunta.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
        _lblPregunta.Text = "Preparando diagnóstico...";

        var panelBotones = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
        };

        ConfigurarBoton(_btnSi, "Sí", Color.FromArgb(46, 139, 87));
        ConfigurarBoton(_btnNo, "No", Color.FromArgb(178, 34, 34));
        ConfigurarBoton(_btnNuevo, "Nuevo diagnóstico", Color.FromArgb(30, 64, 120));
        _btnNuevo.Width = 180;
        _btnNuevo.Margin = new Padding(40, 3, 3, 3);

        _btnSi.Click += (_, _) => _respuestaPendiente?.TrySetResult(true);
        _btnNo.Click += (_, _) => _respuestaPendiente?.TrySetResult(false);
        _btnNuevo.Click += async (_, _) => await IniciarDiagnostico();

        panelBotones.Controls.AddRange(new Control[] { _btnSi, _btnNo, _btnNuevo });
        grpPregunta.Controls.Add(panelBotones);
        grpPregunta.Controls.Add(_lblPregunta);

        // Zona inferior: historial (izquierda) y diagnóstico (derecha)
        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 320,
        };

        var grpHistorial = new GroupBox { Text = "Respuestas registradas", Dock = DockStyle.Fill, Padding = new Padding(8) };
        _lstHistorial.Dock = DockStyle.Fill;
        _lstHistorial.HorizontalScrollbar = true;
        _lstHistorial.BorderStyle = BorderStyle.None;
        grpHistorial.Controls.Add(_lstHistorial);
        split.Panel1.Controls.Add(grpHistorial);

        var grpResultado = new GroupBox { Text = "Diagnóstico y explicación", Dock = DockStyle.Fill, Padding = new Padding(8) };
        _rtbResultado.Dock = DockStyle.Fill;
        _rtbResultado.ReadOnly = true;
        _rtbResultado.BorderStyle = BorderStyle.None;
        _rtbResultado.BackColor = Color.White;
        grpResultado.Controls.Add(_rtbResultado);
        split.Panel2.Controls.Add(grpResultado);

        // El orden de Add importa con Dock: lo último agregado se acopla primero
        Controls.Add(split);
        Controls.Add(grpPregunta);
        Controls.Add(_lblTitulo);
    }

    private static void ConfigurarBoton(Button b, string texto, Color color)
    {
        b.Text = texto;
        b.Size = new Size(110, 40);
        b.FlatStyle = FlatStyle.Flat;
        b.FlatAppearance.BorderSize = 0;
        b.BackColor = color;
        b.ForeColor = Color.White;
        b.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        b.Cursor = Cursors.Hand;
    }

    // ------------------------------------------------------------
    //  Flujo del diagnóstico
    // ------------------------------------------------------------
    private async Task IniciarDiagnostico()
    {
        // Si había un diagnóstico a medias, se cancela
        _cts?.Cancel();
        var cts = new CancellationTokenSource();
        _cts = cts;

        _lstHistorial.Items.Clear();
        _rtbResultado.Clear();
        _btnNuevo.Enabled = false;
        _lblPregunta.Text = "Responda según los síntomas observables.";

        try
        {
            var motor = new MotorInferencia(pregunta => PreguntarAsync(pregunta, cts.Token));
            await motor.Inferir();

            MostrarResultado(motor);
            _lblPregunta.Text = "Diagnóstico terminado.";
            _btnSi.Enabled = false;
            _btnNo.Enabled = false;
        }
        catch (OperationCanceledException)
        {
            // Se inició un nuevo diagnóstico o se cerró la ventana: no hay nada que mostrar.
            return;
        }
        finally
        {
            if (_cts == cts) _btnNuevo.Enabled = true;
        }
    }

    /// <summary>Muestra la pregunta en el formulario y espera a que se presione Sí o No.</summary>
    private async Task<bool> PreguntarAsync(string pregunta, CancellationToken token)
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _respuestaPendiente = tcs;

        _lblPregunta.Text = pregunta;
        _btnSi.Enabled = true;
        _btnNo.Enabled = true;
        _btnNuevo.Enabled = true;

        using var registro = token.Register(() => tcs.TrySetCanceled());
        bool respuesta = await tcs.Task; // lanza OperationCanceledException si se canceló

        _btnSi.Enabled = false;
        _btnNo.Enabled = false;
        _lstHistorial.Items.Add($"{pregunta}  →  {(respuesta ? "SÍ" : "NO")}");
        _lstHistorial.TopIndex = _lstHistorial.Items.Count - 1;
        return respuesta;
    }

    // ------------------------------------------------------------
    //  Resultado y explicación
    // ------------------------------------------------------------
    private void MostrarResultado(MotorInferencia motor)
    {
        Escribir("DIAGNÓSTICO\n", Color.FromArgb(30, 64, 120), true, 13F);

        var fallas = motor.FallasEncontradas();

        if (fallas.Count > 0)
        {
            var principal = fallas[0];
            Escribir("Falla principal: ", Color.Black, true);
            Escribir($"{principal.Nombre}\n", Color.FromArgb(178, 34, 34), true);
            Escribir("Acción recomendada: ", Color.Black, true);
            Escribir($"{principal.Accion}\n", Color.Black, false);

            if (fallas.Count > 1)
            {
                Escribir("\nOtras causas que también coinciden (por prioridad):\n", Color.Black, true);
                foreach (var f in fallas.Skip(1))
                    Escribir($"  • {f.Nombre}: {f.Accion}\n", Color.Black, false);
            }
        }
        else if (motor.Deducido("sin_falla_hardware"))
        {
            Escribir("No es una falla de hardware.\n", Color.FromArgb(46, 139, 87), true);
            Escribir("La lentitud no presenta otros síntomas de hardware.\n", Color.Black, false);
        }
        else if (motor.Deducido("energia_sin_senales"))
        {
            // Caso no definido en la entrevista: problema de energía pero cable/regleta con falla.
            Escribir("Problema de energía, pero aún no se puede culpar a la fuente de poder.\n", Color.FromArgb(178, 34, 34), true);
            Escribir("El cable o la regleta están en mal estado: corríjalos y vuelva a diagnosticar.\n", Color.Black, false);
        }
        else
        {
            Escribir("No hay diagnóstico: no se detectó ningún síntoma de hardware.\n", Color.Black, true);
        }

        // Módulo de explicación: reglas que se activaron
        Escribir("\nREGLAS ACTIVADAS (explicación)\n", Color.FromArgb(30, 64, 120), true, 12F);
        if (motor.Disparadas.Count == 0)
            Escribir("Ninguna regla se activó.\n", Color.Black, false);
        else
            foreach (var r in motor.Disparadas)
                Escribir($"[{r.Id}] {r.Descripcion}\n", Color.Black, false);
    }

    private void Escribir(string texto, Color color, bool negrita, float tamano = 10F)
    {
        _rtbResultado.SelectionStart = _rtbResultado.TextLength;
        _rtbResultado.SelectionColor = color;
        _rtbResultado.SelectionFont = new Font("Segoe UI", tamano, negrita ? FontStyle.Bold : FontStyle.Regular);
        _rtbResultado.AppendText(texto);
    }
}
