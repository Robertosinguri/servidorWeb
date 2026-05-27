# Servidor Web C# - Programación Concurrente con Sockets TCP

## 📋 Requisitos del Trabajo Práctico

| # | Requisito | Archivo(s) |
|---|-----------|------------|
| **1** | **Concurrencia** - Debe poder atender un número indefinido de solicitudes en forma concurrente. | `req-01-10-ServidorHTTP.cs` |
| **2** | **Index.html por defecto** - Por defecto, deberá servir el archivo index.html, si la URL no especifica el archivo. | `req-02-05-06-ManejadorSolicitud.cs` |
| **3** | **Carpeta raíz configurable** - La carpeta raíz desde donde se servirán los archivos debe ser configurable. | `req-03-04-Configuracion.cs` |
| **4** | **Puerto configurable** - El puerto de escucha debe ser configurable. | `req-03-04-Configuracion.cs` |
| **5** | **Error 404 personalizado** - En caso de que el usuario haya solicitado un archivo inexistente, deberá devolver un código de error 404 y un documento personalizado indicando el error. | `req-02-05-06-ManejadorSolicitud.cs`, `req-05-08-RespuestaHTTP.cs` |
| **6** | **GET y POST** - Debe aceptar solicitudes de tipo GET y POST. En el caso de solicitudes POST, sólo deben loguearse los datos recibidos. | `req-02-05-06-ManejadorSolicitud.cs`, `req-06-07-SolicitudHTTP.cs` |
| **7** | **Parámetros de consulta** - Los parámetros de consulta (query string) deben ser parseados y logueados. | `req-06-07-SolicitudHTTP.cs` |
| **8** | **Compresión GZip** - Se debe implementar compresión GZip para reducir el tamaño de las respuestas. | `req-05-08-RespuestaHTTP.cs`, `req-08-CompresorGZip.cs` |
| **9** | **Log por día** - Los datos de todas las solicitudes deben loguearse en un archivo por día, incluyendo la IP de origen. | `req-09-Logger.cs` |
| **10** | **Sockets TCP** - Sólo deben usar sockets (directamente en la capa de transporte) y se deben parsear las solicitudes HTTP. | `req-01-10-ServidorHTTP.cs`, `req-06-07-SolicitudHTTP.cs` |

---

## 🧠 Arquitectura General

El servidor combina **ThreadPool + SemaphoreSlim** para manejar un número indefinido de solicitudes concurrentes sin saturar los recursos del sistema. Cuando un cliente se conecta, se encola en el ThreadPool con `QueueUserWorkItem()`. Dentro del hilo del ThreadPool, se adquiere un **SemaphoreSlim(50)** que limita a 50 ejecuciones simultáneas. Si ya hay 50 hilos activos, el hilo espera hasta que uno termine y libere el semáforo. Esto permite aceptar todas las solicitudes que lleguen (el ThreadPool nunca se bloquea) pero controla el uso de CPU, memoria y archivos abiertos.

```
┌─────────────────────────────────────────────────────────────────┐
│                        SERVIDOR WEB                             │
│                                                                 │
│  ┌──────────────┐    ┌──────────────────────────────────────┐   │
│  │   Socket TCP  │───▶│        Bucle de Aceptación          │   │
│  │  (Escucha)    │    │  while(ejecutando) {                │   │
│  │  Puerto:8080  │    │    cliente = socket.Accept();       │   │
│  └──────────────┘    │    ThreadPool.QueueUserWorkItem(     │   │
│                      │      ProcesarCliente, cliente);      │   │
│                      │  }                                   │   │
│                      └────────────┬─────────────────────────┘   │
│                                   │                             │
│                                   ▼                             │
│            ┌──────────────────────────────────────────┐         │
│            │           ThreadPool de .NET              │         │
│            │  Encola todas las solicitudes sin límite  │         │
│            │  El runtime administra los hilos          │         │
│            └────────────────────┬─────────────────────┘         │
│                                 │                               │
│                           ┌─────┴─────┐                         │
│                           │ Semaphore  │                         │
│                           │ Slim(50)   │  ← bloquea si ya hay   │
│                           │ .Wait()    │    50 hilos activos    │
│                           └─────┬─────┘                         │
│                                 │                               │
│            ┌────────────────────┼──────────────────────┐        │
│            ▼                    ▼                      ▼        │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐ │
│  │  Hilo ThreadPool │  │  Hilo ThreadPool │  │  Hilo ThreadPool│ │
│  │  #1              │  │  #2              │  │  ... #50        │ │
│  │                  │  │                  │  │                 │ │
│  │  Procesar()      │  │  Procesar()      │  │  Procesar()     │ │
│  │  semaforo.Release│  │  semaforo.Release│  │  semaforo.Release│ │
│  └────────┬─────────┘  └────────┬─────────┘  └────────┬────────┘ │
│           │                     │                     │          │
│           ▼                     ▼                     ▼          │
│  ┌──────────────────────────────────────────────────────────┐    │
│  │              ManejadorSolicitud.Procesar()                │    │
│  │  1. Parsear HTTP (SolicitudHTTP.Parsear)                  │    │
│  │  2. Obtener ruta del archivo (index.html por defecto)    │    │
│  │  3. Verificar si existe (sino → 404)                     │    │
│  │  4. Leer archivo del disco                               │    │
│  │  5. Comprimir con GZip si es texto                       │    │
│  │  6. Construir respuesta HTTP                             │    │
│  │  7. Enviar respuesta al cliente                          │    │
│  │  8. Registrar en log                                     │    │
│  │  9. Cerrar conexión                                      │    │
│  │  10. ThreadPool libera el hilo automáticamente           │    │
│  └──────────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────────┘
```

---

## 🔄 Diagrama de Comunicación entre Archivos

```
                        ┌─────────────────────┐
                        │   appsettings.json   │
                        │  (Configuración)     │
                        └──────────┬──────────┘
                                   │ Lee
                                   ▼
┌──────────────────────┐    ┌─────────────────────┐
│   req-01-10-Program  │───▶│ req-03-04-Configurac│
│   (Punto de entrada) │    │ (Puerto, RutaRaiz)  │
└──────────────────────┘    └─────────────────────┘
           │
           │ Crea e inicia
           ▼
┌──────────────────────────────────────────────────────┐
│              req-01-10-ServidorHTTP                   │
│  ┌────────────────────────────────────────────────┐  │
│  │ Socket TCP (Bind + Listen + Accept)            │  │
│  │ ThreadPool.QueueUserWorkItem() para            │  │
│  │ concurrencia indefinida                       │  │
│  │ SemaphoreSlim(50) dentro del hilo limita       │  │
│  │ a 50 ejecuciones simultáneas                  │  │
│  └────────────────────┬───────────────────────────┘  │
└───────────────────────┼──────────────────────────────┘
                        │
                        │ Cada hilo llama a:
                        ▼
┌──────────────────────────────────────────────────────┐
│           req-02-05-06-ManejadorSolicitud             │
│  ┌────────────────────────────────────────────────┐  │
│  │ 1. Parsear solicitud ───────────────┐          │  │
│  │ 2. Obtener ruta (index.html)        │          │  │
│  │ 3. Verificar existencia (→ 404)     │          │  │
│  │ 4. Leer archivo                     │          │  │
│  │ 5. Construir respuesta              │          │  │
│  │ 6. Enviar respuesta                 │          │  │
│  │ 7. Registrar en log                 │          │  │
│  └─────────────────────────────────────┼──────────┘  │
└────────────────────────────────────────┼─────────────┘
                                         │
                    ┌────────────────────┼────────────────────┐
                    │                    │                    │
                    ▼                    ▼                    ▼
        ┌───────────────────┐ ┌────────────────┐ ┌──────────────────┐
        │ req-06-07-Solicitu│ │req-05-08-Respu │ │ req-09-Logger    │
        │ dHTTP             │ │estaHTTP        │ │                  │
        │                   │ │                │ │ Archivo:         │
        │ Parsea HTTP crudo │ │ Construye      │ │ logs/YYYY-MM-DD  │
        │ Extrae:           │ │ HTTP/1.1:      │ │ .log             │
        │ • Método (GET/POST│ │ • 200 OK       │ │                  │
        │ • Ruta            │ │ • 404 NotFound │ │ Formato:         │
        │ • Cabeceras       │ │ • 400 Bad Req  │ │ [timestamp] IP   │
        │ • Cuerpo (POST)   │ │ • 500 Error    │ │ - METODO ruta    │
        │ • Query string    │ │                │ │ - código         │
        └───────────────────┘ └───────┬────────┘ └──────────────────┘
                                      │
                                      ▼
                            ┌──────────────────┐
                            │ req-08-Compresor │
                            │ GZip             │
                            │                  │
                            │ Comprime con     │
                            │ GZipStream       │
                            └──────────────────┘
```

---

## 📂 Estructura del Proyecto

```
ServidorWeb/
│
├── 📄 req-01-10-Program.cs              # Punto de entrada
├── 📄 req-01-10-ServidorHTTP.cs         # Socket TCP + ThreadPool + SemaphoreSlim
├── 📄 req-02-05-06-ManejadorSolicitud.cs # Procesa cada solicitud
├── 📄 req-03-04-Configuracion.cs        # Configuración (puerto, ruta)
├── 📄 req-05-08-RespuestaHTTP.cs        # Construye respuestas HTTP
├── 📄 req-06-07-SolicitudHTTP.cs        # Parsea solicitudes HTTP
├── 📄 req-08-CompresorGZip.cs           # Compresión GZip
├── 📄 req-09-Logger.cs                  # Log por día
├── 📄 ServidorWeb.csproj                # Archivo de proyecto .NET
├── 📄 appsettings.json                  # Configuración externa
│
├── 📁 wwwroot/                          # Archivos estáticos
│   ├── 📄 index.html                    # Página principal
│   └── 📄 404.html                      # Página de error 404
│
├── 📁 logs/                             # Archivos de log (se crean al ejecutar)
│
└── 📁 docs/                             # Documentación
    └── 📄 README.md                     # Este archivo
```

---

## 🚀 Cómo ejecutar

```bash
cd ServidorWeb
dotnet run
```

Luego abrir en el navegador: [http://localhost:8080](http://localhost:8080)

### Probar los requisitos manualmente

| Prueba | Comando |
|--------|---------|
| **GET /** | `curl http://localhost:8080/` |
| **GET con query string** | `curl "http://localhost:8080/?nombre=Juan&edad=25"` |
| **POST** | `curl -X POST -d "usuario=test&mensaje=hola" http://localhost:8080/` |
| **Error 404** | `curl http://localhost:8080/no-existe.html` |
| **Comprobar compresión** | `curl -H "Accept-Encoding: gzip" -o /dev/null -w "%{size_download}" http://localhost:8080/` |


## ⚙️ Cómo manejamos la concurrencia

El servidor usa **3 niveles de control de concurrencia**, cada uno con un propósito específico:

### Nivel 1: ThreadPool (encolado de solicitudes)

**Archivo:** `req-01-10-ServidorHTTP.cs`

```
ThreadPool.QueueUserWorkItem(ProcesarCliente, cliente);
```

- Cada cliente que se conecta se **encola en el ThreadPool** de .NET
- El runtime administra los hilos automáticamente: crea nuevos si la carga aumenta, reutiliza los que terminan
- **No hay límite** en la cantidad de solicitudes que puede encolar
- El bucle de aceptación nunca se bloquea, puede seguir aceptando conexiones

### Nivel 2: SemaphoreSlim (límite de ejecución simultánea)

**Archivo:** `req-01-10-ServidorHTTP.cs`

```csharp
private readonly SemaphoreSlim _semaforo = new(50, 50);

// Dentro del hilo del ThreadPool:
_semaforo.Wait();     // Espera si ya hay 50 ejecutándose
try { Procesar(); }
finally { _semaforo.Release(); }  // Libera al terminar
```

- Se adquiere **dentro** del hilo del ThreadPool, así el ThreadPool nunca se bloquea
- Limita a **50 ejecuciones simultáneas** para no saturar CPU, memoria y archivos abiertos
- El hilo #51 queda en **standby** (esperando en `Wait()`) hasta que uno de los 50 activos termine
- El `finally` garantiza que siempre se libere, incluso si hay excepción

### Nivel 3: lock (protección del recurso compartido)

**Archivo:** `req-09-Logger.cs`

```csharp
private static readonly object _candado = new();

public static void Registrar(...)
{
    lock (_candado)
    {
        // Solo un hilo escribe al archivo de log a la vez
        File.AppendAllText(_rutaArchivoActual, sb.ToString());
    }
}
```

- El **único recurso compartido** entre hilos es el archivo de log
- `lock` garantiza exclusión mutua: dos hilos no pueden escribir al mismo `.log` simultáneamente
- No hay riesgo de deadlock porque solo hay un `lock` en todo el sistema y nunca se adquiere dentro de otro `lock`

### ¿Por qué es suficiente?

| Recurso | ¿Compartido? | Protección |
|---------|-------------|------------|
| Socket del cliente | ❌ Cada hilo tiene el suyo | No necesita protección |
| Archivo leído del disco | ❌ Cada hilo lee el suyo | No necesita protección |
| Archivo de log | ✅ Todos los hilos escriben aquí | `lock(_candado)` |
| Estado de RespuestaHTTP | ❌ Métodos estáticos sin estado | No necesita protección |
| Estado de CompresorGZip | ❌ Métodos estáticos sin estado | No necesita protección |
| Variable _ejecutando | ✅ Leída por todos los hilos | `volatile` |

A diferencia del problema de los **5 filósofos** (donde varios procesos compiten por recursos limitados y pueden llegar a un deadlock), aquí cada hilo tiene sus propios recursos exclusivos (socket, archivo leído) y solo comparten el archivo de log con un `lock` simple. No se necesita `Mutex`, `Monitor`, `ReaderWriterLock` ni `SpinLock`.

---

## 🔍 Detalle por Requisito

### Requisito 1 - Concurrencia
**Archivo:** `req-01-10-ServidorHTTP.cs`

Se combina **ThreadPool.QueueUserWorkItem()** + **SemaphoreSlim(50)**:
- ThreadPool encola cada cliente sin límite (número indefinido de solicitudes)
- SemaphoreSlim limita a 50 ejecuciones simultáneas para no saturar recursos
- El semáforo se adquiere dentro del hilo del ThreadPool, así el ThreadPool nunca se bloquea
- El `finally` garantiza `Release()` incluso si hay excepción

### Requisito 2 - Index.html por defecto
**Archivo:** `req-02-05-06-ManejadorSolicitud.cs`

En el método `ObtenerRutaArchivo()`, si la ruta es "/" o está vacía, se reemplaza por "/index.html".

### Requisito 3 y 4 - Configuración
**Archivo:** `req-03-04-Configuracion.cs`

Lee `appsettings.json` y expone `Puerto` (default: 8080) y `RutaRaiz` (default: "wwwroot").

### Requisito 5 - Error 404
**Archivos:** `req-02-05-06-ManejadorSolicitud.cs`, `req-05-08-RespuestaHTTP.cs`

Si el archivo no existe, se devuelve `HTTP/1.1 404 Not Found` con una página personalizada (carga `wwwroot/404.html` o usa HTML hardcodeado).

### Requisito 6 - GET y POST
**Archivos:** `req-02-05-06-ManejadorSolicitud.cs`, `req-06-07-SolicitudHTTP.cs`

Se parsea el método HTTP. Para GET se sirve el archivo solicitado. Para POST **solo se loguean los datos recibidos** (cuerpo y parámetros) en el archivo de log, y se responde con 200 OK sin contenido. No se sirve ningún archivo en POST.

### Requisito 7 - Parámetros de consulta
**Archivo:** `req-06-07-SolicitudHTTP.cs`

Se detecta "?" en la ruta y se parsea `clave=valor&clave2=valor2` en un diccionario.

### Requisito 8 - Compresión GZip
**Archivos:** `req-05-08-RespuestaHTTP.cs`, `req-08-CompresorGZip.cs`

Se comprime con `GZipStream` si el tipo MIME es texto y la compresión reduce el tamaño.

### Requisito 9 - Log por día
**Archivo:** `req-09-Logger.cs`

Crea archivos `logs/YYYY-MM-DD.log` con formato `[timestamp] IP - METODO ruta - código`. Usa `lock` para seguridad multi-hilo.

### Requisito 10 - Sockets TCP
**Archivos:** `req-01-10-ServidorHTTP.cs`, `req-06-07-SolicitudHTTP.cs`

Se usa `System.Net.Sockets.Socket` directamente (no `TcpListener`, no `HttpListener`). El parseo HTTP/1.1 se hace manualmente.
