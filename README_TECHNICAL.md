# SnG Control Backend — Guía técnica

## Estado y stack

La solución `SnG.Control.sln` utiliza .NET 10 (`net10.0`), C#, ASP.NET Core Web API con Controllers tradicionales y Clean Architecture. El núcleo de negocio contiene `Client`, `Project`, `ProjectTask`, la versión de cotización confirmada, el catálogo comercial base y sus snapshots; sus reglas se describen en [README.md](README.md).

La persistencia utiliza Entity Framework Core 10 y el proveedor Npgsql para PostgreSQL. El modelo y las seis migraciones, desde `InitialCreate` hasta `AddCommercialQuotationHandoff`, están en Infrastructure. La base esperada es **`sng_control`**, creada previamente por el administrador. Su host, puerto y credenciales deben suministrarse explícitamente.

Application implementa catorce casos de uso: crear, obtener por Id y listar clientes y proyectos; enviar un proyecto a Presupuestos; listar tareas por área/estado y por proyecto; asignar o corregir SG; obtener un preview técnico de cotización XLSX y confirmar sus valores revisados; enviar la cotización a Comercial y descargar su Excel operativo. Los Controllers delegan en estos casos de uso, que acceden a PostgreSQL mediante interfaces específicas implementadas en Infrastructure. El preview solo consulta datos y utiliza un parser de Infrastructure, sin persistir catálogo ni documentos. No hay repositorios genéricos, Unit of Work propio, autenticación, autorización ni Docker. La API exige configuración de conexión y almacenamiento, pero no aplica migraciones ni abre una conexión a la base durante el arranque.

## Requisitos y paquetes

- SDK de .NET 10. No hay `global.json` que fije una versión concreta del SDK.
- Acceso a NuGet para restaurar paquetes y herramientas, o una caché local disponible.
- PostgreSQL accesible y la base existente `sng_control` para aplicar migraciones o persistir datos. Los tests y la generación de migraciones no requieren servidor ni contraseña.
- Certificado de desarrollo confiable para ejecutar el perfil HTTPS.

| Proyecto | PackageReference | Versión | Uso |
| --- | --- | --- | --- |
| Infrastructure | Microsoft.EntityFrameworkCore.Relational | 10.0.12 | Fluent API relacional y migraciones; fija también EF Core en 10.0.12 |
| Infrastructure | Npgsql.EntityFrameworkCore.PostgreSQL | 10.0.3 | Proveedor PostgreSQL |
| Infrastructure | DocumentFormat.OpenXml | 3.5.1 | Lectura XLSX, cachés de fórmulas e imágenes con anchors; licencia MIT |
| Api | Microsoft.EntityFrameworkCore.Design | 10.0.12 | Tooling del startup project; `PrivateAssets=all` |
| Api | Microsoft.AspNetCore.OpenApi | 10.0.12 | Documento OpenAPI en desarrollo |
| Tests | Microsoft.NET.Test.Sdk | 17.14.1 | Ejecución de tests |
| Tests | xunit | 2.9.3 | Tests unitarios |
| Tests | xunit.runner.visualstudio | 3.1.4 | Descubrimiento y ejecución de xUnit |
| Tests | coverlet.collector | 6.0.4 | Cobertura opcional |

`Microsoft.EntityFrameworkCore` 10.0.12 y `Npgsql` 10.0.3 se resuelven transitivamente. No se duplican sus referencias en Api ni Tests. Domain y Application no dependen de EF ni de paquetes externos. No se utilizan proveedores InMemory o SQLite.

[Open XML SDK 3.5.1](https://www.nuget.org/packages/DocumentFormat.OpenXml/3.5.1), mantenido en [su repositorio oficial bajo MIT](https://github.com/dotnet/Open-XML-SDK), permite leer valores almacenados, formatos numéricos y posiciones/bytes originales de dibujos sin Office, COM ni procesos externos. Es el único paquete XLSX directo; sus dependencias transitivas las resuelve NuGet. No contiene un motor de cálculo: se usan exclusivamente los valores cacheados del archivo. Application y Domain no referencian la librería.

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
    Contracts/AssignSgCodeRequest.cs
    Contracts/ProductLineJsonConverter.cs
    Contracts/CommercialItemSectionJsonConverter.cs
    Contracts/QuotationMultipartReader.cs
    Controllers/ClientsController.cs
    Controllers/ProjectsController.cs
    Controllers/ProjectTasksController.cs
    Controllers/BudgetQuotationController.cs
    Controllers/CommercialHandoffController.cs
    Filters/DisableFormValueModelBindingAttribute.cs
    Errors/ApiExceptionHandler.cs
    Persistence/SnGControlDbContextFactory.cs
    appsettings.json
    Properties/launchSettings.json
  Application/
    Files/IFileStorage.cs
    Features/
      Budgets/
        IBudgetsWorkflowStore.cs
        BudgetQuotationWorkflow.cs
        CommercialHandoff/IBudgetQuotationCommercialHandoffStore.cs
        CommercialHandoff/IBudgetQuotationWorkbookGenerator.cs
        CommercialHandoff/SendBudgetQuotationToCommercialUseCase.cs
        CommercialHandoff/DownloadCommercialQuotationUseCase.cs
        ConfirmQuotation/BudgetQuotationConfirmation.cs
        ConfirmQuotation/IBudgetQuotationConfirmationStore.cs
        ConfirmQuotation/ConfirmBudgetQuotationUseCase.cs
        AssignSg/AssignProjectSgCodeUseCase.cs
        AssignSg/AssignProjectSgCodeResult.cs
        AssignSg/AssignProjectSgCodeResponse.cs
        PreviewQuotation/IBudgetQuotationParser.cs
        PreviewQuotation/BudgetQuotationParseResult.cs
        PreviewQuotation/BudgetQuotationPreviewResponse.cs
        PreviewQuotation/PreviewBudgetQuotationResult.cs
        PreviewQuotation/PreviewBudgetQuotationUseCase.cs
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
    Entities/BudgetQuotationVersion.cs
    Entities/CommercialCatalog.cs
    Entities/CommercialCatalogItem.cs
    Entities/CommercialCatalogItemExtraction.cs
    ValueObjects/CatalogItemValues.cs
    Enums/CommercialItemSection.cs
    Enums/CatalogItemImageStatus.cs
    Enums/ProductLine.cs
    Enums/OperationalArea.cs
    Enums/ProjectTaskType.cs
    Enums/ProjectTaskStatus.cs
  Infrastructure/
    Files/FileStorageOptions.cs
    Files/FileStorageOptionsValidator.cs
    Files/LocalFileStorage.cs
    Quotations/
      XlsxBudgetQuotationParser.cs
      XlsxBudgetQuotationWorkbookGenerator.cs
      QuotationWorksheetLayout.cs
      QuotationCellReader.cs
      BudgetQuotationParserOptions.cs
      UnsupportedQuotationException.cs
    Persistence/
      SnGControlDbContext.cs
      Stores/EfClientStore.cs
      Stores/EfProjectStore.cs
      Stores/EfProjectTaskStore.cs
      Stores/EfBudgetsWorkflowStore.cs
      Stores/EfBudgetQuotationConfirmationStore.cs
      Stores/EfBudgetQuotationCommercialHandoffStore.cs
      Configurations/ClientConfiguration.cs
      Configurations/ProjectConfiguration.cs
      Configurations/ProjectTaskConfiguration.cs
      Configurations/BudgetQuotationVersionConfiguration.cs
      Configurations/CommercialCatalogConfiguration.cs
      Configurations/CommercialCatalogItemConfiguration.cs
      Configurations/CommercialCatalogItemExtractionConfiguration.cs
      Migrations/
        20261013143318_InitialCreate.cs
        20261013143318_InitialCreate.Designer.cs
        20261013190849_AddProjectTasks.cs
        20261013190849_AddProjectTasks.Designer.cs
        20261013192753_EnforceUniquePrepareQuotationTask.cs
        20261013192753_EnforceUniquePrepareQuotationTask.Designer.cs
        20261013203446_EnforceSgCodeFormat.cs
        20261013203446_EnforceSgCodeFormat.Designer.cs
        20261013220201_AddBudgetQuotationCatalog.cs
        20261013220201_AddBudgetQuotationCatalog.Designer.cs
        20261014141907_AddCommercialQuotationHandoff.cs
        20261014141907_AddCommercialQuotationHandoff.Designer.cs
        SnGControlDbContextModelSnapshot.cs
tests/
  SnG.Control.Tests/
    Domain/
    Application/
    Api/
    TestDoubles/
    Fixtures/QuotationWorkbook.cs
    Fixtures/ConfirmationScenario.cs
    Fixtures/CommercialHandoffScenario.cs
    Fixtures/TemporaryStorage.cs
    Storage/LocalFileStorageTests.cs
    Quotations/XlsxBudgetQuotationParserTests.cs
    Quotations/CommercialWorkbookGeneratorTests.cs
    Persistence/SnGControlDbContextTests.cs
    Persistence/EfProjectTaskStoreTests.cs
    Persistence/EfBudgetsWorkflowStoreTests.cs
    Persistence/QuotationCatalogModelTests.cs
    Persistence/QuotationConfirmationCompensationTests.cs
    Persistence/CommercialHandoffCompensationTests.cs
```

| Proyecto | Responsabilidad actual |
| --- | --- |
| Domain | Entidades e invariantes, sin dependencias de persistencia o HTTP. |
| Application | Catorce casos de uso, respuestas e interfaces específicas de parsing, archivos y persistencia. |
| Infrastructure | Contexto EF, stores PostgreSQL, configuraciones Fluent API, migraciones, parser/generador XLSX y almacenamiento local. |
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

### Almacenamiento local

La API exige `FileStorage:RootPath` al arrancar. Debe ser una ruta absoluta, no vacía, que exista o pueda crearse. `FileStorageOptionsValidator` valida esa configuración sin revelar el valor en sus mensajes. El usuario del proceso necesita permisos de escritura, rename y eliminación; un fallo posterior de I/O es un error de servidor.

Configurar exclusivamente en User Secrets o entorno, con una carpeta confirmada para esa instalación:

```shell
dotnet user-secrets set "FileStorage:RootPath" "<DIRECTORIO_ABSOLUTO_DE_ARCHIVOS>" --project src/Api/Api.csproj
```

Alternativa: `FileStorage__RootPath=<DIRECTORIO_ABSOLUTO_DE_ARCHIVOS>`. Las variables de entorno tienen prioridad sobre User Secrets. No versionar rutas reales ni añadir RootPath a PostgreSQL o appsettings.json. El tooling EF puede inspeccionar el modelo sin esta configuración; el arranque HTTP normal la exige, incluido cuando se utiliza solo preview.

`Application/Files/IFileStorage` expone SaveAsync, ReadAsync y DeleteAsync mediante clave relativa, bytes y CancellationToken. No expone RootPath ni rutas físicas. `Infrastructure/Files/LocalFileStorage` resuelve las claves dentro de la raíz configurada, rechaza rutas absolutas, traversal, segmentos vacíos/punto, backslashes, colon y caracteres inválidos. También rechaza symlinks/junctions existentes dentro de la raíz. La carpeta debe pertenecer al servicio: no se pretende proteger frente a otro proceso privilegiado que cambie enlaces mientras se escribe.

La escritura crea directorios necesarios, escribe un temporal único con CreateNew en el directorio destino, hace flush a disco y rename al nombre final con overwrite desactivado. Si falla, intenta eliminar su temporal. DeleteAsync elimina exclusivamente la clave solicitada; no realiza limpieza global ni borra directorios ajenos. Los directorios vacíos de una operación fallida pueden permanecer.

```text
<ROOT>/projects/<projectId>/budget-quotations/<quotationVersionId>/
  original.xlsx
  confirmed.xlsx  (después del envío a Comercial)
  images/<catalogItemId>.<extension>
```

Las claves se generan internamente con Guid del dominio, nunca con SG, nombres de personas/proyectos o ItemNumber. El XLSX original conserva exactamente los bytes recibidos; no se vuelve a guardar con OpenXML. El generador edita únicamente la copia operativa. Su contentType es `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`. Las imágenes conservan bytes y contentType originales: PNG→`.png`, JPEG→`.jpg`, GIF→`.gif`, BMP→`.bmp`, TIFF→`.tiff`; otros tipos usan `.bin` sin conversión. El Excel operativo se descarga por HTTP después del envío a Comercial. No hay servicio HTTP de imágenes ni soporte S3/Azure.

### Compilación y tests

```shell
dotnet build
dotnet test
```

Los tests normales funcionan sin conexión configurada, sin contraseña y sin PostgreSQL levantado. Los tests del modelo instancian el contexto con `UseNpgsql()` sin cadena y consultan metadata o compilan consultas sin ejecutarlas. Los tests de Application usan dobles de `IClientStore`, `IProjectStore`, `IProjectTaskStore` e `IBudgetsWorkflowStore`, ubicados exclusivamente en Tests.

Los tests HTTP arrancan Kestrel en un puerto loopback dinámico, utilizan el registro real `AddApiServices`, los Controllers y el middleware de errores, y reemplazan los stores por dobles. Comprueban rutas, binding, validación, serialización, `Location`, códigos HTTP y ausencia de detalles internos. No necesitan paquetes de hosting de tests adicionales, Docker ni proveedores ficticios de EF. Requieren permiso del entorno para abrir un puerto local. No prueban PostgreSQL, el perfil HTTPS ni la carga de secretos de Program; esa parte se verifica por smoke test controlado con la base local.

La suite contiene 460 tests. Cubre invariantes, fechas UTC, nombres repetidos, propagación de CancellationToken, rechazo de proyectos con cliente inexistente antes de escribir, contratos numéricos de enums, PK/FK e índices, tipos PostgreSQL, nullability y consistencia de las migraciones. Para tareas prueba creación Pending, rechazo de inserciones duplicadas de cualquier estado y área, filtros, historial vacío/inexistente, 201/404/409, parámetros requeridos y rechazo de números o nombres desconocidos. También verifica que crear Project no crea tareas y que no existe POST genérico de tareas.

Los 66 casos de preview usan XLSX anónimos generados en memoria: bloques reales/vacíos, Main/Additional, números repetidos y formatos con ceros, orden documental, etiquetas, fórmulas cacheadas sin cálculo, M2, imágenes por anchor y sus warnings, SG, archivos inválidos y límites técnicos. Application prueba todos los requisitos del workflow con stores cuyas operaciones de escritura fallan si se invocan. Los tests multipart utilizan el parser real y comprueban el contrato completo y sus errores, sin datos reales de clientes.

Los tests de SG cubren normalización y dígitos ASCII, ceros iniciales, actualización de fechas, Start, primera asignación, corrección e idempotencia, conflictos de workflow y unicidad, request HTTP estricto, CHECK y traducción específica de errores. `EfBudgetsWorkflowStoreTests` comprueba que ambos cambios llegan a una sola llamada SaveChanges, la precisión de timestamps y el detach de ambas entidades sin limpiar entidades ajenas. La transacción y los bloqueos se validan con PostgreSQL real. El doble de workflow usa copias de entidades para no confundir cambios fallidos con datos guardados; la reflexión de copiado se limita a Tests. Los escenarios InProgress usan Start; Complete permite finalizar PrepareQuotation mediante el envío a Comercial.

`EfProjectTaskStoreTests` usa el ChangeTracker real y un contexto de test que sustituye únicamente SaveChangesAsync, sin conexión ni paquetes adicionales. Verifica éxito y token, traducción exclusiva de SQLSTATE 23505 con el índice esperado, propagación de otros constraints/códigos/errores, detach de la entidad rechazada y conservación de otras entidades tracked. Estos tests comprueban la traducción y limpieza; la garantía de concurrencia se valida además contra PostgreSQL real mediante el smoke descrito más abajo.

Cobertura opcional:

```shell
dotnet test --collect:"XPlat Code Coverage"
```

Los artefactos de compilación y `TestResults` están excluidos por `.gitignore`.

## Contexto y materialización

`SnGControlDbContext` está en `SnG.Control.Infrastructure.Persistence`. Recibe `DbContextOptions<SnGControlDbContext>`, expone `Clients`, `Projects` y `ProjectTasks` y aplica explícitamente `ClientConfiguration`, `ProjectConfiguration` y `ProjectTaskConfiguration`.

Las entidades mantienen sus constructores públicos y validaciones. Sus propiedades tienen setters privados; Project expone SetSgCode y ProjectTask expone Start. EF enlaza los parámetros de los constructores existentes con las propiedades mapeadas y restaura el resto de valores mediante esos setters; no se necesita un constructor vacío.

Los identificadores nacen de `Guid.NewGuid()` en el dominio y se mapean con `ValueGeneratedNever()`: no hay generación de UUID en PostgreSQL. Las fechas son `DateTime` UTC; `CreatedAt` y `UpdatedAt` coinciden al crear clientes o proyectos. Los timestamps e identificadores almacenados se conservan al materializar entidades; no hay mecanismo automático de actualización de fechas.

`ProjectTask(projectId, area, type)` exige un ProjectId no vacío y enums definidos. Genera Id y AssignedAt UTC; fija Status=Pending y StartedAt/CompletedAt=null. Start solo acepta Pending, cambia a InProgress, fija StartedAt UTC y deja CompletedAt=null; rechaza InProgress y Completed sin reiniciar fechas. Complete exige InProgress, StartedAt presente y CompletedAt null; cambia a Completed y fija CompletedAt UTC preservando AssignedAt y StartedAt. No hay Return/Cancel ni endpoint genérico de finalización. Sus ocho propiedades tienen setters privados. `Project` conserva su estructura, sin estado global ni colección Tasks.

`Project.SetSgCode` rechaza null/vacío/whitespace, aplica Trim y ToUpperInvariant y exige SG seguido de al menos un dígito ASCII. La validación recorre los caracteres, equivalente a `^SG[0-9]+$` sobre la entrada normalizada completa; no acepta dígitos Unicode ni caracteres adicionales. Conserva ceros iniciales. Si el valor es distinto, modifica SgCode y UpdatedAt UTC; si es igual no modifica nada. CreatedAt permanece intacto y no existe generación de consecutivos.

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
- `CK_Projects_SgCode_Format`: CHECK `"SgCode" IS NULL OR "SgCode" ~ '^SG[0-9]+$'`. Rechaza códigos externos sin normalizar o con formato inválido. SgCode conserva su tipo text y nullability; el CHECK no normaliza datos ni usa triggers.
- No hay índices únicos sobre nombres, longitudes máximas de texto, defaults de identidad/fechas ni datos seed.
- `ProductLine` se convierte a integer: **Classic=1, Essential=2, Bioconfort=3, Signature=4**. Estos valores forman parte del contrato de datos y no deben renumerarse. El constructor rechaza valores de enum no definidos; no se añade una restricción CHECK en esta fase.

`ProjectTasks` tiene `PK_ProjectTasks` sobre Id y `FK_ProjectTasks_Projects_ProjectId` hacia Projects.Id, relación 1:N requerida sin navegaciones y con `ON DELETE RESTRICT`. No hay borrado en cascada. Conserva los índices no únicos `IX_ProjectTasks_ProjectId`, para consultar todas las tareas del proyecto independientemente de su tipo, e `IX_ProjectTasks_Area_Status`.

El índice **`UX_ProjectTasks_ProjectId_PrepareQuotation`** es único sobre ProjectId con filtro exacto **`"Type" = 1`**. Impide dos tareas PrepareQuotation para el mismo proyecto, independientemente de área o estado, incluso ante inserciones concurrentes. No se usa UNIQUE(ProjectId, Type): cada filtro limita la regla a su tipo y no impone unicidad a otros tipos futuros. Fluent API utiliza un índice con nombre propio en el modelo para conservar también el índice no único sobre ProjectId. El nombre se comparte entre configuración y detección de conflictos mediante una constante interna de Infrastructure.

`UX_ProjectTasks_ProjectId_ReviewQuotation` aplica la misma garantía a ReviewQuotation con filtro `"Type" = 2`, independientemente del área o estado. Su nombre explícito también se comparte con el detector de conflictos del store de envío a Comercial.

Los enums de tarea se convierten explícitamente a integer y conservan estos contratos estables:

| Enum | Valores |
| --- | --- |
| OperationalArea | Commercial=1, Budgets=2 |
| ProjectTaskType | PrepareQuotation=1, ReviewQuotation=2 |
| ProjectTaskStatus | Pending=1, InProgress=2, Completed=3 |

Domain valida los valores; no hay restricciones CHECK ni valores predeterminados de base para estos campos.

### Cotización y catálogo comercial persistidos

El modelo relacional añade cuatro tablas, sin JSON/JSONB, bytea ni columnas económicas:

| Tabla/entidad | Campos principales y relación |
| --- | --- |
| BudgetQuotationVersions | Id, ProjectId, VersionNumber, OriginalFileName, OriginalFileStorageKey, ConfirmedFileStorageKey nullable, OriginalFileContentType, DocumentClientName, DocumentProjectName, DocumentLocation, ClientLine, ProductionLine, PreparedBy, DocumentSgCode, ConfirmedAt. FK requerida hacia Project. |
| CommercialCatalogs | Id, BudgetQuotationVersionId y CreatedAt. FK única hacia la versión: relación 1:1. |
| CommercialCatalogItems | Id, CommercialCatalogId, Section, SourceRow y los once valores confirmados. Relación N:1 al catálogo. |
| CommercialCatalogItemExtractions | CatalogItemId como PK/FK 1:1 al ítem; los once valores extraídos, ImageStatus, ImageStorageKey e ImageContentType. |

Los once valores técnicos son ItemNumber, Location, System, Glass, AluminumFinish, LockType, Observations, Width, Height, SquareMeters y Quantity. Se guardan como columnas explícitas en ambas tablas. Textos y claves son `text`, medidas son `numeric` sin escala artificial, cantidad/fila/versión/enums son `integer`, identificadores son `uuid` y fechas son `timestamp with time zone`. ConfirmedFileStorageKey, ImageStorageKey e ImageContentType admiten null en estas tablas. No se almacena RootPath ni el contenido binario de archivos.

Domain genera los Guid, conserva setters privados y fechas UTC a precisión de microsegundos. `CatalogItemValues` concentra la validación común sin ser una entidad EF: ItemNumber no vacío y recortado; textos opcionales null se normalizan a cadena vacía, con Trim exterior y contenido interno intacto; medidas/cantidad positivas, sin recalcular m². Las entidades contienen propiedades escalares; sus constructores privados sin parámetros se reservan para materialización EF. SourceRow debe ser positivo y Section definido. El orden se recupera mediante SourceRow; ItemNumber puede repetirse y cambiarse en la confirmación.

`CommercialItemSection`: Main=1, Additional=2. `CatalogItemImageStatus`: Stored=1, Missing=2, Ambiguous=3. Una imagen Stored exige clave relativa y contentType; Missing/Ambiguous exigen ambos null. El estado pertenece al snapshot de extracción. No se persisten mensajes libres de warnings. DocumentSgCode es el SG normalizado extraído durante la confirmación y no cambia cuando se corrige posteriormente Project.SgCode.

Todas las FK nuevas utilizan Restrict. Los índices son:

- `UX_BudgetQuotationVersions_ProjectId_VersionNumber`: UNIQUE(ProjectId, VersionNumber), autoridad de concurrencia.
- `IX_BudgetQuotationVersions_OriginalFileStorageKey`: UNIQUE sobre la clave del XLSX.
- `IX_BudgetQuotationVersions_ConfirmedFileStorageKey`: UNIQUE nullable sobre el archivo operativo; permite múltiples null.
- `IX_CommercialCatalogs_BudgetQuotationVersionId`: UNIQUE, un catálogo por versión.
- `IX_CommercialCatalogItems_CommercialCatalogId`: consulta de ítems del catálogo.
- `IX_CommercialCatalogItems_CommercialCatalogId_SourceRow`: UNIQUE, una fila fuente por catálogo.
- `IX_CommercialCatalogItemExtractions_ImageStorageKey`: UNIQUE nullable; permite varios null.

La extracción utiliza PK/FK CatalogItemId y no necesita otra identidad. No hay índices únicos sobre ItemNumber. CHECKs protegen versión positiva, fila positiva, Section definido, medidas/cantidades positivas, ItemNumber no vacío y consistencia del estado de imagen. Domain admite VersionNumber positivo, pero Application crea exclusivamente **1**; no existe operación para versión 2, reemplazo o edición del catálogo.

## Migraciones

`20261014141907_AddCommercialQuotationHandoff` agrega únicamente ConfirmedFileStorageKey nullable (`text`), su índice único y `UX_ProjectTasks_ProjectId_ReviewQuotation`, UNIQUE(ProjectId) con filtro `"Type" = 2`. Mantiene el índice de PrepareQuotation y las cinco migraciones anteriores intactas. Su Down revierte exclusivamente estas tres incorporaciones.

`20261013220201_AddBudgetQuotationCatalog` crea únicamente BudgetQuotationVersions, CommercialCatalogs, CommercialCatalogItems y CommercialCatalogItemExtractions, con sus FK, índices y CHECKs. Las cuatro migraciones anteriores no se modifican. El snapshot incorpora el nuevo modelo; RootPath no forma parte del esquema. Su Down elimina las cuatro tablas nuevas en orden de dependencias.

`20261013143318_InitialCreate` crea Clients y Projects, sus PK, FK e índices. `20261013190849_AddProjectTasks` agrega ProjectTasks, su PK, FK e índices de consulta. `20261013192753_EnforceUniquePrepareQuotationTask` agrega el índice único parcial. Las tres permanecen intactas, incluidos sus Designers y modelos históricos. `20261013203446_EnforceSgCodeFormat`, generada mediante EF tooling, agrega únicamente el CHECK de SG; no cambia columnas, relaciones ni ProjectTasks. El snapshot vigente incluye estas restricciones. Los métodos `Down` contienen la reversión generada por EF; no forman parte del procedimiento normal de instalación. EF administra además su tabla técnica `__EFMigrationsHistory` al aplicar migraciones.

No se utilizan `EnsureCreated()` ni migraciones automáticas en startup. No crear tablas manualmente, borrar o recrear la base. Antes de aplicar, confirmar que la conexión apunta a la base **existente** `sng_control` y que el usuario tiene permisos sobre ella. El tooling de EF puede intentar crear una base inexistente; no ejecutarlo contra un destino sin verificar.

La fábrica `SnG.Control.Api.Persistence.SnGControlDbContextFactory` implementa `IDesignTimeDbContextFactory<SnGControlDbContext>` y utiliza la configuración estándar de ASP.NET Core. Permite configurar únicamente el proveedor cuando no existe conexión, para generar/inspeccionar migraciones offline sin credenciales ficticias. Esa excepción se limita al tooling; el arranque normal sigue exigiendo la cadena. Los comandos incluyen `-- --environment Development` para cargar los User Secrets de la API de forma explícita.

### Inspeccionar sin conectarse

```shell
dotnet ef migrations list --no-connect --project src/Infrastructure --startup-project src/Api -- --environment Development
dotnet ef migrations has-pending-model-changes --project src/Infrastructure --startup-project src/Api -- --environment Development
dotnet ef migrations script 0 AddBudgetQuotationCatalog --project src/Infrastructure --startup-project src/Api -- --environment Development
dotnet ef migrations script EnforceSgCodeFormat AddBudgetQuotationCatalog --project src/Infrastructure --startup-project src/Api -- --environment Development
dotnet ef migrations script EnforceUniquePrepareQuotationTask EnforceSgCodeFormat --project src/Infrastructure --startup-project src/Api -- --environment Development
```

### Aplicar a la base existente

Después de configurar y verificar la conexión, si la base ya tiene ProjectTasks, comprobar duplicados antes de aplicar el índice:

```sql
SELECT "ProjectId", COUNT(*)
FROM "ProjectTasks"
WHERE "Type" = 1
GROUP BY "ProjectId"
HAVING COUNT(*) > 1;
```

Si devuelve filas, detener la aplicación de la migración, reportar los ProjectId afectados y esperar una decisión sobre esos datos. No borrar ni escoger registros automáticamente. La migración no sanea datos y el índice rechazará duplicados que aparezcan entre la comprobación y su creación.

El SQL de la nueva protección, revisado antes de aplicar, es:

```sql
CREATE UNIQUE INDEX "UX_ProjectTasks_ProjectId_PrepareQuotation"
ON "ProjectTasks" ("ProjectId") WHERE "Type" = 1;
```

Antes de aplicar EnforceSgCodeFormat, comprobar también los SG existentes:

```sql
SELECT "Id", "SgCode" FROM "Projects"
WHERE "SgCode" IS NOT NULL AND "SgCode" !~ '^SG[0-9]+$';
```

Si aparecen filas, detenerse, reportar los proyectos afectados y esperar decisión. No normalizar, corregir ni borrar automáticamente. El SQL de esta migración agrega exclusivamente:

```sql
ALTER TABLE "Projects" ADD CONSTRAINT "CK_Projects_SgCode_Format"
CHECK ("SgCode" IS NULL OR "SgCode" ~ '^SG[0-9]+$');
```

Con las comprobaciones sin datos incompatibles:

```shell
dotnet ef database update --project src/Infrastructure --startup-project src/Api -- --environment Development
```

Comprobar que el comando termina correctamente y verificar el estado aplicado:

```shell
dotnet ef migrations list --project src/Infrastructure --startup-project src/Api -- --environment Development
```

El modelo tiene exactamente seis migraciones, en orden: `InitialCreate`, `AddProjectTasks`, `EnforceUniquePrepareQuotationTask`, `EnforceSgCodeFormat`, `AddBudgetQuotationCatalog` y `AddCommercialQuotationHandoff`. Las seis están aplicadas y verificadas en `__EFMigrationsHistory` de la instalación local `sng_control`; las comprobaciones previas encontraron cero grupos duplicados y cero SG incompatibles. Otros entornos deben comprobar su propio historial antes de operar. Los comandos de inspección offline no verifican si una base concreta tiene migraciones aplicadas.

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

En desarrollo, `GET /openapi/v1.json` publica OpenAPI con las catorce operaciones de negocio. No hay Swagger UI ni Minimal APIs de negocio; `/` devuelve 404. Fuera de desarrollo no se publica OpenAPI. Los cuerpos multipart del preview y confirmación se leen manualmente; sus contratos de carga se describen más abajo y no se infiere de un parámetro IFormFile.

- `Program.cs`: valida la presencia de conexión, registra `SnGControlDbContext` scoped con Npgsql, llama `AddApiServices` y registra OpenAPI; configura manejo de excepciones, HTTPS y Controllers.
- `DependencyInjection.cs`: `AddApiServices` registra los catorce casos de uso, los seis stores, IFileStorage, el parser y el generador con lifetime scoped, Controllers, opciones JSON, límites técnicos validados al arrancar y manejo de errores. Application no necesita depender de una librería de DI.
- `appsettings.json`: logging (`Information` general y `Warning` para ASP.NET Core) y `AllowedHosts=*`; no contiene conexiones.
- `Properties/launchSettings.json`: perfiles/puertos locales y `ASPNETCORE_ENVIRONMENT=Development`.
- El registro del contexto no ejecuta operaciones de base de datos ni aplica migraciones.

## Casos de uso y persistencia

El flujo de datos persistidos es `HTTP → Controller → UseCase → Store EF → PostgreSQL`; el preview añade una llamada al parser XLSX después de consultar el workflow. Application está organizada verticalmente en `Features/Clients` y `Features/Projects`, cada una con `Create`, `GetById` y `List`; `Features/ProjectTasks`, con `SendToBudgets`, `ListByArea` y `ListByProject`; y `Features/Budgets`, con `AssignSg`, `PreviewQuotation`, `ConfirmQuotation` y `CommercialHandoff`. Los casos de uso son clases explícitas con `ExecuteAsync`, sin MediatR ni interfaces por caso de uso.

`IClientStore` declara `AddAsync`, `GetByIdAsync`, `ListAsync` y `ExistsAsync`; `IProjectStore` declara los tres primeros. Las interfaces viven junto a su feature en Application y trabajan con entidades del dominio, sin exponer EF, `IQueryable` ni detalles de PostgreSQL.

`EfClientStore` y `EfProjectStore` implementan esas operaciones con el contexto existente. `AddAsync` agrega la entidad y ejecuta `SaveChangesAsync` antes de completar; no se requiere un método separado de commit. Todas las lecturas usan `AsNoTracking` y operaciones asíncronas. No se define orden de listado. Controllers, casos de uso y stores propagan `CancellationToken`.

Crear cliente construye la entidad validada, la guarda y mapea `ClientResponse`. Crear proyecto construye la entidad validada, verifica explícitamente la existencia del cliente con `ExistsAsync`, guarda y mapea `ProjectResponse`. Devuelve `null` si el cliente no existe, sin intentar guardar ni esperar un error de FK. Obtener por Id devuelve `null` si la entidad no existe; listar devuelve una colección, vacía cuando no hay registros.

Las respuestas de Application se mapean explícitamente con `FromEntity`, compartido por los casos de uso de cada feature. Los Controllers reciben requests propios de Api y devuelven esos DTO de respuesta, nunca entidades del dominio. No hay AutoMapper, repositorios genéricos ni Unit of Work adicional.

### Envío y consultas de tareas

`Features/ProjectTasks` contiene `IProjectTaskStore`, `ProjectTaskResponse` y los casos de uso agrupados en `SendToBudgets`, `ListByArea` y `ListByProject`. La interfaz declara únicamente `TryAddPrepareQuotationAsync`, `ListByAreaAsync` y `ListByProjectAsync`. La operación de inserción devuelve true si la tarea se guardó y false si ya existe PrepareQuotation para el proyecto. `EfProjectTaskStore` utiliza el mismo DbContext scoped; guarda con SaveChangesAsync, lee con AsNoTracking y propaga CancellationToken en todas las operaciones.

`SendProjectToBudgetsUseCase` primero consulta el Project. Si no existe devuelve `ProjectNotFound`; si existe, construye una tarea Budgets/PrepareQuotation/Pending e intenta insertarla mediante `TryAddPrepareQuotationAsync`. Un true produce `Created` con su DTO; un false produce `AlreadySent`. No hay consulta previa de tareas como garantía de unicidad. `SendProjectToBudgetsResult` expresa esos resultados mediante records específicos, sin excepciones de negocio ni framework Result genérico. Crear Project no invoca este caso de uso.

`ListProjectTasksByAreaUseCase` valida los enums y consulta por igualdad de Area y Status. `ListProjectTasksByProjectUseCase` verifica primero el proyecto: devuelve null si no existe, colección vacía si no tiene tareas o todas sus tareas en otro caso. No hay paginación ni orden garantizado en ninguna consulta.

PostgreSQL es la autoridad de integridad: la inserción se intenta directamente y el índice único parcial arbitra los envíos concurrentes. El store captura un DbUpdateException **solo** cuando su InnerException es PostgresException con SqlState=`PostgresErrorCodes.UniqueViolation` (23505) **y** ConstraintName=`UX_ProjectTasks_ProjectId_PrepareQuotation`. Ese conflicto se traduce a false → AlreadySent → HTTP 409. Una violación de otra restricción, otro SQLSTATE o cualquier otro error se propaga al manejo global de errores inesperados, sin exponer mensajes PostgreSQL por HTTP.

Después del conflicto esperado, el store establece `context.Entry(task).State = EntityState.Detached`. Solo desacopla la tarea rechazada; no vacía el ChangeTracker ni descarta otras entidades. Así un SaveChanges posterior en el mismo scope no reintenta la inserción fallida. No hay transacciones externas ni UnitOfWork adicional en este flujo; SaveChangesAsync utiliza el comportamiento transaccional de EF.

## Contrato HTTP actual

### Asignación y corrección de SG

`AssignProjectSgCodeUseCase` usa `IBudgetsWorkflowStore.LoadAsync` para obtener Project y su PrepareQuotation y `TrySaveAsync` para persistir ambos juntos. Es una frontera específica de Presupuestos; no combina los SaveChanges de los stores de creación. La respuesta reutiliza ProjectResponse y ProjectTaskResponse.

| Estado previo | Resultado |
| --- | --- |
| Project inexistente | ProjectNotFound |
| Project sin PrepareQuotation | ProjectNotSentToBudgets |
| SG null, tarea Pending, StartedAt/CompletedAt null | SetSgCode + Start + guardado atómico |
| SG existente, tarea InProgress, StartedAt presente y CompletedAt null | SetSgCode + guardado; conserva toda la tarea |
| Cualquier otro estado, incluido Completed o fechas inconsistentes | InvalidWorkflowState, sin reparación |

La validación de workflow precede a la modificación del dominio. Un valor normalizado idéntico produce Success sin cambios de fechas ni nuevas tareas. El guardado devuelve false únicamente para SgCodeAlreadyInUse; el caso de uso nunca devuelve Success después de ese conflicto.

`EfBudgetsWorkflowStore` comienza una transacción al cargar y obtiene Project con SELECT FOR UPDATE parametrizado. Carga la tarea con tracking en el mismo contexto. Esto serializa asignaciones al mismo proyecto: una segunda solicitud observa la primera asignación confirmada y la trata como corrección o repetición, conservando StartedAt. La transacción abarca la lectura, un único SaveChangesAsync para las dos entidades y el commit; no hay commits separados. Si el caso de uso termina antes de guardar, la disposición async del store scoped revierte/libera la transacción al cerrar el request.

El índice existente `IX_Projects_SgCode` arbitra solicitudes sobre proyectos distintos. El store traduce solo DbUpdateException con InnerException PostgresException, `PostgresErrorCodes.UniqueViolation` (23505) y ConstraintName=`IX_Projects_SgCode`. Revierte la transacción y desacopla exclusivamente Project y ProjectTask. No vacía el ChangeTracker: otras entidades conservan su seguimiento. Las referencias desacopladas no representan datos guardados; para continuar se deben cargar de nuevo. Otros errores se propagan al manejo global, que responde 500 genérico.

Antes de guardar, Infrastructure ajusta únicamente UpdatedAt y StartedAt modificados a precisión de microsegundos, que es la conservada por PostgreSQL. Esto evita diferencias entre la respuesta inicial y una lectura posterior; un StartedAt existente no se modifica. Los valores siguen siendo UTC. No se añade un framework transaccional ni se realizan preconsultas de unicidad de SG.

### Rutas

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
| PUT `/api/projects/{id}/sg-code` | `sgCode` | 200, AssignProjectSgCodeResponse; 409 por workflow o SG ocupado | 404 |
| POST `/api/projects/{id}/budget-quotation/preview` | multipart, un archivo `file` | 200, BudgetQuotationPreviewResponse; 400 archivo inválido; 409 workflow o SG distinto | 404 |
| POST `/api/projects/{id}/budget-quotation/confirm` | multipart: `file` y JSON `confirmation` | 201, ConfirmedBudgetQuotationResponse; 400 datos inválidos; 409 workflow, SG o confirmación existente | 404 |
| POST `/api/projects/{id}/send-to-commercial` | Sin body | 200, SendBudgetQuotationToCommercialResponse; 409 si no está listo o ya enviado | 404 |
| GET `/api/projects/{id}/budget-quotation/commercial-file` | Sin body | 200, XLSX con nombre original; 409 si aún no enviado; 500 si falta el archivo referenciado | 404 |

`ClientResponse` contiene `id`, `name`, `createdAt` y `updatedAt`. `ProjectResponse` contiene `id`, `clientId`, `name`, `city`, `productLine`, `sgCode`, `createdAt` y `updatedAt`. Las fechas son UTC en formato ISO 8601. Las respuestas de proyecto no incluyen el objeto Client completo. `sgCode` es null hasta su primera asignación y después contiene el código normalizado.

Se usa camelCase. `ProductLineJsonConverter` delega la lectura y escritura a `JsonStringEnumConverter<ProductLine>` de System.Text.Json con `allowIntegerValues=false`, y exige un único nombre: rechaza combinaciones separadas por comas que el convertidor estándar puede interpretar como una combinación numérica. Los nombres canónicos devueltos son `Classic`, `Essential`, `Bioconfort` y `Signature`; números JSON, cadenas numéricas, combinaciones y nombres desconocidos producen 400. El almacenamiento sigue siendo integer 1–4. La comparación de nombres no distingue mayúsculas/minúsculas.

Los requests de creación no incluyen `id`, fechas ni `sgCode`. `JsonUnmappedMemberHandling.Disallow` rechaza campos JSON desconocidos, incluidos esos campos controlados por el dominio. Los nombres repetidos continúan permitidos.

### Contrato de tareas

El envío a Presupuestos no recibe DTO ni body. Devuelve 201 con el DTO creado, sin cabecera Location: no existe GET por Id de tarea. Tampoco existe POST genérico `/api/project-tasks` ni endpoint separado para iniciar tareas; PrepareQuotation se inicia con la primera asignación de SG y ReviewQuotation al enviar a Comercial.

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

El GET por área acepta los nombres definidos Commercial/Budgets y Pending/InProgress/Completed, sin distinguir mayúsculas. Ambos parámetros son obligatorios; valores ausentes, vacíos, numéricos, combinados con comas o desconocidos producen 400. La validación de nombres se realiza explícitamente en el Controller porque el binding ordinario de enums también admite números. El envío crea Budgets/Pending; la primera asignación de SG permite consultar esa tarea como Budgets/InProgress. El envío a Comercial completa PrepareQuotation y crea ReviewQuotation en Commercial/InProgress. El historial devuelve `[]` para un proyecto existente sin tareas.

### Request y respuesta de SG

PUT `/api/projects/{id}/sg-code` recibe `AssignSgCodeRequest`, por ejemplo:

```json
{ "sgCode": " sg0012 " }
```

Devuelve 200 con `AssignProjectSgCodeResponse`:

```json
{
  "project": {
    "id": "b0825722-a7d2-4385-9d13-a1a5e6fd7e66",
    "clientId": "c17113f3-f4cd-49b9-a46f-169edda59237",
    "name": "Casa Campestre",
    "city": "Bucaramanga",
    "productLine": "Signature",
    "sgCode": "SG0012",
    "createdAt": "2026-10-13T19:00:00Z",
    "updatedAt": "2026-10-13T20:00:00Z"
  },
  "prepareQuotationTask": {
    "id": "90628337-3801-4276-996b-43d901623d96",
    "projectId": "b0825722-a7d2-4385-9d13-a1a5e6fd7e66",
    "area": "Budgets",
    "type": "PrepareQuotation",
    "status": "InProgress",
    "assignedAt": "2026-10-13T19:30:00Z",
    "startedAt": "2026-10-13T20:00:00Z",
    "completedAt": null
  }
}
```

Una corrección devuelve la misma estructura con el SG y UpdatedAt nuevos, conservando StartedAt exactamente. Un reenvío del mismo valor normalizado conserva también UpdatedAt. No se acepta null para borrar SG ni campos desconocidos. El endpoint responde 400 por request/formato inválido; 404 si no existe Project; 409 si no fue enviado, el workflow es inconsistente/no soportado o el SG está ocupado. No hay endpoints separados para corregir o iniciar.

### Preview técnico de cotización XLSX

`Application/Features/Budgets/PreviewQuotation/IBudgetQuotationParser` define `ParseAsync(Stream, string fileName, CancellationToken)` y devuelve `BudgetQuotationParseResult.Success` o `Invalid` con un código estable. Sus DTO y códigos de warning viven en Application. `CommercialItemSection` es el único enum Main=1/Additional=2, ubicado en Domain y compartido por parser, preview y catálogo persistido. HTTP conserva los nombres Main/Additional.

`PreviewBudgetQuotationUseCase` consulta `IProjectStore.GetByIdAsync` y `IProjectTaskStore.ListByProjectAsync`; sus implementaciones EF usan `AsNoTracking`. Exige un Project existente con SG y exactamente una PrepareQuotation, de área Budgets, InProgress, StartedAt presente y CompletedAt null. Invoca el parser y compara su SG normalizado con el del proyecto mediante igualdad ordinal. No usa `IBudgetsWorkflowStore`, transacciones explícitas, SELECT FOR UPDATE, mutadores del dominio ni SaveChanges. El estado se comprueba al leer; el preview no reserva ni bloquea el proyecto frente a cambios concurrentes posteriores.

`Infrastructure/Quotations/XlsxBudgetQuotationParser` abre el paquete en modo de solo lectura y AutoSave desactivado. `QuotationCellReader` centraliza shared strings, valores cacheados y formatos de ItemNumber; `UnsupportedQuotationException` es exclusivamente una señal interna de validación, convertida a resultado controlado. No se guardan ni recalculan workbooks, no se siguen enlaces ni se ejecutan macros o fórmulas.

**Formato soportado:** extensión `.xlsx` y paquete ZIP/OpenXML de workbook normal, sin VBA. La hoja debe llamarse `COTIZACIÓN`, tolerando espacios exteriores y diferencias de mayúsculas; no se busca una hoja parecida ni se consultan las bases internas. La metadata se toma de C10/C11/C12 (cliente/proyecto/ubicación), I10/I11/I12 (línea cliente/línea producción/elaborado por) y O10 (SG). Los metadatos descriptivos pueden estar vacíos; SG es obligatorio, se recorta, convierte a mayúsculas y exige `SG` + dígitos ASCII, preservando ceros iniciales. Espacios internos, signos o letras adicionales son inválidos. El nombre devuelto elimina componentes de ruta y nunca se usa para abrir un archivo local.

Un encabezado candidato tiene A=ÍTEM/ITEM. Si A de la fila siguiente está vacío se ignora el bloque, incluso si la plantilla vacía utiliza AN/AL abreviados. Para un ítem real se exige además B=UBICACIÓN, C=CONFIGURACIÓN, D=DESCRIPCIÓN (con o sin sus acentos), J=AN (MT), K=AL (MT), L=M2/M², M=CNT, N=VR UNT y O=VR TOTAL. La normalización solo recorta y compara en mayúsculas, sin aproximaciones. N/O se usan exclusivamente para reconocer el encabezado; sus valores económicos no se extraen.

Cada bloque termina antes del siguiente encabezado o marcador ADICIONALES, o al final de los datos de la hoja. `sourceRow` es la fila del número, con índice inicial 1. La fila principal proporciona ubicación B, ancho J, alto K, m² L y cantidad M. Dentro del bloque se exigen una sola vez las etiquetas SISTEMA, CRISTAL, ACABADO ALUMINIO, CERRADURA y OBSERVACIONES en C y se leen sus valores de D, independientemente de su orden. Se permiten valores textuales vacíos, incluidas observaciones. PELÍCULA PROTECCIÓN no se exporta.

ItemNumber es texto visible, nunca identidad ni clave única. Texto e inline/shared strings conservan los ceros; para celdas numéricas se soportan General, texto (`@`) y máscaras enteras de ceros como `00`. Se rechazan otros formatos y números General de magnitud igual o superior a 100.000.000.000 que no puedan representarse con confianza. No se intenta reproducir todo el renderizado de Excel. Se conserva el orden documental y se permiten números repetidos. Antes de ADICIONALES la sección es Main; después es Additional. El título y las plantillas vacías no crean ítems.

Width, Height y SquareMeters son `decimal` positivos; Quantity es `int` positivo sin fracciones, y TotalQuantity suma en `long`. Se leen valores numéricos OpenXML con cultura invariante; textos que parecen números no se convierten por heurísticas regionales. SquareMeters procede del documento y representa el total del ítem; no se calcula Width × Height ni se multiplican unidades. Las fórmulas requieren un valor cacheado utilizable; no se devuelven expresiones ni se recalculan, tampoco si hacen referencia a otro libro.

Las imágenes incrustadas se asocian por la fila superior izquierda de su `OneCellAnchor` o `TwoCellAnchor`, dentro de los límites del bloque, incluido el encabezado. El orden de los drawings o del ZIP no decide la asociación. Una coincidencia devuelve bytes originales como base64 y su contentType; ninguna produce `MissingImage`, y varias producen `AmbiguousImage`, siempre con `image: null`. Logos fuera de los bloques se ignoran. Imágenes vinculadas externamente, agrupadas o sin anchor por celda interpretable hacen rechazar el formato. No se escriben imágenes a disco.

Se rechazan archivos sin ítems, encabezados reales incompletos, etiquetas requeridas ausentes/repetidas, medidas/cantidades inválidas, cachés requeridas inutilizables y estructuras CONTRATADO/MODIFICADO. La recotización/liquidación identificada como SG780 está fuera del contrato por su **estructura**, sin condiciones por nombre o SG en el código. `.xls`, `.csv`, `.xlsm`, archivos cifrados, corruptos o ilegibles no se admiten. Los errores esperables de ZIP/XML/OpenXML se convierten en errores de archivo sin exponer detalles internos; la cancelación se propaga.

**Carga HTTP:** POST `/api/projects/{id}/budget-quotation/preview`, `multipart/form-data`, exactamente una parte de archivo llamada `file`, sin campos adicionales. `QuotationMultipartReader`, compartido por las dos acciones de `BudgetQuotationController`, usa MultipartReader y buffers en memoria limitados; `DisableFormValueModelBindingAttribute` evita que MVC consuma antes el formulario o lo almacene temporalmente en disco. No recibe JSON con paths, URLs ni varios archivos. El parser también admite streams no seekable y deja abierto el stream del llamador.

Los límites son técnicos, configurables bajo `BudgetQuotationPreview`, y se validan como positivos al arrancar:

| Opción | Valor predeterminado | Propósito |
| --- | --- | --- |
| MaxUploadBytes | 30000000 | Cota en memoria del archivo; alineada con el límite de request predeterminado de Kestrel |
| MaxUncompressedBytes | 134217728 | Cota de 128 MiB para el contenido ZIP expandido, comprobada sobre longitudes declaradas y bytes leídos; también limita XML por parte |
| MaxZipEntries | 10000 | Cota de entradas de paquete |

Por ejemplo, se pueden configurar mediante `BudgetQuotationPreview__MaxUploadBytes`, `BudgetQuotationPreview__MaxUncompressedBytes` y `BudgetQuotationPreview__MaxZipEntries`. No son reglas comerciales de tamaño o cantidad de ítems. No se cambia el límite del servidor/proxy; este incluye el overhead multipart y puede rechazar antes la petición, normalmente con 413. Las cotas detectadas por el controller/parser devuelven 400. Cambiar estas opciones requiere evaluar la memoria disponible: los bytes comprimidos, XML, imágenes y base64 ocupan memoria simultáneamente. Nunca se extraen entradas ZIP al filesystem; se rechazan nombres duplicados y rutas inseguras.

**Respuesta 200**, ejemplo anónimo con una imagen ausente:

```json
{
  "projectId": "b0825722-a7d2-4385-9d13-a1a5e6fd7e66",
  "projectSgCode": "SG0012",
  "document": {
    "fileName": "cotizacion.xlsx",
    "clientName": "Cliente de ejemplo",
    "projectName": "Proyecto de ejemplo",
    "location": "Ubicación de ejemplo",
    "clientLine": "Línea cliente",
    "productionLine": "Línea producción",
    "preparedBy": "Equipo de ejemplo",
    "sgCode": "SG0012"
  },
  "itemCount": 1,
  "totalQuantity": 2,
  "items": [{
    "itemNumber": "01",
    "section": "Main",
    "location": "Zona de ejemplo",
    "system": "Sistema de ejemplo",
    "glass": "Cristal de ejemplo",
    "aluminumFinish": "Acabado de ejemplo",
    "lockType": "Cerradura de ejemplo",
    "observations": "",
    "width": 1,
    "height": 2.8,
    "squareMeters": 5.6,
    "quantity": 2,
    "image": null,
    "sourceRow": 15
  }],
  "warnings": [{
    "code": "MissingImage",
    "itemNumber": "01",
    "sourceRow": 15,
    "message": "El ítem no tiene una imagen asociable."
  }]
}
```

Cuando existe imagen, su objeto contiene `contentType` y `base64`. Section se serializa como Main/Additional y los warnings como MissingImage/AmbiguousImage. ItemCount cuenta bloques reales; TotalQuantity suma cantidades agrupadas. No hay unitPrice, totalPrice, projectTotal, costos, utilidad ni película de protección. Las advertencias solo cubren imágenes; no sustituyen errores estructurales.

El controller valida la carga, luego el caso de uso comprueba proyecto/workflow, interpreta el archivo y compara SG. Devuelve 400 por carga o documento inválido, 404 por proyecto inexistente, 409 por workflow no habilitado o SG válido distinto. Los errores del parser incluyen `code`: UnreadableWorkbook, UnsupportedWorkbookFormat, InvalidDocumentSgCode o TechnicalLimitExceeded, dentro de ProblemDetails. Un error inesperado del servidor sigue siendo 500 genérico.

Ejemplo con un proyecto existente ya habilitado y un XLSX cuyo SG coincide:

```powershell
curl.exe -F "file=@<RUTA_XLSX>" "https://localhost:7265/api/projects/<PROJECT_ID>/budget-quotation/preview"
```

El preview no guarda archivo, metadata, catálogo ni versión, ni cambia tablas. La persistencia ocurre únicamente mediante confirmación. El smoke local verificó preview 200 con PostgreSQL sobre proyectos sintéticos habilitados antes de confirmar.

### Validación local de corpus sin versionarlo

Los tests sintéticos se ejecutan con `dotnet test`; para solo el parser, `dotnet test --filter FullyQualifiedName~XlsxBudgetQuotationParserTests`. El corpus real debe permanecer fuera del repositorio y abrirse en modo lectura. No copiarlo, extraerlo, modificarlo ni registrar nombres, SG, observaciones, imágenes o previews completos.

Para repetir el smoke agregado, crear un ejecutor local descartable en `artifacts` (ignorado por Git), con referencia a Infrastructure:

```shell
dotnet new console -f net10.0 -o artifacts/CorpusPreview
dotnet add artifacts/CorpusPreview/CorpusPreview.csproj reference src/Infrastructure/Infrastructure.csproj
```

Reemplazar su Program.cs por el siguiente código y ejecutar `dotnet run --project artifacts/CorpusPreview -- "<ZIP_FUERA_DEL_REPOSITORIO>"`. El nombre sintético evita devolver nombres reales en el resultado usado por el smoke:

```csharp
using System.IO.Compression;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SnG.Control.Application.Features.Budgets.PreviewQuotation;
using SnG.Control.Domain.Enums;
using SnG.Control.Infrastructure.Quotations;

var parser = new XlsxBudgetQuotationParser(Options.Create(new BudgetQuotationParserOptions()));
using var zip = ZipFile.OpenRead(args[0]);
var stats = new Dictionary<string, long>();
void Count(string key, long amount = 1) => stats[key] = stats.GetValueOrDefault(key) + amount;
foreach (var entry in zip.Entries.Where(e => e.Name.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)))
{
    using var input = entry.Open();
    Count("processed");
    if (await parser.ParseAsync(input, "quotation.xlsx", CancellationToken.None) is BudgetQuotationParseResult.Success result)
    {
        Count("valid"); Count("items", result.Items.Count);
        Count("Main", result.Items.Count(i => i.Section == CommercialItemSection.Main));
        Count("Additional", result.Items.Count(i => i.Section == CommercialItemSection.Additional));
        Count("images", result.Items.Count(i => i.Image is not null));
        foreach (var warning in result.Warnings) Count(warning.Code.ToString());
    }
    else Count("rejected");
}
Console.WriteLine(JsonSerializer.Serialize(stats));
```

Retirar el ejecutor al terminar; no agregarlo ni agregar el corpus a Git. El smoke local procesó **66 XLSX: 62 válidos y 4 rechazados**, con **1.038 ítems: 1.037 Main, 1 Additional, 1.038 imágenes, 0 MissingImage y 0 AmbiguousImage**. Los rechazos fueron un SG con espacios internos, un documento CONTRATADO/MODIFICADO y dos contenedores OLE ilegibles como XLSX. El archivo con SG inválido tiene apariencia estándar, pero no se sanea el identificador para aceptarlo. No se añadieron excepciones por nombre ni heurísticas para forzar aprobaciones.

### Confirmación de cotización

POST `/api/projects/{id}/budget-quotation/confirm` recibe exactamente dos partes multipart, en cualquier orden: `file` (XLSX) y `confirmation` (JSON UTF-8 con Content-Type application/json; charset ausente o utf-8). Se acepta que el cliente envíe el JSON como Blob con filename; ese nombre se ignora. Se rechazan duplicados de parte, partes adicionales, archivos ausentes/vacíos y JSON inválido. No acepta URLs ni paths como alternativa al archivo.

`QuotationMultipartReader` sirve a preview y confirmación, con `DisableFormValueModelBindingAttribute` para evitar buffering de MVC a disco. Aplica el límite técnico existente MaxUploadBytes a la suma de bytes de las partes; el límite del servidor/proxy también incluye overhead multipart. Propaga cancelación. El JSON usa las opciones estrictas de la API: miembros desconocidos producen 400, Quantity debe ser entera y Section acepta únicamente Main/Additional como nombres, sin números ni combinaciones.

Ejemplo de `confirmation` para un documento de un ítem:

```json
{
  "items": [{
    "section": "Main",
    "sourceRow": 15,
    "itemNumber": "01",
    "location": "V1",
    "system": "Sistema de ejemplo",
    "glass": "Cristal corregido",
    "aluminumFinish": "Acabado de ejemplo",
    "lockType": "Cerradura de ejemplo",
    "observations": "",
    "width": 2.54,
    "height": 2.8,
    "squareMeters": 5.6,
    "quantity": 8
  }]
}
```

El cliente envía solo valores confirmados. No se aceptan extracted, imagen, metadatos, SG ni precios. Section y SourceRow identifican el ítem; no son correcciones permitidas. El matching es **(Section, SourceRow)** y se exige cobertura exacta: misma cantidad, filas únicas, sin claves extra ni ausentes. ItemNumber nunca participa como identidad. El orden del payload puede variar; el catálogo y la respuesta siguen SourceRow. Los valores textuales opcionales pueden estar ausentes/null/vacíos y se normalizan a vacío; ItemNumber no puede quedar vacío. Las medidas y cantidades deben ser positivas y no se recalculan.

`ConfirmBudgetQuotationUseCase` reutiliza `PreviewBudgetQuotationUseCase`: vuelve a ejecutar el parser vigente sobre los bytes recibidos, con las mismas reglas de workflow y SG. `BudgetQuotationWorkflow.ReadyTask` concentra la regla de exactamente una PrepareQuotation en Budgets/InProgress, SG presente, StartedAt no null y CompletedAt null. No se confía en el preview anterior ni en una extracción remitida por el frontend. Como el preview es stateless, no existe un token/hash de sesión: el XLSX que llega en la confirmación es el que se reinterpreta y conserva.

Tras validar la cobertura y valores, el caso de uso construye BudgetQuotationVersion (versión 1), CommercialCatalog, ítems oficiales y snapshots inmutables de extracción, además de los archivos que deben guardarse. `IBudgetQuotationConfirmationStore` define esa única frontera de persistencia. Application no conoce DbContext, transacciones concretas ni RootPath. No se usa un repositorio genérico, UnitOfWork ni segundo parser.

**Transacción y archivos:** `EfBudgetQuotationConfirmationStore` abre una transacción después del parsing. Relee Project con `FOR SHARE` y AsNoTracking, y consulta las tareas sin locks ni tracking. El lock de Project impide una corrección concurrente de SG hasta terminar el commit; no bloquea entre sí dos confirmaciones. Revalida el workflow y el SG documental contra el valor actual. Ni Project ni ProjectTask se modifican.

Comprueba si existe versión 1 para responder pronto 409; esa consulta no sustituye la garantía única. Agrega las cuatro clases de registros y realiza un SaveChanges dentro de la transacción, todavía sin commit. Después escribe el XLSX y todas las imágenes. Solo cuando todas las escrituras terminan realiza commit. Así no se confirma una transacción antes de guardar sus archivos.

Si falla SaveChanges, una escritura o un commit que pueda revertirse, hace rollback y elimina exclusivamente las claves cuyo SaveAsync terminó correctamente en esa operación. Usa CancellationToken.None para compensar aunque el request haya sido cancelado y continúa intentando las otras eliminaciones si una falla. Desacopla únicamente sus entidades, sin limpiar entradas ajenas del ChangeTracker. Un error de compensación se propaga como error de servidor y requiere inspección; no se informa éxito.

PostgreSQL y filesystem no comparten una transacción distribuida. Un crash entre escritura y commit puede dejar archivos huérfanos. Si COMMIT pierde su respuesta y el rollback también falla, el resultado puede ser incierto: se conservan los archivos para no borrar los que quizá respaldan registros ya confirmados, y se responde error. No hay cleaner en background ni recuperación automática. La revisión administrativa debe comparar claves/identificadores de esa operación antes de eliminar archivos; nunca realizar una limpieza global. Los backups/restores de DB y archivos también deben coordinarse.

La concurrencia se resuelve mediante **UX_BudgetQuotationVersions_ProjectId_VersionNumber**. Solo DbUpdateException con PostgresException, SQLSTATE `23505` (`UniqueViolation`) y ese nombre exacto se convierte en AlreadyConfirmed/409, después de rollback/compensación. Otras restricciones o errores no se confunden con una segunda confirmación. Al ejecutar SaveChanges antes de escribir archivos, la operación perdedora no llega a crear sus archivos. Una confirmación posterior no reemplaza la existente ni crea versión 2.

**Respuesta:** 201 con ConfirmedBudgetQuotationResponse, sin Location ni endpoint GET artificial:

- `quotationVersionId`, `versionNumber`, `commercialCatalogId`, `projectId`, `confirmedAt`.
- `document`: fileName, clientName, projectName, location, clientLine, productionLine, preparedBy y sgCode del archivo reparseado.
- `itemCount` y `totalQuantity`, calculado con cantidades **confirmadas** en long.
- `items`: id, section, sourceRow, `extracted`, `confirmed` e `image`.
- extracted y confirmed contienen cada uno los once campos técnicos del request, sin section/sourceRow repetidos.
- image contiene únicamente `status` (Stored/Missing/Ambiguous) y `contentType`, null en los dos últimos estados. No se devuelve base64 ni claves de almacenamiento.
- `prepareQuotationTask`: ProjectTaskResponse completo. Continúa InProgress, con el mismo StartedAt y CompletedAt null.

Se conserva también Project.UpdatedAt, SgCode, Name, City y ProductLine. No se crea una tarea Comercial ni se completa Presupuestos. No se exponen precios, rutas físicas, RootPath o StorageKey. Los códigos son 400 por carga/archivo/confirmación inválida, 404 por Project inexistente, 409 por workflow no habilitado, SG distinto o versión inicial existente, y 500 genérico por errores inesperados de DB/filesystem/configuración.

**Prueba manual:** con la API iniciada, un proyecto de prueba enviado a Presupuestos y con SG asignado, ejecutar preview, preparar confirmation.json con todos sus ítems y cambiar únicamente los campos editables. Enviar el mismo XLSX junto al JSON:

```powershell
curl.exe -F "file=@<XLSX_SINTETICO>" -F "confirmation=@<CONFIRMATION_JSON>;type=application/json" "https://localhost:7265/api/projects/<PROJECT_ID>/budget-quotation/confirm"
```

Exigir 201 y comparar extracted/confirmed; una segunda petición debe producir 409. Verificar en PostgreSQL una versión 1, un catálogo y un snapshot por ítem, con claves relativas. En la raíz configurada comparar bytes del XLSX y de imágenes con los enviados. Comprobar que Project y PrepareQuotation conservan todos sus valores. No confirmar masivamente el corpus real.

Los tests de confirmación cubren cada campo editable, cambios múltiples, orden invertido, Main/Additional y números repetidos, validaciones de cobertura, JSON estricto, imágenes Stored/Missing/Ambiguous, extensiones seguras, invariantes y metadata EF. Los tests de almacenamiento utilizan directorios temporales propios y comprueban bytes, traversal, no overwrite, cancelación y retirada de temporales. Los tests de compensación utilizan el ChangeTracker y LocalFileStorage reales, con SaveChanges/transacción controlados para provocar fallos en SaveChanges, XLSX, imagen intermedia y commit; cubren además cancelación, constraint específico y resultado incierto de commit. No necesitan PostgreSQL. Infrastructure expone sus miembros internos únicamente al assembly de tests para verificar esa fase de persistencia sin crear un framework de transacciones.

El smoke con PostgreSQL y almacenamiento local verificó preview 200 y confirmación 201 con correcciones de Width, Glass y Quantity. Cada escenario guardó **1 versión, 1 catálogo, 3 ítems y 3 extracciones**, con estados Stored/Missing/Ambiguous y **2 archivos** (XLSX y una imagen), ambos byte por byte iguales a los originales. Dos confirmaciones simultáneas produjeron **1×201 y 1×409**, una sola versión/catálogo y únicamente archivos de la ganadora. Project y tarea permanecieron idénticos. La limpieza eliminó exclusivamente las dos pruebas, restauró el snapshot inicial de todas las tablas y comprobó ausencia de sus carpetas. La migración aplicada y la configuración local se conservan.

### Envío de Presupuestos a Comercial

POST `/api/projects/{id}/send-to-commercial` no recibe body; contenido inesperado produce 400. `SendBudgetQuotationToCommercialUseCase` consulta el estado mediante `IBudgetQuotationCommercialHandoffStore`. Requiere exactamente una PrepareQuotation en Budgets/InProgress, StartedAt presente y CompletedAt null; versión 1, catálogo con al menos un ítem y una extracción por ítem; original disponible; ausencia de ReviewQuotation y de ConfirmedFileStorageKey. Project inexistente produce 404; estado inválido, envío previo, original ausente o documento incompatible producen 409.

Project.SgCode debe coincidir ordinalmente con BudgetQuotationVersion.DocumentSgCode. Un cambio posterior a la confirmación produce 409 con `code: SgChangedAfterConfirmation`, también al revalidar dentro de la transacción. No se corrige O10 ni se reconcilia el SG. El endpoint no modifica Project, catálogo, ítems ni extracciones.

`IFileStorage.ReadAsync` carga el original mediante su clave relativa. `XlsxBudgetQuotationWorkbookGenerator`, implementación de `IBudgetQuotationWorkbookGenerator`, valida el original con el parser existente de solo lectura y compara los ítems con sus extracciones persistidas. La escritura está separada del parser y reutiliza únicamente la lectura estructural de filas. Abre una copia en memoria con OpenXML; no reconstruye el workbook ni reinserta imágenes. El matching exacto de Section+SourceRow conserva Main/Additional y permite ItemNumber repetidos; SourceRow corresponde a la primera fila del bloque, inmediatamente después del encabezado validado.

| Valor confirmado | Celda de destino |
| --- | --- |
| ItemNumber | A en SourceRow |
| Location | B en SourceRow |
| Width / Height / SquareMeters / Quantity | J / K / L / M en SourceRow |
| System | D de la fila cuya C contiene SISTEMA |
| Glass | D de la fila cuya C contiene CRISTAL |
| AluminumFinish | D de la fila cuya C contiene ACABADO ALUMINIO |
| LockType | D de la fila cuya C contiene CERRADURA |
| Observations | D de la fila cuya C contiene OBSERVACIONES |

Las etiquetas se localizan dentro del bloque, sin asumir offsets. No se modifican N/O, PELÍCULA PROTECCIÓN, metadata ni SG documental. Las celdas existentes mantienen StyleIndex y formatos; los textos sin fórmula se escriben como InlineString local, sin cambiar entradas compartidas de SharedStringTable. Las fórmulas se conservan; se actualiza únicamente su cached value con el valor confirmado, numérico en cultura invariante o textual según el campo. El m² oficial de DB no se recalcula. `CalculationProperties` usa Auto, FullCalculationOnLoad=true y ForceFullCalculation=true: Excel puede recalcular las fórmulas al abrir.

El generador cierra y reabre el resultado en modo lectura para validar valores, fórmulas y estilos de destino. Compara byte por byte todas las entradas ZIP salvo workbook.xml y la hoja editada: incluye ImageParts, drawings, anchors, relaciones, shared strings, estilos y otras hojas. Conserva hojas/orden, nombres definidos, merges, dimensiones y metadata. No exige igualdad binaria del ZIP completo. Una discrepancia impide almacenar el resultado.

**Persistencia:** `EfBudgetQuotationCommercialHandoffStore` abre la transacción después de generar y validar el archivo. Bloquea Project, tareas del proyecto y versión 1 con `FOR UPDATE`, en ese orden, y vuelve a comprobar estado, SG y datos usados para generar. Project se lee sin tracking. Completa PrepareQuotation, crea ReviewQuotation de Commercial e invoca Start inmediatamente; asocia la clave mediante `BudgetQuotationVersion.SetConfirmedFileStorageKey`, que admite una única asignación relativa segura. Guarda las tres entidades con un SaveChanges, escribe el archivo y confirma la transacción. Los timestamps modificados se ajustan a microsegundos de PostgreSQL; AssignedAt y StartedAt de Presupuestos permanecen intactos.

Clave operativa: `projects/{projectId}/budget-quotations/{quotationVersionId}/confirmed.xlsx`. La original y las imágenes no reciben escrituras ni eliminaciones en este flujo. `ConfirmedFileStorageKey` es nullable antes del envío, con índice único que admite múltiples null. El índice parcial `UX_ProjectTasks_ProjectId_ReviewQuotation` impide dos tareas ReviewQuotation por proyecto. Solo SQLSTATE 23505 con ese nombre exacto se convierte en AlreadySent/409; otras violaciones se propagan como errores inesperados. Dos envíos simultáneos producen un 200 y un 409, con una única tarea y archivo operativos.

Si falla SaveChanges no se escribe archivo; si falla el guardado se revierte DB; si falla el commit con rollback confirmado se elimina exclusivamente el confirmed.xlsx creado por esa operación. Se restauran y desacoplan las entidades afectadas, preservando otras entradas del contexto. La compensación usa CancellationToken.None. Un fallo de limpieza se propaga como error técnico; un commit incierto conserva el archivo si tampoco puede confirmarse el rollback. Se mantienen las limitaciones DB/filesystem descritas para la confirmación: no hay transacción distribuida ni recuperación automática tras un crash.

**Respuesta 200:** `projectId`, `budgetTask`, `commercialTask`, `quotationVersionId`, `commercialCatalogId` y `commercialFileAvailable: true`. Las tareas usan ProjectTaskResponse: Presupuestos Completed con CompletedAt UTC; Comercial ReviewQuotation/InProgress con StartedAt UTC y CompletedAt null. StartedAt de Comercial es la base futura del Leak Time; no existen SLA ni alertas. La respuesta no expone StorageKey ni rutas físicas.

### Descarga del Excel operativo

GET `/api/projects/{id}/budget-quotation/commercial-file` utiliza `DownloadCommercialQuotationUseCase`. Project inexistente produce 404; versión 1, clave operativa o ReviewQuotation ausentes producen 409. Una clave existente en DB cuyo archivo falta produce 500 genérico y diagnóstico técnico en logs, sin revelar rutas ni regenerar el documento.

Responde 200 con `Content-Type: application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`. `File(bytes, contentType, OriginalFileName)` de ASP.NET serializa Content-Disposition de forma segura; filename/filename* representan el nombre original, sin sufijos. Los bytes son exactamente los almacenados. La consulta no guarda DB, no escribe archivos, no cambia fechas/tareas y no incrementa contadores. Puede repetirse sin efectos secundarios.

**Prueba manual:** tras confirmar un XLSX sintético de dos ítems con imágenes, fórmula M2 y precios ficticios, cambiar Width, Glass y Quantity durante esa confirmación. Enviar sin cuerpo y descargar:

```powershell
$projectUrl = "https://localhost:7265/api/projects/<PROJECT_ID>"
Invoke-RestMethod -Method Post -Uri "$projectUrl/send-to-commercial"
curl.exe -D "<HEADERS_TEMPORALES>" -o "<XLSX_DESCARGADO>" "$projectUrl/budget-quotation/commercial-file"
```

Verificar estado de ambas tareas, clave relativa, hash del original antes/después, valores corregidos, fórmula/cache, ImageParts y N/O. Repetir GET y comparar bytes, headers y snapshot de DB, incluidas las fechas. Para concurrencia usar otro proyecto sintético confirmado y dos POST simultáneos: exigir 1×200 y 1×409, una PrepareQuotation Completed, una ReviewQuotation InProgress y un solo confirmed.xlsx. Retirar solo los registros y carpetas creados por la prueba.

Los tests cubren Complete y la clave operativa; precondiciones y SG histórico; cada campo del generador, cambios múltiples, filas/secciones, estilos, fórmulas/cache, shared strings, imágenes/anchors, estructura y celdas protegidas; compensación con fallos de SaveChanges, archivo, commit y cleanup; códigos HTTP, nombre Unicode, bytes exactos y descargas sin cambios. El smoke local con PostgreSQL y filesystem verificó ambos escenarios descritos, incluidas las descargas repetidas y la igualdad SHA-256 del original. La limpieza restauró el snapshot inicial de las siete tablas de negocio y retiró exclusivamente las carpetas sintéticas; las seis migraciones quedaron aplicadas.

### Errores

- `[ApiController]` responde 400 para errores de binding, JSON, Guid, enum o campos de texto requeridos.
- Las invariantes permanecen en Domain. `ApiExceptionHandler` convierte `ArgumentException` y sus derivadas de validación en 400 con un ProblemDetails genérico.
- Los Controllers convierten los resultados inexistentes en 404 y AlreadySent en 409 con ProblemDetails. Clientes y proyectos utilizan `CreatedAtAction` para los 201, cuya cabecera Location apunta al GET correspondiente; el envío a Presupuestos responde 201 con la tarea y sin Location; el envío a Comercial responde 200 con ambas tareas.
- El middleware de excepciones está activo también en Development. Los errores inesperados responden 500 con ProblemDetails genérico, sin mensajes de EF/Npgsql, SQL, stack traces ni detalles de configuración. ApiExceptionHandler registra explícitamente estos errores con ILogger, pues .NET 10 puede suprimir el diagnóstico del middleware para excepciones atendidas.
- Cada store traduce únicamente su conflicto único esperado: los índices PrepareQuotation y ReviewQuotation a AlreadySent/409 y el índice SG a SgCodeAlreadyInUse/409 y el índice de versión de cotización a AlreadyConfirmed/409. Otros errores de persistencia no se confunden con duplicados esperados. Las FK siguen protegiendo la integridad si un cliente o proyecto desaparece externamente entre la comprobación y el guardado; la API no ofrece eliminación.

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

# Usar códigos libres; el sistema no calcula el siguiente SG.
$sgUrl = "$baseUrl/api/projects/$($project.id)/sg-code"
$assignment = Invoke-RestMethod -Method Put -Uri $sgUrl -ContentType 'application/json' -Body '{"sgCode":" sg0012 "}'
$originalStart = $assignment.prepareQuotationTask.startedAt
$correction = Invoke-RestMethod -Method Put -Uri $sgUrl -ContentType 'application/json' -Body '{"sgCode":"sg0042"}'
$repeat = Invoke-RestMethod -Method Put -Uri $sgUrl -ContentType 'application/json' -Body '{"sgCode":" SG0042 "}'
$originalStart -eq $correction.prepareQuotationTask.startedAt # True
$originalStart -eq $repeat.prepareQuotationTask.startedAt # True
$correction.project.updatedAt -eq $repeat.project.updatedAt # True
Invoke-RestMethod "$baseUrl/api/project-tasks?area=Budgets&status=InProgress"
```

Los POST de creación y el primer envío generan registros reales y responden 201; los GET del ejemplo responden 200. El segundo envío responde 409 sin escribir otra tarea. Un Guid válido inexistente responde 404 al consultar entidades, enviar o consultar su historial. Cambiar `productLine`, `area` o `status` por un número o nombre desconocido, omitir área/estado, dejar `name`/`city` en blanco o enviar `Guid.Empty` como `clientId` produce 400. Un proyecto con cliente inexistente produce 404 sin insertar el proyecto.

No hay endpoint de borrado. Para validaciones controladas en una base compartida, identificar los registros de prueba con una marca única y retirar exclusivamente esos registros mediante una operación administrativa segura, primero ProjectTasks, después Projects y finalmente Clients. No modificar datos ajenos a la prueba.

### Validación de concurrencia real

La suite sin conexión no sustituye esta comprobación. Crear un cliente y un proyecto nuevos con una marca única, y lanzar dos POST simultáneos al envío de ese proyecto con clientes HTTP reales. Esperar todas las respuestas y comprobar exactamente un 201 y un 409, sin 500. Consultar PostgreSQL con los identificadores de la prueba y verificar una sola fila ProjectTasks con ProjectId del proyecto temporal y Type=1. Si se lanzan más solicitudes, una sola debe crear y todas las demás deben responder 409.

El smoke local por HTTPS contra PostgreSQL cumplió ese resultado: **2 requests concurrentes, 1 respuesta 201, 1 respuesta 409 y 1 fila PrepareQuotation**. También se verificó directamente que el store devuelve false para otro intento, deja la entidad Detached y permite un SaveChanges posterior que escribe cero filas. La limpieza retiró exclusivamente una tarea, un proyecto y un cliente temporales, en ese orden, y comprobó cero filas restantes de la prueba.

Para SG, crear dos proyectos temporales enviados a Presupuestos y elegir un código libre. Lanzar dos PUT simultáneos con ese mismo código. Exigir **1×200 y 1×409**, y consultar la base: solo un proyecto debe tenerlo; el rechazado debe conservar SgCode=null, tarea Pending y StartedAt=null. No basta con comprobar los códigos HTTP.

El smoke de SG validó primera asignación `"  sg0012  "` → SG0012/InProgress, corrección a SG0042 con StartedAt idéntico, y repetición normalizada con respuesta idéntica. La concurrencia entre dos proyectos con SG99001 produjo 1×200, 1×409 y un solo propietario, con rollback completo del rechazado. Dos primeras asignaciones simultáneas al mismo proyecto produjeron 2×200 y el mismo StartedAt. Se comprobó el detach de ambas entidades y cero escrituras posteriores tras un conflicto, y el rechazo del CHECK a escrituras externas inválidas. La ejecución completa retiró sus cuatro tareas, cuatro proyectos y un cliente; las ejecuciones de diagnóstico también limpiaron sus registros. No quedaron datos temporales.

## Reglas permanentes para continuar

- Implementar solo funcionalidad solicitada y reglas confirmadas. Reportar decisiones pendientes sin codificarlas silenciosamente.
- Mantener invariantes en Domain, coordinación de casos de uso en Application, persistencia e integraciones en Infrastructure y Controllers delgados en Api.
- Respetar referencias y namespaces. No introducir abstracciones, servicios, patrones ni librerías sin necesidad actual.
- Eliminar código muerto, dependencias sin uso, duplicación y ejemplos de template. No conservar código comentado ni versiones históricas; Git conserva la historia.
- Revisar y mantener ambos README como documentación viva: el funcional explica negocio y alcance, el técnico explica implementación y operación. No son changelogs.
- Validar herramientas, restore, build, tests y migraciones; corregir warnings atribuibles al código sin ocultarlos mediante configuración. Reportar los que legítimamente permanezcan y cualquier supuesto o impedimento.
- No incluir secretos reales en archivos versionados. No aplicar migraciones automáticamente al arrancar.

Siguen pendientes de alcance posterior la edición de otros campos, eliminación, generación automática de SG, auditoría/historial de cambios SG, nuevas versiones y edición posterior de cotizaciones, procesamiento económico del Excel de Comercial, finalización de ReviewQuotation y devolución de tareas y demás módulos funcionales. El preview XLSX es stateless; la confirmación crea el catálogo base y conserva el documento inicial, sin implementar versión 2 ni edición posterior. Los únicos filtros disponibles son Area+Status para tareas; no hay paginación, búsqueda ni orden configurable. Las reglas de reenvío/retrabajo requieren definición posterior; el flujo inicial vigente admite una sola PrepareQuotation y una sola ReviewQuotation por proyecto.
