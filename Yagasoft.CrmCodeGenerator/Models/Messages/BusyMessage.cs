using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Yagasoft.CrmCodeGenerator.Models.Messages
{
	public class BusyMessage<TStyle>
	{
		public Guid? Id;
		public string Message;
		public TStyle Style;
		public Action Finished;
		public Action<int> FinishedProgress;
	}
}
