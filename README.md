# SnG Control — Steel & Glass

SnG Control es la plataforma en desarrollo para el control operativo y gerencial de Steel & Glass. Su propósito es organizar la información de clientes y proyectos como base para los procesos comerciales, técnicos y operativos de la empresa.

## Funcionalidad actualmente implementada

El backend permite a Comercial crear clientes (`Client`) y proyectos (`Project`), consultarlos por su identificador y listar cada colección mediante HTTP. También permite enviar explícitamente un proyecto a Presupuestos, consultar sus tareas pendientes y consultar el historial de tareas del proyecto. Los datos se guardan en PostgreSQL, en la base `sng_control`.

La API requiere una conexión configurada y el esquema preparado mediante las migraciones `InitialCreate` y `AddProjectTasks` para operar. No hay interfaz de usuario ni operaciones de edición o eliminación. Las pruebas automatizadas verifican dominio, casos de uso, contratos HTTP y modelo de persistencia sin requerir una base de datos activa.

## Clientes y proyectos

Un **Client** representa un cliente. Tiene un identificador interno único, un nombre y fechas de creación y actualización. Su nombre es descriptivo y nunca reemplaza su identidad interna.

Un **Project** representa un proyecto asociado a un cliente. Tiene identificador interno único, identificador del cliente, nombre, ciudad, línea de producto, código SG opcional y fechas de creación y actualización.

La relación es **Client 1 → N Project**: un cliente puede tener varios proyectos y cada proyecto identifica a su cliente mediante `ClientId`. El cliente debe existir antes de crear el proyecto: el caso de uso lo comprueba y rechaza la creación si no lo encuentra. La base de datos refuerza esa relación mediante una clave foránea. La relación impide eliminar un cliente que tenga proyectos asociados; no existe todavía una funcionalidad de eliminación.

## Reglas funcionales vigentes

- El nombre de un cliente y el nombre y la ciudad de un proyecto son obligatorios. No se aceptan valores nulos, vacíos o formados solamente por espacios en blanco.
- Los espacios en blanco al inicio y al final se eliminan al crear las entidades; los espacios internos se conservan.
- Los nombres no se utilizan para identificar entidades. Se permiten proyectos con el mismo nombre tanto para clientes distintos como para un mismo cliente. No hay detección ni advertencias de posibles duplicados.
- Cada entidad recibe su propio identificador interno al crearse. Las relaciones se establecen por ese identificador, nunca por nombre ni por código SG.
- Las fechas se registran en UTC. Al crear un cliente o proyecto, su fecha de actualización coincide exactamente con la de creación. No existen operaciones de edición en esta fase.

### Líneas de producto

Todo proyecto requiere una de estas cuatro opciones:

- Classic
- Essential
- Bioconfort
- Signature

La API recibe y devuelve estos nombres como texto, no como números. No se aceptan valores fuera de esta lista.

### Código SG

Todo proyecto se crea con `SgCode` sin asignar (`null`). Actualmente no existe una operación para asignarlo o modificarlo.

La regla de negocio conocida para una fase posterior es que Presupuestos asigne un código como `SG1043` o `SG1044`. Será un identificador de negocio y no reemplazará la identidad interna del proyecto. El modelo de persistencia incluye un índice único para ese código: permite múltiples proyectos sin código y rechaza códigos no nulos repetidos. El proceso de asignación y las reglas de formato siguen pendientes.

## Flujo funcional disponible

1. Se crea un cliente proporcionando su nombre y se obtiene su identificador interno.
2. Se crea un proyecto indicando el identificador de ese cliente, nombre, ciudad y línea de producto.
3. El sistema valida los datos y la existencia del cliente, normaliza los textos y guarda el proyecto con su identidad y fechas; su código SG queda sin asignar.
4. Se puede consultar cada cliente o proyecto por su identificador, o listar la colección correspondiente. La respuesta de un proyecto incluye el identificador de su cliente, sin incrustar el objeto cliente completo.
5. Comercial puede ejecutar **Enviar a Presupuestos** sobre un proyecto existente. Crear el proyecto no ejecuta este envío automáticamente.
6. El envío crea una tarea de **preparar cotización**, asignada a Presupuestos y en estado **Pendiente**. Se puede consultar la bandeja de tareas por área y estado, y el historial completo de tareas de cada proyecto.
7. Un nuevo intento de envío se rechaza si el proyecto ya tiene esa tarea, cualquiera que sea su estado. No existe reenvío ni retrabajo en el flujo actual.

Las creaciones de clientes y proyectos devuelven el registro guardado y una referencia para consultarlo; el envío devuelve la tarea creada. Las consultas o envíos sobre proyectos inexistentes, o la creación de un proyecto para un cliente inexistente, responden como recurso no encontrado. Las entradas inválidas se rechazan sin crear registros. El historial de un proyecto existente sin tareas es una colección vacía.

Los identificadores, las fechas y el código SG no pueden suministrarse en las solicitudes de creación. Los listados de clientes y proyectos y el historial de tareas no tienen filtros. La bandeja de tareas exige área y estado. No hay paginación ni orden garantizado. La información persiste entre ejecuciones; la preparación de la base se realiza explícitamente y no al iniciar la API.

## Tareas de las áreas

Una **ProjectTask** representa trabajo asignado a un área y pertenece a un proyecto: **Project 1 → N ProjectTask**. Registra el área, tipo de trabajo, estado y fechas de asignación, inicio y finalización. Al asignarla, solo tiene fecha de asignación; inicio y finalización quedan vacíos.

El contrato contempla Comercial (`Commercial`) y Presupuestos (`Budgets`), y estados Pendiente (`Pending`), En proceso (`InProgress`) y Completado (`Completed`). El flujo disponible solo crea tareas Pendientes para Presupuestos. No permite crear tareas arbitrarias, iniciarlas, completarlas, devolverlas ni procesar una cotización.

Las tareas constituyen el historial operacional y no se eliminan al terminar un área. Actualmente no hay operaciones de finalización ni borrado; la base impide eliminar un proyecto con tareas asociadas. El estado operacional pertenece a las tareas, sin añadir un estado global al proyecto.

## Contexto funcional futuro

El contexto conocido contempla procesos comerciales, técnicos, operativos y gerenciales alrededor del proyecto: procesamiento de cotizaciones, transiciones de tareas, catálogo comercial, mediciones, Actas de Vanos versionadas, catálogo técnico, pagos, producción, logística y órdenes de producción.

Estos módulos no están implementados. Sus reglas y flujos se definirán en sus respectivas fases, sin anticiparlos en el núcleo actual.

Para ejecutar o continuar el backend, consultar [README_TECHNICAL.md](README_TECHNICAL.md).
