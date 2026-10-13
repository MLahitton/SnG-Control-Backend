# SnG Control — Steel & Glass

SnG Control es la plataforma en desarrollo para el control operativo y gerencial de Steel & Glass. Su propósito es organizar la información de clientes y proyectos como base para los procesos comerciales, técnicos y operativos de la empresa.

## Funcionalidad actualmente implementada

El backend contiene el núcleo de dominio de clientes (`Client`), proyectos (`Project`) y líneas de producto (`ProductLine`). Permite construir entidades en memoria y validar sus datos de creación mediante código.

Todavía no hay registro o consulta por HTTP, interfaz de usuario ni almacenamiento persistente. La API puede arrancar, pero no expone operaciones de negocio. Las pruebas automatizadas verifican las reglas del dominio.

## Clientes y proyectos

Un **Client** representa un cliente. Tiene un identificador interno único, un nombre y fechas de creación y actualización. Su nombre es descriptivo y nunca reemplaza su identidad interna.

Un **Project** representa un proyecto asociado a un cliente. Tiene identificador interno único, identificador del cliente, nombre, ciudad, línea de producto, código SG opcional y fechas de creación y actualización.

La relación es **Client 1 → N Project**: un cliente puede tener varios proyectos y cada proyecto identifica a su cliente mediante `ClientId`. Actualmente se exige un identificador de cliente no vacío; no se consulta un registro de clientes para verificar su existencia porque todavía no hay persistencia.

## Reglas funcionales vigentes

- El nombre de un cliente y el nombre y la ciudad de un proyecto son obligatorios. No se aceptan valores nulos, vacíos o formados solamente por espacios en blanco.
- Los espacios en blanco al inicio y al final se eliminan al crear las entidades; los espacios internos se conservan.
- Los nombres no se utilizan para identificar entidades. Se permiten proyectos con el mismo nombre tanto para clientes distintos como para un mismo cliente. No hay detección ni advertencias de posibles duplicados.
- Cada entidad recibe su propio identificador interno al crearse. Las relaciones se establecen por ese identificador, nunca por nombre ni por código SG.
- Las fechas se registran en UTC. Al crear una entidad, su fecha de actualización coincide exactamente con la de creación. No existen operaciones de edición en esta fase.

### Líneas de producto

Todo proyecto requiere una de estas cuatro opciones:

- Classic
- Essential
- Bioconfort
- Signature

No se aceptan valores fuera de esta lista.

### Código SG

Todo proyecto se crea con `SgCode` sin asignar (`null`). Actualmente no existe una operación para asignarlo o modificarlo.

La regla de negocio conocida para una fase posterior es que Presupuestos asigne un código como `SG1043` o `SG1044`. Será un identificador de negocio que deberá ser único cuando exista, pero no reemplazará la identidad interna del proyecto. El proceso de asignación y el mecanismo que garantice esa unicidad todavía no están implementados.

## Flujo funcional disponible

1. Se crea un cliente con un nombre válido y se obtiene su identificador interno.
2. Se crea un proyecto indicando el identificador del cliente, nombre, ciudad y línea de producto.
3. El dominio valida los datos, elimina los espacios exteriores y genera la identidad y las fechas del proyecto; su código SG queda sin asignar.
4. Pueden crearse más proyectos para el mismo cliente, incluso con nombres repetidos.

Este flujo se ejecuta únicamente en memoria desde código; no guarda datos entre ejecuciones.

## Contexto funcional futuro

El contexto conocido contempla procesos comerciales, técnicos, operativos y gerenciales alrededor del proyecto: catálogo comercial, mediciones, Actas de Vanos versionadas, catálogo técnico, procesos y tareas, pagos, producción, logística y órdenes de producción.

Estos módulos no están implementados. Sus reglas y flujos se definirán en sus respectivas fases, sin anticiparlos en el núcleo actual.

Para ejecutar o continuar el backend, consultar [README_TECHNICAL.md](README_TECHNICAL.md).
