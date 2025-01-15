using SuperFlow.Core.Models;

namespace SuperFlow.Core.Tools
{
    /// <summary>
    /// Clase base para acciones, facilita la implementación de IFlowTool.
    /// </summary>
    public abstract class BaseTool : IFlowTool
    {
        public string Name { get; protected set; }

        protected BaseTool(string name)
        {
            Name = name;
        }

        public abstract Task<object?> ExecuteAsync(FlowContext context, dynamic? parameters = null);
    }
}
