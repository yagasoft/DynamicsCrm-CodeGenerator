using System.Collections.Generic;

namespace CrmCodeGenerator.VSPackage.Helpers
{
    static class IEnumerablesExtensions
    {
        public static T FirstOr<T>(this IEnumerable<T> source, T alternate)
        {
            foreach (T t in source)
                return t;
            return alternate;
        }
    }
}
