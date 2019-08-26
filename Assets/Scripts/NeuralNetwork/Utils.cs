using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

/// <summary>
/// Some helpful functions.
/// </summary>
public static class Utils
{
    // random number generator is initialized on the start of the program
    private static readonly Random RandomGenerator = new Random();

    /// <summary>
    /// Returns random double.
    /// </summary>
    /// <returns>Random double.</returns>
    public static double Rand()
    {
        return RandomGenerator.NextDouble();
    }

    /// <summary>
    /// Returns random double from in range from a to b.
    /// </summary>
    /// <param name="a">Beginning of range.</param>
    /// <param name="b">End of range.</param>
    /// <returns>Random double in specified range.</returns>
    public static double RandBetween(double a, double b)
    {
        return (RandomGenerator.NextDouble() * (b - a)) + a;
    }

    /// <summary>
    /// Converts value in one range to another range.
    /// </summary>
    /// <param name="val">Value in the first range which is going to be converted to another range.</param>
    /// <param name="min1">Start of first range.</param>
    /// <param name="max1">End of first range.</param>
    /// <param name="min2">Start of second range.</param>
    /// <param name="max2">End of second range.</param>
    /// <param name="clamp">If <c>val</c> is not in the first range, clamp converted value to edges of second range.</param>
    /// <returns>Value in the second range.</returns>
    public static double MapRange(double val, double min1, double max1, double min2, double max2, bool clamp = false)
    {
        double range = max1 - min1;
        if (range == 0)
        {
            return 0;
        }
        double scaledRange = max2 - min2;
        double scale = scaledRange / range;
        double dist = val - min1;
        double scaledDist = dist * scale;
        double result = min2 + scaledDist;
        if (clamp)
        {
            if (result < min2)
            {
                result = min2;
            }
            if (result > max2)
            {
                result = max2;
            }
        }
        return result;
    }

    /// <summary>
    /// Adds key to a dictionary if it doesn't already contains that key.
    /// </summary>
    /// <typeparam name="TKey">Type of the key.</typeparam>
    /// <typeparam name="TValue">Type of the value.</typeparam>
    /// <param name="dict">Dictionary to add key to.</param>
    /// <param name="key">Key name.</param>
    /// <param name="value">Value.</param>
    public static void AddIfNotExists<TKey, TValue>(this Dictionary<TKey, TValue> dict, TKey key, TValue value)
    {
        if (!dict.ContainsKey(key))
        {
            dict.Add(key, value);
        }
    }

    /// <summary>
    /// Adds key to a dictionary if it doesn't already contains that key or updates the value if it does.
    /// </summary>
    /// <typeparam name="TKey">Type of the key.</typeparam>
    /// <typeparam name="TValue">Type of the value.</typeparam>
    /// <param name="dict">Dictionary.</param>
    /// <param name="key">Key name.</param>
    /// <param name="value">Value.</param>
    public static void AddOrUpdate<TKey, TValue>(this Dictionary<TKey, TValue> dict, TKey key, TValue value)
    {
        if (!dict.ContainsKey(key))
        {
            dict.Add(key, value);
        }
        else
        {
            dict[key] = value;
        }
    }

    /// <summary>
    /// Returns number from low discrepancy additive sequence.
    /// </summary>
    /// <param name="index">Index of iteration.</param>
    /// <param name="s">Value of index 0. Must be between 0 and 1.</param>
    /// <param name="a">Number which will be added. Good value is (Math.Sqrt(5) - 1) / 2.</param>
    /// <returns>Number from low discrepancy additive sequence.</returns>
    public static double LowDiscrepancySequence(int index, double s, double a)
    {
        // allows to have maximum possible s if index is 0
        if (index == 0)
        {
            return s;
        }

        for (int i = 0; i < index; i++)
        {
            s = (s + a) % 1;
        }
        return s;
    }

    /// <summary>
    /// Reference Article http://www.codeproject.com/KB/tips/SerializedObjectCloner.aspx
    /// Provides a method for performing a deep copy of an object.
    /// Binary Serialization is used to perform the copy.
    /// </summary>
    public static class ObjectCopier
    {
        /// <summary>
        /// Perform a deep Copy of the object.
        /// </summary>
        /// <typeparam name="T">The type of object being copied.</typeparam>
        /// <param name="source">The object instance to copy.</param>
        /// <returns>The copied object.</returns>
        public static T Clone<T>(T source)
        {
            if (!typeof(T).IsSerializable)
            {
                throw new ArgumentException("The type must be serializable.", "source");
            }

            // Don't serialize a null object, simply return the default for that object
            if (source == null)
            {
                return default;
            }

            IFormatter formatter = new BinaryFormatter();
            Stream stream = new MemoryStream();
            using (stream)
            {
                formatter.Serialize(stream, source);
                stream.Seek(0, SeekOrigin.Begin);
                return (T)formatter.Deserialize(stream);
            }
        }
    }
}
