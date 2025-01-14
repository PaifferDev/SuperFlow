namespace SuperFlow.Core.Actions
{
    /// <summary>
    /// Registro estático de acciones para que Steps las obtengan por nombre.
    /// </summary>
    public static class ActionRegistry
    {
        private static readonly Dictionary<string, IFlowAction> _actions = new();

        public static void RegisterAction(IFlowAction action)
        {
            _actions[action.Name] = action;
        }

        public static IFlowAction? GetAction(string name)
        {
            _actions.TryGetValue(name, out var action);
            return action;
        }

        public static IEnumerable<IFlowAction> ListActions()
        {
            return _actions.Values.ToList();
        }
    }
}
