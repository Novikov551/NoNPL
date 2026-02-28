using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NoNPL.Extensions
{
    public static class EnumerableExtensions
    {
        public static bool IsNullOrEmpty<T>([NotNullWhen(false)] this IEnumerable<T> source)
        {
            if (source == null)
            {
                return true;
            }

            if (!source.TryGetNonEnumeratedCount(out var count))
            {
                return !source.Any();
            }

            return count == 0;
        }
    }
}
