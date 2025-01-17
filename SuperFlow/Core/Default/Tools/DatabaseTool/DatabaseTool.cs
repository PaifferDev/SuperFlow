using Dapper;
using SuperFlow.Core.Models;
using SuperFlow.Core.Tools;
using System.Data;

namespace SuperFlow.Core.Default.Tools.DatabaseTool
{
	public class DatabaseTool : BaseTool
	{
		private readonly DatabaseToolConfig _config;

		public DatabaseTool(string name, DatabaseToolConfig config) : base(name)
		{
			_config = config ?? throw new ArgumentNullException(nameof(config));
		}

		public override async Task<object?> ExecuteAsync(FlowContext context, dynamic? parameters = null)
		{
			var args = parameters as DatabaseToolParameters;
			if (args == null)
				throw new ArgumentException("Se requieren parámetros de tipo DatabaseToolParameters");

			using IDbConnection db = _config.ConnectionFactory();
			if (args.Operation == DatabaseOperation.Query)
			{
				var result = await db.QueryAsync<dynamic>(args.Query, args.Parameters);
				return result.ToList();
			}
			else if (args.Operation == DatabaseOperation.Execute)
			{
				var affectedRows = await db.ExecuteAsync(args.Query, args.Parameters);
				return new { AffectedRows = affectedRows };
			}
			else
			{
				throw new ArgumentException("Operación no soportada. Use 'query' o 'execute'.");
			}
		}
	}

	public class DatabaseToolConfig
	{
		public Func<IDbConnection> ConnectionFactory { get; set; }
	}

	public class DatabaseToolParameters
	{
		public DatabaseOperation Operation { get; set; } = DatabaseOperation.Query;
		public string Query { get; set; } = "";
		public object? Parameters { get; set; }
	}
	public enum DatabaseOperation
	{
		Query,
		Execute
	}
}
