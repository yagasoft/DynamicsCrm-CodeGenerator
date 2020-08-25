using System;
using Yagasoft.CrmCodeGenerator;
using Yagasoft.CrmCodeGenerator.Mapper;

namespace Yagasoft.CrmCodeGenerator.Models.Mapper
{
    public class MapperEventArgs : EventArgs
    {
        public string Message { get; set; }
        public int? Progress { get; set; }
	    public Exception Exception { get; set; }
	    public MapperStatus Status { get; set; }
    }
}
