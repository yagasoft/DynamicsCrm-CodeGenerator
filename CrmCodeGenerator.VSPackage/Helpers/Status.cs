using System;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Controls;
using System.Windows.Threading;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Xceed.Wpf.Toolkit;
using Yagasoft.Libraries.Common;
using Application = System.Windows.Forms.Application;
using MessageBox = System.Windows.MessageBox;

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

		public static void PopException(Dispatcher dispatcher, Exception exception)
		{
			dispatcher.InvokeAsync(
				() =>
				{
					var message = exception.Message
						+ (exception.InnerException != null ? "\n" + exception.InnerException.Message : "");
					MessageBox.Show(message, exception.GetType().FullName, MessageBoxButton.OK, MessageBoxImage.Error);

					var error = exception.BuildExceptionMessage("[ERROR]");
					UpdateStatus(dispatcher, error, false);
				}, DispatcherPriority.Loaded);
		}

		public static void ShowBusy(Dispatcher dispatcher, BusyIndicator busyIndicator, string message,
			DependencyProperty heightProperty, Style originalProgressBarStyle = null, double? progress = null)
		{
			if (busyIndicator == null)
			{
				return;
			}

			dispatcher.InvokeAsync(
				() =>
				{
					busyIndicator.IsBusy = true;
					busyIndicator.BusyContent =
						string.IsNullOrEmpty(message) ? "Please wait ..." : message;

					if (progress == null)
					{
						busyIndicator.ProgressBarStyle = originalProgressBarStyle ?? busyIndicator.ProgressBarStyle;
					}
					else
					{
						var style = new Style(typeof(ProgressBar));
						style.Setters.Add(new Setter(heightProperty, 15d));
						style.Setters.Add(new Setter(RangeBase.ValueProperty, progress));
						style.Setters.Add(new Setter(RangeBase.MaximumProperty, 100d));
						busyIndicator.ProgressBarStyle = style;
					}
				}, DispatcherPriority.Loaded);
		}

		public static void HideBusy(Dispatcher dispatcher, BusyIndicator busyIndicator)
		{
			if (busyIndicator == null)
			{
				return;
			}

			dispatcher.InvokeAsync(
				() =>
				{
					busyIndicator.IsBusy = false;
					busyIndicator.BusyContent = "Please wait ...";
				}, DispatcherPriority.Loaded);
		}

		public static void UpdateStatus(Dispatcher dispatcher, string message, bool working, bool allowBusy = true, bool newLine = true,
			BusyIndicator busyIndicator = null, Style originalProgressBarStyle = null, DependencyProperty heightProperty = null)
		{
			if (allowBusy)
			{
				if (working)
				{
					ShowBusy(dispatcher, busyIndicator, message, heightProperty, originalProgressBarStyle);
				}
				else
				{
					HideBusy(dispatcher, busyIndicator);
				}
			}

			if (!string.IsNullOrWhiteSpace(message))
			{
				Update(message, newLine);
			}

			Application.DoEvents();
			// Needed to allow the output window to update (also allows the cursor wait and form disable to show up)
		}
    }
}
