# SuperFlow

**SuperFlow** es una librería .NET Core diseñada para **crear bots** y procesos automatizados basados en “Steps” (pasos) y “Actions” (acciones reutilizables). Gracias a su **motor de flujos**, permite orquestar de forma flexible la lógica de tu aplicación, definiendo transiciones condicionales, paralelas y secuenciales, todo con un alto grado de reutilización y facilidad de mantenimiento.

## Características Principales

1. **Creación de Bots**  
   - Estructura tus flujos de bot como una serie de Steps configurables.  
   - Define fácilmente qué hacer en cada paso (ej. llamar a un API, resolver un captcha, enviar mensajes) y a dónde ir si hay error o éxito.

2. **Motor de Flujos (FlowEngine)**  
   - Ejecuta tus _Steps_ en un orden configurable.  
   - Permite transiciones **dinámicas** (si un step retorna “X”, voy al StepA; si retorna “Y”, voy al StepB).  
   - Soporta **ejecución en paralelo** de varios Steps y combina los resultados.

3. **Steps y Actions**  
   - **Steps**: representan unidades de trabajo (por ejemplo, un “paso” de tu bot) que reciben un `FlowContext` y devuelven un `StepResult`.  
   - **Actions**: operaciones reutilizables (enviar mensajes a Telegram, encriptar, peticiones HTTP, etc.) que puedes registrar en `ActionRegistry` y llamar desde tus Steps.

4. **FlowContext**  
   - Un contenedor que provee **datos** y **servicios** a los Steps (por ejemplo: HttpClientFactory, DbContext, configuración).  
   - Facilita la inyección de dependencias con `IServiceCollection`.

5. **Inyección de Dependencias**  
   - Métodos de extensión (`AddSuperFlowCore`) que te permiten configurar **HttpClient** con Polly, logger por consola (o el tuyo propio), etc.  
   - Integra con cualquier contenedor de DI.

6. **Logging y Resiliencia**  
   - Logger de consola por defecto (`ConsoleFlowLogger`).  
   - Posibilidad de integrar Serilog, NLog o cualquier `IFlowLogger`.  
   - Uso de **Polly** para reintentos y manejo de fallos en requests HTTP, ideal para bots que operan en entornos poco confiables.

## Instalación

Este paquete se distribuye como un **paquete NuGet**. Para instalarlo:

```bash
dotnet add package SuperFlow
