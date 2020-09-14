using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Controls;
using System.Windows.Threading;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Xceed.Wpf.Toolkit;
using Yagasoft.CrmCodeGenerator.Models.Messages;
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

		public static void PopException(Dispatcher dispatcher, string message)
		{
			dispatcher.Invoke(
				() =>
				{
					MessageBox.Show(message, "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
				}, DispatcherPriority.Send);
		}

		public static void PopException(Dispatcher dispatcher, Exception exception)
		{
			dispatcher.Invoke(
				() =>
				{
					var message = exception.Message
						+ (exception.InnerException != null ? "\n" + exception.InnerException.Message : "");
					MessageBox.Show(message, exception.GetType().FullName, MessageBoxButton.OK, MessageBoxImage.Error);

					var error = exception.BuildExceptionMessage("!! [ERROR]");
					Update(error);
				}, DispatcherPriority.Send);
		}

	    private static Style originalProgressBarStyle;
	    private static readonly ConcurrentStack<BusyMessage<Style>> busyStack = new ConcurrentStack<BusyMessage<Style>>();
	    private static readonly HashSet<Guid> busyPopped = new HashSet<Guid>();

		public static BusyMessage<Style> ShowBusy(Dispatcher dispatcher, BusyIndicator busyIndicator, string message,
			double? progress = null, Guid? popId = null)
		{
			if (busyIndicator == null)
			{
				return null;
			}

			if (progress == null)
			{
				lock (lockObj)
				{
					dispatcher.Invoke(
						() =>
						{
							originalProgressBarStyle = originalProgressBarStyle ?? busyIndicator.ProgressBarStyle;
						});
				}
			}

			return
				dispatcher.Invoke(
				() =>
				{
					Guid? id = null;
					Style style = null;

					if (popId != null)
					{
						busyPopped.Add(popId.Value);

						BusyMessage<Style> top;

						while (busyStack.TryPeek(out top) && busyPopped.Remove(top?.Id ?? Guid.Empty))
						{
							busyStack.TryPop(out _);
						}

						if (busyStack.TryPeek(out top))
						{
							id = top.Id;
							message = top.Message;
							style = top.Style;
						}
						else
						{
							HideBusy(dispatcher, busyIndicator);
							return null;
						}
					}

					busyIndicator.IsBusy = true;
					busyIndicator.BusyContent = string.IsNullOrEmpty(message) ? "Please wait ..." : message;

					if (style == null)
					{
						if (progress == null)
						{
							style = originalProgressBarStyle;
						}
						else
						{
							style = new Style(typeof(ProgressBar));
							style.Setters.Add(new Setter(FrameworkElement.HeightProperty, 10d));
							style.Setters.Add(new Setter(RangeBase.ValueProperty, progress));
							style.Setters.Add(new Setter(RangeBase.MaximumProperty, 100d));
						}
					}

					busyIndicator.ProgressBarStyle = style;

					BusyMessage<Style> busyMessage = null;

					if (id == null)
					{
						busyMessage =
							new BusyMessage<Style>
							{
								Id = Guid.NewGuid(),
								Message = message,
								Style = progress.HasValue ? null : style
							};
						busyMessage.Finished = () => ShowBusy(dispatcher, busyIndicator, message, null, busyMessage.Id);
						busyMessage.FinishedProgress = progressQ => ShowBusy(dispatcher, busyIndicator, message,
							progress.HasValue ? progressQ : (double?)null, busyMessage.Id);
						busyStack.Push(busyMessage);
					}

					return busyMessage;
				}, DispatcherPriority.Loaded);
		}

		public static void HideBusy(Dispatcher dispatcher, BusyIndicator busyIndicator)
		{
			if (busyIndicator == null)
			{
				return;
			}

			dispatcher.Invoke(
				() =>
				{
					busyStack.Clear();
					busyPopped.Clear();
					busyIndicator.IsBusy = false;
					busyIndicator.BusyContent = "Please wait ...";
				}, DispatcherPriority.Loaded);
		}
    }
}
