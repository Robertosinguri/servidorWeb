/*
═══════════════════════════════════════════════════════════════
REQUISITO 9 - LOG POR DÍA CON IP DE ORIGEN
═══════════════════════════════════════════════════════════════

Registra todas las solicitudes HTTP en archivos de log diarios,
incluyendo la IP de origen del cliente.

┌─────────────────────────────────────────────────────────┐
│ Req 9 - Log por día                                     │
│ "Los datos de todas las solicitudes deben loguearse en  │
│  un archivo por día, incluyendo la IP de origen."       │
└─────────────────────────────────────────────────────────┘
- Cada día se crea un archivo: logs/YYYY-MM-DD.log
- Formato: [YYYY-MM-DD HH:mm:ss] IP - METODO Ruta - Código
- Usa lock(_candado) para seguridad multi-hilo
═══════════════════════════════════════════════════════════════
*/

using System;
using System.IO;
using System.Text;

namespace ServidorWeb;

public static class Logger
{
    // Objeto para sincronizar el acceso al archivo de log.
    // lock garantiza que solo un hilo escriba a la vez.
    private static readonly object _candado = new();

    private static readonly string _carpetaLogs;
    private static string _fechaActual = "";
    private static string _rutaArchivoActual = "";

    static Logger()
    {
        _carpetaLogs = Path.Combine(Directory.GetCurrentDirectory(), "logs");
        ActualizarRutaArchivo();
    }

    // Registra una solicitud HTTP en el archivo de log del día.
    // lock(_candado) evita corrupción por escritura concurrente.
    public static void Registrar(string ipOrigen, string metodo, string ruta,
                                    string codigoEstado,
                                    string? parametros = null, string? cuerpo = null)
    {
        try
        {
            lock (_candado)
            {
                ActualizarRutaArchivo();

                StringBuilder sb = new StringBuilder();
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                sb.Append($"[{timestamp}] {ipOrigen} - {metodo} {ruta} - {codigoEstado}");

                if (!string.IsNullOrEmpty(parametros))
                    sb.Append($" | Parámetros: {parametros}");

                if (!string.IsNullOrEmpty(cuerpo))
                    sb.Append($" | Body: {cuerpo}");

                sb.AppendLine();

                File.AppendAllText(_rutaArchivoActual, sb.ToString());
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Logger ERROR] No se pudo escribir en el log: {ex.Message}");
        }
    }

    private static void ActualizarRutaArchivo()
    {
        string fechaHoy = DateTime.Now.ToString("yyyy-MM-dd");

        if (fechaHoy != _fechaActual)
        {
            _fechaActual = fechaHoy;

            if (!Directory.Exists(_carpetaLogs))
                Directory.CreateDirectory(_carpetaLogs);

            _rutaArchivoActual = Path.Combine(_carpetaLogs, $"{fechaHoy}.log");
        }
    }
}

