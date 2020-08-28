using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CrmCodeGenerator.VSPackage.ViewModels
{
    public class EntityFilterGridRow : GridRow
    {
	    public string Language { get; set; }

	    public bool IsClearFlagEnabled => IsReadOnlyEnabled;

	    public bool IsClearFlag
	    {
		    get => isClearFlag;
		    set
		    {
			    isClearFlag = value;
			    OnPropertyChanged();
		    }
	    }

	    public bool IsReadOnlyEnabled
	    {
		    get; set;
	    }

	    public bool IsReadOnly
	    {
		    get => isReadOnly;
		    set
		    {
			    isReadOnly = value;
			    OnPropertyChanged();
		    }
	    }

	    private bool isReadOnly;
	    private bool isClearFlag;
    }
}
