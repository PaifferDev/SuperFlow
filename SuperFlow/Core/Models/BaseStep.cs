using SuperFlow.Core.Contracts;

namespace SuperFlow.Core.Models
{
    /// <summary>
    /// Clase base para simplificar la creación de Steps.
    /// </summary>
    public abstract class BaseStep : IStep
    {
        public string Name { get; protected set; }

        protected BaseStep(string name)
        {
            Name = name;
        }

        public abstract Task<StepResult> ExecuteAsync(FlowContext context);
    }
}
