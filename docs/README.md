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

El servidor usa un **SemaphoreSlim** para limitar la concurrencia a 10 hilos como máximo. Cuando un cliente se conecta, el hilo aceptador espera en el semáforo, crea un hilo para procesar al cliente, y ese hilo libera el semáforo al terminar. No hay cola FIFO ni pool fijo de hilos.

```
┌─────────────────────────────────────────────────────────────────┐
│                        SERVIDOR WEB                             │
│                                                                 │
│  ┌──────────────┐    ┌──────────────────────────────────────┐   │
│  │   Socket TCP  │───▶│        Bucle de Aceptación          │   │
│  │  (Escucha)    │    │  while(ejecutando) {                │   │
│  │  Puerto:8080  │    │    cliente = socket.Accept();       │   │
│  └──────────────┘    │    semaforo.Wait();  ← bloquea si    │   │
│                      │    Thread(cliente).Start();          │   │
│                      │  }                                   │   │
│                      └────────────┬─────────────────────────┘   │
│                                   │                             │
│                                   ▼                             │
│                      ┌──────────────────────┐                   │
│                      │   SemaphoreSlim(10)   │                   │
│                      │  Permite hasta 10     │                   │
│                      │  hilos concurrentes   │                   │
│                      │  El #11 espera hasta  │                   │
│                      │  que uno termine      │                   │
│                      └────────────┬──────────┘                   │
│                                   │                             │
│            ┌──────────────────────┼──────────────────────┐      │
│            ▼                      ▼                      ▼      │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐ │
│  │   Hilo Cliente   │  │   Hilo Cliente   │  │   Hilo Cliente  │ │
│  │   #1             │  │   #2             │  │   ... #10       │ │
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
│  │  10. semaforo.Release() ← libera lugar para otro         │    │
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
│  │ SemaphoreSlim(10) para limitar concurrencia    │  │
│  │ Cada cliente crea un hilo, el semáforo bloquea │  │
│  │ si ya hay 10 ejecutándose                     │  │
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
├── 📄 req-01-10-ServidorHTTP.cs         # Socket TCP + Pool de hilos
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

---

## 🔍 Detalle por Requisito

### Requisito 1 - Concurrencia
**Archivo:** `req-01-10-ServidorHTTP.cs`

Se usa un **SemaphoreSlim(10, 10)** para limitar la concurrencia a 10 hilos como máximo. Cuando llega un cliente, el hilo aceptador llama a `_semaforo.Wait()`: si ya hay 10 hilos ejecutándose, se bloquea hasta que uno termine. Cada cliente se procesa en un hilo separado que libera el semáforo al finalizar (`_semaforo.Release()` en el `finally`). Esto evita crear hilos ilimitados y no necesita cola FIFO ni `Monitor.Wait/Pulse`.

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

Se parsea el método HTTP. Para POST, además de servir el archivo, se loguea el cuerpo recibido.

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
