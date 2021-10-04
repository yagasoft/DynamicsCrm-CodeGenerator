#region Imports

using System;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using CrmCodeGenerator.VSPackage.Dialogs;
using CrmCodeGenerator.VSPackage.Helpers;
using EnvDTE;
using EnvDTE80;

using Microsoft;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.VersionControl.Client;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Yagasoft.CrmCodeGenerator.Helpers.Assembly;
using Task = System.Threading.Tasks.Task;

#endregion

namespace CrmCodeGenerator.VSPackage
{
	/// <summary>
	///     This is the class that implements the package exposed by this assembly.
	///     The minimum requirement for a class to be considered a valid package for Visual Studio
	///     is to implement the IVsPackage interface and register itself with the shell.
	///     This package uses the helper classes defined inside the Managed Package Framework (MPF)
	///     to do it: it derives from the Package class that provides the implementation of the
	///     IVsPackage interface and uses the registration attributes defined in the framework to
	///     register itself and its components with the shell.
	/// </summary>
	// This attribute tells the PkgDef creation utility (CreatePkgDef.exe) that this class is
	// a package.
	[PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
	// This attribute is used to register the information needed to show this package
	// in the Help/About dialog of Visual Studio.
	[InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)]
	//this causes the class to load when VS starts [ProvideAutoLoad("ADFC4E64-0397-11D1-9F4E-00A0C911004F")]
	//[ProvideAutoLoad(VSConstants.UICONTEXT.SolutionHasSingleProject_string)]
	//[ProvideAutoLoad(VSConstants.UICONTEXT.SolutionHasMultipleProjects_string)]
	// This attribute is needed to let the shell know that this package exposes some menus.
	[ProvideMenuResource("Menus.ctmenu", 1)]
	[Guid(GuidList.guidCrmCodeGenerator_VSPackagePkgString)]
	public sealed class CrmCodeGenerator_VSPackagePackage : AsyncPackage, IVsSolutionEvents3
	{
		/// <summary>
		///     Default constructor of the package.
		///     Inside this method you can place any initialization code that does not require
		///     any Visual Studio service because at this point the package object is created but
		///     not sited yet inside Visual Studio environment. The place to do all the other
		///     initialization is the Initialize method.
		/// </summary>
		public CrmCodeGenerator_VSPackagePackage()
		{
			Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "Entering constructor for: {0}", ToString()));
		}


		/////////////////////////////////////////////////////////////////////////////
		// Overridden Package Implementation

		#region Package Members

		/// <summary>
		///     Initialization of the package; this method is called right after the package is sited, so this is the place
		///     where you can put all the initialization code that rely on services provided by VisualStudio.
		/// </summary>
		protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
		{
			////AssemblyHelpers.RedirectAssembly("Microsoft.Xrm.Sdk", new Version("9.0.0.0"), "31bf3856ad364e35");
			////AssemblyHelpers.RedirectAssembly("Microsoft.Xrm.Sdk.Deployment", new Version("9.0.0.0"), "31bf3856ad364e35");
			////AssemblyHelpers.RedirectAssembly("Microsoft.Xrm.Tooling.Connector", new Version("4.0.0.0"), "31bf3856ad364e35");
			////AssemblyHelpers.RedirectAssembly("Microsoft.IdentityModel.Clients.ActiveDirectory",
			////	new Version("3.19.8.16603"), "31bf3856ad364e35");
			////AssemblyHelpers.RedirectAssembly("Newtonsoft.Json", new Version("10.0.0.0"), "30ad4fe6b2a6aeed");

			await JoinableTaskFactory.SwitchToMainThreadAsync(DisposalToken);

			Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "Entering Initialize() of: {0}", ToString()));
			await base.InitializeAsync(cancellationToken, progress);

			// Add our command handlers for menu (commands must exist in the .vsct file)
			if (await GetServiceAsync(typeof (IMenuCommandService)) is OleMenuCommandService mcs)
			{
				var templateCmd = new CommandID(GuidList.guidCrmCodeGenerator_VSPackageCmdSet, (int) PkgCmdIDList.cmdidAddTemplate);
				var templateItem = new MenuCommand((o,e) => AddTemplateCallbackAsync(o, e).Wait(cancellationToken), templateCmd);
				mcs.AddCommand(templateItem);
			}

			await AdviseSolutionEventsAsync();
		}

		protected override void Dispose(bool disposing)
		{
			UnadviseSolutionEvents();

			base.Dispose(disposing);
		}

		private IVsSolution solution;
		private uint _handleCookie;

		private async Task AdviseSolutionEventsAsync()
		{
			await JoinableTaskFactory.SwitchToMainThreadAsync(DisposalToken);

			UnadviseSolutionEvents();

			solution = await GetServiceAsync(typeof (SVsSolution)) as IVsSolution;

			if (solution != null)
			{
				solution.AdviseSolutionEvents(this, out _handleCookie);
			}
		}

		private void UnadviseSolutionEvents()
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			if (solution != null)
			{
				if (_handleCookie != uint.MaxValue)
				{
					solution.UnadviseSolutionEvents(_handleCookie);
					_handleCookie = uint.MaxValue;
				}

				solution = null;
			}
		}

		#endregion

		/// <summary>
		///     This function is the callback used to execute a command when the a menu item is clicked.
		///     See the Initialize method to see how the menu item is associated to this function using
		///     the OleMenuCommandService service and the MenuCommand class.
		/// </summary>
		private async Task AddTemplateCallbackAsync(object sender, EventArgs args)
		{
			await JoinableTaskFactory.SwitchToMainThreadAsync(DisposalToken);

			try
			{
				await AddTemplateAsync();
			}
			catch (UserException e)
			{
				VsShellUtilities.ShowMessageBox(ServiceProvider.GlobalProvider, e.Message, "Error", OLEMSGICON.OLEMSGICON_WARNING,
					OLEMSGBUTTON.OLEMSGBUTTON_OK, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
			}
			catch (Exception e)
			{
				var error = e.Message + "\n" + e.StackTrace;
				MessageBox.Show(error, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}

		private async Task AddTemplateAsync()
		{
			await JoinableTaskFactory.SwitchToMainThreadAsync(DisposalToken);

			var dte2 = await GetServiceAsync(typeof(SDTE)) as DTE2;
			Assumes.Present(dte2);

			var project = dte2.GetSelectedProject();

			if (project == null || string.IsNullOrWhiteSpace(project.FullName))
			{
				throw new UserException("Please select a project first");
			}

			var m = new AddTemplate(dte2, project);

			m.Closed +=
				(sender, e) =>
				{
					ThreadHelper.ThrowIfNotOnUIThread();

					// logic here Will be called after the child window is closed
					if (((AddTemplate)sender).Canceled)
					{
						return;
					}

					var templatePath = Path.GetFullPath(Path.Combine(project.GetPath(), m.Props.Template));
					//GetFullpath removes un-needed relative paths  (ie if you are putting something in the solution directory)

					if (File.Exists(templatePath))
					{
						var results = VsShellUtilities.ShowMessageBox(ServiceProvider.GlobalProvider,
							"'" + templatePath + "' already exists, are you sure you want to overwrite?", "Overwrite",
							OLEMSGICON.OLEMSGICON_QUERY, OLEMSGBUTTON.OLEMSGBUTTON_YESNOCANCEL,
							OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
						if (results != 6)
						{
							return;
						}

						//if the window is open we have to close it before we overwrite it.
						var pi = project.GetProjectItem(m.Props.Template);
						if (pi != null && pi.Document != null)
						{
							pi.Document.Close(vsSaveChanges.vsSaveChangesNo);
						}
					}

					var templateSamplesPath = Path.Combine(DteHelper.AssemblyDirectory(), @"Resources\Templates");
					var defaultTemplatePath = Path.Combine(templateSamplesPath, m.DefaultTemplate.SelectedValue.ToString());
					if (!File.Exists(defaultTemplatePath))
					{
						throw new UserException("T4Path: " + defaultTemplatePath + " is missing or you can't access it.");
					}

					var dir = Path.GetDirectoryName(templatePath);
					if (!Directory.Exists(dir))
					{
						Directory.CreateDirectory(dir);
					}

					Status.Update("[Template] Adding " + templatePath + " to project ... ");
					// When you add a TT file to visual studio, it will try to automatically compile it, 
					// if there is error (and there will be error because we have custom generator) 
					// the error will persit until you close Visual Studio. The solution is to add 
					// a blank file, then overwrite it
					// http://stackoverflow.com/questions/17993874/add-template-file-without-custom-tool-to-project-programmatically
					var blankTemplatePath = Path.Combine(DteHelper.AssemblyDirectory(), @"Resources\Templates\_Blank.tt");
					// check out file if in TFS
					try
					{
						var workspaceInfo = Workstation.Current.GetLocalWorkspaceInfo(templatePath);

						if (workspaceInfo != null)
						{
							var server = new TfsTeamProjectCollection(workspaceInfo.ServerUri);
							var workspace = workspaceInfo.GetWorkspace(server);
							workspace.PendEdit(templatePath);
							Status.Update("[Template] Checked out template file from TFS' current workspace.");
						}
					}
					catch (Exception)
					{
						// ignored
					}

					try
					{
						File.Copy(blankTemplatePath, templatePath, true);
					}
					catch (Exception ex)
					{
						var error = ex.Message + "\n" + ex.StackTrace;
						MessageBox.Show(error, "Error", MessageBoxButton.OK, MessageBoxImage.Error);

						throw;
					}

					Status.Update("[Template] [DONE] Adding template file to project.");

					var p = project.ProjectItems.AddFromFile(templatePath);
					p.Properties.SetValue("CustomTool", "");

					File.Copy(defaultTemplatePath, templatePath, true);
					p.Properties.SetValue("CustomTool", nameof(CrmCodeGenerator2011));
				};

			m.ShowModal();
		}

		#region SolutionEvents

		public int OnAfterCloseSolution(object pUnkReserved)
		{
			return VSConstants.S_OK;
		}

		public int OnAfterClosingChildren(IVsHierarchy pHierarchy)
		{
			return VSConstants.S_OK;
		}

		public int OnAfterLoadProject(IVsHierarchy pStubHierarchy, IVsHierarchy pRealHierarchy)
		{
			return VSConstants.S_OK;
		}

		public int OnAfterMergeSolution(object pUnkReserved)
		{
			return VSConstants.S_OK;
		}

		public int OnAfterOpenProject(IVsHierarchy pHierarchy, int fAdded)
		{
			return VSConstants.S_OK;
		}

		public int OnAfterOpenSolution(object pUnkReserved, int fNewSolution)
		{
			return VSConstants.S_OK;
		}

		public int OnAfterOpeningChildren(IVsHierarchy pHierarchy)
		{
			return VSConstants.S_OK;
		}

		public int OnBeforeCloseProject(IVsHierarchy pHierarchy, int fRemoved)
		{
			return VSConstants.S_OK;
		}

		public int OnBeforeCloseSolution(object pUnkReserved)
		{
			return VSConstants.S_OK;
		}

		public int OnBeforeClosingChildren(IVsHierarchy pHierarchy)
		{
			return VSConstants.S_OK;
		}

		public int OnBeforeOpeningChildren(IVsHierarchy pHierarchy)
		{
			return VSConstants.S_OK;
		}

		public int OnBeforeUnloadProject(IVsHierarchy pRealHierarchy, IVsHierarchy pStubHierarchy)
		{
			return VSConstants.S_OK;
		}

		public int OnQueryCloseProject(IVsHierarchy pHierarchy, int fRemoving, ref int pfCancel)
		{
			return VSConstants.S_OK;
		}

		public int OnQueryCloseSolution(object pUnkReserved, ref int pfCancel)
		{
			return VSConstants.S_OK;
		}

		public int OnQueryUnloadProject(IVsHierarchy pRealHierarchy, ref int pfCancel)
		{
			return VSConstants.S_OK;
		}

		#endregion
	}
}
