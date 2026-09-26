# 🚲 Plataforma de Incidencias de Bicicletas Compartidas - Examen Parcial

Sistema empresarial desarrollado en **ASP.NET Core (.NET 10)** para la gestión y seguimiento operativo de incidencias y averías en estaciones de bicicletas compartidas, con búsqueda indexada, caché distribuido y sincronización en tiempo real.

---

## 📌 Enlaces de Entrega Oficial

| Recurso | Enlace |
| :--- | :--- |
| **Repositorio GitHub** | [https://github.com/aspm1901/PARCIAL](https://github.com/aspm1901/PARCIAL) |
| **URL de Render (Web Service)** | [https://parcial-incidencias.onrender.com](https://parcial-incidencias.onrender.com) |
| **Commit Final Desplegado** | `667b7d96af8fa249136b111cf63168fb27c9d97a` |
| **Credenciales de Supervisor** | **Usuario:** `supervisor@bicis.com` &nbsp;\|&nbsp; **Contraseña:** `Password123!` |

---

## 🔀 Pull Requests Independientes (GitHub)

Los tres desarrollos partieron del **mismo commit inicial de `main` (`fbf560e`)** sin trabajo directo sobre la rama principal:

1. **[PR A (#1) - Búsqueda con Algolia](https://github.com/aspm1901/PARCIAL/pull/1)**
   - **Rama:** `feature/busqueda-algolia`
   - **Descripción:** Implementación de búsqueda por estación o descripción consultando el índice de Algolia desde el servidor sin exponer claves de administración en el frontend. Muestra solo incidencias abiertas registradas en la base.
   - **Título asignado:** `<h1>Incidencias abiertas encontradas</h1>`

2. **[PR B (#2) - Caché con Redis](https://github.com/aspm1901/PARCIAL/pull/2)**
   - **Rama:** `feature/cache-redis`
   - **Descripción:** Implementación de caché distribuido en Redis con expiración de 60 segundos para el listado general de incidencias abiertas. La búsqueda por texto consulta directamente a Algolia sin usar esta caché. Al cerrar una incidencia, se invalida activamente la clave de Redis antes de volver a consultarla. Logs en consola detallan lectura desde Redis o desde SQLite.
   - **Título asignado:** `<h1>Incidencias abiertas con consulta rápida</h1>`

3. **[PR C (#3) - Actualización con PieHost](https://github.com/aspm1901/PARCIAL/pull/3)**
   - **Rama:** `feature/websocket-piehost`
   - **Descripción:** Sincronización instantánea mediante WebSockets con PieHost. Al cerrar una incidencia, se persiste primero el cambio en la base de datos y se publica el evento `IncidenciaActualizada` con `Id` y `Estado`. La vista actualiza la lista sin recargar la página y consulta el estado vigente al reconectar.
   - **Título asignado:** `<h1>Incidencias abiertas en tiempo real</h1>`

---

## 🌳 Historial del Grafo de Ramas (`git log --graph --oneline --all`)

```text
*   667b7d9 Merge pull request #3 from aspm1901/feature/websocket-piehost
|\  
| *   0945cc8 merge: resolver conflicto integrando busqueda algolia, cache redis y websocket piehost
| |\  
| |/  
|/|   
* |   74f1d75 Merge pull request #2 from aspm1901/feature/cache-redis
|\ \  
| * \   a5b2008 merge: resolver conflicto integrando busqueda algolia y cache redis
| |\ \  
| |/ /  
|/| |   
* | |   0b3f174 Merge pull request #1 from aspm1901/feature/busqueda-algolia
|\ \ \  
| * | | ef3131d feat(pregunta-1): implementar busqueda con algolia y filtrado de incidencias abiertas
|/ / /  
| * / 128e729 feat(pregunta-2): implementar cache distribuido con redis e invalidacion
|/ /  
| * 652be22 feat(pregunta-3): implementar sincronizacion en tiempo real con piehost websocket
|/  
* fbf560e feat: proyecto base mvc con incidencias, autenticacion y sqlite
* e5571da chore: initial repository setup
```

---

## 🛠️ Explicación de la Resolución de Conflictos

### Conflicto 1: Integración de `main` (PR A) en `feature/cache-redis` (Commit: `a5b2008`)
- **Archivos en conflicto:** `Controllers/OperacionesController.cs` y `Views/Operaciones/Incidencias.cshtml`.
- **Causa:** Ambas ramas modificaron la línea del título compartido (`<h1>...</h1>`) y la lógica de consulta del listado de incidencias en el controlador.
- **Resolución:**
  1. En `OperacionesController`: Se unificaron los servicios inyectados (`IAlgoliaSearchService` y `IRedisCacheService`). Se condicionó el flujo: si existe parámetro de búsqueda `q`, se consulta Algolia directamente sin caché; si la búsqueda es vacía, se consulta o almacena en la caché de Redis con TTL de 60 segundos. Al cerrar incidencia se invalida la clave de Redis.
  2. En `Incidencias.cshtml`: Se combinó el buscador de Algolia con el indicador de origen de datos de Redis (`Redis (Caché 60s)` o `Base de Datos`).

### Conflicto 2: Integración del nuevo `main` (PR B) en `feature/websocket-piehost` (Commit: `0945cc8`)
- **Archivos en conflicto:** `Controllers/OperacionesController.cs`, `Program.cs` y `Views/Operaciones/Incidencias.cshtml`.
- **Causa:** La rama C introdujo el servicio de WebSocket y JavaScript de tiempo real, mientras que `main` ya contenía Algolia y Redis.
- **Resolución:**
  1. En `Program.cs`: Se registraron en el contenedor de dependencias los tres servicios (`AlgoliaSearchService`, `RedisCacheService` con `IConnectionMultiplexer`, y `PieSocketService`).
  2. En `OperacionesController`: Se implementó estrictamente la **secuencia requerida**:
     - **Paso 1:** Cierre del estado en la base de datos SQLite (`await _context.SaveChangesAsync()`).
     - **Paso 2:** Invalidación de la clave de Redis (`await _cacheService.InvalidateIncidenciasAbiertasAsync()`).
     - **Paso 3:** Publicación del evento `IncidenciaActualizada` a PieHost.
     - **Paso 4:** Filtrado estricto en BD (`Estado == "Abierta"`) para garantizar que la búsqueda de Algolia jamás vuelva a mostrar una incidencia cerrada.
  3. En `Incidencias.cshtml`: Se preservó el buscador, la tabla dinámica con IDs para manipulación DOM y el script WebSocket que elimina filas en vivo al recibir el evento y sincroniza el estado vigente al reconectar.

---

## ⚙️ Configuración de Variables de Entorno en Render

Para garantizar la seguridad y cumplir con la rúbrica, ninguna credencial sensible fue incluida en el repositorio Git. En el panel de **Render (Environment Variables)** se configuraron:

- `ASPNETCORE_ENVIRONMENT`: `Production`
- `PORT`: `8080`
- `Redis__ConnectionString`: Conexión segura a la instancia de Redis Cloud.
- `Algolia__ApplicationId`: Identificador de la aplicación en Algolia.
- `Algolia__SearchApiKey`: Clave de búsqueda de Algolia para el backend.
- `Algolia__WriteApiKey`: Clave de escritura para indexación de registros.
- `Algolia__IndexName`: `incidencias`
- `PieSocket__ClusterId`: Identificador del cluster de PieSocket (`free.blr2`).
- `PieSocket__ApiKey`: Clave API para clientes WebSocket y servidor.
- `PieSocket__ApiSecret`: Secreto para autenticación de publicaciones REST.
- `PieSocket__ChannelId`: `incidencias-channel`
