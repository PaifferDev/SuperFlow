using System;
using SuperFlow.Core.Models;

namespace SuperFlow.Core.Contracts
{
    public interface IFlowLogger
    {
        void LogFlowStart();
        void LogFlowEnd();
        void LogStepStart(string stepName);
        void LogStepEnd(string stepName, StepResult result);
        void LogParallelStepStart(IEnumerable<string> stepNames);
        void LogParallelStepEnd(Dictionary<string, StepResult> results);
        void LogError(string message, Exception? ex = null);
    }
}
