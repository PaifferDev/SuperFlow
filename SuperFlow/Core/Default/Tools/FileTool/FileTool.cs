using SuperFlow.Core.Models;
using SuperFlow.Core.Tools;

namespace SuperFlow.Core.Default.Tools.FileTool
{
	public class FileTool : BaseTool
	{
		private readonly FileToolConfig _config;

		public FileTool(string name, FileToolConfig config) : base(name)
		{
			_config = config ?? throw new ArgumentNullException(nameof(config));
		}

		public override async Task<object?> ExecuteAsync(FlowContext context, dynamic? parameters = null)
		{
			var args = parameters as FileToolParameters;
			if (args == null)
				throw new ArgumentException("Se requieren parámetros de tipo FileToolParameters");

			string filePath = Path.Combine(_config.BaseDirectory, args.FileName);

			switch (args.Operation.ToLower())
			{
				case "read":
					if (!File.Exists(filePath))
						throw new FileNotFoundException("Archivo no encontrado.", filePath);
					string content = await File.ReadAllTextAsync(filePath);
					return new { Content = content };

				case "write":
					await File.WriteAllTextAsync(filePath, args.Content);
					return new { Status = "File Written", Path = filePath };

				case "delete":
					if (File.Exists(filePath))
						File.Delete(filePath);
					return new { Status = "File Deleted", Path = filePath };

				default:
					throw new ArgumentException("Operación no soportada. Use 'read', 'write' o 'delete'.");
			}
		}
	}

	public class FileToolConfig
	{
		public string BaseDirectory { get; set; } = "./files"; // Directorio base por defecto
	}

	public class FileToolParameters
	{
		public string Operation { get; set; } = "read"; // 'read', 'write', 'delete'
		public string FileName { get; set; } = "default.txt";
		public string? Content { get; set; } // Requerido para 'write'
	}
}
