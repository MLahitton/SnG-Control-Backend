# SnG Control — Steel & Glass

SnG Control es la plataforma en desarrollo para el control operativo y gerencial de Steel & Glass. Su propósito es organizar la información de clientes y proyectos como base para los procesos comerciales, técnicos y operativos de la empresa.

## Funcionalidad actualmente implementada

El backend permite a Comercial crear clientes (`Client`) y proyectos (`Project`), consultarlos por su identificador y listar cada colección mediante HTTP. También permite enviar explícitamente un proyecto a Presupuestos, donde se asigna o corrige su código SG, consultar las tareas por área y estado y consultar el historial de tareas del proyecto. Los datos se guardan en PostgreSQL, en la base `sng_control`. Presupuestos puede obtener un preview técnico de una cotización XLSX estándar y confirmar sus valores corregidos para crear el catálogo comercial base oficial. El preview no persiste; la confirmación conserva datos en PostgreSQL y archivos en almacenamiento local configurado. Una acción posterior envía la cotización a Comercial y genera su Excel operativo temporal. El Jefe Comercial puede subir su revisión, consultar importes y diferencias técnicas y confirmar el archivo final, iniciando la presentación al cliente.

La API requiere configuración de autenticación Google, identidades del Jefe Comercial y de la cuenta compartida de Presupuestos, conexión y carpeta de almacenamiento, además de todas las migraciones del repositorio aplicadas para operar. Los campos de Project solo pueden modificarse mediante la asignación o corrección del SG; confirmar una cotización no modifica Project. Este repositorio contiene solo el backend; no ofrece operaciones de eliminación. Las pruebas automatizadas verifican dominio, casos de uso, contratos HTTP, almacenamiento temporal y modelo de persistencia sin requerir una base de datos activa ni acceso a Google.

## Identidad y acceso

Las operaciones de negocio requieren autenticación mediante un ID Token de Google válido para el Client ID configurado. Durante esta fase de desarrollo puede acceder cualquier cuenta Google válida para ese cliente OAuth; no hay restricción por dominio corporativo ni jerarquías o roles específicos.

Google identifica al usuario mediante el **Subject (`sub`)**, no el email. `GET /api/auth/me` devuelve únicamente subject, email y nombre del usuario autenticado. Existe un único Jefe Comercial global, identificado por `Authentication:CommercialHeadSubject`. Solo esa identidad puede descargar el Excel operativo mediante `commercial-file`; otro usuario autenticado recibe 403. La comparación es exacta por Subject y no depende del email ni del nombre.

No existe tabla Users y no se almacenan passwords ni tokens. Una solicitud sin autenticación, con un token inválido o sin Subject recibe 401. Durante desarrollo, cualquier usuario Google autenticado puede crear proyectos y ejecutar las acciones generales, incluido enviar a Presupuestos; todavía no se modelan usuarios por área. La descarga del Excel temporal y el preview y confirmación de Comercial están restringidos al Jefe Comercial. El archivo comercial final puede descargarlo el Jefe o el creador/responsable del proyecto, siempre por Subject.

La cuenta configurada mediante `Authentication:BudgetsAssigneeSubject` controla el serial SG: solo ella puede asignarlo o corregirlo, ejecutar preview y confirmación de la cotización inicial y enviarla de Presupuestos a Comercial. La política `BudgetsAssignee` compara exactamente el Google `sub`, sin usar email ni fallback; otra identidad autenticada recibe 403 antes de ejecutar la operación. Ser Jefe Comercial no concede estos permisos. El Subject es configuración de User Secrets o entorno, nunca un valor hardcodeado.

## Clientes y proyectos

Un **Client** representa un cliente. Tiene un identificador interno único, un nombre y fechas de creación y actualización. Su nombre es descriptivo y nunca reemplaza su identidad interna.

Un **Project** representa un proyecto asociado a un cliente. Tiene identificador interno único, identificador del cliente, nombre, ciudad, línea de producto, código SG opcional, fechas de creación y actualización e identidad del creador. El servidor registra el Subject del usuario autenticado y snapshots opcionales de email y nombre; el body no puede elegirlos. El creador es actualmente el asesor comercial responsable del proyecto. No existe reasignación ni un segundo responsable persistido. Los proyectos históricos pueden tener `createdBy: null`, sin inventarles identidad.

La relación es **Client 1 → N Project**: un cliente puede tener varios proyectos y cada proyecto identifica a su cliente mediante `ClientId`. El cliente debe existir antes de crear el proyecto: el caso de uso lo comprueba y rechaza la creación si no lo encuentra. La base de datos refuerza esa relación mediante una clave foránea. La relación impide eliminar un cliente que tenga proyectos asociados; no existe todavía una funcionalidad de eliminación.

## Reglas funcionales vigentes

- El nombre de un cliente y el nombre y la ciudad de un proyecto son obligatorios. No se aceptan valores nulos, vacíos o formados solamente por espacios en blanco.
- Los espacios exteriores de los nombres de cliente/proyecto y de la ciudad se eliminan; los espacios internos se conservan. El Subject se conserva exactamente. Los snapshots de email y nombre pueden ser null y no se actualizan en solicitudes posteriores.
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

Mientras su tarea siga En proceso, Presupuestos puede corregir el SG. La corrección conserva exactamente el inicio, el deadline y el historial de atraso; no crea ni reinicia tareas. Reenviar el mismo valor normalizado tampoco cambia la fecha de actualización del proyecto. La fecha de finalización permanece vacía.

No se permite asignar SG antes de enviar el proyecto a Presupuestos. Los estados inconsistentes y las tareas completadas producen un conflicto; no se reparan automáticamente.

## Flujo funcional disponible

1. Se crea un cliente proporcionando su nombre y se obtiene su identificador interno.
2. Se crea un proyecto indicando el identificador de ese cliente, nombre, ciudad y línea de producto.
3. El sistema valida los datos y la existencia del cliente, normaliza los textos y guarda el proyecto con su identificador, creador autenticado y fechas; su código SG queda sin asignar.
4. Se puede consultar cada cliente o proyecto por su identificador, o listar la colección correspondiente. La respuesta de un proyecto incluye el identificador de su cliente, sin incrustar el objeto cliente completo.
5. Comercial puede ejecutar **Enviar a Presupuestos** sobre un proyecto existente. Crear el proyecto no ejecuta este envío automáticamente.
6. El envío crea una tarea de **preparar cotización**, asignada a Presupuestos y en estado **Pendiente**. Se puede consultar la bandeja de tareas por área y estado, y el historial completo de tareas de cada proyecto.
7. Cada proyecto solo puede enviarse una vez al flujo inicial de Presupuestos. Un nuevo intento responde conflicto si el proyecto ya tiene esa tarea, cualquiera que sea su estado. La garantía también se mantiene ante solicitudes simultáneas: solo una crea la tarea y las demás responden conflicto. No existe reenvío ni retrabajo en el flujo actual.
8. Presupuestos asigna manualmente SG y la tarea pasa a En proceso. Puede corregir el código mediante la misma operación sin cambiar la fecha original de inicio.
9. Presupuestos prepara la cotización fuera de SnG Control, en Excel, y carga el XLSX estándar para revisar su preview técnico. El sistema comprueba el SG y conserva la tarea En proceso.
10. Presupuestos corrige los valores permitidos y confirma, enviando nuevamente el XLSX y los valores confirmados. El sistema vuelve a interpretar el archivo, valida la correspondencia exacta y crea el catálogo oficial inicial. La tarea continúa En proceso.
11. Presupuestos ejecuta **Enviar a Comercial**. El sistema genera el Excel operativo, completa PrepareQuotation e inicia ReviewQuotation de Comercial asignada al Subject del Jefe Comercial configurado.
12. El Jefe Comercial descarga el archivo temporal con su nombre original y lo modifica externamente en Excel.
13. Sube el XLSX revisado y consulta un preview informativo: precios, total del proyecto y diferencias técnicas respecto a Presupuestos. Para corregir algo debe editar el archivo y volver a subirlo.
14. Confirma el XLSX. El sistema conserva sus bytes exactos como commercial.xlsx, guarda un snapshot comercial separado, elimina confirmed.xlsx y conserva original.xlsx e imágenes.
15. ReviewQuotation se completa y PresentQuotationToClient comienza InProgress, asignada al creador original. Su StartedAt UTC inicia el Leak Time de presentación al cliente.

Las creaciones de clientes y proyectos devuelven el registro guardado y una referencia para consultarlo; el envío devuelve la tarea creada. Las consultas o envíos sobre proyectos inexistentes, o la creación de un proyecto para un cliente inexistente, responden como recurso no encontrado. Las entradas inválidas se rechazan sin crear registros. El historial de un proyecto existente sin tareas es una colección vacía.

Los identificadores, las fechas, el código SG y la identidad del creador no pueden suministrarse en las solicitudes de creación. Los listados de clientes y proyectos y el historial de tareas no tienen filtros. La bandeja de tareas exige área y estado. No hay paginación ni orden garantizado. La información persiste entre ejecuciones; la preparación de la base se realiza explícitamente y no al iniciar la API.

## Tareas de las áreas

Una **ProjectTask** representa trabajo asignado a un área y pertenece a un proyecto: **Project 1 → N ProjectTask**. Registra el área, tipo de trabajo, estado, fechas de asignación, inicio y finalización y `AssignedToGoogleSubject` opcional. PrepareQuotation se asigna a la cuenta Google compartida de Presupuestos; ReviewQuotation al Jefe Comercial y PresentQuotationToClient al creador del proyecto. Las tareas históricas pueden mantener Subject null. No existe reasignación genérica. PrepareQuotation nace Pendiente, con inicio y finalización vacíos; ReviewQuotation se inicia inmediatamente al enviar a Comercial.

El contrato contempla Comercial (`Commercial`) y Presupuestos (`Budgets`), y estados Pendiente (`Pending`), En proceso (`InProgress`) y Completado (`Completed`). El envío crea una tarea Pendiente y la primera asignación de SG la inicia. No hay una acción separada para iniciar tareas. No se permite crear tareas arbitrarias ni devolverlas. El envío a Comercial completa PrepareQuotation e inicia ReviewQuotation; el preview y la confirmación de Presupuestos no cambian el estado de su tarea. La confirmación comercial completa ReviewQuotation y crea/inicia PresentQuotationToClient para el creador original.

Las tareas constituyen el historial operacional y no se eliminan al terminar un área. No hay finalización genérica de tareas ni borrado; la base impide eliminar un proyecto con tareas asociadas. El estado operacional pertenece a las tareas, sin añadir un estado global al proyecto.

## Preview de cotización de Presupuestos

Presupuestos crea la cotización externamente y carga un único `.xlsx` con la hoja estándar **COTIZACIÓN**. El proyecto debe tener SG asignado y su tarea PrepareQuotation de Presupuestos debe estar **InProgress**, con fecha de inicio y sin fecha de finalización. El SG del documento se normaliza como el del proyecto y debe coincidir exactamente, incluidos los ceros iniciales. La carga no asigna ni corrige SG, ni reemplaza datos del proyecto o del cliente.

El preview incluye metadatos del documento y los ítems técnicos con ubicación, sistema, cristal, acabado de aluminio, cerradura, observaciones, ancho, alto, m², cantidad e imagen. Conserva el orden y el número visible de cada ítem, aunque se repita. Distingue principales (`Main`) y adicionales (`Additional`); los bloques vacíos de plantilla no generan ítems. **Quantity continúa agrupada**: no se crean unidades individuales. Los m² se toman del documento, sin recalcularlos.

Las imágenes se extraen en su formato original. Si falta una imagen o hay varias asociables al mismo bloque, ese ítem devuelve imagen vacía y una advertencia identificada por su número y fila. Una estructura o medida que no pueda interpretarse con seguridad causa rechazo del archivo completo.

Los precios de Presupuestos no se devuelven ni constituyen el catálogo económico definitivo; la autoridad económica es el XLSX revisado y confirmado por Comercial. El preview no guarda catálogo, documento, imágenes ni metadatos y no modifica ninguna entidad. Solo la confirmación convierte los valores revisados en información oficial.

No se admiten `.xls`, `.csv`, `.xlsm`, archivos cifrados/ilegibles ni recotizaciones o liquidaciones con estructura distinta, como bloques CONTRATADO/MODIFICADO. El rechazo depende del contenido, no del nombre del archivo.

## Confirmación y catálogo comercial base

Antes de confirmar se pueden corregir **ItemNumber, Location, System, Glass, AluminumFinish, LockType, Observations, Width, Height, SquareMeters y Quantity**. Los textos se recortan por fuera, conservando su contenido interno; el número de ítem es obligatorio, puede repetirse y no requiere una secuencia. Las medidas y cantidades deben ser positivas y Quantity debe ser entera. Los m² confirmados no se recalculan.

**Section, SourceRow, imagen, metadatos y SG documental no son editables.** Cada corrección debe corresponder a un ítem extraído, mediante Section y SourceRow; no se pueden agregar, omitir ni duplicar ítems. El servidor vuelve a interpretar el XLSX durante la confirmación y obtiene de allí la extracción original. No acepta una extracción declarada por el cliente como autoridad.

Confirmar crea una versión inicial de cotización y un catálogo oficial sin precios. Conserva por separado **lo extraído automáticamente y lo confirmado por Presupuestos**, incluidos los valores que no cambiaron. Main/Additional y el orden documental se mantienen; Quantity sigue agrupada. Las imágenes faltantes o ambiguas permiten confirmar y conservan ese estado, sin inventar un archivo de imagen.

El **Excel original queda intacto** y las imágenes conservan sus bytes originales. Los archivos se guardan fuera de PostgreSQL; la base conserva sus referencias. El SG del documento queda como dato histórico de esa confirmación, aunque después se corrija el SG del proyecto. Las relaciones y el almacenamiento utilizan identificadores internos estables.

Solo se admite una confirmación inicial por proyecto; una segunda produce conflicto, también si ambas se solicitan simultáneamente. No existe versión 2, reemplazo, edición posterior ni borrado del catálogo confirmado. La descarga del Excel operativo se habilita después del envío a Comercial; no hay servicio HTTP de imágenes.

La confirmación **no completa Presupuestos ni envía el proyecto a Comercial**: PrepareQuotation permanece InProgress, mantiene su fecha original de inicio y no tiene fecha de finalización. Tampoco modifica Project, Client ni sus fechas. Si falla el almacenamiento, se revierte la operación y se intenta retirar exclusivamente los archivos creados por ella; las limitaciones ante interrupciones del proceso se detallan en la guía técnica.

## Envío a Comercial y Excel operativo

**Enviar a Comercial** es una acción explícita posterior a confirmar. Requiere la versión inicial, un catálogo con ítems y sus extracciones, el archivo original disponible y PrepareQuotation de Presupuestos En proceso. Solo se permite un envío. Si el SG actual difiere del SG histórico confirmado, el envío se bloquea; no se modifica el documento ni se reconcilian códigos automáticamente.

El sistema genera `confirmed.xlsx` a partir del original y aplica los once valores técnicos oficiales, ubicando cada ítem por su sección y fila fuente. Conserva formato, estructura, fórmulas, imágenes, metadatos y SG documental. No procesa precios ni modifica las columnas económicas o PELÍCULA PROTECCIÓN. Los m² oficiales permanecen en el catálogo; cuando una celda contiene fórmula, conserva la fórmula y su valor almacenado se actualiza con lo confirmado. Excel puede recalcularla al abrir el documento.

`original.xlsx` permanece intacto como evidencia histórica. `confirmed.xlsx` es el archivo operativo para Comercial. El envío completa **PrepareQuotation** conservando sus fechas de asignación e inicio, y crea **ReviewQuotation** de Comercial asignada personalmente al Jefe Comercial y directamente **InProgress**, con StartedAt UTC y sin fecha de finalización. Ese StartedAt inicia el plazo de cinco días laborales de revisión y sus avisos operativos. Project, catálogo y extracciones conservan sus datos.

La descarga, exclusiva del Jefe Comercial, usa exactamente el nombre original del archivo, sin sufijos. Las descargas repetidas entregan los mismos bytes almacenados y no regeneran el Excel ni cambian el workflow. Después de confirmar la revisión comercial, el temporal deja de estar disponible y su ruta responde conflicto; la descarga final tiene una ruta y autorización distintas.

## Revisión y confirmación comercial

El preview comercial es **solo de lectura**: no admite correcciones web, precios ni totales enviados por JSON. Requiere ReviewQuotation InProgress asignada al Jefe configurado, el catálogo y sus extracciones, confirmed.xlsx disponible y SG coincidente con el histórico. Confirmar vuelve a interpretar el XLSX recibido; no confía en un preview anterior.

Comercial puede cambiar los once valores técnicos: número de ítem, ubicación, sistema, cristal, acabado, cerradura, observaciones, ancho, alto, m² y cantidad. El preview muestra valores anteriores y actuales y un resumen de diferencias. Esos cambios no bloquean la confirmación. Debe conservarse exactamente el conjunto **Section + SourceRow**: altas, bajas o desplazamientos de filas se rechazan como conflicto estructural. Esta limitación no define una regla permanente para futuras revisiones; no se intenta vincular ítems por número, nombre u orden.

Los importes se leen directamente del documento: **UnitPrice en N** y **TotalPrice en O** en la fila del ítem. **ProjectTotal** se toma de O en la única fila cuya N dice VALOR TOTAL antes del marcador ADICIONALES de A. Las filas son variables. El total posterior de adicionales no se suma. Se admiten cero y fracciones; no se recalculan multiplicaciones, sumas, IVA ni descuentos. Las fórmulas requieren un resultado numérico guardado en el XLSX.

La confirmación crea CommercialQuotation y CommercialQuotationItem como **realidad comercial vigente**, conservando CommercialCatalog, CommercialCatalogItem y CommercialCatalogItemExtraction como historia de Presupuestos. Los ítems nuevos enlazan sus ítems históricos para reutilizar las imágenes; no se crea otro conjunto de imágenes. El archivo commercial.xlsx se conserva exactamente como fue subido, sin regenerarlo.

La operación completa ReviewQuotation, preserva su inicio y crea/inicia una única PresentQuotationToClient para el Subject del creador original. Un proyecto histórico sin creador puede tener preview, pero confirmar responde **409 ProjectCreatorUnavailable**: no se inventa asesor. No existe reasignación. Solo se admite una confirmación comercial inicial; solicitudes repetidas o concurrentes no crean otra cotización ni otra tarea.

Al terminar, permanecen **original.xlsx, commercial.xlsx y las imágenes históricas**. confirmed.xlsx se elimina físicamente y su referencia queda null. Ante fallos se revierte DB y se compensan los archivos cuando el resultado es conocido; una interrupción o commit incierto puede requerir inspección administrativa, como detalla la guía técnica. La descarga final está disponible para el Jefe Comercial y el creador del proyecto y no cambia estado ni archivos.

## Plazos laborales y notificaciones

Preparar cotización, revisar cotización y presentar al cliente tienen **cinco días laborales**. Para Steel & Glass cuentan lunes a sábado; no cuentan domingos ni festivos nacionales de Colombia. El día de inicio se excluye: se cuenta desde el siguiente día laboral y se vence el quinto día a las **17:00 de Colombia**, usando America/Bogota. Por ejemplo, una tarea iniciada el 14/10/2026 vence el 20/10/2026 a las 17:00; el sábado 17 cuenta.

El deadline se guarda al iniciar y queda congelado. Consultas, reinicios, correcciones de SG y cambios futuros de política no lo recalculan. Enviar a Presupuestos asigna inmediatamente a su cuenta Google compartida y genera un aviso, pero conserva la tarea Pending sin deadline: su plazo comienza únicamente con el primer SG. ReviewQuotation y PresentQuotationToClient se asignan e inician en sus transiciones actuales.

Cada aviso se dirige exclusivamente al asignado. Se registra la asignación, un recordatorio dos días laborales antes a las 08:00, otro el día del vencimiento a las 08:00 y un único aviso de atraso después del deadline. Completar exactamente en el deadline cuenta como cumplimiento. WasOverdue conserva si hubo atraso alguna vez y nunca vuelve a false; una tarea legacy sin seguimiento conserva deadline y cumplimiento desconocidos, sin reconstrucción ficticia.

Al completar tarde, el aviso de atraso se resuelve con la fecha de finalización, pero permanece en el historial. Si el worker aún no lo había generado, la finalización deja igualmente la evidencia resuelta. No se generan recordatorios posteriores a la terminación ni se borran avisos históricos.

Se pueden consultar las notificaciones personales y el historial cronológico completo por proyecto. Son registros internos: no hay leído/no leído, contador de campana, email, WhatsApp, SMS ni push. La asignación identifica al destinatario; no agrega restricciones nuevas a las acciones generales del proyecto.

El worker comprueba las tareas al arrancar y aproximadamente cada minuto. Tras una interrupción recupera asignaciones faltantes y únicamente el hito temporal más relevante que corresponda en ese momento, sin duplicados ni acumulación de recordatorios superados. ScheduledAt conserva cuándo correspondía el aviso y CreatedAt cuándo se generó. Si el proceso está detenido, los avisos se retrasan hasta su recuperación.

La reasignación queda para un alcance futuro: deberá conservar historia, emitir una nueva asignación y reiniciar los cinco días para el nuevo responsable. Actualmente no existe operación de reasignación.

## Contexto funcional futuro

El contexto conocido contempla procesos comerciales, técnicos, operativos y gerenciales alrededor del proyecto: aprobación del cliente, devolución de tareas, evolución del catálogo comercial, nuevas versiones de cotización, mediciones, Actas de Vanos versionadas, catálogo técnico, pagos, producción, logística y órdenes de producción.

PresentQuotationToClient está iniciada para el asesor, pero todavía no hay confirmación de presentación, aprobación del cliente ni anticipo. Los módulos futuros no están implementados. Sus reglas y flujos se definirán en sus respectivas fases, sin anticiparlos en el núcleo actual.

Para ejecutar o continuar el backend, consultar [README_TECHNICAL.md](README_TECHNICAL.md).
