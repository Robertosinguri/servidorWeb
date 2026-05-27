
/*
══════════════════════════════════════════════════════════════════════════
REQUISITO 1  - CONCURRENCIA  (Manejo de Carga)
REQUISITO 10 - SOCKETS TCP NATIVOS (Capa de Transporte)
══════════════════════════════════════════════════════════════════════════

Núcleo del Servidor Web. Utiliza sockets TCP para escuchar,
aceptar y despachar conexiones entrantes de forma concurrente y no bloqueante.

┌────────────────────────────────────────────────────────────────────────┐
│ Req 1 - Concurrencia Híbrida (ThreadPool + SemaphoreSlim)              │
│ "Debe poder atender un número indefinido de solicitudes..."            │
│                                                                        │
│ El servidor implementa un modelo de arquitectura de hilos híbrida para │
│ maximizar el rendimiento bajo estrés sin agotar los recursos:          │
│                                                                        │
│ 1. Despacho Inmediato (ThreadPool): El bucle principal acepta la       │
│    conexión y la delega al pool de hilos mediante QueueUserWorkItem.   │
│    Esto libera al hilo de escucha al instante para recibir más tráfico.│
│                                                                        │
│ 2. Control de Estrés (SemaphoreSlim 50, 50): El semáforo se adquiere   │
│    DENTRO del hilo de trabajo. Si hay más de 50 peticiones activas, los│
│    hilos excedentes esperan pacientemente en el Pool. Esto protege la  │
│    CPU, la memoria y el límite de descriptores de archivos del S.O.    │
└────────────────────────────────────────────────────────────────────────┘

┌────────────────────────────────────────────────────────────────────────┐
│ Req 10 - Sockets TCP Puros (Sin Abstracciones de Alto Nivel)           │
│ "...Sólo deben usar sockets y se deben parsear las solicitudes HTTP."  │
│                                                                        │
│ El servidor opera directamente sobre la capa de transporte utilizando  │
│ la clase primitiva 'Socket' de .NET configurada para TCP (Stream).     │
│                                                                        │
│ - Flujo de Red: Bind() (enlaza puerto) -> Listen() (habilita escucha)  │
│   -> Accept() (bloqueante, extrae el socket del cliente).              │
│ - El ciclo de vida y parseo del protocolo HTTP se delega por completo  │
│   a la capa de aplicación en 'ManejadorSolicitud.cs'.                  │
└────────────────────────────────────────────────────────────────────────┘

FLUJO DE UNA SOLICITUD:
[Cliente] ──> [Accept()] ──> [ThreadPool.Queue] ──> [Semaphore (¿< 50?)] ──> [Manejador.Procesar()]
══════════════════════════════════════════════════════════════════════════
*/

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace ServidorWeb; 

public class ServidorHTTP
{        
    private readonly Configuracion _config;
    private Socket? _socketEscucha;
    private volatile bool _ejecutando;
    private readonly SemaphoreSlim _semaforo = new(50, 50);
    
    public ServidorHTTP(Configuracion config)
    {
        _config = config;
    }       

    public void Iniciar()
    {
        try
        {
            _ejecutando = true;
            _socketEscucha = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, _config.Puerto);

            _socketEscucha.Bind(endPoint);              
            _socketEscucha.Listen(100);

            Console.WriteLine($"=========================================================");
            Console.WriteLine($"  Servidor Web iniciado en el puerto {_config.Puerto}");
            Console.WriteLine($"  Carpeta raíz: {_config.RutaRaiz}");                
            Console.WriteLine($"  http://localhost:{_config.Puerto}/");
            Console.WriteLine($"=========================================================");

            while (_ejecutando)
            {
                try
                {
                    Socket cliente = _socketEscucha.Accept();
                    Console.WriteLine($"[Servidor] Nueva conexión desde {cliente.RemoteEndPoint}");                                              
                    ThreadPool.QueueUserWorkItem(ProcesarCliente, cliente);
                }                    
                catch (Exception ex)
                {
                    // Control de Clientes: Si se destruye el objeto o se apaga el servidor, rompemos el bucle
                    if (ex is ObjectDisposedException || !_ejecutando)
                    {
                        break;
                    }

                    // Errores de red comunes durante la escucha de un cliente
                    Console.WriteLine($"[Servidor] Error al aceptar conexión: {ex.Message}");
                }
            }
        }

        catch (Exception ex)
        {
            // Control del Sistema: Atrapa errores críticos de inicio (ej. Puerto Ocupado)
            Console.WriteLine($"[Servidor] Error crítico en la inicialización: {ex.Message}");
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

        Console.WriteLine("[Servidor] Servidor detenido.");
    }

    private void ProcesarCliente(object? estado)
    {
        Socket? cliente = estado as Socket;
        if (cliente == null) return;
        _semaforo.Wait();

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
            _semaforo.Release();
        }
    }
}
