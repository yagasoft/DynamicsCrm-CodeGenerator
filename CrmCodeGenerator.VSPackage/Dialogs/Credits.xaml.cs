#region Imports

using System.Windows;
using System.Windows.Input;

#endregion

namespace CrmCodeGenerator.VSPackage.Dialogs
{
	/// <summary>
	///     Interaction logic for Credits.xaml
	/// </summary>
	public partial class Credits
	{
		#region Init

		public Credits(Window parentWindow)
		{
			InitializeComponent();
			Owner = parentWindow;
		}

		#endregion

		private void Close_Click(object sender, ExecutedRoutedEventArgs e)
		{
			Close();
		}
	}
}
