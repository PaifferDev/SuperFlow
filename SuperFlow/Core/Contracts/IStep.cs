using System.Threading.Tasks;
using SuperFlow.Core.Models;

namespace SuperFlow.Core.Contracts
{
    /// <summary>
    /// Interfaz base para cada Step del flujo.
    /// </summary>
    public interface IStep
    {
        string Name { get; }

        Task<StepResult> ExecuteAsync(FlowContext context);
    }
}
