# Jarvis 🔵

## Novedades de este rediseño

- **Widget flotante en el escritorio** (`FloatingWidget.xaml(.cs)`): una esfera de orbes chica (pero no tan chica: 112px), siempre visible y siempre encima de todo, que vive permanentemente en el escritorio como Jarvis "de fondo". Se **arrastra con el mouse** a cualquier parte de la pantalla (y recuerda la posición entre reinicios), y un **click corto** (sin arrastrar) abre o cierra una **burbuja de chat chica** al lado (`ChatBubble.xaml(.cs)`) para preguntas rápidas sin abrir la ventana completa. Click derecho sobre el widget da un menú con "Chatear", "Ventana completa", "Ocultar widget" y "Salir". La app ahora arranca mostrando **solo el widget**; la ventana grande de siempre (chat completo, mic grande, captura de pantalla, configuración) sigue estando a un click de distancia — desde el widget, la bandeja del sistema, o el hotkey de siempre **Ctrl+Alt+J**.
- **Modo "Pensando" rediseñado, sin movimiento caótico**: antes cada orbe caía en espiral hacia el centro en su propio ciclo independiente, lo que se veía como una lluvia descoordinada. Ahora los orbes se organizan en **7 anillos concéntricos estables** que giran a velocidad tipo Kepler (los anillos internos giran más rápido que los externos, igual que un disco de acreción real o los planetas de un sistema solar), con solo una variación sutil de velocidad entre orbes para que no se vea perfectamente sincronizado/robótico. El resultado es un giro parejo y calmo que se lee tanto como "agujero negro" (disco achatado) como "sistema solar" (anillos orbitando), sin la sensación de caos de antes.
- **Se acabaron los `***********` en el chat y en la voz.** Gemini devuelve las respuestas en Markdown (`**negrita**`, `# títulos`, etc.) y antes se mostraban y se leían tal cual. Ahora hay una limpieza (`Utils/TextUtils.cs`) que se aplica tanto al texto que se ve en el chat como al texto que se manda a hablar.
- **La luz de Ctrl+Alt+K ahora dura exactamente lo que dura Jarvis hablando.** Antes el aro de luz se cerraba a los 14 segundos fijos, sin importar si la respuesta era más corta o más larga que eso. Ahora `VoiceService` avisa con un evento real (`SpeakCompleted`) cuándo termina el audio, y el overlay espera ese evento en vez de un tiempo inventado.
- **Rediseño completo a paleta "Jarvis"**: negro-azulado profundo + cian, sin restos del esquema cobre/rojo anterior (`App.xaml`, `Converters.cs`, `MainWindow.xaml`, `SettingsWindow.xaml`).
- **Esfera de orbes reactiva** (`Controls/OrbVisualizer.xaml(.cs)`), ocupando la mayor parte de la ventana (o, en el widget, toda la ventanita chica):
  - **Pensando** → anillos concéntricos estables tipo disco de acreción / sistema solar (ver arriba).
  - **Hablando** → los orbes se agrupan en 26 bandas radiales que suben y bajan como un ecualizador circular, sincronizadas con cada palabra pronunciada (evento `WordSpoken` de `VoiceService`).
  - **Escuchando** (mic activo) → pulso suave hacia afuera.
  - **En reposo** → esfera flotando y rotando despacio.
- **Voz**: se mejoró la selección automática de voz (prioriza voces "Natural"/neuronales de Windows 11 si están instaladas) y se agregó un leve *contour* de tono vía SSML para que suene menos "lectura plana". Sobre pedir una voz *idéntica* a la de la película: no es algo que se pueda ofrecer, ni por lo técnico (`System.Speech` es un motor local, no un clonador de voces) ni porque esa voz específica es una interpretación de un actor real, protegida por derechos de autor. Lo más cerca que se puede llegar hoy con una app así es: (a) la mejor voz masculina en inglés que tengas instalada en Windows con el tono bajado, que es lo que hace ahora por defecto, o (b) conectar una API de voz premium (Azure Neural, ElevenLabs, etc.) si en algún momento querés ese salto de calidad — no está cableado en este proyecto para no depender de otra cuenta/costo sin que lo pidas explícitamente.

---


App de escritorio para Windows (WPF, .NET 8) que:
- Vive como un **widget flotante** en el escritorio: una esfera de orbes chica, movible, que con un click abre una burbuja de chat rápida.
- Ve tu pantalla (captura de pantalla enviada como imagen a Gemini).
- Responde por texto y por voz.
- Corre en segundo plano (bandeja del sistema).
- Se maneja con atajos globales aunque esté oculta:
  - **Ctrl+Alt+J** → mostrar/ocultar Jarvis.
  - **Ctrl+Alt+K** → abre un **overlay tipo HUD** con un aro de luz neón celeste alrededor del monitor donde está Jarvis, y una burbuja flotante donde escribís vos la pregunta (no se abre el chat). Enter para preguntar, Escape o click afuera para cancelar. La respuesta aparece en la misma burbuja y se lee en voz alta si tenés esa opción activada.
- Deja configurar tu **API key de Google (Gemini)** desde una pantalla de Configuración (⚙), guardada cifrada en tu usuario de Windows (DPAPI), nunca en texto plano.

## Cómo abrirlo en Visual Studio

1. Descomprimí el zip.
2. Abrí `JarvisTeto.sln` con Visual Studio 2022 (necesitás el workload **".NET desktop development"** instalado — si no lo tenés, el instalador de VS te lo ofrece).
3. Si VS te pide restaurar paquetes NuGet, dejalo (solo usa `System.Speech`, todo lo demás es del SDK de Windows).
4. Compilá con **Ctrl+Shift+B** y corré con **F5**.

Si por algún motivo el `.sln` no abre bien, también podés hacer **Archivo → Abrir → Carpeta** y apuntar directo a la carpeta `JarvisTeto` (la del `.csproj`); Visual Studio lo detecta solo.

## Conseguir la API key de Google

1. Andá a **https://aistudio.google.com/apikey**.
2. Creá una key gratis (tiene cuota gratuita generosa para `gemini-2.5-flash`).
3. Pegala en Jarvis: botón ⚙ (Configuración) → API Key → Guardar.

## Notas

- El reconocimiento y la síntesis de voz usan el motor nativo de Windows (`System.Speech`), así que **no necesitan API key aparte** — funcionan offline, en español si tenés el paquete de idioma español instalado en Windows (Configuración → Hora e idioma → Idioma → agregar español si hace falta).
- La primera vez que uses el micrófono, Windows puede pedirte permiso de acceso al micrófono para la app — aceptalo.
- Jarvis solo captura el **monitor donde está posicionada su ventana** (o donde estaba antes de minimizarse), nunca otros monitores conectados — tanto para "adjuntar pantalla" en el chat como para el overlay de Ctrl+Alt+K.
- Cerrar la ventana con la X no cierra la app, la minimiza a la bandeja (así queda corriendo en segundo plano). Para salir del todo, click derecho en el ícono de la bandeja → **Salir**.
- El modelo por defecto es `gemini-3.6-flash` porque es el más rápido; podés cambiar a `gemini-2.5-pro` en Configuración si preferís más precisión sobre velocidad.
- **Voz de Jarvis**: en Configuración podés elegir cuál de las voces instaladas en Windows usar, y ajustar el **tono** (más grave = más robótico/mayordomo) y la **velocidad**. Jarvis no puede clonar la voz real de la película (ni `System.Speech` lo permite ni sería legal), pero con una voz en inglés masculina y el tono bajado se acerca bastante a esa sensación. Si tenés instalado el paquete de voz en inglés británico de Windows (`Microsoft George` u otra `en-GB`), la app la va a preferir automáticamente. Podés instalar más voces desde **Configuración de Windows → Hora e idioma → Voz → Agregar voces**. Usá el botón "▶ Probar voz" para escuchar el resultado antes de guardar.

## Estructura del proyecto

```
JarvisTeto/
  App.xaml(.cs)            → arranque (ahora abre el widget, no la ventana completa), ícono de bandeja, instancia única
  MainWindow.xaml(.cs)      → chat completo, mic, captura de pantalla, atajos
  FloatingWidget.xaml(.cs)  → Jarvis flotando en el escritorio: se arrastra, y un click abre/cierra ChatBubble
  ChatBubble.xaml(.cs)      → burbuja de chat chica que abre el widget al hacer click
  SettingsWindow.xaml(.cs)  → configuración de API key / modelo / voz
  ScreenAskOverlay.xaml(.cs)→ HUD neón + burbuja de pregunta rápida (Ctrl+Alt+K)
  Converters.cs             → estilos de burbujas de chat
  Models/ChatMessage.cs
  Controls/
    OrbVisualizer.xaml(.cs) → la esfera de orbes (reposo / pensando / hablando / escuchando), reutilizada en la ventana grande y en el widget
  Utils/
    TextUtils.cs            → limpieza de Markdown para mostrar y para hablar
  Services/
    GeminiService.cs        → llamadas a la API de Gemini (texto + visión)
    ScreenCaptureService.cs → captura de pantalla a PNG base64
    VoiceService.cs         → reconocimiento y síntesis de voz
    SettingsService.cs      → guarda/lee configuración cifrada (DPAPI), ahora también guarda la posición del widget
    HotkeyManager.cs        → atajos globales (Ctrl+Alt+J / Ctrl+Alt+K)
```

## Ideas para seguir mejorando (no incluidas todavía)

- Streaming real de la respuesta (que el texto vaya apareciendo palabra por palabra).
- Elegir qué monitor capturar si tenés varios.
- Overlay estilo "HUD" en vez de ventana de chat tradicional.
- Ícono propio de Teto para la bandeja del sistema (ahora usa el ícono genérico de Windows).
