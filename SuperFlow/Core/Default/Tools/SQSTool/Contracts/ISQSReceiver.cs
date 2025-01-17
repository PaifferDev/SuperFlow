using Amazon.SQS.Model;
using System;
using System.Threading.Tasks;

namespace SuperFlow.Core.Default.Tools.SQSTool
{
	/// <summary>
	/// Interfaz para recibir mensajes de SQS.
	/// </summary>
	public interface ISQSReceiver
	{
		Task<Message?> ReceiveMessageAsync(int waitTimeSeconds);
	}
}
