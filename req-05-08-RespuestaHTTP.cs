using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ServidorWeb
{
    /*
    ═══════════════════════════════════════════════════════════════
    REQUISITO 5 - ERROR 404 PERSONALIZADO (respuesta)
    REQUISITO 8 - COMPRESIÓN GZIP (aplicación en respuesta)
    ═══════════════════════════════════════════════════════════════

    Construye respuestas HTTP/1.1 completas (cabeceras + cuerpo)
    para diferentes códigos de estado: 200 OK, 404 Not Found,
    400 Bad Request, 500 Internal Server Error.

    ┌─────────────────────────────────────────────────────────┐
    │ Req 5 - Error 404                                       │
    │ "En caso de que el usuario haya solicitado un archivo   │
    │  inexistente, deberá devolver un código de error 404    │
    │  y un documento personalizado indicando el error."      │
    └─────────────────────────────────────────────────────────┘
    En NotFound():
    - Carga wwwroot/404.html si existe (documento personalizado)
    - Si no existe, usa un HTML hardcodeado con estilo visual
    - Devuelve HTTP/1.1 404 Not Found

    ┌─────────────────────────────────────────────────────────┐
    │ Req 8 - Compresión GZip                                 │
    │ "Se debe implementar compresión GZip para reducir el    │
    │  tamaño de las respuestas."                             │
    └─────────────────────────────────────────────────────────┘
    En OK():
    - Si usarCompresion=true, comprime el contenido con GZip
    - Solo comprime si el resultado es más pequeño que el original
    - Agrega cabecera "Content-Encoding: gzip" a la respuesta
    - La compresión real la hace CompresorGZip.Comprimir()
    ═══════════════════════════════════════════════════════════════
    */

    public class RespuestaHTTP
    {
        // ── Req 8: Respuesta 200 OK con compresión opcional ──
        public static byte[] OK(byte[] contenido, string tipoMIME, bool usarCompresion = false)
        {
            var cabeceras = new Dictionary<string, string>
            {
                ["Content-Type"] = tipoMIME,
                ["Server"] = "ServidorWeb-CSharp/1.0",
                ["Connection"] = "close"
            };

            byte[] cuerpoFinal = contenido;

            if (usarCompresion && contenido.Length > 0)
            {
                byte[] comprimido = CompresorGZip.Comprimir(contenido);
                if (comprimido.Length < contenido.Length)
                {
                    cuerpoFinal = comprimido;
                    cabeceras["Content-Encoding"] = "gzip";
                }
            }

            cabeceras["Content-Length"] = cuerpoFinal.Length.ToString();
            return ConstruirRespuesta("200", "OK", cabeceras, cuerpoFinal);
        }

        // ── Req 5: Respuesta 404 con página personalizada ──
        public static byte[] NotFound()
        {
            string html = ObtenerPagina404();
            byte[] cuerpo = Encoding.UTF8.GetBytes(html);

            var cabeceras = new Dictionary<string, string>
            {
                ["Content-Type"] = "text/html; charset=utf-8",
                ["Content-Length"] = cuerpo.Length.ToString(),
                ["Server"] = "ServidorWeb-CSharp/1.0",
                ["Connection"] = "close"
            };

            return ConstruirRespuesta("404", "Not Found", cabeceras, cuerpo);
        }

        public static byte[] ErrorInterno()
        {
            string html = "<html><body><h1>500 - Error Interno del Servidor</h1><p>Ocurrió un error inesperado.</p></body></html>";
            byte[] cuerpo = Encoding.UTF8.GetBytes(html);

            var cabeceras = new Dictionary<string, string>
            {
                ["Content-Type"] = "text/html; charset=utf-8",
                ["Content-Length"] = cuerpo.Length.ToString(),
                ["Server"] = "ServidorWeb-CSharp/1.0",
                ["Connection"] = "close"
            };

            return ConstruirRespuesta("500", "Internal Server Error", cabeceras, cuerpo);
        }

        public static byte[] SolicitudInvalida()
        {
            string html = "<html><body><h1>400 - Solicitud Inválida</h1><p>El servidor no pudo entender la solicitud.</p></body></html>";
            byte[] cuerpo = Encoding.UTF8.GetBytes(html);

            var cabeceras = new Dictionary<string, string>
            {
                ["Content-Type"] = "text/html; charset=utf-8",
                ["Content-Length"] = cuerpo.Length.ToString(),
                ["Server"] = "ServidorWeb-CSharp/1.0",
                ["Connection"] = "close"
            };

            return ConstruirRespuesta("400", "Bad Request", cabeceras, cuerpo);
        }

        // ── Métodos privados ──

        // Construye el mensaje HTTP completo: línea de estado + cabeceras + cuerpo
        private static byte[] ConstruirRespuesta(string codigo, string mensaje, Dictionary<string, string> cabeceras, byte[] cuerpo)
        {
            string lineaEstado = $"HTTP/1.1 {codigo} {mensaje}\r\n";

            StringBuilder sb = new StringBuilder();
            sb.Append(lineaEstado);

            foreach (var kvp in cabeceras)
                sb.Append($"{kvp.Key}: {kvp.Value}\r\n");

            sb.Append("\r\n");

            byte[] cabecerasBytes = Encoding.UTF8.GetBytes(sb.ToString());

            byte[] respuestaCompleta = new byte[cabecerasBytes.Length + cuerpo.Length];
            Buffer.BlockCopy(cabecerasBytes, 0, respuestaCompleta, 0, cabecerasBytes.Length);
            Buffer.BlockCopy(cuerpo, 0, respuestaCompleta, cabecerasBytes.Length, cuerpo.Length);

            return respuestaCompleta;
        }

        // ── Req 5: Obtiene la página 404 personalizada ──
        // Busca wwwroot/404.html. Si no existe, usa HTML hardcodeado.
        private static string ObtenerPagina404()
        {
            string ruta404 = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "404.html");
            if (File.Exists(ruta404))
                return File.ReadAllText(ruta404);

            return @"<html>
<head><title>404 - No Encontrado</title>
<style>
    body { font-family: Arial, sans-serif; text-align: center; padding: 50px; background: #1a1a2e; color: #eee; }
    h1 { font-size: 72px; color: #e94560; margin: 0; }
    h2 { color: #ccc; }
    .container { max-width: 600px; margin: auto; }
</style>
</head>
<body>
<div class='container'>
    <h1>404</h1>
    <h2>Archivo No Encontrado</h2>
    <p>El recurso solicitado no existe en este servidor.</p>
    <p><a href='/' style='color: #0f3460;'>Volver al inicio</a></p>
</div>
</body>
</html>";
        }
    }
}
