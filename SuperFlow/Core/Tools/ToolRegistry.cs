namespace SuperFlow.Core.Tools
{
    /// <summary>
    /// Registro estático de acciones para que Steps las obtengan por nombre.
    /// </summary>
    public static class ToolRegistry
    {
        private static readonly Dictionary<string, IFlowTool> _tools = new();

        public static void RegisterTool(IFlowTool tool)
        {
            _tools[tool.Name] = tool;
        }

        public static IFlowTool? GetTool(string name)
        {
            _tools.TryGetValue(name, out var tool);
            return tool;
        }

        public static IEnumerable<IFlowTool> ListTools()
        {
            return _tools.Values.ToList();
        }
    }
}
