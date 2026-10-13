# SnG Control Backend — Guía técnica

## Estado y stack

La solución `SnG.Control.sln` utiliza .NET 10 (`net10.0`), C#, ASP.NET Core Web API con Controllers tradicionales y Clean Architecture. El núcleo de negocio contiene `Client`, `Project`, `ProjectTask` y sus enums; sus reglas se describen en [README.md](README.md).

La persistencia utiliza Entity Framework Core 10 y el proveedor Npgsql para PostgreSQL. El modelo y las migraciones `InitialCreate` y `AddProjectTasks` están en Infrastructure. La base esperada es **`sng_control`**, creada previamente por el administrador. Su host, puerto y credenciales deben suministrarse explícitamente.

Application implementa nueve casos de uso: crear, obtener por Id y listar clientes y proyectos; enviar un proyecto a Presupuestos; listar tareas por área/estado y por proyecto. Los Controllers delegan en estos casos de uso, que acceden a PostgreSQL mediante interfaces específicas implementadas en Infrastructure. No hay repositorios genéricos, Unit of Work propio, autenticación, autorización ni Docker. La API exige configuración de conexión, pero no aplica migraciones ni abre una conexión a la base durante el arranque.

## Requisitos y paquetes

- SDK de .NET 10. No hay `global.json` que fije una versión concreta del SDK.
- Acceso a NuGet para restaurar paquetes y herramientas, o una caché local disponible.
- PostgreSQL accesible y la base existente `sng_control` para aplicar migraciones o persistir datos. Los tests y la generación de migraciones no requieren servidor ni contraseña.
- Certificado de desarrollo confiable para ejecutar el perfil HTTPS.

| Proyecto | PackageReference | Versión | Uso |
| --- | --- | --- | --- |
| Infrastructure | Microsoft.EntityFrameworkCore.Relational | 10.0.12 | Fluent API relacional y migraciones; fija también EF Core en 10.0.12 |
| Infrastructure | Npgsql.EntityFrameworkCore.PostgreSQL | 10.0.3 | Proveedor PostgreSQL |
| Api | Microsoft.EntityFrameworkCore.Design | 10.0.12 | Tooling del startup project; `PrivateAssets=all` |
| Api | Microsoft.AspNetCore.OpenApi | 10.0.12 | Documento OpenAPI en desarrollo |
| Tests | Microsoft.NET.Test.Sdk | 17.14.1 | Ejecución de tests |
| Tests | xunit | 2.9.3 | Tests unitarios |
| Tests | xunit.runner.visualstudio | 3.1.4 | Descubrimiento y ejecución de xUnit |
| Tests | coverlet.collector | 6.0.4 | Cobertura opcional |

`Microsoft.EntityFrameworkCore` 10.0.12 y `Npgsql` 10.0.3 se resuelven transitivamente. No se duplican sus referencias en Api ni Tests. Domain y Application no dependen de EF ni de paquetes externos. No se utilizan proveedores InMemory o SQLite.

`dotnet-tools.json` en la raíz registra **dotnet-ef 10.0.12** como herramienta local reproducible. No se necesita una instalación global. Las versiones son estables; la dependencia relacional explícita mantiene alineados los componentes de EF Core.

## Estructura y responsabilidades

```text
dotnet-tools.json
SnG.Control.sln
src/
  Api/
    Program.cs
    DependencyInjection.cs
    Contracts/CreateClientRequest.cs
    Contracts/CreateProjectRequest.cs
    Contracts/ProductLineJsonConverter.cs
    Controllers/ClientsController.cs
    Controllers/ProjectsController.cs
    Controllers/ProjectTasksController.cs
    Errors/ApiExceptionHandler.cs
    Persistence/SnGControlDbContextFactory.cs
    appsettings.json
    Properties/launchSettings.json
  Application/
    Features/
      Clients/
        IClientStore.cs
        ClientResponse.cs
        Create/CreateClientUseCase.cs
        GetById/GetClientByIdUseCase.cs
        List/ListClientsUseCase.cs
      Projects/
        IProjectStore.cs
        ProjectResponse.cs
        Create/CreateProjectUseCase.cs
        GetById/GetProjectByIdUseCase.cs
        List/ListProjectsUseCase.cs
      ProjectTasks/
        IProjectTaskStore.cs
        ProjectTaskResponse.cs
        SendToBudgets/SendProjectToBudgetsUseCase.cs
        SendToBudgets/SendProjectToBudgetsResult.cs
        ListByArea/ListProjectTasksByAreaUseCase.cs
        ListByProject/ListProjectTasksByProjectUseCase.cs
  Domain/
    Entities/Client.cs
    Entities/Project.cs
    Entities/ProjectTask.cs
    Enums/ProductLine.cs
    Enums/OperationalArea.cs
    Enums/ProjectTaskType.cs
    Enums/ProjectTaskStatus.cs
  Infrastructure/
    Persistence/
      SnGControlDbContext.cs
      Stores/EfClientStore.cs
      Stores/EfProjectStore.cs
      Stores/EfProjectTaskStore.cs
      Configurations/ClientConfiguration.cs
      Configurations/ProjectConfiguration.cs
      Configurations/ProjectTaskConfiguration.cs
      Migrations/
        20261013143318_InitialCreate.cs
        20261013143318_InitialCreate.Designer.cs
        20261013190849_AddProjectTasks.cs
        20261013190849_AddProjectTasks.Designer.cs
        SnGControlDbContextModelSnapshot.cs
tests/
  SnG.Control.Tests/
    Domain/
    Application/
    Api/
    TestDoubles/
    Persistence/SnGControlDbContextTests.cs
```

| Proyecto | Responsabilidad actual |
| --- | --- |
| Domain | Entidades e invariantes, sin dependencias de persistencia o HTTP. |
| Application | Nueve casos de uso organizados por feature, respuestas e interfaces de persistencia específicas. |
| Infrastructure | Contexto EF, stores PostgreSQL, configuraciones Fluent API y migraciones. |
| Api | Contratos HTTP, Controllers, manejo de errores y composition root. Incluye la fábrica del tooling del startup project. |
| Tests | Tests de dominio, casos de uso, HTTP y metadata/SQL del proveedor real, sin conexión PostgreSQL. |

Referencias entre proyectos:

```text
Domain         → ninguna otra capa
Application    → Domain
Infrastructure → Application, Domain
Api            → Application, Infrastructure
Tests          → Domain, Application, Infrastructure, Api
```

## Preparación del entorno

Clonar usando la URL del repositorio facilitada por el equipo y entrar en su raíz:

```shell
git clone <URL_DEL_REPOSITORIO> SnG-Control-Backend
cd SnG-Control-Backend
dotnet tool restore
dotnet restore
```

Los comandos siguientes se ejecutan desde esa raíz.

### Conexión y secretos

La clave de configuración es `ConnectionStrings:SnGControl`. La API tiene `UserSecretsId` inicializado y carga User Secrets en `Development`. El identificador del archivo de proyecto no es una credencial.

Configurar localmente el secreto, reemplazando todos los placeholders por datos confirmados:

```shell
dotnet user-secrets set "ConnectionStrings:SnGControl" "Host=<HOST>;Port=<PUERTO>;Database=sng_control;Username=<USUARIO>;Password=<CONTRASEÑA>" --project src/Api
```

No guardar la contraseña ni una conexión real en código, migraciones, archivos versionados o documentación. User Secrets se almacena fuera del repositorio, en el perfil del usuario; no es un almacén cifrado ni una solución para producción. No hay archivos `.env` ni `appsettings.Local.json`.

Como alternativa, suministrar la variable estándar **`ConnectionStrings__SnGControl`** mediante el entorno de ejecución o su gestor de secretos. Las variables de entorno tienen prioridad sobre User Secrets. Ejemplo PowerShell con placeholders:

```powershell
$env:ConnectionStrings__SnGControl = 'Host=<HOST>;Port=<PUERTO>;Database=sng_control;Username=<USUARIO>;Password=<CONTRASEÑA>'
```

La API rechaza una conexión ausente, vacía o solo whitespace al arrancar, con un mensaje que identifica la clave requerida, sin imprimir su valor. No se presume host, puerto, usuario o contraseña. Tener configuración no demuestra que PostgreSQL esté accesible: la conexión efectiva ocurre al ejecutar una operación de base de datos.

### Compilación y tests

```shell
dotnet build
dotnet test
```

Los tests normales funcionan sin conexión configurada, sin contraseña y sin PostgreSQL levantado. Los tests del modelo instancian el contexto con `UseNpgsql()` sin cadena y consultan metadata o compilan consultas sin ejecutarlas. Los tests de Application usan dobles de `IClientStore`, `IProjectStore` e `IProjectTaskStore`, ubicados exclusivamente en Tests.

Los tests HTTP arrancan Kestrel en un puerto loopback dinámico, utilizan el registro real `AddApiServices`, los Controllers y el middleware de errores, y reemplazan los stores por dobles. Comprueban rutas, binding, validación, serialización, `Location`, códigos HTTP y ausencia de detalles internos. No necesitan paquetes de hosting de tests adicionales, Docker ni proveedores ficticios de EF. Requieren permiso del entorno para abrir un puerto local. No prueban PostgreSQL, el perfil HTTPS ni la carga de secretos de Program; esa parte se verifica por smoke test controlado con la base local.

La suite contiene 140 tests. Cubre invariantes, fechas UTC, nombres repetidos, propagación de CancellationToken, rechazo de proyectos con cliente inexistente antes de escribir, contratos numéricos de enums, PK/FK e índices, tipos PostgreSQL, nullability y consistencia de las migraciones. Para tareas prueba creación Pending, duplicados de cualquier estado y área sin nueva escritura, filtros, historial vacío/inexistente, 201/404/409, parámetros requeridos y rechazo de números o nombres desconocidos. También verifica que crear Project no crea tareas y que no existe POST genérico de tareas.

Cobertura opcional:

```shell
dotnet test --collect:"XPlat Code Coverage"
```

Los artefactos de compilación y `TestResults` están excluidos por `.gitignore`.

## Contexto y materialización

`SnGControlDbContext` está en `SnG.Control.Infrastructure.Persistence`. Recibe `DbContextOptions<SnGControlDbContext>`, expone `Clients`, `Projects` y `ProjectTasks` y aplica explícitamente `ClientConfiguration`, `ProjectConfiguration` y `ProjectTaskConfiguration`.

Las entidades mantienen sus constructores públicos y validaciones. Sus propiedades tienen setters privados, sin métodos de edición ni de asignación de SG. EF enlaza los parámetros de los constructores existentes con las propiedades mapeadas y restaura el resto de valores mediante esos setters; no se necesita un constructor vacío.

Los identificadores nacen de `Guid.NewGuid()` en el dominio y se mapean con `ValueGeneratedNever()`: no hay generación de UUID en PostgreSQL. Las fechas son `DateTime` UTC; `CreatedAt` y `UpdatedAt` coinciden al crear clientes o proyectos. Los timestamps e identificadores almacenados se conservan al materializar entidades; no hay mecanismo automático de actualización de fechas.

`ProjectTask(projectId, area, type)` exige un ProjectId no vacío y enums definidos. Genera Id y AssignedAt UTC; fija Status=Pending y StartedAt/CompletedAt=null. No acepta estado ni fechas como parámetros ni expone transiciones. Sus ocho propiedades tienen setters privados. `Project` conserva su estructura, sin estado global ni colección Tasks.

Los proyectos y carpetas bajo `src` se llaman `Api`, `Application`, `Domain` e `Infrastructure`; cada archivo de proyecto tiene el mismo nombre que su carpeta. `RootNamespace` mantiene el prefijo `SnG.Control` en los namespaces del código y de las migraciones. Se utilizan nullable reference types e implicit usings. Las validaciones siguen utilizando `ArgumentNullException`, `ArgumentException` y `ArgumentOutOfRangeException`. Domain no contiene DataAnnotations de persistencia, referencias EF, colecciones de navegación ni dependencias hacia Infrastructure.

## Tablas y restricciones

Se usan nombres explícitos `Clients`, `Projects` y `ProjectTasks`, con columnas PascalCase; el SQL PostgreSQL generado utiliza identificadores entre comillas. No se introduce un esquema personalizado.

| Tabla | Columnas y tipos | Nullability |
| --- | --- | --- |
| Clients | `Id`: uuid; `Name`: text; `CreatedAt`, `UpdatedAt`: timestamp with time zone | Todas NOT NULL |
| Projects | `Id`, `ClientId`: uuid; `Name`, `City`, `SgCode`: text; `ProductLine`: integer; `CreatedAt`, `UpdatedAt`: timestamp with time zone | Solo `SgCode` admite NULL |
| ProjectTasks | `Id`, `ProjectId`: uuid; `Area`, `Type`, `Status`: integer; `AssignedAt`, `StartedAt`, `CompletedAt`: timestamp with time zone | Solo `StartedAt` y `CompletedAt` admiten NULL |

- `PK_Clients` sobre `Clients.Id` y `PK_Projects` sobre `Projects.Id`.
- `FK_Projects_Clients_ClientId`: `Projects.ClientId` referencia `Clients.Id`, relación 1:N sin navegaciones, con `ON DELETE RESTRICT`. No se eliminan proyectos en cascada.
- `IX_Projects_ClientId`: índice no único de la FK.
- `IX_Projects_SgCode`: índice UNIQUE estándar, sin filtro. PostgreSQL permite múltiples NULL y rechaza valores no nulos repetidos.
- No hay índices únicos sobre nombres, longitudes máximas de texto, defaults de identidad/fechas ni datos seed.
- `ProductLine` se convierte a integer: **Classic=1, Essential=2, Bioconfort=3, Signature=4**. Estos valores forman parte del contrato de datos y no deben renumerarse. El constructor rechaza valores de enum no definidos; no se añade una restricción CHECK en esta fase.

`ProjectTasks` tiene `PK_ProjectTasks` sobre Id y `FK_ProjectTasks_Projects_ProjectId` hacia Projects.Id, relación 1:N requerida sin navegaciones y con `ON DELETE RESTRICT`. No hay borrado en cascada. Sus únicos índices secundarios son `IX_ProjectTasks_ProjectId` y `IX_ProjectTasks_Area_Status`, ambos no únicos. No existe un índice único ProjectId+Type.

Los enums de tarea se convierten explícitamente a integer y conservan estos contratos estables:

| Enum | Valores |
| --- | --- |
| OperationalArea | Commercial=1, Budgets=2 |
| ProjectTaskType | PrepareQuotation=1 |
| ProjectTaskStatus | Pending=1, InProgress=2, Completed=3 |

Domain valida los valores; no hay restricciones CHECK ni valores predeterminados de base para estos campos.

## Migraciones

`20261013143318_InitialCreate` crea Clients y Projects, sus PK, FK e índices; permanece intacta junto con su Designer y modelo histórico. `20261013190849_AddProjectTasks`, generada con EF tooling, agrega exclusivamente ProjectTasks, su PK, FK e índices. El snapshot actual incluye las tres entidades. Los métodos `Down` contienen la reversión generada por EF; no forman parte del procedimiento normal de instalación. EF administra además su tabla técnica `__EFMigrationsHistory` al aplicar migraciones.

No se utilizan `EnsureCreated()` ni migraciones automáticas en startup. No crear tablas manualmente, borrar o recrear la base. Antes de aplicar, confirmar que la conexión apunta a la base **existente** `sng_control` y que el usuario tiene permisos sobre ella. El tooling de EF puede intentar crear una base inexistente; no ejecutarlo contra un destino sin verificar.

La fábrica `SnG.Control.Api.Persistence.SnGControlDbContextFactory` implementa `IDesignTimeDbContextFactory<SnGControlDbContext>` y utiliza la configuración estándar de ASP.NET Core. Permite configurar únicamente el proveedor cuando no existe conexión, para generar/inspeccionar migraciones offline sin credenciales ficticias. Esa excepción se limita al tooling; el arranque normal sigue exigiendo la cadena. Los comandos incluyen `-- --environment Development` para cargar los User Secrets de la API de forma explícita.

### Inspeccionar sin conectarse

```shell
dotnet ef migrations list --no-connect --project src/Infrastructure --startup-project src/Api -- --environment Development
dotnet ef migrations has-pending-model-changes --project src/Infrastructure --startup-project src/Api -- --environment Development
dotnet ef migrations script 0 AddProjectTasks --project src/Infrastructure --startup-project src/Api -- --environment Development
dotnet ef migrations script InitialCreate AddProjectTasks --project src/Infrastructure --startup-project src/Api -- --environment Development
```

### Aplicar a la base existente

Después de configurar y verificar la conexión:

```shell
dotnet ef database update --project src/Infrastructure --startup-project src/Api -- --environment Development
```

Comprobar que el comando termina correctamente y verificar el estado aplicado:

```shell
dotnet ef migrations list --project src/Infrastructure --startup-project src/Api -- --environment Development
```

El modelo tiene exactamente dos migraciones: `InitialCreate` y `AddProjectTasks`. Ambas están aplicadas y verificadas en `__EFMigrationsHistory` de la instalación local `sng_control`; otros entornos deben comprobar su propio historial antes de operar. Los comandos de inspección offline no verifican si una base concreta tiene migraciones aplicadas.

### Crear migraciones futuras

Solo después de aprobar el nuevo alcance y ajustar el modelo:

```shell
dotnet ef migrations add <NombreDescriptivo> --project src/Infrastructure --startup-project src/Api --output-dir Persistence/Migrations -- --environment Development
```

Revisar la migración y el snapshot: columnas, tipos, nullability, claves, índices y comportamiento de borrado. Si una migración todavía no aplicada es incorrecta, corregir la configuración y regenerarla mediante tooling. No editar como primera opción el SQL o el snapshot a mano ni alterar migraciones aplicadas. Compilar y ejecutar tests antes de aplicar.

## Ejecutar la API

Con la conexión configurada y, para persistir datos, las migraciones aplicadas:

```shell
dotnet dev-certs https --trust
dotnet run --project src/Api --launch-profile https
```

El perfil `https` usa `https://localhost:7265` y `http://localhost:5051`, en `Development`, con redirección HTTP a HTTPS. Detener con `Ctrl+C`. También existe el perfil `http` en 5051, sin puerto HTTPS propio; la redirección puede advertir que no encuentra ese puerto. Se recomienda el perfil `https`.

En desarrollo, `GET /openapi/v1.json` publica OpenAPI con las seis operaciones de negocio. No hay Swagger UI ni Minimal APIs de negocio; `/` devuelve 404. Fuera de desarrollo no se publica OpenAPI.

- `Program.cs`: valida la presencia de conexión, registra `SnGControlDbContext` scoped con Npgsql, llama `AddApiServices` y registra OpenAPI; configura manejo de excepciones, HTTPS y Controllers.
- `DependencyInjection.cs`: `AddApiServices` registra los nueve casos de uso y los tres stores con lifetime scoped, Controllers, opciones JSON y manejo de errores. Application no necesita depender de una librería de DI.
- `appsettings.json`: logging (`Information` general y `Warning` para ASP.NET Core) y `AllowedHosts=*`; no contiene conexiones.
- `Properties/launchSettings.json`: perfiles/puertos locales y `ASPNETCORE_ENVIRONMENT=Development`.
- El registro del contexto no ejecuta operaciones de base de datos ni aplica migraciones.

## Casos de uso y persistencia

El flujo es `HTTP → Controller → UseCase → Store EF → PostgreSQL`. Application está organizada verticalmente en `Features/Clients` y `Features/Projects`, cada una con `Create`, `GetById` y `List`. Los casos de uso son clases explícitas con `ExecuteAsync`, sin MediatR ni interfaces por caso de uso.

`IClientStore` declara `AddAsync`, `GetByIdAsync`, `ListAsync` y `ExistsAsync`; `IProjectStore` declara los tres primeros. Las interfaces viven junto a su feature en Application y trabajan con entidades del dominio, sin exponer EF, `IQueryable` ni detalles de PostgreSQL.

`EfClientStore` y `EfProjectStore` implementan esas operaciones con el contexto existente. `AddAsync` agrega la entidad y ejecuta `SaveChangesAsync` antes de completar; no se requiere un método separado de commit. Todas las lecturas usan `AsNoTracking` y operaciones asíncronas. No se define orden de listado. Controllers, casos de uso y stores propagan `CancellationToken`.

Crear cliente construye la entidad validada, la guarda y mapea `ClientResponse`. Crear proyecto construye la entidad validada, verifica explícitamente la existencia del cliente con `ExistsAsync`, guarda y mapea `ProjectResponse`. Devuelve `null` si el cliente no existe, sin intentar guardar ni esperar un error de FK. Obtener por Id devuelve `null` si la entidad no existe; listar devuelve una colección, vacía cuando no hay registros.

Las respuestas de Application se mapean explícitamente con `FromEntity`, compartido por los casos de uso de cada feature. Los Controllers reciben requests propios de Api y devuelven esos DTO de respuesta, nunca entidades del dominio. No hay AutoMapper, repositorios genéricos ni Unit of Work adicional.

### Envío y consultas de tareas

`Features/ProjectTasks` contiene `IProjectTaskStore`, `ProjectTaskResponse` y los casos de uso agrupados en `SendToBudgets`, `ListByArea` y `ListByProject`. La interfaz declara únicamente `AddAsync`, `HasPrepareQuotationAsync`, `ListByAreaAsync` y `ListByProjectAsync`. `EfProjectTaskStore` utiliza el mismo DbContext scoped; guarda con SaveChangesAsync, lee con AsNoTracking y propaga CancellationToken en todas las operaciones.

`SendProjectToBudgetsUseCase` primero consulta el Project, luego comprueba si existe una tarea con ese ProjectId y Type=PrepareQuotation, sin filtrar área ni estado. Si no existe, construye una tarea Budgets/PrepareQuotation/Pending, la guarda y devuelve su DTO. `SendProjectToBudgetsResult` expresa los tres resultados mediante records específicos: `Created`, `ProjectNotFound` y `AlreadySent`. Los dos últimos son resultados esperados, sin excepciones ni framework Result genérico. Crear Project no invoca este caso de uso.

`ListProjectTasksByAreaUseCase` valida los enums y consulta por igualdad de Area y Status. `ListProjectTasksByProjectUseCase` verifica primero el proyecto: devuelve null si no existe, colección vacía si no tiene tareas o todas sus tareas en otro caso. No hay paginación ni orden garantizado en ninguna consulta.

La prevención de duplicados reside en Application y protege reintentos cuando la tarea anterior ya está guardada, cualquiera que sea su estado. La comprobación y la inserción no son atómicas: dos solicitudes simultáneas podrían crear duplicados. No hay índice único ni bloqueo transaccional que lo impida. La política de concurrencia queda pendiente; no se introduce una regla de unicidad permanente que impida futuros retrabajos.

## Contrato HTTP actual

| Método y ruta | Request | Éxito | Recurso inexistente |
| --- | --- | --- | --- |
| POST `/api/clients` | `name` | 201, ClientResponse y Location | No aplica |
| GET `/api/clients` | Sin body | 200, colección de ClientResponse | Colección vacía |
| GET `/api/clients/{id}` | Guid en ruta | 200, ClientResponse | 404 |
| POST `/api/projects` | `clientId`, `name`, `city`, `productLine` | 201, ProjectResponse y Location | 404 si no existe Client |
| GET `/api/projects` | Sin body | 200, colección de ProjectResponse | Colección vacía |
| GET `/api/projects/{id}` | Guid en ruta | 200, ProjectResponse | 404 |
| POST `/api/projects/{id}/send-to-budgets` | Guid en ruta, sin body | 201, ProjectTaskResponse; 409 si ya enviado | 404 |
| GET `/api/project-tasks?area=Budgets&status=Pending` | area y status requeridos como nombres | 200, colección de ProjectTaskResponse | Colección vacía |
| GET `/api/projects/{id}/tasks` | Guid en ruta, sin filtros | 200, colección de ProjectTaskResponse | 404 |

`ClientResponse` contiene `id`, `name`, `createdAt` y `updatedAt`. `ProjectResponse` contiene `id`, `clientId`, `name`, `city`, `productLine`, `sgCode`, `createdAt` y `updatedAt`. Las fechas son UTC en formato ISO 8601. Las respuestas de proyecto no incluyen el objeto Client completo. `sgCode` se devuelve como JSON null.

Se usa camelCase. `ProductLineJsonConverter` delega la lectura y escritura a `JsonStringEnumConverter<ProductLine>` de System.Text.Json con `allowIntegerValues=false`, y exige un único nombre: rechaza combinaciones separadas por comas que el convertidor estándar puede interpretar como una combinación numérica. Los nombres canónicos devueltos son `Classic`, `Essential`, `Bioconfort` y `Signature`; números JSON, cadenas numéricas, combinaciones y nombres desconocidos producen 400. El almacenamiento sigue siendo integer 1–4. La comparación de nombres no distingue mayúsculas/minúsculas.

Los requests de creación no incluyen `id`, fechas ni `sgCode`. `JsonUnmappedMemberHandling.Disallow` rechaza campos JSON desconocidos, incluidos esos campos controlados por el dominio. Los nombres repetidos continúan permitidos.

### Contrato de tareas

El envío no recibe DTO ni body. Devuelve 201 con el DTO creado, sin cabecera Location: no existe GET por Id de tarea. Tampoco existe POST genérico `/api/project-tasks` ni endpoints para modificar estados.

Ejemplo de respuesta del envío:

```json
{
  "id": "90628337-3801-4276-996b-43d901623d96",
  "projectId": "b0825722-a7d2-4385-9d13-a1a5e6fd7e66",
  "area": "Budgets",
  "type": "PrepareQuotation",
  "status": "Pending",
  "assignedAt": "2026-10-13T19:00:00Z",
  "startedAt": null,
  "completedAt": null
}
```

`ProjectTaskResponse` expone exactamente estos ocho campos, sin objetos Client/Project incrustados, nombres duplicados ni SG. Los tres enums se serializan mediante JsonStringEnumConverter con valores numéricos deshabilitados.

El GET por área acepta los nombres definidos Commercial/Budgets y Pending/InProgress/Completed, sin distinguir mayúsculas. Ambos parámetros son obligatorios; valores ausentes, vacíos, numéricos, combinados con comas o desconocidos producen 400. La validación de nombres se realiza explícitamente en el Controller porque el binding ordinario de enums también admite números. El flujo solo crea Budgets/Pending; las otras consultas pueden devolver `[]`. El historial devuelve `[]` para un proyecto existente sin tareas.

### Errores

- `[ApiController]` responde 400 para errores de binding, JSON, Guid, enum o campos de texto requeridos.
- Las invariantes permanecen en Domain. `ApiExceptionHandler` convierte `ArgumentException` y sus derivadas de validación en 400 con un ProblemDetails genérico.
- Los Controllers convierten los resultados inexistentes en 404 y AlreadySent en 409 con ProblemDetails. Clientes y proyectos utilizan `CreatedAtAction` para los 201, cuya cabecera Location apunta al GET correspondiente; el envío responde 201 con la tarea y sin Location.
- El middleware de excepciones está activo también en Development. Los errores inesperados responden 500 con ProblemDetails genérico, sin mensajes de EF/Npgsql, SQL, stack traces ni detalles de configuración. Los diagnósticos quedan en los logs del servidor.
- No hay traducción especial de conflictos de escritura concurrentes. La FK sigue protegiendo la integridad si un cliente desaparece externamente entre la comprobación y el guardado; la API no ofrece eliminación.

### Prueba manual

Con la API iniciada por el perfil HTTPS, ejecutar desde PowerShell con datos ficticios:

```powershell
$baseUrl = 'https://localhost:7265'
$clientBody = @{ name = 'Cliente Demo' } | ConvertTo-Json
$client = Invoke-RestMethod -Method Post -Uri "$baseUrl/api/clients" -ContentType 'application/json' -Body $clientBody

$projectBody = @{
    clientId = $client.id
    name = 'Casa Campestre'
    city = 'Bucaramanga'
    productLine = 'Signature'
} | ConvertTo-Json
$project = Invoke-RestMethod -Method Post -Uri "$baseUrl/api/projects" -ContentType 'application/json' -Body $projectBody

Invoke-RestMethod "$baseUrl/api/clients/$($client.id)"
Invoke-RestMethod "$baseUrl/api/projects/$($project.id)"
Invoke-RestMethod "$baseUrl/api/clients"
Invoke-RestMethod "$baseUrl/api/projects"

# Debe estar vacío: crear Project no lo envía a Presupuestos.
Invoke-RestMethod "$baseUrl/api/projects/$($project.id)/tasks"
$task = Invoke-RestMethod -Method Post -Uri "$baseUrl/api/projects/$($project.id)/send-to-budgets"
$task # Budgets / PrepareQuotation / Pending; fechas de inicio y fin nulas.
Invoke-RestMethod "$baseUrl/api/project-tasks?area=Budgets&status=Pending"
Invoke-RestMethod "$baseUrl/api/projects/$($project.id)/tasks"
# Este segundo envío debe producir HTTP 409 (Invoke-RestMethod informa el error).
Invoke-RestMethod -Method Post -Uri "$baseUrl/api/projects/$($project.id)/send-to-budgets"
```

Los POST de creación y el primer envío generan registros reales y responden 201; los GET del ejemplo responden 200. El segundo envío responde 409 sin escribir otra tarea. Un Guid válido inexistente responde 404 al consultar entidades, enviar o consultar su historial. Cambiar `productLine`, `area` o `status` por un número o nombre desconocido, omitir área/estado, dejar `name`/`city` en blanco o enviar `Guid.Empty` como `clientId` produce 400. Un proyecto con cliente inexistente produce 404 sin insertar el proyecto.

No hay endpoint de borrado. Para validaciones controladas en una base compartida, identificar los registros de prueba con una marca única y retirar exclusivamente esos registros mediante una operación administrativa segura, primero ProjectTasks, después Projects y finalmente Clients. No modificar datos ajenos a la prueba.

El smoke local fue validado mediante la API real por HTTPS y PostgreSQL: creación de cliente/proyecto, historial inicial vacío, envío 201, tarea Pending persistida, consultas por área e historial, y segundo envío 409. Se retiraron exclusivamente una tarea, un proyecto y un cliente temporales y se verificó que no quedaran filas de prueba.

## Reglas permanentes para continuar

- Implementar solo funcionalidad solicitada y reglas confirmadas. Reportar decisiones pendientes sin codificarlas silenciosamente.
- Mantener invariantes en Domain, coordinación de casos de uso en Application cuando se definan, persistencia e integraciones en Infrastructure y Controllers delgados en Api.
- Respetar referencias y namespaces. No introducir abstracciones, servicios, patrones ni librerías sin necesidad actual.
- Eliminar código muerto, dependencias sin uso, duplicación y ejemplos de template. No conservar código comentado ni versiones históricas; Git conserva la historia.
- Revisar y mantener ambos README como documentación viva: el funcional explica negocio y alcance, el técnico explica implementación y operación. No son changelogs.
- Validar herramientas, restore, build, tests y migraciones; corregir warnings atribuibles al código sin ocultarlos mediante configuración. Reportar los que legítimamente permanezcan y cualquier supuesto o impedimento.
- No incluir secretos reales en archivos versionados. No aplicar migraciones automáticamente al arrancar.

Siguen pendientes de alcance posterior la edición, eliminación, asignación/formato de SG, Excel, cotización, transiciones de tarea y demás módulos funcionales. Los únicos filtros disponibles son Area+Status para tareas; no hay paginación, búsqueda ni orden configurable. La garantía frente a envíos simultáneos y las reglas de reenvío/retrabajo requieren definición posterior.
