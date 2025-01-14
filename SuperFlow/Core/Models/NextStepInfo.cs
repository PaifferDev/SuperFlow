namespace SuperFlow.Core.Models
{
    /// <summary>
    /// Clase que describe la siguiente transición.
    /// </summary>
    public class NextStepInfo
    {
        public TransitionType TransitionType { get; set; } = TransitionType.Single;
        public List<string> StepNames { get; set; } = new List<string>();
    }
}
