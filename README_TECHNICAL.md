# SnG Control Backend — Guía técnica

## Estado y stack

La solución `SnG.Control.sln` utiliza .NET 10 (`net10.0`), C#, ASP.NET Core Web API con Controllers tradicionales y Clean Architecture. El núcleo de negocio es `Client`, `Project` y `ProductLine`; sus reglas se describen en [README.md](README.md).

La persistencia utiliza Entity Framework Core 10 y el proveedor Npgsql para PostgreSQL. El modelo y la migración `InitialCreate` están en Infrastructure. La base esperada es **`sng_control`**, creada previamente por el administrador. Su host, puerto y credenciales deben suministrarse explícitamente.

Application no tiene casos de uso. No hay Controllers ni endpoints de negocio, repositorios, Unit of Work, autenticación, autorización ni Docker. La API registra el contexto y exige configuración de conexión, pero no aplica migraciones ni abre una conexión a la base durante el arranque.

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
  SnG.Control.Api/
    Program.cs
    Persistence/SnGControlDbContextFactory.cs
    appsettings.json
    Properties/launchSettings.json
  SnG.Control.Application/
  SnG.Control.Domain/
    Entities/Client.cs
    Entities/Project.cs
    Enums/ProductLine.cs
  SnG.Control.Infrastructure/
    Persistence/
      SnGControlDbContext.cs
      Configurations/ClientConfiguration.cs
      Configurations/ProjectConfiguration.cs
      Migrations/
        20261013143318_InitialCreate.cs
        20261013143318_InitialCreate.Designer.cs
        SnGControlDbContextModelSnapshot.cs
tests/
  SnG.Control.Tests/
    Domain/
    Persistence/SnGControlDbContextTests.cs
```

| Proyecto | Responsabilidad actual |
| --- | --- |
| Domain | Entidades e invariantes, sin dependencias de persistencia o HTTP. |
| Application | Capa destinada a coordinar casos de uso; todavía sin implementaciones. |
| Infrastructure | Contexto EF, configuraciones Fluent API y migraciones PostgreSQL. |
| Api | Composition root: configuración, registro de DbContext, Controllers, OpenAPI y HTTPS. Incluye la fábrica del tooling para utilizar la configuración del startup project. |
| Tests | Tests de dominio y metadata/SQL del proveedor real, sin abrir conexiones. |

Referencias entre proyectos:

```text
Domain         → ninguna otra capa
Application    → Domain
Infrastructure → Application, Domain
Api            → Application, Infrastructure
Tests          → Domain, Application, Infrastructure
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
dotnet user-secrets set "ConnectionStrings:SnGControl" "Host=<HOST>;Port=<PUERTO>;Database=sng_control;Username=<USUARIO>;Password=<CONTRASEÑA>" --project src/SnG.Control.Api
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

Los tests normales funcionan sin conexión configurada, sin contraseña y sin PostgreSQL levantado. Instancian el contexto con `UseNpgsql()` sin cadena y consultan metadata o compilan consultas sin ejecutarlas.

Cubren invariantes y normalización del dominio, fechas UTC, identidad, nombres repetidos, contrato numérico del enum, tablas y PK, campos obligatorios, tipos PostgreSQL, FK restrictiva, unicidad nullable de SG, ausencia de límites de longitud, generación de SQL y correspondencia del snapshot con el modelo.

Cobertura opcional:

```shell
dotnet test --collect:"XPlat Code Coverage"
```

Los artefactos de compilación y `TestResults` están excluidos por `.gitignore`.

## Contexto y materialización

`SnGControlDbContext` está en `SnG.Control.Infrastructure.Persistence`. Recibe `DbContextOptions<SnGControlDbContext>`, expone `Clients` y `Projects` y aplica explícitamente `ClientConfiguration` y `ProjectConfiguration`.

Las entidades mantienen sus constructores públicos y validaciones. Sus propiedades tienen setters privados, sin métodos de edición ni de asignación de SG. EF enlaza los parámetros de los constructores existentes con las propiedades mapeadas y restaura el resto de valores mediante esos setters; no se necesita un constructor vacío.

Los identificadores nacen de `Guid.NewGuid()` en el dominio y se mapean con `ValueGeneratedNever()`: no hay generación de UUID en PostgreSQL. Las fechas son `DateTime` UTC y ambos valores coinciden al crear la entidad. Los timestamps e identificadores almacenados se conservan al materializar entidades; no hay mecanismo automático de actualización de fechas.

Se mantienen los namespaces por proyecto/carpeta, nullable reference types e implicit usings. Las validaciones siguen utilizando `ArgumentNullException`, `ArgumentException` y `ArgumentOutOfRangeException`. Domain no contiene DataAnnotations de persistencia, referencias EF, colecciones de navegación ni dependencias hacia Infrastructure.

## Tablas y restricciones

Se usan nombres explícitos `Clients` y `Projects`, con columnas PascalCase; el SQL PostgreSQL generado utiliza identificadores entre comillas. No se introduce un esquema personalizado.

| Tabla | Columnas y tipos | Nullability |
| --- | --- | --- |
| Clients | `Id`: uuid; `Name`: text; `CreatedAt`, `UpdatedAt`: timestamp with time zone | Todas NOT NULL |
| Projects | `Id`, `ClientId`: uuid; `Name`, `City`, `SgCode`: text; `ProductLine`: integer; `CreatedAt`, `UpdatedAt`: timestamp with time zone | Solo `SgCode` admite NULL |

- `PK_Clients` sobre `Clients.Id` y `PK_Projects` sobre `Projects.Id`.
- `FK_Projects_Clients_ClientId`: `Projects.ClientId` referencia `Clients.Id`, relación 1:N sin navegaciones, con `ON DELETE RESTRICT`. No se eliminan proyectos en cascada.
- `IX_Projects_ClientId`: índice no único de la FK.
- `IX_Projects_SgCode`: índice UNIQUE estándar, sin filtro. PostgreSQL permite múltiples NULL y rechaza valores no nulos repetidos.
- No hay índices únicos sobre nombres, longitudes máximas de texto, defaults de identidad/fechas ni datos seed.
- `ProductLine` se convierte a integer: **Classic=1, Essential=2, Bioconfort=3, Signature=4**. Estos valores forman parte del contrato de datos y no deben renumerarse. El constructor rechaza valores de enum no definidos; no se añade una restricción CHECK en esta fase.

## Migraciones

`20261013143318_InitialCreate` crea únicamente las dos tablas del modelo, sus PK, FK e índices. Su `Down` contiene la reversión generada por EF; no forma parte del procedimiento normal de instalación. EF administra además su tabla técnica `__EFMigrationsHistory` al aplicar migraciones.

No se utilizan `EnsureCreated()` ni migraciones automáticas en startup. No crear tablas manualmente, borrar o recrear la base. Antes de aplicar, confirmar que la conexión apunta a la base **existente** `sng_control` y que el usuario tiene permisos sobre ella. El tooling de EF puede intentar crear una base inexistente; no ejecutarlo contra un destino sin verificar.

La fábrica `SnG.Control.Api.Persistence.SnGControlDbContextFactory` implementa `IDesignTimeDbContextFactory<SnGControlDbContext>` y utiliza la configuración estándar de ASP.NET Core. Permite configurar únicamente el proveedor cuando no existe conexión, para generar/inspeccionar migraciones offline sin credenciales ficticias. Esa excepción se limita al tooling; el arranque normal sigue exigiendo la cadena. Los comandos incluyen `-- --environment Development` para cargar los User Secrets de la API de forma explícita.

### Inspeccionar sin conectarse

```shell
dotnet ef migrations list --no-connect --project src/SnG.Control.Infrastructure --startup-project src/SnG.Control.Api -- --environment Development
dotnet ef migrations has-pending-model-changes --project src/SnG.Control.Infrastructure --startup-project src/SnG.Control.Api -- --environment Development
dotnet ef migrations script 0 InitialCreate --project src/SnG.Control.Infrastructure --startup-project src/SnG.Control.Api -- --environment Development
```

### Aplicar a la base existente

Después de configurar y verificar la conexión:

```shell
dotnet ef database update --project src/SnG.Control.Infrastructure --startup-project src/SnG.Control.Api -- --environment Development
```

Comprobar que el comando termina correctamente y verificar el estado aplicado:

```shell
dotnet ef migrations list --project src/SnG.Control.Infrastructure --startup-project src/SnG.Control.Api -- --environment Development
```

El repositorio contiene la migración generada y validada; la aplicación es una operación por entorno. Sin conexión configurada queda pendiente aplicar `InitialCreate`, sin impedir restauración, compilación, tests ni validación offline.

### Crear migraciones futuras

Solo después de aprobar el nuevo alcance y ajustar el modelo:

```shell
dotnet ef migrations add <NombreDescriptivo> --project src/SnG.Control.Infrastructure --startup-project src/SnG.Control.Api --output-dir Persistence/Migrations -- --environment Development
```

Revisar la migración y el snapshot: columnas, tipos, nullability, claves, índices y comportamiento de borrado. Si una migración todavía no aplicada es incorrecta, corregir la configuración y regenerarla mediante tooling. No editar como primera opción el SQL o el snapshot a mano ni alterar migraciones aplicadas. Compilar y ejecutar tests antes de aplicar.

## Ejecutar la API

Con la conexión configurada y, para persistir datos, las migraciones aplicadas:

```shell
dotnet dev-certs https --trust
dotnet run --project src/SnG.Control.Api --launch-profile https
```

El perfil `https` usa `https://localhost:7265` y `http://localhost:5051`, en `Development`, con redirección HTTP a HTTPS. Detener con `Ctrl+C`. También existe el perfil `http` en 5051, sin puerto HTTPS propio; la redirección puede advertir que no encuentra ese puerto. Se recomienda el perfil `https`.

En desarrollo, `GET /openapi/v1.json` publica OpenAPI sin operaciones de negocio. No hay Swagger UI ni Minimal APIs de negocio; `/` devuelve 404. Fuera de desarrollo no se publica OpenAPI.

- `Program.cs`: valida la presencia de conexión, registra `SnGControlDbContext` con lifetime scoped y Npgsql, Controllers y OpenAPI; configura HTTPS y rutas de Controllers.
- `appsettings.json`: logging (`Information` general y `Warning` para ASP.NET Core) y `AllowedHosts=*`; no contiene conexiones.
- `Properties/launchSettings.json`: perfiles/puertos locales y `ASPNETCORE_ENVIRONMENT=Development`.
- El registro del contexto no ejecuta operaciones de base de datos ni aplica migraciones.

## Reglas permanentes para continuar

- Implementar solo funcionalidad solicitada y reglas confirmadas. Reportar decisiones pendientes sin codificarlas silenciosamente.
- Mantener invariantes en Domain, coordinación de casos de uso en Application cuando se definan, persistencia e integraciones en Infrastructure y Controllers delgados en Api.
- Respetar referencias y namespaces. No introducir abstracciones, servicios, patrones ni librerías sin necesidad actual.
- Eliminar código muerto, dependencias sin uso, duplicación y ejemplos de template. No conservar código comentado ni versiones históricas; Git conserva la historia.
- Revisar y mantener ambos README como documentación viva: el funcional explica negocio y alcance, el técnico explica implementación y operación. No son changelogs.
- Validar herramientas, restore, build, tests y migraciones; corregir warnings atribuibles al código sin ocultarlos mediante configuración. Reportar los que legítimamente permanezcan y cualquier supuesto o impedimento.
- No incluir secretos reales en archivos versionados. No aplicar migraciones automáticamente al arrancar.

Siguen pendientes de alcance posterior los casos de uso, endpoints, edición, asignación/formato de SG y demás módulos funcionales. No hay decisión adicional de negocio necesaria para el modelo de persistencia actual.
