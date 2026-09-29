# Email Archive Indexer — Guía del usuario

Un programa portátil para Windows (sin instalación) que te permite encontrar y organizar rápidamente los archivos de copia de seguridad de tu correo de Outlook (.msg / .eml) y hacer copias de seguridad automáticas del correo de Outlook.

## 1. Introducción

1. Coloca el archivo `EmailIndexer.exe` donde quieras (por ejemplo, en el escritorio o en Documentos). No hace falta instalarlo.
2. Haz doble clic en él para ejecutarlo.
   - Si aparece **"Windows protegió tu PC"** → **Más información** → **Ejecutar de todas formas**
   - Si sigue sin ejecutarse: haz clic con el botón derecho en el archivo → **Propiedades** → activa **Desbloquear** en la parte inferior → Aceptar → vuelve a ejecutarlo
3. Haz clic en **Cambiar carpeta** y elige la carpeta de copia de seguridad del correo. (También puedes arrastrar una carpeta desde el Explorador de archivos a la ventana).
4. La primera vez se lee toda la carpeta. Después solo se leen los archivos que cambiaron, así que se abre en segundos.

> No la ejecutes como administrador. Si se ejecuta con un nivel de permisos distinto del de Outlook, la copia de seguridad de Outlook no funcionará.

**Idioma:** la aplicación se inicia en inglés. Para cambiarlo, abre **Configuración** → **Language / 언어**, elige un idioma y reinicia la aplicación. Cada idioma aparece con su propio nombre (English, 한국어, Español, Français, 日本語, 简体中文).

## 2. La ventana principal

| Área | Qué hace |
|---|---|
| Parte superior | Carpeta de copia de seguridad, **Analizar (F5)**, **Copia de seguridad de Outlook**, **Configuración**, **Ayuda (F1)** |
| Filtros (izquierda) | Fecha · Recibido/Enviado · Datos adjuntos · Reunión (solicitud/aceptada/rechazada/provisional/cancelada) · Estado (duplicado/similar/error/sin normalizar) · Formato · Carpeta · 30 remitentes principales |
| Cuadro de búsqueda | Busca a la vez en el asunto, las personas, el cuerpo y los nombres de los datos adjuntos. Varias palabras = deben coincidir todas, `"comillas"` = frase exacta. Usa los botones de al lado para limitar el ámbito |
| Lista | Haz clic en el encabezado de una columna para ordenar. Gris = duplicado, naranja = similar, rojo = error de lectura |
| Parte inferior | Recuento total · mostrados · seleccionados, resultado del análisis |

## 3. Tareas habituales

| Para hacer esto | Haz esto |
|---|---|
| Leer un correo rápidamente | Doble clic o Enter → cambia entre **Ver con formato** y **Ver como texto**, ◀ ▶ para el anterior/siguiente |
| Abrir en Outlook | Ctrl+O o **Abrir** |
| Seleccionar varios correos | Ctrl/Shift + clic, Ctrl+A |
| Ir al cuadro de búsqueda | Ctrl+F (Esc borra la búsqueda) |
| Menú contextual | Vista previa · Abrir · Mostrar en la carpeta · Copiar ruta · Normalizar nombres · Mover · Eliminar · Mostrar solo este remitente |
| Ver cómo funciona una función | F1 o **Ayuda (F1)** |

## 4. Organizar archivos

- **Normalizar nombres**: cambia el nombre de los archivos al formato `260823_175434_Alex Kim [Sales Team]_W35 team agenda_AttN.msg`.
  - El correo recibido usa la hora de recepción; el enviado, la hora de envío.
  - El marcador de datos adjuntos del final sigue el idioma de la aplicación (en español `_AttY`/`_AttN`). Los archivos ya normalizados en cualquier idioma se dejan como están.
  - Si no hay nada seleccionado, se incluye todo el correo que se muestra en la lista.
  - Antes de cambiar nada, se muestra una tabla "antes → después". **Deshacer última normalización**, en la misma ventana, restaura los nombres originales.
  - Los caracteres como `: / ?` del asunto se convierten en caracteres de ancho completo parecidos (`： ／ ？`).
- **Mover selección**: mueve los archivos a la carpeta que elijas. Si existe un archivo con el mismo nombre, se agrega ` (2)`.
- **Eliminar selección (Del)**: envía los archivos a la **Papelera de reciclaje**, desde donde puedes restaurarlos.
- **Limpiar duplicados**: muestra en una tabla los archivos confirmados como el mismo correo y, después, los envía a la Papelera de reciclaje. Se conserva un original de cada grupo.
- **Eliminación automática de duplicados**: si copias correo que ya está en la carpeta, la copia recién agregada va automáticamente a la Papelera de reciclaje en el siguiente análisis.
  - En el primer análisis no se elimina nada automáticamente; los duplicados solo se marcan.
  - Los archivos que restaures desde la Papelera de reciclaje no se vuelven a eliminar.
  - Los archivos a los que solo cambiaste el nombre o que moviste a otra carpeta no se tratan como duplicados.

## 5. Copia de seguridad automática de Outlook

1. Si Outlook (clásico) no se está ejecutando, se inicia automáticamente. Si aparece un selector de perfiles, elige tu perfil. (El nuevo Outlook no es compatible).
2. **Copia de seguridad de Outlook** → elige un intervalo (Desde la última copia / Últimos N días / Todo) → **Iniciar copia**
3. El correo de la Bandeja de entrada y de Elementos enviados se guarda **directamente en la carpeta de copia de seguridad** (sin subcarpetas) con nombres normalizados. Después puedes mover los archivos a subcarpetas tú mismo; el análisis los sigue encontrando todos.
4. El correo que ya tiene copia de seguridad se omite. El correo original de Outlook no se modifica.
5. Si Outlook muestra "Un programa está intentando obtener acceso…", haz clic en **Permitir**.

## 6. Solución de problemas

| Problema | Solución |
|---|---|
| Copia de seguridad de Outlook: "No se puede conectar" | Reinicia Outlook y este programa de forma normal (doble clic, no como administrador) |
| Copia de seguridad de Outlook: mensaje de "Nuevo Outlook" | Desactiva el modificador **Nuevo Outlook** en la esquina superior derecha de Outlook para volver al clásico |
| Archivos con "Error" en rojo en la lista | El archivo está dañado o no es de correo. El motivo aparece en la columna Asunto |
| Algo no funciona bien | Envía a tu contacto de soporte el resultado de **Configuración** → **Comprobar entorno** y los archivos de registro siguientes |

**Archivos de registro** (adjúntalos al informar de un problema; siempre se escriben en inglés)
- `carpeta de copia de seguridad\.emailindex\last-scan.log`: resultado del último análisis y motivos de error
- `carpeta de copia de seguridad\.emailindex\actions.log`: historial de cambios de nombre, movimientos y eliminaciones
- `carpeta de copia de seguridad\.emailindex\outlook-backup.log`: historial de copias de seguridad de Outlook
- `%APPDATA%\EmailIndexer\error.log`: errores inesperados

La carpeta oculta `.emailindex` es una caché que hace que la lista se abra rápidamente. Eliminarla no afecta a tu correo; se vuelve a crear desde cero en la siguiente ejecución. (Después de eliminarla, la eliminación automática de duplicados se salta un análisis, como en el primer análisis, y se pierde el punto "Desde la última copia" de la copia de seguridad de Outlook).
