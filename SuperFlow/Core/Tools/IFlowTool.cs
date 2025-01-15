using SuperFlow.Core.Models;

namespace SuperFlow.Core.Tools
{
    public interface IFlowTool
    {
        string Name { get; }

        /// <summary>
        /// Ejecuta la acción con un FlowContext y parámetros dinámicos opcionales.
        /// </summary>
        Task<object?> ExecuteAsync(FlowContext context, dynamic? parameters = null);
    }
}
