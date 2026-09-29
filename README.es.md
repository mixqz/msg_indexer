# Email Archive Indexer

[English](README.md) · [한국어](README.ko.md) · **Español** · [Français](README.fr.md) · [日本語](README.ja.md) · [简体中文](README.zh.md)

Un programa portátil para Windows pensado para quienes guardan su correo como archivos `.msg` / `.eml`. Encuentra cualquier correo en una carpeta de copia de seguridad de hasta unos 10.000 archivos en segundos, da a los archivos nombres coherentes, limpia los duplicados y hace automáticamente una copia de seguridad de la Bandeja de entrada y los Elementos enviados de Outlook (clásico).

Un solo archivo `.exe`, sin instalación, sin permisos de administrador y sin conexión a Internet.

![Ventana principal](docs/images/main-en.png)

*Captura de pantalla tomada con Mono en Linux con datos de ejemplo; el aspecto real en Windows es ligeramente distinto.*

## Características

- **Búsqueda rápida** en el asunto, las personas (De/Para/CC), el cuerpo y los nombres de los datos adjuntos. Varias palabras = deben coincidir todas; `"comillas"` = frase exacta.
- **Filtros**: fecha, recibido/enviado, datos adjuntos, respuestas a reuniones (solicitud/aceptada/rechazada/provisional/cancelada), duplicados, correo similar, errores de lectura, carpeta y remitentes principales.
- **Vista previa** en vista con formato o vista de texto. Se bloquean las imágenes externas, los scripts y las redirecciones, y los vínculos piden confirmación antes de abrirse. Abre el correo en Outlook con Ctrl+O.
- **Normalización de nombres de archivo** a `AAMMDD_HHMMSS_Remitente_Asunto_AttY.msg`, con la hora de recepción para el correo recibido y la hora de envío para el correo enviado. Primero ves una vista previa del antes y el después, y puedes deshacerlo.
- **Duplicados**: las copias exactas que se agregan a la carpeta van automáticamente a la Papelera de reciclaje (nunca en el primer análisis). Una limpieza guiada se encarga del resto. Nunca se elimina nada de forma permanente.
- **Copia de seguridad de Outlook (clásico)**: guarda la Bandeja de entrada y los Elementos enviados como `.msg`, omite el correo que ya tiene copia de seguridad, no modifica Outlook y registra cada operación correcta y cada error. Si Outlook no se está ejecutando, lo inicia.
- **Análisis incrementales** con una pequeña caché local: después del primer análisis, solo se leen los archivos que cambiaron.
- **6 idiomas**: English, 한국어, Español, Français, 日本語, 简体中文 (Configuración → Language / 언어).

## Descargar y ejecutar

1. Descarga `EmailIndexer-v<version>.zip` desde [Releases](https://github.com/mixqz/msg_indexer/releases) y descomprímelo.
2. Haz doble clic en `EmailIndexer.exe`. La aplicación no tiene firma de código, así que Windows puede mostrar **"Windows protegió tu PC"**. Elige **Más información → Ejecutar de todas formas**, o haz clic con el botón derecho en el archivo → **Propiedades** → **Desbloquear**.
3. Haz clic en **Cambiar carpeta** y elige la carpeta de copia de seguridad del correo.

Requisitos: Windows 11 (probado) o Windows 10 con .NET Framework 4.8, que viene incluido en Windows. La copia de seguridad de Outlook necesita Outlook (clásico); el nuevo Outlook no tiene interfaz de automatización.

Guía del usuario: [English](docs/guide/USER_GUIDE.en.md) · [한국어](docs/guide/USER_GUIDE.ko.md) · [Español](docs/guide/USER_GUIDE.es.md) · [Français](docs/guide/USER_GUIDE.fr.md) · [日本語](docs/guide/USER_GUIDE.ja.md) · [简体中文](docs/guide/USER_GUIDE.zh.md)

## Privacidad

Todo se queda en tu PC. La aplicación no establece ninguna conexión de red. Su caché y sus registros se guardan en una carpeta oculta `.emailindex` dentro de tu carpeta de copia de seguridad y en `%APPDATA%\EmailIndexer`.

## Compilar desde el código fuente

Solo necesitas Docker; no se instala nada en el equipo host.

```bash
./build.sh          # tests + Windows exe → dist/EmailIndexer.exe
./build.sh test     # core tests only
python3 package.py  # release zip → dist/EmailIndexer-v<version>.zip (+ .sha256)
```

- `src/EmailIndexer.Core`: análisis del correo, caché, duplicados, reglas de nombres de archivo y traducciones (netstandard2.0, probado en Docker)
- `src/EmailIndexer.App`: interfaz WinForms y Outlook COM (.NET Framework 4.8, combinado en un solo exe)
- `tests/EmailIndexer.Core.Tests`: pruebas xUnit
- Versión: actualiza a la vez `src/EmailIndexer.Core/AppInfo.cs`, `src/EmailIndexer.App/EmailIndexer.App.csproj` y `src/EmailIndexer.App/app.manifest`.
- Diagnóstico: `EmailIndexer.exe --scan-test <folder>` (informe de análisis de solo lectura) y `--index-test <folder>` (informe de caché y duplicados; no elimina nada).

## Contribuir

Las correcciones de traducción y los nuevos idiomas son muy bienvenidos; las traducciones se escribieron con ayuda de IA y todavía no las han revisado hablantes nativos. Consulta [CONTRIBUTING.md](CONTRIBUTING.md).

## Licencia

[MIT](LICENSE). Los componentes de terceros se enumeran en [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
