namespace SuperFlow.Core.Models
{
    /// <summary>
    /// Contexto global que viaja entre Steps. 
    /// Aquí se pueden inyectar servicios, DbContext, etc.
    /// </summary>
    public class FlowContext
    {
        public IServiceProvider? ServiceProvider { get; set; }

        // Datos genéricos que se quieran compartir
        public Dictionary<string, object?> Data { get; private set; } = new Dictionary<string, object?>();
    }
}
