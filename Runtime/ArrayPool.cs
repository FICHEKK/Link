using System;
using System.Collections.Generic;

namespace Link
{
    /// <summary>
    /// Component that stores reusable arrays of various sizes to avoid frequent memory allocations.
    /// </summary>
    internal static class ArrayPool
    {
        /// <summary>
        /// Initializes buckets that contain array instances.
        /// </summary>
        static ArrayPool()
        {
            for (var i = 0; i < Buckets.Length; i++)
            {
                Buckets[i] = new Queue<byte[]>();
            }
        }
        
        /// <summary>
        /// Buckets that store array instances. Each subsequent bucket stores arrays 
        /// that have double the size of previous bucket. First bucket contains arrays
        /// of size <see cref="Packet.MaxSize"/>.
        /// </summary>
        private static readonly Queue<byte[]>[] Buckets = new Queue<byte[]>[9];
        
        /// <summary>
        /// Maximum possible array size that can be requested from the pool or returned to the pool.
        /// </summary>
        internal static int MaxSize => Packet.MaxSize * 256;

        /// <summary>
        /// Gets an array that will have size of at least <see cref="size"/> bytes.
        /// </summary>
        public static byte[] Get(int size)
        {
            if (size < 0)
                throw new InvalidOperationException("Cannot get array with negative size.");
            
            if (size > MaxSize)
                throw new InvalidOperationException($"Cannot get array with size that exceeds {MaxSize} bytes.");

            if (size == 0)
                return Array.Empty<byte>();
            
            var bucketIndex = CalculateBucketIndex(size);
            var bucket = Buckets[CalculateBucketIndex(size)];
            
            lock (Buckets) return bucket.Count > 0 ? bucket.Dequeue() : new byte[Packet.MaxSize * (1 << bucketIndex)];
        }

        /// <summary>
        /// Returns given array to the pool so it can be reused later.
        /// </summary>
        public static void Return(byte[] array)
        {
            if (array is null)
                throw new ArgumentNullException(nameof(array));
            
            if (array.Length > MaxSize)
                throw new InvalidOperationException($"Cannot return array with size that exceeds {MaxSize} bytes.");

            if (array.Length == 0)
                return;
            
            lock (Buckets) Buckets[CalculateBucketIndex(array.Length)].Enqueue(array);
        }

        /// <summary>
        /// Returns the index of a bucket which contains arrays that are at least
        /// equal to or greater than <see cref="minimalLength"/> bytes in size.
        /// </summary>
        internal static int CalculateBucketIndex(int minimalLength)
        {
            // This minus 1 at the end is a bitwise trick, which allows fast calculations of base 2 logarithms.
            var packetsRequired = minimalLength / Packet.MaxSize + (minimalLength % Packet.MaxSize != 0 ? 1 : 0) - 1;
            var bucketIndex = 0;

            // Counts the number of leading zeros.
            while (packetsRequired > 0)
            {
                bucketIndex++;
                packetsRequired >>= 1;
            }

            return bucketIndex;
        }
    }
}
