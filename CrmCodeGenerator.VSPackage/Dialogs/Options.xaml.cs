#region Imports

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Threading;
using CrmCodeGenerator.VSPackage.Helpers;
using Yagasoft.CrmCodeGenerator;
using Yagasoft.CrmCodeGenerator.Cache.Metadata;
using Yagasoft.CrmCodeGenerator.Connection;
using Yagasoft.CrmCodeGenerator.Connection.OrgSvcs;
using Yagasoft.CrmCodeGenerator.Helpers;
using Yagasoft.CrmCodeGenerator.Models.Settings;
using Application = System.Windows.Forms.Application;

#endregion

namespace CrmCodeGenerator.VSPackage.Dialogs
{
	/// <summary>
	///     Interaction logic for Filter.xaml
	/// </summary>
	public partial class Options : INotifyPropertyChanged
	{
		private readonly IConnectionManager<IDisposableOrgSvc> connectionManager;
		private readonly MetadataCacheManagerBase metadataCacheManager;

		#region Hide close button stuff

		private const int GWL_STYLE = -16;
		private const int WS_SYSMENU = 0x80000;

		[DllImport("user32.dll", SetLastError = true)]
		private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

		[DllImport("user32.dll")]
		private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

		#endregion

		#region Properties

		public Settings Settings { get; set; }

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

		#region Property events

		public event PropertyChangedEventHandler PropertyChanged;

		protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			var handler = PropertyChanged;
			handler?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		#endregion

		#region Init

		public Options(Window parentWindow, Settings settings,
			IConnectionManager<IDisposableOrgSvc> connectionManager, MetadataCacheManagerBase metadataCacheManager)
		{
			InitializeComponent();

			this.connectionManager = connectionManager;
			this.metadataCacheManager = metadataCacheManager;

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
			CheckBoxGenerateGlobalActions.DataContext = Settings;

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

		#region UI events

		private void LoadGlobalActions_Click(object sender, RoutedEventArgs e)
		{
			new Thread(
				() =>
				{
					try
					{
						Status.ShowBusy(Dispatcher, BusyIndicator, "Loading Global Actions ...");
						var actions = MetadataHelpers.RetrieveActionNames(Settings, connectionManager, metadataCacheManager).ToArray();
						Dispatcher.Invoke(
							() =>
							{
								GlobalActionNames = new ObservableCollection<string>(actions);
								SelectedGlobalActions = new ObservableCollection<string>(Settings.SelectedGlobalActions?.Intersect(actions)
									?? Array.Empty<string>());
								IsGlobalActionsVisible = true;
							});
					}
					catch (Exception ex)
					{
						Status.PopException(Dispatcher, ex);
					}
					finally
					{
						Status.HideBusy(Dispatcher, BusyIndicator);
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
