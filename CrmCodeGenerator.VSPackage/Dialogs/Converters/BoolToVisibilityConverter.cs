using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace CrmCodeGenerator.VSPackage.Dialogs.Converters
{

	[ValueConversion(typeof(bool), typeof(bool))]
	public class InverseCheckboxBooleanConverter : IValueConverter
	{
		#region IValueConverter Members

		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			value = ((CheckBox) value).IsChecked;
			value = ((bool?) value).HasValue && ((bool?)value).Value;
			return !(bool) value;
		}

		public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			value = ((CheckBox)value).IsChecked;
			value = ((bool?)value).HasValue && ((bool?)value).Value;
			return !(bool)value;
		}

		#endregion
	}

	public class EnumerableNullReplaceConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			var collection = (IEnumerable)value;

			return
				collection
				.Cast<object>()
				.Select(x => x ?? parameter)
				.ToArray();
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		}
	}

	public class NullReplaceConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return value ?? parameter;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return value.Equals(parameter) ? null : value;
		}
	}

	/// <summary>
	/// Call this converter to change a True value to Visible and a False value to Collapsed
	/// http://stackoverflow.com/questions/3128023/wpf-booleantovisibilityconverter-that-converts-to-hidden-instead-of-collapsed-wh
	/// </summary>
	[ValueConversion(typeof(bool), typeof(Visibility))]
    public sealed class BoolToVisibilityConverter : IValueConverter
    {
        public Visibility TrueValue { get; set; }
        public Visibility FalseValue { get; set; }

        public BoolToVisibilityConverter()
        {
            // set defaults
            TrueValue = Visibility.Visible;
            FalseValue = Visibility.Collapsed;
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is bool))
                return null;
            return (bool)value ? TrueValue : FalseValue;
        }

        public object ConvertBack(object value, Type targetType,   object parameter, CultureInfo culture)
        {
            if (Equals(value, TrueValue))
                return true;
            if (Equals(value, FalseValue))
                return false;
            return null;
        }
    }
}
