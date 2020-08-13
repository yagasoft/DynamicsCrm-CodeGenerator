using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using EnvDTE;
using Microsoft.VisualStudio;

namespace CrmCodeGenerator.VSPackage.Helpers
{
    public static class Status
    {
	    private static readonly object lockObj = new object();

        public static void Update(string message, bool newLine = true)
        {
			lock (lockObj)
			{
				var dte = (DTE)Package.GetGlobalService(typeof(SDTE));
				var win = dte.Windows.Item("{34E76E81-EE4A-11D0-AE2E-00A0C90FFFC3}");
				win.Visible = true;

				var outputWindow = (IVsOutputWindow)Package.GetGlobalService(typeof(SVsOutputWindow));
				var guidGeneral = VSConstants.OutputWindowPaneGuid.GeneralPane_guid;
				outputWindow.CreatePane(guidGeneral, "Crm Code Generator", 1, 0);
				outputWindow.GetPane(guidGeneral, out var pane);
				pane.Activate();

				pane.OutputString(message);

				if (newLine)
				{
					pane.OutputString("\n");
				}

				pane.FlushToTaskList();
				Application.DoEvents(); 
			}
        }

	    public static void Clear()
	    {
		    lock (lockObj)
		    {
			    var outputWindow = (IVsOutputWindow)Package.GetGlobalService(typeof(SVsOutputWindow));
			    var guidGeneral = VSConstants.OutputWindowPaneGuid.GeneralPane_guid;
			    outputWindow.CreatePane(guidGeneral, "Crm Code Generator", 1, 0);
			    outputWindow.GetPane(guidGeneral, out var pane);
			    pane.Clear();
			    pane.FlushToTaskList();
			    Application.DoEvents();
		    }
	    }
    }
}
