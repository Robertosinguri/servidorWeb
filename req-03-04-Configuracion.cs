/*
═══════════════════════════════════════════════════════════════
REQUISITO 3 - CARPETA RAÍZ CONFIGURABLE
REQUISITO 4 - PUERTO CONFIGURABLE
═══════════════════════════════════════════════════════════════

Lee la configuración del servidor desde un archivo JSON externo
(appsettings.json) y expone las propiedades Puerto y RutaRaiz.

┌─────────────────────────────────────────────────────────┐
│ Req 3 - Carpeta raíz configurable                       │
│ "La carpeta raíz desde donde se servirán los archivos   │
│  debe ser configurable."                                │
└─────────────────────────────────────────────────────────┘
- Propiedad RutaRaiz con valor por defecto "wwwroot"
- Se carga desde appsettings.json → Servidor.RutaRaiz

┌─────────────────────────────────────────────────────────┐
│ Req 4 - Puerto configurable                             │
│ "El puerto de escucha debe ser configurable."           │
└─────────────────────────────────────────────────────────┘
- Propiedad Puerto con valor por defecto 8080
- Se carga desde appsettings.json → Servidor.Puerto

Formato de appsettings.json:
{
    "Servidor": {
    "Puerto": 8080,
    "RutaRaiz": "wwwroot"
    }
}
═══════════════════════════════════════════════════════════════
*/

using System;
using System.IO;
using System.Text.Json;

namespace ServidorWeb;

public class Configuracion
{
    // ── Req 4: Puerto configurable (default: 8080) ──
    public int Puerto { get; set; } = 8080;

    // ── Req 3: Ruta raíz configurable (default: "wwwroot") ──
    public string RutaRaiz { get; set; } = "wwwroot";

    // Carga la configuración desde el archivo JSON especificado.
    // Si el archivo no existe o hay error, usa valores por defecto.
    public static Configuracion Cargar(string rutaArchivo = "appsettings.json")
    {
        try
        {
            if (!File.Exists(rutaArchivo))
            {
                Console.WriteLine($"[Configuracion] Archivo '{rutaArchivo}' no encontrado. Usando valores por defecto.");
                return new Configuracion();
            }

            string json = File.ReadAllText(rutaArchivo);
            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement raiz = doc.RootElement;

            if (raiz.TryGetProperty("Servidor", out JsonElement seccionServidor))
            {
                var config = new Configuracion();

                // Req 4: Leer puerto configurable
                if (seccionServidor.TryGetProperty("Puerto", out JsonElement puertoElem))
                    config.Puerto = puertoElem.GetInt32();

                // Req 3: Leer ruta raíz configurable
                if (seccionServidor.TryGetProperty("RutaRaiz", out JsonElement rutaElem))
                    config.RutaRaiz = rutaElem.GetString() ?? "wwwroot";

                Console.WriteLine($"[Configuracion] Configuración cargada: Puerto={config.Puerto}, RutaRaiz='{config.RutaRaiz}'");
                return config;
            }

            Console.WriteLine("[Configuracion] No se encontró la sección 'Servidor'. Usando valores por defecto.");
            return new Configuracion();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Configuracion] Error al cargar configuración: {ex.Message}. Usando valores por defecto.");
            return new Configuracion();
        }
    }
}

