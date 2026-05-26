using System;
using System.IO;
using System.IO.Compression;

namespace ServidorWeb
{
    /*
    ═══════════════════════════════════════════════════════════════
    REQUISITO 8 - COMPRESIÓN GZIP
    ═══════════════════════════════════════════════════════════════

    Proporciona métodos para comprimir y descomprimir datos usando
    el algoritmo GZip (RFC 1952).

    ┌─────────────────────────────────────────────────────────┐
    │ Req 8 - Compresión GZip                                 │
    │ "Se debe implementar compresión GZip para reducir el    │
    │  tamaño de las respuestas."                             │
    └─────────────────────────────────────────────────────────┘
    - Comprimir(): usa GZipStream con CompressionLevel.Fastest
    - Se llama desde RespuestaHTTP.OK() cuando el tipo MIME es texto
    - Solo se aplica si el resultado comprimido es más pequeño
    ═══════════════════════════════════════════════════════════════
    */

    public static class CompresorGZip
    {
        // Comprime un array de bytes usando GZip.
        // CompressionLevel.Fastest prioriza velocidad sobre tasa de compresión.
        public static byte[] Comprimir(byte[] datos)
        {
            if (datos == null || datos.Length == 0)
                return datos ?? Array.Empty<byte>();

            using (var memoria = new MemoryStream())
            {
                using (var gzip = new GZipStream(memoria, CompressionLevel.Fastest))
                {
                    gzip.Write(datos, 0, datos.Length);
                }

                return memoria.ToArray();
            }
        }

        // Descomprime datos comprimidos con GZip (útil para pruebas).
        public static byte[] Descomprimir(byte[] datosComprimidos)
        {
            if (datosComprimidos == null || datosComprimidos.Length == 0)
                return datosComprimidos ?? Array.Empty<byte>();

            using (var memoriaEntrada = new MemoryStream(datosComprimidos))
            using (var memoriaSalida = new MemoryStream())
            {
                using (var gzip = new GZipStream(memoriaEntrada, CompressionMode.Decompress))
                {
                    gzip.CopyTo(memoriaSalida);
                }

                return memoriaSalida.ToArray();
            }
        }
    }
}
