# SnG Control Backend — Guía técnica

## Estado y stack

La solución `SnG.Control.sln` utiliza .NET 10 (`net10.0`), C# y ASP.NET Core Web API con Controllers tradicionales, dentro de una estructura de Clean Architecture. Las reglas funcionales están descritas en [README.md](README.md).

El código de negocio actual se limita a `Client`, `Project` y `ProductLine` en Domain. Application e Infrastructure contienen únicamente sus archivos de proyecto y referencias. La API configura Controllers y OpenAPI, sin Controllers de negocio ni Minimal APIs de negocio.

No hay persistencia, conexión a PostgreSQL, EF Core, Npgsql, migraciones, repositorios, autenticación ni autorización. Tampoco se utiliza Docker. PostgreSQL es una tecnología prevista para otra fase, no un requisito para ejecutar esta solución.

### Requisitos y dependencias

- SDK de .NET 10. No hay `global.json` que fije una versión concreta del SDK.
- Acceso a los paquetes NuGet durante la primera restauración, o disponibilidad en la caché local.
- Editor o IDE compatible con .NET 10, opcional; todos los comandos pueden ejecutarse desde una terminal.
- Para ejecutar el perfil HTTPS, un certificado de desarrollo confiable.

Las versiones declaradas actualmente son:

| Proyecto | Paquete | Versión | Uso |
| --- | --- | --- | --- |
| Api | Microsoft.AspNetCore.OpenApi | 10.0.12 | Documento OpenAPI en desarrollo |
| Tests | Microsoft.NET.Test.Sdk | 17.14.1 | Ejecución de tests |
| Tests | xunit | 2.9.3 | Tests unitarios |
| Tests | xunit.runner.visualstudio | 3.1.4 | Descubrimiento y ejecución de xUnit |
| Tests | coverlet.collector | 6.0.4 | Recolección opcional de cobertura |

Domain, Application e Infrastructure no declaran paquetes externos.

## Estructura y responsabilidades

```text
SnG.Control.sln
src/
  SnG.Control.Api/
    Program.cs
    appsettings.json
    Properties/launchSettings.json
  SnG.Control.Application/
  SnG.Control.Domain/
    Entities/Client.cs
    Entities/Project.cs
    Enums/ProductLine.cs
  SnG.Control.Infrastructure/
tests/
  SnG.Control.Tests/
    Domain/ClientTests.cs
    Domain/ProjectTests.cs
```

| Proyecto | Responsabilidad y estado actual |
| --- | --- |
| Domain | Entidades, identidad e invariantes de creación; independiente del transporte y del almacenamiento. |
| Application | Capa destinada a coordinar casos de uso. No tiene implementaciones todavía. |
| Infrastructure | Capa destinada a adaptadores e integraciones externas. No tiene implementaciones todavía. |
| Api | Punto de entrada y composición HTTP; registra Controllers, OpenAPI y redirección HTTPS. |
| Tests | Tests unitarios del dominio con xUnit. Referencia Domain y Application; no hay lógica de Application para probar todavía. |

Las referencias de proyecto existentes son:

```text
Domain         → ninguna otra capa
Application    → Domain
Infrastructure → Application, Domain
Api            → Application, Infrastructure
Tests          → Domain, Application
```

Las referencias mantienen la estructura de capas aunque Application e Infrastructure todavía no tengan código funcional. No se añaden clases vacías para ocuparlas.

## Restaurar, compilar y probar

Ejecutar desde la raíz del repositorio:

```shell
dotnet restore
dotnet build
dotnet test
```

Para recolectar cobertura con el collector instalado:

```shell
dotnet test --collect:"XPlat Code Coverage"
```

Los resultados de cobertura se generan bajo `TestResults` en el proyecto de tests; los artefactos de compilación y tests están excluidos por `.gitignore`.

Los tests comprueban creación, normalización, rechazo de argumentos inválidos, identidades independientes, fechas UTC, asociación por `ClientId`, las cuatro líneas admitidas, rechazo de valores de enum no definidos, código SG inicialmente nulo y aceptación de proyectos con nombres repetidos. No requieren base de datos ni servidor HTTP.

## Ejecutar la API

Preparar el certificado local si es necesario:

```shell
dotnet dev-certs https --trust
```

Ejecutar el perfil HTTPS:

```shell
dotnet run --project src/SnG.Control.Api --launch-profile https
```

Este perfil escucha en `https://localhost:7265` y `http://localhost:5051`, con ambiente `Development`. El middleware redirige las peticiones HTTP hacia HTTPS. Detener el proceso con `Ctrl+C`.

También existe un perfil `http` en el puerto 5051:

```shell
dotnet run --project src/SnG.Control.Api --launch-profile http
```

Ese perfil no configura un puerto HTTPS; si no hay otro puerto HTTPS configurado, el middleware puede registrar una advertencia al intentar redirigir. El perfil `https` es el recomendado para la ejecución local normal.

En `Development`, `GET /openapi/v1.json` entrega el documento OpenAPI. No hay interfaz Swagger UI instalada ni operaciones de negocio en el documento. La raíz `/` devuelve 404 porque no tiene una ruta registrada. Fuera de `Development` no se publica el endpoint OpenAPI.

### Configuración

- `Program.cs`: registra `AddControllers` y `AddOpenApi`; configura `MapOpenApi` solo en desarrollo, `UseHttpsRedirection` y `MapControllers`.
- `appsettings.json`: niveles de logging (`Information` general y `Warning` para `Microsoft.AspNetCore`) y `AllowedHosts` igual a `*`.
- `Properties/launchSettings.json`: puertos y perfiles locales. Ambos establecen `ASPNETCORE_ENVIRONMENT=Development`.
- No hay variables de entorno propias del negocio, cadenas de conexión ni secretos requeridos. Se utiliza la configuración estándar de ASP.NET Core.

## Diseño y convenciones de dominio

Los namespaces siguen el nombre de cada proyecto y sus carpetas: `SnG.Control.Domain.Entities`, `SnG.Control.Domain.Enums` y `SnG.Control.Tests.Domain`. Se utilizan namespaces de ámbito de archivo, nullable reference types e implicit usings habilitados en los proyectos existentes.

Las entidades tienen constructores públicos con los datos necesarios para una creación válida:

```csharp
var client = new Client("Steel & Glass");
var project = new Project(client.Id, "Torre Norte", "Bogotá", ProductLine.Classic);
```

El ejemplo requiere `using SnG.Control.Domain.Entities;` y `using SnG.Control.Domain.Enums;`. La relación se expresa mediante `Project.ClientId`, sin colecciones de navegación ni carga de entidades relacionadas.

Las propiedades son de solo lectura. No hay setters públicos, métodos de edición, constructor vacío para un ORM ni operación de asignación de SG. Los identificadores se generan con `Guid.NewGuid()`. Las fechas son `DateTime` con `Kind=Utc`; se toma una sola lectura de `DateTime.UtcNow` por entidad y se asigna a ambas fechas.

La validación utiliza excepciones estándar:

| Argumento inválido | Excepción |
| --- | --- |
| Nombre o ciudad nulos | `ArgumentNullException` |
| Nombre o ciudad vacíos o solo whitespace | `ArgumentException` |
| `ClientId` igual a `Guid.Empty` | `ArgumentException` |
| `ProductLine` no definido en el enum | `ArgumentOutOfRangeException` |

`ArgumentException.ThrowIfNullOrWhiteSpace` valida texto antes de aplicar `Trim()`. `Enum.IsDefined` impide aceptar valores numéricos convertidos a un enum no válido. Los miembros de `ProductLine` tienen los valores implícitos de C# del 0 al 3 en el orden Classic, Essential, Bioconfort y Signature; no existe aún un contrato de serialización o almacenamiento para ellos.

No hay `BaseEntity`, framework de excepciones ni infraestructura genérica de auditoría. La repetición de unas pocas propiedades permite mantener explícito el modelo actual.

## Continuar el proyecto

Las siguientes reglas son permanentes para el repositorio:

- Implementar únicamente funcionalidad solicitada y reglas de negocio confirmadas. Reportar decisiones pendientes sin codificarlas silenciosamente.
- Mantener las invariantes en Domain y la coordinación de casos de uso en Application cuando estos se definan. Infrastructure alojará las integraciones que se soliciten.
- Mantener los futuros Controllers delgados: recibir HTTP, delegar y producir la respuesta. No colocar lógica de negocio en Controllers ni sustituirlos por Minimal APIs.
- Respetar la dirección de dependencias y los namespaces. No introducir bibliotecas, abstracciones, interfaces o servicios sin una necesidad actual.
- Eliminar código muerto, ejemplos del template, dependencias sin uso y duplicación innecesaria. No conservar código comentado ni implementaciones históricas; Git conserva el historial.
- Revisar y actualizar siempre ambos README como documentación del estado vigente: el funcional explica negocio y alcance; el técnico explica implementación y operación. No utilizarlos como changelog.
- Cerrar cada tarea solo después de validar funcionalidad, ejecutar `dotnet restore`, `dotnet build` y `dotnet test`, revisar residuos y reportar supuestos o decisiones pendientes. Corregir los warnings del código sin ocultarlos mediante configuración y reportar los que legítimamente permanezcan.

La asignación y unicidad de SG, la verificación de existencia de clientes y cualquier operación de edición o persistencia requieren alcance posterior explícito. No se incluyen en esta fase.
