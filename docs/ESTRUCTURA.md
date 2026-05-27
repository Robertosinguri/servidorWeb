# Estructura de Clases del Servidor Web

---

## 📦 CLASE: Program (Punto de entrada)
**Archivo:** `req-01-10-Program.cs`

```
📦 CLASE: Program (El lanzador del servidor)
│
├── 🚀 MÉTODOS PÚBLICOS (Acciones que se activan desde afuera)
│   │
│   └── 🔹 Main()
│        └── [Código que inicia la aplicación]
│            - Muestra banner del servidor
│            - Carga configuración desde appsettings.json
│            - Crea instancia de ServidorHTTP
│            - Maneja Ctrl+C para detener ordenadamente
│            - Llama a servidor.Iniciar() (bloqueante)
```

---

## 📦 CLASE: ServidorHTTP (El contenedor global)
**Archivo:** `req-01-10-ServidorHTTP.cs`

```
📦 CLASE: ServidorHTTP (El núcleo del servidor)
│
├── 🛠️ HERRAMIENTAS INTERNAS (Variables Privadas)
│   ├── _config           → Configuración (puerto, ruta raíz)
│   ├── _socketEscucha    → Socket TCP de escucha
│   ├── _ejecutando       → Flag volatile para control del bucle
│   └── _semaforo         → SemaphoreSlim(50, 50) para límite de concurrencia
│
├── 🚀 MÉTODOS PÚBLICOS (Acciones que se activan desde afuera)
│   │
│   ├── 🔹 Iniciar()
│   │    └── [Código que arranca el servidor y acepta clientes]
│   │        - Crea Socket TCP (IPv4, Stream)
│   │        - Bind() al puerto configurado
│   │        - Listen(100)
│   │        - Bucle while(_ejecutando):
│   │            - socket.Accept() → obtiene cliente
│   │            - ThreadPool.QueueUserWorkItem(ProcesarCliente, cliente)
│   │
│   └── 🔹 Detener()
│        └── [Código que apaga el socket y limpia todo]
│            - _ejecutando = false
│            - socket.Close()
│            - Mensaje de servidor detenido
│
└── 🔒 MÉTODOS PRIVADOS (Acciones que solo se activan desde adentro)
    │
    └── 🔸 ProcesarCliente()
         └── [Código que controla el semáforo y procesa la petición]
             - _semaforo.Wait() → espera si hay 50 hilos activos
             - Crea ManejadorSolicitud(cliente, _config)
             - manejador.Procesar()
             - finally → _semaforo.Release()
```

---

## 📦 CLASE: ManejadorSolicitud (El procesador de cada petición)
**Archivo:** `req-02-05-06-ManejadorSolicitud.cs`

```
📦 CLASE: ManejadorSolicitud (El gestor de cada solicitud HTTP)
│
├── 🛠️ HERRAMIENTAS INTERNAS (Variables Privadas)
│   ├── _cliente           → Socket del cliente conectado
│   ├── _config            → Configuración del servidor
│   └── _rutaRaizAbsoluta  → Ruta absoluta de wwwroot/
│
├── 🚀 MÉTODOS PÚBLICOS (Acciones que se activan desde afuera)
│   │
│   └── 🔹 Procesar()
│        └── [Código que procesa la solicitud HTTP completa]
│            - SolicitudHTTP.Parsear(_cliente) → obtiene solicitud
│            - Si es POST:
│                - Loguea datos recibidos (parámetros + cuerpo)
│                - Responde 200 OK sin contenido
│                - Retorna
│            - ObtenerRutaArchivo() → convierte "/" en "/index.html"
│            - Si archivo no existe:
│                - Responde 404 con página personalizada
│                - Loguea el 404
│                - Retorna
│            - Lee archivo del disco (File.ReadAllBytes)
│            - ObtenerTipoMIME() → determina Content-Type
│            - EsTipoCompresible() → decide si comprimir con GZip
│            - RespuestaHTTP.OK() → construye respuesta
│            - EnviarRespuesta() → envía al cliente
│            - Loguea la solicitud
│            - finally → cierra el socket del cliente
│
├── 🔒 MÉTODOS PRIVADOS (Acciones que solo se activan desde adentro)
│   │
│   ├── 🔸 EnviarRespuesta()
│   │    └── [Envía bytes al cliente si está conectado]
│   │
│   ├── 🔸 ObtenerRutaArchivo()
│   │    └── [Convierte URL en ruta de archivo del sistema]
│   │        - Si ruta es "/" o vacía → "/index.html"
│   │        - Normaliza separadores de ruta
│   │        - Previene Path Traversal (../../)
│   │
│   ├── 🔸 ObtenerTipoMIME()
│   │    └── [Devuelve Content-Type según extensión del archivo]
│   │        - .html → text/html
│   │        - .css → text/css
│   │        - .js → application/javascript
│   │        - .png → image/png
│   │        - etc.
│   │
│   ├── 🔸 EsTipoCompresible()
│   │    └── [Determina si el tipo MIME se puede comprimir]
│   │        - text/*, javascript, json, xml, svg → true
│   │
│   └── 🔸 ObtenerParametrosComoString()
│        └── [Convierte diccionario de parámetros a string]
│            - "nombre=Juan, edad=25"
```

---

## 📦 CLASE: Configuracion (El lector de configuración)
**Archivo:** `req-03-04-Configuracion.cs`

```
📦 CLASE: Configuracion (El lector de appsettings.json)
│
├── 🛠️ PROPIEDADES INTERNAS
│   ├── Puerto   → int (default: 8080)
│   └── RutaRaiz → string (default: "wwwroot")
│
├── 🚀 MÉTODOS PÚBLICOS (Acciones que se activan desde afuera)
│   │
│   └── 🔹 Cargar()
│        └── [Código que lee la configuración desde JSON]
│            - Busca appsettings.json
│            - Si no existe → valores por defecto
│            - Parsea JSON con JsonDocument
│            - Lee sección "Servidor" → Puerto y RutaRaiz
│            - Si hay error → valores por defecto
```

---

## 📦 CLASE: RespuestaHTTP (El constructor de respuestas)
**Archivo:** `req-05-08-RespuestaHTTP.cs`

```
📦 CLASE: RespuestaHTTP (El armador de respuestas HTTP)
│
├── 🚀 MÉTODOS PÚBLICOS (Acciones que se activan desde afuera)
│   │
│   ├── 🔹 OK()
│   │    └── [Construye respuesta HTTP 200 OK]
│   │        - Arma cabeceras (Content-Type, Server, Connection)
│   │        - Si usarCompresion=true:
│   │            - Comprime con CompresorGZip.Comprimir()
│   │            - Si comprimido es más pequeño → usa ese
│   │            - Agrega cabecera Content-Encoding: gzip
│   │        - Agrega Content-Length
│   │        - ConstruirRespuesta() → arma el mensaje HTTP completo
│   │
│   ├── 🔹 NotFound()
│   │    └── [Construye respuesta HTTP 404 Not Found]
│   │        - ObtenerPagina404() → carga wwwroot/404.html o HTML hardcodeado
│   │        - Arma cabeceras
│   │        - ConstruirRespuesta()
│   │
│   ├── 🔹 ErrorInterno()
│   │    └── [Construye respuesta HTTP 500 Internal Server Error]
│   │
│   └── 🔹 SolicitudInvalida()
│        └── [Construye respuesta HTTP 400 Bad Request]
│
└── 🔒 MÉTODOS PRIVADOS (Acciones que solo se activan desde adentro)
    │
    ├── 🔸 ConstruirRespuesta()
    │    └── [Arma el mensaje HTTP completo en bytes]
    │        - "HTTP/1.1 {codigo} {mensaje}\r\n"
    │        - Cabeceras en formato "Clave: Valor\r\n"
    │        - \r\n separador
    │        - Cuerpo en bytes
    │
    └── 🔸 ObtenerPagina404()
         └── [Obtiene el HTML para la página 404]
             - Busca wwwroot/404.html
             - Si existe → lo lee y devuelve
             - Si no existe → HTML hardcodeado
```

---

## 📦 CLASE: SolicitudHTTP (El parseador de solicitudes)
**Archivo:** `req-06-07-SolicitudHTTP.cs`

```
📦 CLASE: SolicitudHTTP (El intérprete de solicitudes HTTP)
│
├── 🛠️ PROPIEDADES PÚBLICAS
│   ├── Metodo            → string (GET, POST, etc.)
│   ├── Ruta              → string (/index.html)
│   ├── Version           → string (HTTP/1.1)
│   ├── Cabeceras         → Dictionary<string, string>
│   ├── Cuerpo            → string (para POST)
│   ├── ParametrosConsulta → Dictionary<string, string> (query string)
│   └── IPOrigen          → string (IP del cliente)
│
├── 🚀 MÉTODOS PÚBLICOS (Acciones que se activan desde afuera)
│   │
│   └── 🔹 Parsear()
│        └── [Código que parsea una solicitud HTTP desde un socket]
│            - Obtiene IP de origen del RemoteEndPoint
│            - Lee datos del socket en buffer de 8KB
│            - Busca "\r\n\r\n" para separar cabeceras de cuerpo
│            - Parsea línea de solicitud: "GET /ruta HTTP/1.1"
│            - Extrae método, ruta, versión HTTP
│            - Si hay "?" en ruta:
│                - Separa ruta de query string
│                - ParsearQueryString() → diccionario clave=valor
│            - Parsea cabeceras (Clave: Valor)
│            - Devuelve objeto SolicitudHTTP
│
└── 🔒 MÉTODOS PRIVADOS (Acciones que solo se activan desde adentro)
    │
    └── 🔸 ParsearQueryString()
         └── [Parsea query string en diccionario]
             - "nombre=Juan&edad=25" → {"nombre":"Juan", "edad":"25"}
             - Decodifica URL encoding (Uri.UnescapeDataString)
```

---

## 📦 CLASE: CompresorGZip (El compresor de datos)
**Archivo:** `req-08-CompresorGZip.cs`

```
📦 CLASE: CompresorGZip (El compresor/descompresor GZip)
│
├── 🚀 MÉTODOS PÚBLICOS (Acciones que se activan desde afuera)
│   │
│   ├── 🔹 Comprimir()
│   │    └── [Comprime bytes usando GZipStream]
│   │        - Usa CompressionLevel.Fastest
│   │        - MemoryStream + GZipStream
│   │        - Devuelve bytes comprimidos
│   │
│   └── 🔹 Descomprimir()
│        └── [Descomprime bytes GZip (útil para pruebas)]
│            - GZipStream en modo Decompress
│            - CopyTo() a MemoryStream de salida
```

---

## 📦 CLASE: Logger (El registrador de actividad)
**Archivo:** `req-09-Logger.cs`

```
📦 CLASE: Logger (El escritor de archivos de log)
│
├── 🛠️ HERRAMIENTAS INTERNAS (Variables Privadas)
│   ├── _candado          → object para lock (exclusión mutua)
│   ├── _carpetaLogs      → Ruta a la carpeta logs/
│   ├── _fechaActual      → Fecha del día actual (YYYY-MM-DD)
│   └── _rutaArchivoActual → Ruta al archivo de log del día
│
├── 🚀 MÉTODOS PÚBLICOS (Acciones que se activan desde afuera)
│   │
│   └── 🔹 Registrar()
│        └── [Código que escribe una entrada en el log del día]
│            - lock(_candado) → solo un hilo escribe a la vez
│            - ActualizarRutaArchivo() → verifica si cambió el día
│            - Construye string: "[timestamp] IP - METODO ruta - código"
│            - Si hay parámetros → los agrega
│            - Si hay cuerpo (POST) → lo agrega
│            - File.AppendAllText() → escribe en el archivo
│
└── 🔒 MÉTODOS PRIVADOS (Acciones que solo se activan desde adentro)
    │
    └── 🔸 ActualizarRutaArchivo()
         └── [Actualiza la ruta del archivo de log si cambió el día]
             - Si fecha actual ≠ _fechaActual:
                 - Crea carpeta logs/ si no existe
                 - _rutaArchivoActual = logs/YYYY-MM-DD.log
```

---

## 📁 Archivos estáticos (wwwroot/)

```
📁 wwwroot/ (Archivos servidos al cliente)
│
├── 📄 index.html
│   └── Página principal del servidor
│       - Muestra información del proyecto
│       - Lista los 10 requisitos
│       - Links de prueba (GET con parámetros, 404)
│
└── 📄 404.html
    └── Página de error personalizada
        - Muestra "404 - Archivo no encontrado"
        - Estilo minimalista (blanco, negro)
```

---

## 🔗 Flujo completo de una solicitud

```
[Cliente] ──HTTP──> [Socket TCP] ──Accept()──> [ThreadPool.QueueUserWorkItem()]
                                                       │
                                                       ▼
                                              [SemaphoreSlim.Wait()]
                                                       │
                                                  ┌────┴────┐
                                                  │  ¿Hay   │
                                                  │  lugar? │
                                                  └────┬────┘
                                                       │
                                              ┌────────┴────────┐
                                              │                 │
                                              ▼                 ▼
                                       [Procesar()]      [Espera en
                                              │           standby]
                                              ▼
                                    ┌─────────────────┐
                                    │ ¿GET o POST?    │
                                    └────────┬────────┘
                                             │
                               ┌─────────────┴─────────────┐
                               │                           │
                               ▼                           ▼
                          [GET]                        [POST]
                               │                           │
                               ▼                           ▼
                    ┌────────────────────┐       ┌──────────────────┐
                    │ Buscar archivo     │       │ Loguear datos    │
                    │ en wwwroot/        │       │ + responder 200  │
                    └────────┬───────────┘       └──────────────────┘
                             │
                    ┌────────┴────────┐
                    │                 │
                    ▼                 ▼
              [Existe]          [No existe]
                    │                 │
                    ▼                 ▼
            ┌──────────────┐  ┌──────────────┐
            │ Leer archivo │  │ 404 Not Found│
            │ + GZip (si)  │  │ + página 404 │
            │ + 200 OK     │  └──────────────┘
            └──────────────┘
                    │
                    ▼
            [Logger.Registrar()]
                    │
                    ▼
            [Cerrar socket]
                    │
                    ▼
            [SemaphoreSlim.Release()]
```
