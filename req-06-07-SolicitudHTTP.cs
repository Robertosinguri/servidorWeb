/*
═══════════════════════════════════════════════════════════════
REQUISITO 6 - GET Y POST (parseo de solicitudes)
REQUISITO 7 - PARÁMETROS DE CONSULTA
═══════════════════════════════════════════════════════════════

Parsea una solicitud HTTP cruda recibida a través de un socket
TCP. Extrae el método, la ruta, las cabeceras, el cuerpo y los
parámetros de consulta.

┌─────────────────────────────────────────────────────────┐
│ Req 6 - GET y POST                                      │
│ "Debe aceptar solicitudes de tipo GET y POST. En el     │
│  caso de solicitudes POST, sólo deben loguearse los     │
│  datos recibidos."                                      │
└─────────────────────────────────────────────────────────┘
En Parsear():
- Lee la línea de solicitud: "GET /ruta HTTP/1.1"
- Extrae el método (GET, POST, etc.) en la propiedad Metodo
- Extrae la ruta solicitada en la propiedad Ruta
- Extrae el cuerpo (para POST) en la propiedad Cuerpo

┌─────────────────────────────────────────────────────────┐
│ Req 7 - Parámetros de consulta                          │
│ "Los parámetros de consulta (query string) deben ser    │
│  parseados y logueados."                                │
└─────────────────────────────────────────────────────────┘
En Parsear() y ParsearQueryString():
- Detecta "?" en la ruta y separa ruta de query string
- Parsea "clave=valor&clave2=valor2" en un diccionario
- Los parámetros se guardan en ParametrosConsulta
═══════════════════════════════════════════════════════════════
*/

using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace ServidorWeb;

public class SolicitudHTTP
{
    // ── Propiedades públicas ──

    public string Metodo { get; private set; } = "";           // GET, POST, etc.
    public string Ruta { get; private set; } = "";             // /index.html
    public string Version { get; private set; } = "";          // HTTP/1.1
    public Dictionary<string, string> Cabeceras { get; private set; } = new();
    public string Cuerpo { get; private set; } = "";           // Para POST

    // ── Req 7: Parámetros de consulta ──
    // Ej: "/pagina?nombre=Juan&edad=25" → {"nombre":"Juan", "edad":"25"}
    public Dictionary<string, string> ParametrosConsulta { get; private set; } = new();

    public string IPOrigen { get; private set; } = "";

    // ── Método estático de parseo ──

    // Parsea una solicitud HTTP desde un socket de cliente.
    // Lee los datos entrantes, interpreta el protocolo HTTP y
    // devuelve un objeto SolicitudHTTP estructurado.
    public static SolicitudHTTP? Parsear(Socket cliente)
    {
        try
        {
            var solicitud = new SolicitudHTTP();

            // Obtener IP de origen
            System.Net.IPEndPoint? ep = (System.Net.IPEndPoint?)cliente.RemoteEndPoint;
            solicitud.IPOrigen = ep?.Address.ToString() ?? "desconocida";

            // Buffer de 8KB para leer datos entrantes
            byte[] buffer = new byte[8192];
            StringBuilder datosCrudos = new StringBuilder();

            int bytesLeidos;
            bool cabecerasCompletas = false;

            while ((bytesLeidos = cliente.Receive(buffer)) > 0)
            {
                datosCrudos.Append(Encoding.UTF8.GetString(buffer, 0, bytesLeidos));

                if (datosCrudos.ToString().Contains("\r\n\r\n"))
                {
                    cabecerasCompletas = true;
                    break;
                }
            }

            if (!cabecerasCompletas || datosCrudos.Length == 0)
                return null;

            string solicitudCompleta = datosCrudos.ToString();

            // Separar cabeceras y cuerpo
            int indiceSeparador = solicitudCompleta.IndexOf("\r\n\r\n");
            string parteCabeceras = solicitudCompleta[..indiceSeparador];
            string parteCuerpo = "";

            if (indiceSeparador + 4 < solicitudCompleta.Length)
                parteCuerpo = solicitudCompleta[(indiceSeparador + 4)..];

            solicitud.Cuerpo = parteCuerpo;

            // Parsear línea de solicitud: METODO Ruta HTTP/Version
            string[] lineas = parteCabeceras.Split("\r\n");
            if (lineas.Length == 0) return null;

            string[] partesLineaInicial = lineas[0].Split(' ');
            if (partesLineaInicial.Length < 3) return null;

            // ── Req 6: Extraer método HTTP (GET, POST, etc.) ──
            solicitud.Metodo = partesLineaInicial[0].ToUpper();
            string rutaCompleta = partesLineaInicial[1];
            solicitud.Version = partesLineaInicial[2];

            // ── Req 7: Separar ruta de parámetros de consulta ──
            int indiceQuery = rutaCompleta.IndexOf('?');
            if (indiceQuery >= 0)
            {
                solicitud.Ruta = rutaCompleta[..indiceQuery];
                string queryString = rutaCompleta[(indiceQuery + 1)..];
                solicitud.ParametrosConsulta = ParsearQueryString(queryString);
            }
            else
            {
                solicitud.Ruta = rutaCompleta;
            }

            // Parsear cabeceras
            for (int i = 1; i < lineas.Length; i++)
            {
                string linea = lineas[i];
                if (string.IsNullOrWhiteSpace(linea)) continue;

                int indiceDosPuntos = linea.IndexOf(':');
                if (indiceDosPuntos > 0)
                {
                    string clave = linea[..indiceDosPuntos].Trim();
                    string valor = linea[(indiceDosPuntos + 1)..].Trim();
                    solicitud.Cabeceras[clave] = valor;
                }
            }

            return solicitud;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SolicitudHTTP] Error al parsear solicitud: {ex.Message}");
            return null;
        }
    }

    // ── Métodos privados ──

    // ── Req 7: Parsea query string en diccionario ──
    // Ej: "nombre=Juan&edad=25" → {"nombre":"Juan", "edad":"25"}
    private static Dictionary<string, string> ParsearQueryString(string queryString)
    {
        var parametros = new Dictionary<string, string>();

        if (string.IsNullOrEmpty(queryString))
            return parametros;

        string[] pares = queryString.Split('&');
        foreach (string par in pares)
        {
            if (string.IsNullOrEmpty(par)) continue;

            int indiceIgual = par.IndexOf('=');
            if (indiceIgual > 0)
            {
                string clave = Uri.UnescapeDataString(par[..indiceIgual]);
                string valor = "";

                if (indiceIgual + 1 < par.Length)
                    valor = Uri.UnescapeDataString(par[(indiceIgual + 1)..]);

                parametros[clave] = valor;
            }
            else
            {
                parametros[Uri.UnescapeDataString(par)] = "";
            }
        }

        return parametros;
    }
}

