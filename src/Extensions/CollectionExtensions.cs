using System;
using System.Collections;

namespace JackboxGPT.Extensions
{
    public static class CollectionExtensions
    {
        private static readonly Random _random = new();

        public static int RandomIndex(this ICollection collection)
        {
            return _random.Next(0, collection.Count);
        }
    }
}
