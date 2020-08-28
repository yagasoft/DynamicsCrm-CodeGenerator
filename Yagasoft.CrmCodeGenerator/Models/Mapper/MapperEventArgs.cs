using System;
using Yagasoft.CrmCodeGenerator;
using Yagasoft.CrmCodeGenerator.Mapper;

namespace Yagasoft.CrmCodeGenerator.Models.Mapper
{
    [Flags]
	public enum StatusMessageTarget
	{
        None = 0b00,
        BusyIndicator = 0b01,
        LogPane = 0b10
	}

    public class MapperEventArgs : EventArgs
    {
        public string Message { get; set; }
        public int? Progress { get; set; }
	    public StatusMessageTarget MessageTarget { get; set; } = StatusMessageTarget.None;
	    public MapperStatus Status { get; set; } = MapperStatus.Idle;
	    public Exception Exception { get; set; }
    }
}
