using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace JackboxGPT.Extensions
{
    public static class CollectionExtensions
    {
        private static readonly Random _random = new();

        public static int RandomIndex(this ICollection collection)
        {
            return _random.Next(0, collection.Count);
        }

        public static IEnumerable<T> Shuffle<T>(this IEnumerable<T> collection)
        {
            return collection.OrderBy(_ => _random.Next());
        }
    }
}
