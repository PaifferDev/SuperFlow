using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SuperFlow.Core.Default.Tools.SNSTool.Contracts
{
	public interface ISQSReceiver
	{
		Task<string> PublishMessageAsync(string message, string subject = "");
	}
}
