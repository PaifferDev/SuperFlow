using SuperFlow.Core.Models;

namespace SuperFlow.Core.Actions
{
    /// <summary>
    /// Clase base para acciones, facilita la implementación de IFlowAction.
    /// </summary>
    public abstract class BaseAction : IFlowAction
    {
        public string Name { get; protected set; }

        protected BaseAction(string name)
        {
            Name = name;
        }

        public abstract Task<object?> ExecuteAsync(FlowContext context, dynamic? parameters = null);
    }
}
