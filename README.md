# SnG Control — Steel & Glass

SnG Control es la plataforma en desarrollo para el control operativo y gerencial de Steel & Glass. Su propósito es organizar la información de clientes y proyectos como base para los procesos comerciales, técnicos y operativos de la empresa.

## Funcionalidad actualmente implementada

El backend permite a Comercial crear clientes (`Client`) y proyectos (`Project`), consultarlos por su identificador y listar cada colección mediante HTTP. También permite enviar explícitamente un proyecto a Presupuestos, asignar o corregir su código SG, consultar las tareas por área y estado y consultar el historial de tareas del proyecto. Los datos se guardan en PostgreSQL, en la base `sng_control`. Presupuestos puede obtener un preview técnico de una cotización XLSX estándar y confirmar sus valores corregidos para crear el catálogo comercial base oficial. El preview no persiste; la confirmación conserva datos en PostgreSQL y archivos en almacenamiento local configurado.

La API requiere una conexión, una carpeta de almacenamiento configurada y todas las migraciones del repositorio aplicadas para operar. Los campos de Project solo pueden modificarse mediante la asignación o corrección del SG; confirmar una cotización no modifica Project. No hay interfaz de usuario ni operaciones de eliminación. Las pruebas automatizadas verifican dominio, casos de uso, contratos HTTP, almacenamiento temporal y modelo de persistencia sin requerir una base de datos activa.

## Clientes y proyectos

Un **Client** representa un cliente. Tiene un identificador interno único, un nombre y fechas de creación y actualización. Su nombre es descriptivo y nunca reemplaza su identidad interna.

Un **Project** representa un proyecto asociado a un cliente. Tiene identificador interno único, identificador del cliente, nombre, ciudad, línea de producto, código SG opcional y fechas de creación y actualización.

La relación es **Client 1 → N Project**: un cliente puede tener varios proyectos y cada proyecto identifica a su cliente mediante `ClientId`. El cliente debe existir antes de crear el proyecto: el caso de uso lo comprueba y rechaza la creación si no lo encuentra. La base de datos refuerza esa relación mediante una clave foránea. La relación impide eliminar un cliente que tenga proyectos asociados; no existe todavía una funcionalidad de eliminación.

## Reglas funcionales vigentes

- El nombre de un cliente y el nombre y la ciudad de un proyecto son obligatorios. No se aceptan valores nulos, vacíos o formados solamente por espacios en blanco.
- Los espacios en blanco al inicio y al final se eliminan al crear las entidades; los espacios internos se conservan.
- Los nombres no se utilizan para identificar entidades. Se permiten proyectos con el mismo nombre tanto para clientes distintos como para un mismo cliente. No hay detección ni advertencias de posibles duplicados.
- Cada entidad recibe su propio identificador interno al crearse. Las relaciones se establecen por ese identificador, nunca por nombre ni por código SG.
- Las fechas se registran en UTC. Al crear un cliente o proyecto, su fecha de actualización coincide exactamente con la de creación. Cambiar realmente el SG actualiza la fecha de modificación del proyecto y conserva su fecha de creación.

### Líneas de producto

Todo proyecto requiere una de estas cuatro opciones:

- Classic
- Essential
- Bioconfort
- Signature

La API recibe y devuelve estos nombres como texto, no como números. No se aceptan valores fuera de esta lista.

### Código SG

Todo proyecto se crea con `SgCode` sin asignar (`null`). Después del envío a Presupuestos, el usuario de esa área escribe el código manualmente. El sistema no lo genera, calcula ni reserva consecutivos. SG es un identificador de negocio y no reemplaza la identidad interna del proyecto.

Se eliminan los espacios exteriores y se convierte la entrada a mayúsculas. El formato es `SG` seguido de uno o más dígitos ASCII `0-9`, sin espacios internos, signos ni letras adicionales. Por ejemplo, `"  sg0012  "` se guarda como `SG0012`: los ceros iniciales se conservan y `SG0012` es distinto de `SG12`. No hay una cantidad adicional mínima ni máxima de dígitos.

Un SG solo puede pertenecer a un proyecto, también ante solicitudes simultáneas. La primera asignación cambia la tarea de Presupuestos de **Pendiente** a **En proceso** y fija su fecha de inicio en UTC. El código y el inicio de la tarea se guardan juntos: si el código ya pertenece a otro proyecto, no se guarda ninguno de esos cambios.

Presupuestos puede corregir posteriormente el SG. La corrección conserva exactamente la fecha original de inicio y no crea ni reinicia tareas. Reenviar el mismo valor normalizado tampoco cambia la fecha de actualización del proyecto. La fecha de finalización permanece vacía.

No se permite asignar SG antes de enviar el proyecto a Presupuestos. Los estados inconsistentes y las tareas completadas producen un conflicto; no se reparan automáticamente.

## Flujo funcional disponible

1. Se crea un cliente proporcionando su nombre y se obtiene su identificador interno.
2. Se crea un proyecto indicando el identificador de ese cliente, nombre, ciudad y línea de producto.
3. El sistema valida los datos y la existencia del cliente, normaliza los textos y guarda el proyecto con su identidad y fechas; su código SG queda sin asignar.
4. Se puede consultar cada cliente o proyecto por su identificador, o listar la colección correspondiente. La respuesta de un proyecto incluye el identificador de su cliente, sin incrustar el objeto cliente completo.
5. Comercial puede ejecutar **Enviar a Presupuestos** sobre un proyecto existente. Crear el proyecto no ejecuta este envío automáticamente.
6. El envío crea una tarea de **preparar cotización**, asignada a Presupuestos y en estado **Pendiente**. Se puede consultar la bandeja de tareas por área y estado, y el historial completo de tareas de cada proyecto.
7. Cada proyecto solo puede enviarse una vez al flujo inicial de Presupuestos. Un nuevo intento responde conflicto si el proyecto ya tiene esa tarea, cualquiera que sea su estado. La garantía también se mantiene ante solicitudes simultáneas: solo una crea la tarea y las demás responden conflicto. No existe reenvío ni retrabajo en el flujo actual.
8. Presupuestos asigna manualmente SG y la tarea pasa a En proceso. Puede corregir el código mediante la misma operación sin cambiar la fecha original de inicio.
9. Presupuestos prepara la cotización fuera de SnG Control, en Excel, y carga el XLSX estándar para revisar su preview técnico. El sistema comprueba el SG y conserva la tarea En proceso.
10. Presupuestos corrige los valores permitidos y confirma, enviando nuevamente el XLSX y los valores confirmados. El sistema vuelve a interpretar el archivo, valida la correspondencia exacta y crea el catálogo oficial inicial. La tarea continúa En proceso.

Las creaciones de clientes y proyectos devuelven el registro guardado y una referencia para consultarlo; el envío devuelve la tarea creada. Las consultas o envíos sobre proyectos inexistentes, o la creación de un proyecto para un cliente inexistente, responden como recurso no encontrado. Las entradas inválidas se rechazan sin crear registros. El historial de un proyecto existente sin tareas es una colección vacía.

Los identificadores, las fechas y el código SG no pueden suministrarse en las solicitudes de creación. Los listados de clientes y proyectos y el historial de tareas no tienen filtros. La bandeja de tareas exige área y estado. No hay paginación ni orden garantizado. La información persiste entre ejecuciones; la preparación de la base se realiza explícitamente y no al iniciar la API.

## Tareas de las áreas

Una **ProjectTask** representa trabajo asignado a un área y pertenece a un proyecto: **Project 1 → N ProjectTask**. Registra el área, tipo de trabajo, estado y fechas de asignación, inicio y finalización. Al asignarla, solo tiene fecha de asignación; inicio y finalización quedan vacíos.

El contrato contempla Comercial (`Commercial`) y Presupuestos (`Budgets`), y estados Pendiente (`Pending`), En proceso (`InProgress`) y Completado (`Completed`). El envío crea una tarea Pendiente y la primera asignación de SG la inicia. No hay una acción separada para iniciar tareas. No se permite crear tareas arbitrarias, completarlas ni devolverlas; el preview de cotización no cambia su estado.

Las tareas constituyen el historial operacional y no se eliminan al terminar un área. Actualmente no hay operaciones de finalización ni borrado; la base impide eliminar un proyecto con tareas asociadas. El estado operacional pertenece a las tareas, sin añadir un estado global al proyecto.

## Preview de cotización de Presupuestos

Presupuestos crea la cotización externamente y carga un único `.xlsx` con la hoja estándar **COTIZACIÓN**. El proyecto debe tener SG asignado y su tarea PrepareQuotation de Presupuestos debe estar **InProgress**, con fecha de inicio y sin fecha de finalización. El SG del documento se normaliza como el del proyecto y debe coincidir exactamente, incluidos los ceros iniciales. La carga no asigna ni corrige SG, ni reemplaza datos del proyecto o del cliente.

El preview incluye metadatos del documento y los ítems técnicos con ubicación, sistema, cristal, acabado de aluminio, cerradura, observaciones, ancho, alto, m², cantidad e imagen. Conserva el orden y el número visible de cada ítem, aunque se repita. Distingue principales (`Main`) y adicionales (`Additional`); los bloques vacíos de plantilla no generan ítems. **Quantity continúa agrupada**: no se crean unidades individuales. Los m² se toman del documento, sin recalcularlos.

Las imágenes se extraen en su formato original. Si falta una imagen o hay varias asociables al mismo bloque, ese ítem devuelve imagen vacía y una advertencia identificada por su número y fila. Una estructura o medida que no pueda interpretarse con seguridad causa rechazo del archivo completo.

Los precios de Presupuestos no se devuelven ni constituyen el catálogo económico definitivo; la autoridad económica corresponderá al Excel revisado por Comercial en una fase posterior. El preview no guarda catálogo, documento, imágenes ni metadatos y no modifica ninguna entidad. Solo la confirmación convierte los valores revisados en información oficial.

No se admiten `.xls`, `.csv`, `.xlsm`, archivos cifrados/ilegibles ni recotizaciones o liquidaciones con estructura distinta, como bloques CONTRATADO/MODIFICADO. El rechazo depende del contenido, no del nombre del archivo.

## Confirmación y catálogo comercial base

Antes de confirmar se pueden corregir **ItemNumber, Location, System, Glass, AluminumFinish, LockType, Observations, Width, Height, SquareMeters y Quantity**. Los textos se recortan por fuera, conservando su contenido interno; el número de ítem es obligatorio, puede repetirse y no requiere una secuencia. Las medidas y cantidades deben ser positivas y Quantity debe ser entera. Los m² confirmados no se recalculan.

**Section, SourceRow, imagen, metadatos y SG documental no son editables.** Cada corrección debe corresponder a un ítem extraído, mediante Section y SourceRow; no se pueden agregar, omitir ni duplicar ítems. El servidor vuelve a interpretar el XLSX durante la confirmación y obtiene de allí la extracción original. No acepta una extracción declarada por el cliente como autoridad.

Confirmar crea una versión inicial de cotización y un catálogo oficial sin precios. Conserva por separado **lo extraído automáticamente y lo confirmado por Presupuestos**, incluidos los valores que no cambiaron. Main/Additional y el orden documental se mantienen; Quantity sigue agrupada. Las imágenes faltantes o ambiguas permiten confirmar y conservan ese estado, sin inventar un archivo de imagen.

El **Excel original queda intacto** y las imágenes conservan sus bytes originales. Los archivos se guardan fuera de PostgreSQL; la base conserva sus referencias. El SG del documento queda como dato histórico de esa confirmación, aunque después se corrija el SG del proyecto. Las relaciones y el almacenamiento utilizan identificadores internos estables.

Solo se admite una confirmación inicial por proyecto; una segunda produce conflicto, también si ambas se solicitan simultáneamente. No existe versión 2, reemplazo, edición posterior ni borrado del catálogo confirmado. Todavía no hay descarga del XLSX ni servicio HTTP de imágenes.

La confirmación **no completa Presupuestos ni envía el proyecto a Comercial**: PrepareQuotation permanece InProgress, mantiene su fecha original de inicio y no tiene fecha de finalización. Tampoco modifica Project, Client ni sus fechas. Si falla el almacenamiento, se revierte la operación y se intenta retirar exclusivamente los archivos creados por ella; las limitaciones ante interrupciones del proceso se detallan en la guía técnica.

## Contexto funcional futuro

El contexto conocido contempla procesos comerciales, técnicos, operativos y gerenciales alrededor del proyecto: procesamiento económico de Comercial, finalización y devolución de tareas, evolución del catálogo comercial, nuevas versiones de cotización, mediciones, Actas de Vanos versionadas, catálogo técnico, pagos, producción, logística y órdenes de producción.

Estos módulos no están implementados. Sus reglas y flujos se definirán en sus respectivas fases, sin anticiparlos en el núcleo actual.

Para ejecutar o continuar el backend, consultar [README_TECHNICAL.md](README_TECHNICAL.md).
