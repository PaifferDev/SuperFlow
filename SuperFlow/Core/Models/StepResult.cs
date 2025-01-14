namespace SuperFlow.Core.Models
{
    /// <summary>
    /// Representa el resultado de la ejecución de un Step.
    /// </summary>
    public class StepResult
    {
        public bool IsSuccess { get; set; } = true;
        public string ResultCode { get; set; } = "OK";
        public string? Message { get; set; }
        public object? Data { get; set; }
    }
}
