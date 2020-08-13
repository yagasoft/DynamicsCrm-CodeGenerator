#region Imports

using System;
using System.CodeDom;
using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using CrmCodeGenerator.VSPackage.Helpers;
using CrmCodeGenerator.VSPackage.Model;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata.Query;
using Microsoft.Xrm.Sdk.Query;
using Application = System.Windows.Forms.Application;
using MultiSelectComboBoxClass = CrmCodeGenerator.Controls.MultiSelectComboBox;

#endregion

namespace CrmCodeGenerator.VSPackage.Dialogs
{
	/// <summary>
	///     Interaction logic for Filter.xaml
	/// </summary>
	public partial class Options : INotifyPropertyChanged
	{
		#region Hide close button stuff

		private const int GWL_STYLE = -16;
		private const int WS_SYSMENU = 0x80000;

		[DllImport("user32.dll", SetLastError = true)]
		private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

		[DllImport("user32.dll")]
		private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

		#endregion

		#region Properties

		public SettingsNew Settings { get; set; }

		public ObservableCollection<string> GlobalActionNames
		{
			get => globalActionNames;
			set
			{
				globalActionNames = value;
				OnPropertyChanged();
			}
		}

		public ObservableCollection<string> SelectedGlobalActions
		{
			get => selectedGlobalActions;
			set
			{
				selectedGlobalActions = value;
				OnPropertyChanged();
			}
		}

		public bool IsGlobalActionsVisible
		{
			get => isGlobalActionsVisible;
			set
			{
				isGlobalActionsVisible = value;
				OnPropertyChanged();
				OnPropertyChanged("IsGlobalActionsNotVisible");
			}
		}

		public bool IsGlobalActionsNotVisible => !IsGlobalActionsVisible;

		#endregion

		private ObservableCollection<string> globalActionNames = new ObservableCollection<string>();
		private ObservableCollection<string> selectedGlobalActions = new ObservableCollection<string>();
		private bool isGlobalActionsVisible;
		private Style originalProgressBarStyle;

		#region Property events

		public event PropertyChangedEventHandler PropertyChanged;

		protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			var handler = PropertyChanged;
			handler?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		#endregion

		#region Init

		public Options(Window parentWindow, SettingsNew settings)
		{
			InitializeComponent();

			Owner = parentWindow;
			Settings = settings;
		}

		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			// hide close button
			var hwnd = new WindowInteropHelper(this).Handle;
			SetWindowLong(hwnd, GWL_STYLE, GetWindowLong(hwnd, GWL_STYLE) & ~WS_SYSMENU);

			DataContext = Settings;

			GlobalActionsSection.DataContext = this;

			IntSpinnerThreads.Value = Settings.Threads;
			IntSpinnerEntitiesPerThread.Value = Settings.EntitiesPerThread;

			ComboBoxClearMode.ItemsSource = Enum.GetValues(typeof(ClearModeEnum)).Cast<ClearModeEnum>();
		}

		protected override void OnSourceInitialized(EventArgs e)
		{
			base.OnSourceInitialized(e);
			this.HideMinimizeAndMaximizeButtons();
		}

		#endregion

		#region Status stuff

		private void PopException(Exception exception)
		{
			Dispatcher.Invoke(() =>
			                  {
				                  var message = exception.Message
				                                + (exception.InnerException != null ? "\n" + exception.InnerException.Message : "");
				                  MessageBox.Show(message, exception.GetType().FullName, MessageBoxButton.OK, MessageBoxImage.Error);

				                  var error = "[ERROR] " + exception.Message
				                              +
				                              (exception.InnerException != null
					                               ? "\n" + "[ERROR] " + exception.InnerException.Message
					                               : "");
				                  UpdateStatus(error, false);
				                  UpdateStatus(exception.StackTrace, false);
			                  });
		}

		private void ShowBusy(string message, double? progress = null)
		{
			Dispatcher.Invoke(() =>
			                  {
				                  BusyIndicator.IsBusy = true;
				                  BusyIndicator.BusyContent =
					                  string.IsNullOrEmpty(message) ? "Please wait ..." : message;

				                  if (progress == null)
				                  {
					                  BusyIndicator.ProgressBarStyle = originalProgressBarStyle ?? BusyIndicator.ProgressBarStyle;
				                  }
				                  else
				                  {
					                  originalProgressBarStyle = originalProgressBarStyle ?? BusyIndicator.ProgressBarStyle;

					                  var style = new Style(typeof (ProgressBar));
					                  style.Setters.Add(new Setter(HeightProperty, 15d));
					                  style.Setters.Add(new Setter(RangeBase.ValueProperty, progress));
					                  style.Setters.Add(new Setter(RangeBase.MaximumProperty, 100d));
					                  BusyIndicator.ProgressBarStyle = style;
				                  }
			                  }, DispatcherPriority.Send);
		}

		private void HideBusy()
		{
			Dispatcher.Invoke(() =>
			                  {
				                  BusyIndicator.IsBusy = false;
				                  BusyIndicator.BusyContent = "Please wait ...";
			                  }, DispatcherPriority.Send);
		}

		internal void UpdateStatus(string message, bool working, bool allowBusy = true, bool newLine = true)
		{
			//Dispatcher.Invoke(() => SetEnabledChildren(Inputs, !working, "ButtonCancel"));

			if (allowBusy)
			{
				if (working)
				{
					ShowBusy(message);
				}
				else
				{
					HideBusy();
				}
			}

			if (!string.IsNullOrWhiteSpace(message))
			{
				Dispatcher.BeginInvoke(new Action(() => { Status.Update(message, newLine); }));
			}

			Application.DoEvents();
		}

		#endregion

		#region UI events

		private void LoadGlobalActions_Click(object sender, RoutedEventArgs e)
		{
			new Thread(
				() =>
				{
					try
					{
						ShowBusy("Loading Global Actions ...");
						var actions = EntityHelper.RetrieveActionNames(Settings);
						Dispatcher.Invoke(
							() =>
							{
								GlobalActionNames = new ObservableCollection<string>(actions);
								SelectedGlobalActions = new ObservableCollection<string>(Settings.SelectedGlobalActions ?? Array.Empty<string>());
								IsGlobalActionsVisible = true;
							});
					}
					catch (Exception ex)
					{
						PopException(ex);
					}
					finally
					{
						HideBusy();
					}
				}).Start();
		}

		private void Close_Click(object sender, RoutedEventArgs e)
		{
			if (IntSpinnerThreads.Value.HasValue)
			{
				Settings.Threads = IntSpinnerThreads.Value.Value;
			}

			if (IntSpinnerEntitiesPerThread.Value.HasValue)
			{
				Settings.EntitiesPerThread = IntSpinnerEntitiesPerThread.Value.Value;
			}

			if (IsGlobalActionsVisible)
			{
				Settings.SelectedGlobalActions = SelectedGlobalActions.ToArray();
			}

			Dispatcher.InvokeAsync(Close);
		}

		#endregion
	}
}
