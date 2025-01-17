namespace SuperFlow.Core.Tools
{
    /// <summary>
    /// Registro estático de acciones para que Steps las obtengan por nombre.
    /// </summary>
    public class ToolRegistry
    {
        private readonly Dictionary<string, IFlowTool> _tools = new();

        public void RegisterTool(IFlowTool tool)
        {
            _tools[tool.Name] = tool;
        }

        public IFlowTool? GetTool(string name)
        {
            _tools.TryGetValue(name, out var tool);
            return tool;
        }

        public IEnumerable<IFlowTool> ListTools()
        {
            return _tools.Values.ToList();
        }
    }
}
