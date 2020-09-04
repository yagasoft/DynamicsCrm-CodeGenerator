#region Imports

using System;
using System.CodeDom.Compiler;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using CrmCodeGenerator.VSPackage.Dialogs;
using CrmCodeGenerator.VSPackage.Helpers;
using CrmCodeGenerator.VSPackage.T4;
using EnvDTE80;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Designer.Interfaces;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextTemplating;
using Microsoft.VisualStudio.TextTemplating.VSHost;
using Yagasoft.CrmCodeGenerator.Models.Mapper;
using Yagasoft.Libraries.Common;
//using VSLangProj80;
using IOleServiceProvider = Microsoft.VisualStudio.OLE.Interop.IServiceProvider;

#endregion

namespace CrmCodeGenerator.VSPackage
{
	public static class vsContextGuids
	{
		public const string vsContextGuidVCSProject = "{FAE04EC1-301F-11D3-BF4B-00C04F79EFBC}";
		public const string vsContextGuidVCSEditor = "{694DD9B6-B865-4C5B-AD85-86356E9C88DC}";
		public const string vsContextGuidVBProject = "{164B10B9-B200-11D0-8C61-00A0C91E29D5}";
		public const string vsContextGuidVBEditor = "{E34ACDC0-BAAE-11D0-88BF-00A0C9110049}";
		public const string vsContextGuidVJSProject = "{E6FDF8B0-F3D1-11D4-8576-0002A516ECE8}";
		public const string vsContextGuidVJSEditor = "{E6FDF88A-F3D1-11D4-8576-0002A516ECE8}";
	}

	// http://blogs.msdn.com/b/vsx/archive/2013/11/27/building-a-vsix-deployable-single-file-generator.aspx
	[ComVisible(true)]
	[Guid(GuidList.guidCrmCodeGenerator_SimpleGenerator)]
	[ProvideObject(typeof(CrmCodeGenerator2011))]
	[CodeGeneratorRegistration(typeof(CrmCodeGenerator2011), "CrmCodeGenerator2011",
		vsContextGuids.vsContextGuidVCSProject, GeneratesDesignTimeSource = true)]
	[CodeGeneratorRegistration(typeof(CrmCodeGenerator2011), "CrmCodeGenerator2011", vsContextGuids.vsContextGuidVBProject,
		GeneratesDesignTimeSource = true)]
	public class CrmCodeGenerator2011 : IVsSingleFileGenerator, IObjectWithSite, IDisposable
	{
		private object site;
		private CodeDomProvider codeDomProvider;
		private ServiceProvider serviceProvider;
		private string extension;
		private Context context;

		private CodeDomProvider CodeProvider
		{
			get
			{
				if (codeDomProvider != null)
				{
					return codeDomProvider;
				}

				var provider = (IVSMDCodeDomProvider)SiteServiceProvider.GetService(typeof(IVSMDCodeDomProvider).GUID);

				if (provider != null)
				{
					codeDomProvider = (CodeDomProvider)provider.CodeDomProvider;
				}

				return codeDomProvider;
			}
		}

		private ServiceProvider SiteServiceProvider
		{
			get
			{
				if (serviceProvider != null)
				{
					return serviceProvider;
				}

				var oleServiceProvider = site as IOleServiceProvider;
				serviceProvider = new ServiceProvider(oleServiceProvider);
				return serviceProvider;
			}
		}

		#region IVsSingleFileGenerator

		public int DefaultExtension(out string pbstrDefaultExtension)
		{
			pbstrDefaultExtension = "." + CodeProvider.FileExtension;

			if (extension != null)
			{
				pbstrDefaultExtension = extension;
			}

			return VSConstants.S_OK;
		}

		public int Generate(string wszInputFilePath, string bstrInputFileContents, string wszDefaultNamespace,
			IntPtr[] rgbOutputFileContents, out uint pcbOutput, IVsGeneratorProgress pGenerateProgress)
		{
			bstrInputFileContents.Require(nameof(bstrInputFileContents));
			wszInputFilePath.RequireNotEmpty(nameof(wszInputFilePath));

			Status.Clear();

			var dte = Package.GetGlobalService(typeof(SDTE)) as DTE2;

			Configuration.FileName = Path.GetFileNameWithoutExtension(wszInputFilePath);

			var project = dte.GetSelectedProject();
			var file = $@"{project.GetPath()}\{Configuration.FileName}.dat";

			if (File.Exists(file))
			{
				var isMigrate = DteHelper.IsConfirmed("Pre-v7 settings found, which will be converted to the current format.\r\n\r\n"
					+ "Only the LAST selected profile will be migrated -- all other profiles will be DELETED.\r\n\r\n"
					+ "If unsure, reinstall pre-v7 Generator and select the profile you would like migrated,"
					+ " and then upgrade again.\r\n\r\n"
					+ "Would you like to proceed?",
					">> WARNING << Settings Migration");

				if (isMigrate)
				{
					isMigrate = DteHelper.IsConfirmed("Only the LAST selected profile will be migrated"
						+ " -- all other profiles will be DELETED.\r\n\r\n"
						+ "Are you sure?",
						">> WARNING << Settings Migration");
				}

				if (!isMigrate)
				{
					return Cancel(wszInputFilePath, rgbOutputFileContents, out pcbOutput);
				}
			}

			var m = new Login(dte);
			m.ShowModal();
			context = m.Context;

			if (context == null)
			{
				return Cancel(wszInputFilePath, rgbOutputFileContents, out pcbOutput);
			}

			Status.Update("[Generator] Generating code from template ... ");

			if (!(Package.GetGlobalService(typeof(STextTemplating)) is ITextTemplating t4))
			{
				throw new ArgumentNullException(nameof(t4), "Failed to build T4 object.");
			}

			if (!(t4 is ITextTemplatingSessionHost sessionHost))
			{
				throw new ArgumentNullException(nameof(sessionHost), "Failed to build Session Host object.");
			}

			context.Namespace = wszDefaultNamespace;
			context.FileName = Configuration.FileName;
			sessionHost.Session = sessionHost.CreateSession();
			sessionHost.Session["Context"] = context;

			var cb = new Callback();
			t4.BeginErrorSession();
			var content = t4.ProcessTemplate(wszInputFilePath, bstrInputFileContents, cb);
			t4.EndErrorSession();

			// Append any error/warning to output window
			foreach (var err in cb.ErrorMessages)
			{
				// The templating system (eg t4.ProcessTemplate) will automatically add error/warning to the ErrorList 
				Status.Update($"[Generator] [{(err.Warning ? "WARN" : "ERROR")}] {err.Message} {err.Line}, {err.Column}");
			}

			// If there was an output directive in the TemplateFile, then cb.SetFileExtension() will have been called.
			if (!string.IsNullOrWhiteSpace(cb.FileExtension))
			{
				extension = cb.FileExtension;
			}

			Status.Update("[Generator] [DONE] Generating code.");

			Status.Update("[Generator] Writing code to disk ... ");

			SaveOutputContent(rgbOutputFileContents, out pcbOutput, content);

			Status.Update("[Generator] [DONE] Writing code.");

			return VSConstants.S_OK;
		}

		private static int Cancel(string wszInputFilePath, IntPtr[] rgbOutputFileContents, out uint pcbOutput)
		{
			var fileName = wszInputFilePath.Replace(".tt", ".cs");

			// http://social.msdn.microsoft.com/Forums/vstudio/en-US/d8d72da3-ddb9-4811-b5da-2a167bbcffed/ivssinglefilegenerator-cancel-code-generation
			if (File.Exists(fileName))
			{
				SaveOutputContent(rgbOutputFileContents, out pcbOutput, File.ReadAllText(fileName));
			}
			else
			{
				SaveOutputContent(rgbOutputFileContents, out pcbOutput, "");
			}

			return VSConstants.S_OK;
		}

		private static void SaveOutputContent(IntPtr[] rgbOutputFileContents, out uint pcbOutput, string content)
		{
			var bytes = Encoding.UTF8.GetBytes(content);
			rgbOutputFileContents[0] = Marshal.AllocCoTaskMem(bytes.Length);
			Marshal.Copy(bytes, 0, rgbOutputFileContents[0], bytes.Length);
			pcbOutput = (uint)bytes.Length;
		}

		#endregion IVsSingleFileGenerator

		#region IObjectWithSite

		public void GetSite(ref Guid riid, out IntPtr ppvSite)
		{
			if (site == null)
			{
				Marshal.ThrowExceptionForHR(VSConstants.E_NOINTERFACE);
			}

			// Query for the interface using the site object initially passed to the generator 
			var punk = Marshal.GetIUnknownForObject(site);
			var hr = Marshal.QueryInterface(punk, ref riid, out ppvSite);
			Marshal.Release(punk);
			ErrorHandler.ThrowOnFailure(hr);
		}

		public void SetSite(object pUnkSite)
		{
			// Save away the site object for later use 
			site = pUnkSite;

			// These are initialized on demand via our private CodeProvider and SiteServiceProvider properties 
			codeDomProvider = null;
			serviceProvider = null;
		}

		#endregion IObjectWithSite

		public void Dispose()
		{
			if (codeDomProvider != null)
			{
				codeDomProvider.Dispose();
				codeDomProvider = null;
			}

			if (serviceProvider == null)
			{
				return;
			}

			serviceProvider.Dispose();
			serviceProvider = null;
		}
	}
}
