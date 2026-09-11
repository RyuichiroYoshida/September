#nullable enable
using System;
using System.Collections.Generic;

namespace September.Common.Extensions
{
    public static class LinqExtensions
    {
        private const int DefaultInternalSetCapacity = 7;

        public static IEnumerable<TSource> DistinctBy<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector) => DistinctBy(source, keySelector, null);

        public static IEnumerable<TSource> DistinctBy<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector, IEqualityComparer<TKey>? comparer)
        {
            using IEnumerator<TSource> enumerator = source.GetEnumerator();

            if (enumerator.MoveNext())
            {
                var set = new HashSet<TKey>(DefaultInternalSetCapacity, comparer);
                do
                {
                    TSource element = enumerator.Current;
                    if (set.Add(keySelector(element)))
                    {
                        yield return element;
                    }
                }
                while (enumerator.MoveNext());
            }
        }
    }
}
