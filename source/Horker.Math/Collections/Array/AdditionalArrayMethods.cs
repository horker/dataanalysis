﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using Accord.Math;
using Accord.Math.Random;

namespace Horker.Math.ArrayMethods
{
    public class AdditionalMethods
    {
        internal static object[][] SplitInternal(IReadOnlyList<object> values, double[] rates)
        {
            var results = new List<object[]>();

            int total = values.Count;

            int start = 0;
            foreach (var r in rates)
            {
                var size = (int)System.Math.Round(r >= 1 ? r : total * r);
                if (size > total - start)
                    size = total - start;

                var result = new object[size];
                for (var i = 0; i < size; ++i)
                    result[i] = values[start + i];
                results.Add(result);

                start += size;
                if (start >= total)
                    break;
            }

            if (start < total)
            {
                var size = total - start;
                var result = new object[size];
                for (var i = 0; i < size; ++i)
                    result[i] = values[start + i];
                results.Add(result);
            }

            return results.ToArray();
        }

        public static object[][] Split(PSObject values, double[] rates)
        {
            var results = new List<object[]>();

            var array = Helper.GetObjectArray(values);

            return SplitInternal(array, rates);
        }

        public static object[] DropNa(PSObject values)
        {
            var array = Helper.GetObjectArray(values);

            var result = new List<object>();
            foreach (var value in array)
            {
                if (value == null)
                    continue;
                if (value is double && double.IsNaN((double)value))
                    continue;
                if (value is float && float.IsNaN((float)value))
                    continue;

                result.Add(value);
            }
            return result.ToArray();
        }

        public static double[] DropNaN(PSObject values)
        {
            var array = Converter.ToDoubleArray(values);
            return array.RemoveAll(double.NaN);
        }

    }
}
