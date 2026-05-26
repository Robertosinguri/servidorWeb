using System;

namespace ServidorWeb
{
    /*
    ═══════════════════════════════════════════════════════════════
    REQUISITO 1 - CONCURRENCIA (parte inicial)
    REQUISITO 10 - SOCKETS TCP (punto de entrada)
    ═══════════════════════════════════════════════════════════════

    Punto de entrada de la aplicación. Inicializa la configuración,
    crea el servidor HTTP y lo inicia.

    Req 1 - Concurrencia:
    Inicia el servidor que contiene un pool de hilos (ver
    req-01-10-ServidorHTTP.cs). El pool permite atender múltiples
    solicitudes simultáneamente sin crear un hilo por conexión.

    Req 10 - Sockets TCP:
    Todo el servidor usa System.Net.Sockets (TCP) directamente.
    No se usa HttpListener, ASP.NET ni ninguna biblioteca HTTP.
    El parseo del protocolo HTTP/1.1 se hace manualmente.
    ═══════════════════════════════════════════════════════════════
    */

    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("╔══════════════════════════════════════════════════╗");
            Console.WriteLine("║     SERVIDOR WEB C# - PROGRAMACIÓN CONCURRENTE   ║");
            Console.WriteLine("║     Socket TCP • HTTP/1.1 • Hilos • GZip         ║");
            Console.WriteLine("╚══════════════════════════════════════════════════╝");
            Console.WriteLine();

            // Cargar configuración desde appsettings.json
            Configuracion config = Configuracion.Cargar("appsettings.json");

            // Crear e iniciar el servidor HTTP (bloqueante hasta Ctrl+C)
            var servidor = new ServidorHTTP(config);

            // Manejar Ctrl+C para detener el servidor ordenadamente
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                Console.WriteLine();
                Console.WriteLine("[Program] Ctrl+C detectado. Deteniendo servidor...");
                servidor.Detener();
            };

            servidor.Iniciar();

            Console.WriteLine("[Program] Servidor finalizado.");
        }
    }
}
