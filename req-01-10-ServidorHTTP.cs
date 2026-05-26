using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace ServidorWeb
{
    /*
    ═══════════════════════════════════════════════════════════════
    REQUISITO 1 - CONCURRENCIA (implementación principal)
    REQUISITO 10 - SOCKETS TCP (socket de escucha)
    ═══════════════════════════════════════════════════════════════

    Núcleo del servidor. Crea un socket TCP de escucha, acepta
    conexiones entrantes y las procesa concurrentemente usando
    un SemaphoreSlim para limitar la concurrencia.

    ┌─────────────────────────────────────────────────────────┐
    │ Req 1 - Concurrencia                                    │
    │ "Debe poder atender un número indefinido de solicitudes │
    │  en forma concurrente."                                 │
    └─────────────────────────────────────────────────────────┘
    - SemaphoreSlim(10, 10) limita a 10 hilos concurrentes
    - Cada cliente entrante crea un hilo, pero el semáforo
      bloquea si ya hay 10 ejecutándose
    - No hay cola FIFO, no hay Monitor.Wait/Pulse
    - El semáforo es más liviano y robusto que un Monitor

    ┌─────────────────────────────────────────────────────────┐
    │ Req 10 - Sockets TCP                                    │
    │ "Sólo deben usar sockets (directamente en la capa de    │
    │  transporte) y se deben parsear las solicitudes HTTP."  │
    └─────────────────────────────────────────────────────────┘
    - Socket TCP (AddressFamily.InterNetwork, SocketType.Stream)
    - Bind + Listen + Accept (bucle infinito)
    - No se usa HttpListener, TcpListener, ASP.NET, etc.
    - El parseo HTTP se hace en SolicitudHTTP.cs
    ═══════════════════════════════════════════════════════════════
    */

    public class ServidorHTTP
    {
        // ── Constantes ──

        // Máximo de hilos que pueden ejecutarse concurrentemente.
        // SemaphoreSlim(10, 10) permite hasta 10 hilos a la vez.
        // Si llega un cliente #11, el hilo aceptador se bloquea
        // hasta que uno de los 10 termine y libere el semáforo.
        private const int MAX_CONCURRENCIA = 10;

        // ── Campos privados ──

        private readonly Configuracion _config;
        private Socket? _socketEscucha;
        private volatile bool _ejecutando;

        // Semáforo que limita la concurrencia.
        // - InitialCount: 10 (hilos que pueden entrar sin esperar)
        // - MaximumCount: 10 (máximo de hilos concurrentes)
        // Cuando un hilo termina, llama Release() para liberar un lugar.
        private readonly SemaphoreSlim _semaforo = new(MAX_CONCURRENCIA, MAX_CONCURRENCIA);

        // ── Constructor ──

        public ServidorHTTP(Configuracion config)
        {
            _config = config;
        }

        // ── Métodos públicos ──

        public void Iniciar()
        {
            try
            {
                _ejecutando = true;

                // ── Req 10: Socket TCP (pasos 1-3) ──
                // Creamos un socket TCP/IPv4 manualmente.
                // No usamos TcpListener ni HttpListener.
                _socketEscucha = new Socket(AddressFamily.InterNetwork,
                                            SocketType.Stream,
                                            ProtocolType.Tcp);

                // Asociar (bind) el socket a la dirección y puerto
                IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, _config.Puerto);
                _socketEscucha.Bind(endPoint);

                // Poner en modo escucha
                _socketEscucha.Listen(100);

                Console.WriteLine($"===========================================");
                Console.WriteLine($"  Servidor Web iniciado en el puerto {_config.Puerto}");
                Console.WriteLine($"  Carpeta raíz: {_config.RutaRaiz}");
                Console.WriteLine($"  Concurrencia máxima: {MAX_CONCURRENCIA} hilos (SemaphoreSlim)");
                Console.WriteLine($"  http://localhost:{_config.Puerto}/");
                Console.WriteLine($"===========================================");

                // ── Req 10: Bucle de aceptación ──
                // Acepta conexiones entrantes y las procesa.
                // El SemaphoreSlim bloquea si ya hay 10 hilos activos.
                while (_ejecutando)
                {
                    try
                    {
                        Socket cliente = _socketEscucha.Accept();
                        Console.WriteLine($"[Servidor] Nueva conexión desde {cliente.RemoteEndPoint}");

                        // ── Req 1: Esperar lugar en el semáforo ──
                        // Si ya hay 10 hilos ejecutándose, este hilo
                        // se bloquea aquí hasta que uno termine.
                        _semaforo.Wait();

                        // Creamos un hilo para procesar al cliente.
                        // El hilo libera el semáforo al terminar.
                        Thread hilo = new Thread(() => ProcesarCliente(cliente))
                        {
                            IsBackground = true
                        };
                        hilo.Start();
                    }
                    catch (ObjectDisposedException) { break; }
                    catch (Exception ex)
                    {
                        if (_ejecutando)
                            Console.WriteLine($"[Servidor] Error al aceptar conexión: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Servidor] Error fatal: {ex.Message}");
            }
            finally
            {
                Detener();
            }
        }

        public void Detener()
        {
            _ejecutando = false;

            if (_socketEscucha != null)
            {
                try { _socketEscucha.Close(); } catch { }
                _socketEscucha = null;
            }

            // Liberar el semáforo para que los hilos bloqueados
            // en _semaforo.Wait() puedan continuar y terminar.
            try { _semaforo.Release(MAX_CONCURRENCIA); } catch { }

            Console.WriteLine("[Servidor] Servidor detenido.");
        }

        // ── Métodos privados - Procesamiento ──

        // Procesa un cliente y libera el semáforo al terminar.
        // Se ejecuta en un hilo separado por cada cliente.
        private void ProcesarCliente(Socket cliente)
        {
            try
            {
                Console.WriteLine($"[Servidor] Procesando cliente {cliente.RemoteEndPoint}");

                var manejador = new ManejadorSolicitud(cliente, _config);
                manejador.Procesar();

                Console.WriteLine($"[Servidor] Cliente {cliente.RemoteEndPoint} atendido.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Servidor] Error al procesar cliente: {ex.Message}");
                try { cliente.Close(); } catch { }
            }
            finally
            {
                // Liberar el semáforo para que otro cliente pueda entrar.
                // Esto es CRÍTICO: si no se libera, se pierde un lugar
                // y eventualmente solo podrán entrar 9, luego 8, etc.
                _semaforo.Release();
            }
        }
    }
}
