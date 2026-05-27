using System;
using System.IO;
using System.Net.Sockets;
using System.Text;

namespace ServidorWeb
{
    /*
    ═══════════════════════════════════════════════════════════════
    REQUISITO 2 - INDEX.HTML POR DEFECTO
    REQUISITO 5 - ERROR 404 PERSONALIZADO
    REQUISITO 6 - GET Y POST (procesamiento)
    ═══════════════════════════════════════════════════════════════

    Procesa cada solicitud HTTP individual: parsea la solicitud,
    determina el archivo a servir, lo lee del disco, construye la
    respuesta y la envía al cliente.

    ┌─────────────────────────────────────────────────────────┐
    │ Req 2 - index.html por defecto                          │
    │ "Por defecto, deberá servir el archivo index.html, si   │
    │  la URL no especifica el archivo."                      │
    └─────────────────────────────────────────────────────────┘
    En ObtenerRutaArchivo():
    - Si la ruta está vacía o es "/" → se sirve "/index.html"

    ┌─────────────────────────────────────────────────────────┐
    │ Req 5 - Error 404                                       │
    │ "En caso de que el usuario haya solicitado un archivo   │
    │  inexistente, deberá devolver un código de error 404    │
    │  y un documento personalizado indicando el error."      │
    └─────────────────────────────────────────────────────────┘
    En Procesar():
    - Si File.Exists() es false → llama a RespuestaHTTP.NotFound()
    - NotFound() carga wwwroot/404.html o usa HTML hardcodeado

    ┌─────────────────────────────────────────────────────────┐
    │ Req 6 - GET y POST                                      │
    │ "Debe aceptar solicitudes de tipo GET y POST. En el     │
    │  caso de solicitudes POST, sólo deben loguearse los     │
    │  datos recibidos."                                      │
    └─────────────────────────────────────────────────────────┘
    - GET: se sirve el archivo solicitado (flujo normal)
    - POST: solo se loguean los datos recibidos, no se sirve
      ningún archivo. Responde 200 OK sin contenido.
    ═══════════════════════════════════════════════════════════════
    */

    public class ManejadorSolicitud
    {
        private readonly Socket _cliente;
        private readonly Configuracion _config;
        private readonly string _rutaRaizAbsoluta;

        public ManejadorSolicitud(Socket cliente, Configuracion config)
        {
            _cliente = cliente;
            _config = config;

            _rutaRaizAbsoluta = Path.GetFullPath(
                Path.Combine(Directory.GetCurrentDirectory(), _config.RutaRaiz)
            );
        }

        // Procesa la solicitud HTTP completa.
        // Llamado desde el hilo asignado a esta conexión.
        public void Procesar()
        {
            try
            {
                // Parsear la solicitud HTTP
                SolicitudHTTP? solicitud = SolicitudHTTP.Parsear(_cliente);

                if (solicitud == null)
                {
                    EnviarRespuesta(RespuestaHTTP.SolicitudInvalida());
                    return;
                }

                // ── Req 6: POST - solo loguear datos recibidos ──
                if (solicitud.Metodo == "POST")
                {
                    string parametrosStr = ObtenerParametrosComoString(solicitud);
                    string? cuerpoLog = !string.IsNullOrEmpty(solicitud.Cuerpo)
                        ? (solicitud.Cuerpo.Length > 500
                            ? solicitud.Cuerpo[..500] + "... [truncado]"
                            : solicitud.Cuerpo)
                        : null;

                    Logger.Registrar(solicitud.IPOrigen, solicitud.Metodo,
                                     solicitud.Ruta, "200", parametrosStr, cuerpoLog);

                    byte[] respuestaOK = RespuestaHTTP.OK([], "text/plain", false);
                    EnviarRespuesta(respuestaOK);
                    return;
                }

                // ── Req 2: Determinar la ruta del archivo ──
                // ObtenerRutaArchivo() convierte "/" en "/index.html"
                string rutaArchivo = ObtenerRutaArchivo(solicitud.Ruta);

                // ── Req 5: Verificar si el archivo existe ──
                if (!File.Exists(rutaArchivo))
                {
                    byte[] respuesta404 = RespuestaHTTP.NotFound();
                    EnviarRespuesta(respuesta404);

                    string parametrosStr = ObtenerParametrosComoString(solicitud);
                    Logger.Registrar(solicitud.IPOrigen, solicitud.Metodo,
                                     solicitud.Ruta, "404", parametrosStr);
                    return;
                }

                // Leer el archivo del disco
                byte[] contenidoArchivo = File.ReadAllBytes(rutaArchivo);

                // Determinar el tipo MIME
                string tipoMIME = ObtenerTipoMIME(rutaArchivo);

                // Determinar si se debe comprimir (texto = compresible)
                bool usarCompresion = EsTipoCompresible(tipoMIME);

                // Construir y enviar respuesta
                byte[] respuesta = RespuestaHTTP.OK(contenidoArchivo, tipoMIME, usarCompresion);
                EnviarRespuesta(respuesta);

                // ── Req 6: Registrar en el log ──
                string parametrosStrLog = ObtenerParametrosComoString(solicitud);
                Logger.Registrar(solicitud.IPOrigen, solicitud.Metodo,
                                 solicitud.Ruta, "200", parametrosStrLog);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ManejadorSolicitud] Error: {ex.Message}");
                try { EnviarRespuesta(RespuestaHTTP.ErrorInterno()); } catch { }
            }
            finally
            {
                try
                {
                    if (_cliente.Connected)
                        _cliente.Shutdown(SocketShutdown.Both);
                    _cliente.Close();
                }
                catch { }
            }
        }

        // ── Métodos privados ──

        private void EnviarRespuesta(byte[] respuesta)
        {
            if (_cliente.Connected)
                _cliente.Send(respuesta);
        }

        // Convierte la ruta URL solicitada en una ruta de archivo del sistema.
        // ── Req 2: Si la ruta está vacía o es "/", sirve index.html ──
        private string ObtenerRutaArchivo(string rutaURL)
        {
            if (string.IsNullOrEmpty(rutaURL) || rutaURL == "/")
            {
                rutaURL = "/index.html";
            }

            string rutaNormalizada = rutaURL.Replace('/', Path.DirectorySeparatorChar);

            if (rutaNormalizada.StartsWith(Path.DirectorySeparatorChar))
                rutaNormalizada = rutaNormalizada[1..];

            string rutaCompleta = Path.Combine(_rutaRaizAbsoluta, rutaNormalizada);

            // Seguridad: evitar Path Traversal (../../archivo-secreto.txt)
            string rutaCompletaAbsoluta = Path.GetFullPath(rutaCompleta);
            if (!rutaCompletaAbsoluta.StartsWith(_rutaRaizAbsoluta, StringComparison.OrdinalIgnoreCase))
            {
                return Path.Combine(_rutaRaizAbsoluta, "__no_existe__");
            }

            return rutaCompletaAbsoluta;
        }

        private static string ObtenerTipoMIME(string rutaArchivo)
        {
            string extension = Path.GetExtension(rutaArchivo).ToLowerInvariant();

            return extension switch
            {
                ".html" or ".htm" => "text/html; charset=utf-8",
                ".css" => "text/css; charset=utf-8",
                ".js" => "application/javascript; charset=utf-8",
                ".json" => "application/json; charset=utf-8",
                ".xml" => "application/xml; charset=utf-8",
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".svg" => "image/svg+xml",
                ".ico" => "image/x-icon",
                ".webp" => "image/webp",
                ".woff" => "font/woff",
                ".woff2" => "font/woff2",
                ".ttf" => "font/ttf",
                ".pdf" => "application/pdf",
                ".zip" => "application/zip",
                ".txt" => "text/plain; charset=utf-8",
                _ => "application/octet-stream",
            };
        }

        private static bool EsTipoCompresible(string tipoMIME)
        {
            return tipoMIME.StartsWith("text/") ||
                   tipoMIME.Contains("javascript") ||
                   tipoMIME.Contains("json") ||
                   tipoMIME.Contains("xml") ||
                   tipoMIME.Contains("svg");
        }

        private static string ObtenerParametrosComoString(SolicitudHTTP solicitud)
        {
            if (solicitud.ParametrosConsulta.Count == 0)
                return "";

            var sb = new StringBuilder();
            bool primero = true;
            foreach (var kvp in solicitud.ParametrosConsulta)
            {
                if (!primero) sb.Append(", ");
                sb.Append($"{kvp.Key}={kvp.Value}");
                primero = false;
            }
            return sb.ToString();
        }
    }
}
