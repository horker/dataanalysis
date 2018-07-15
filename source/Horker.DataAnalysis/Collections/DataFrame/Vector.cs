﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Text.RegularExpressions;
using Accord;
using Accord.Math;
using Accord.Statistics;
using Accord.Math.Random;
using Accord.Statistics.Moving;
using System.Diagnostics;

namespace Horker.DataAnalysis
{
    public enum CodificationType
    {
        OneHotDropFirst,
        OneHot,
        Multilevel,
        Boolean
    }

    public class Vector : List<object>
    {
        private WeakReference _arrayCache;

        #region Constructors

        public Vector()
            : base()
        {
        }

        public Vector(int capacity)
            : base(capacity)
        {
        }

        public Vector(Vector v)
            : base(v)
        {
        }

        public Vector(IEnumerable data)
            : base()
        {
            foreach (var d in data) {
                Add(d);
            }
        }

        #endregion

        #region Factory methods

        public static Vector Create<T>(IEnumerable<T> data)
        {
            var result = new Vector(data.Count());
            foreach (var d in data) {
                result.Add(d);
            }
            return result;
        }

        public static Vector Create<T>(T[] data)
        {
            var result = new Vector(data.Count());
            foreach (var d in data) {
                result.Add(d);
            }
            return result;
        }

        public static Vector GetDoubleRange(double a, double b, double step = 1.0, bool inclusive = true)
        {
            var result = new Vector();

            if (inclusive) {
                if (a <= b) {
                    if (step <= 0.0) {
                        throw new RuntimeException("Range definition inconsistent");
                    }
                    for (var i = a; i <= b; i += step) {
                        result.Add(i);
                    }
                }
                else {
                    if (step >= 0.0) {
                        throw new RuntimeException("Range definition inconsistent");
                    }
                    for (var i = a; i >= b; i += step) {
                        result.Add(i);
                    }
                }
            }
            else {
                if (a <= b) {
                    if (step <= 0.0) {
                        throw new RuntimeException("Range definition inconsistent");
                    }
                    for (var i = a; i < b; i += step) {
                        result.Add(i);
                    }
                }
                else {
                    if (step >= 0.0) {
                        throw new RuntimeException("Range definition inconsistent");
                    }
                    for (var i = a; i > b; i += step) {
                        result.Add(i);
                    }
                }
            }

            return result;
        }

        public static Vector GetDoubleInterval(double a, double b, int n, bool inclusive = true)
        {
            if (a >= b || n <= 0) {
                throw new RuntimeException("Range definition inconsistent");
            }

            var result = new Vector(n);

            Double step;

            if (inclusive) {
                step = (b - a) / (n - 1);
                for (var i = 0; i < n; ++i) {
                    result.Add(a + step * i);
                }
            }
            else {
                step = (b - a) / n;
                for (var i = 0; i < n; ++i) {
                    result.Add(a + step * i);
                }
            }

            return result;
        }

        public static Vector WithValue(double value, int size)
        {
            var result = new Vector(size);

            for (int i = 0; i < size; ++i) {
                result.Add(value);
            }

            return result;
        }

        public static Vector Zero(int size)
        {
            return WithValue(0, size);
        }

        private void InvalidateCache()
        {
            _arrayCache.Target = null;
        }

        #endregion

        #region Conversions

        public double[] ToDoubleArray()
        {
            if (_arrayCache != null) {
                if (_arrayCache.IsAlive && _arrayCache.Target != null) {
                    return (double[])_arrayCache.Target;
                }
            }
            else {
                _arrayCache = new WeakReference(null);
            }

            var result = new double[Count];

            for (var i = 0; i < Count; ++i) {
                result[i] = Converter.ToDouble(this[i]);
            }

            _arrayCache.Target = result;

            return result;
        }

        public void ToDoubleArray(double[] dest)
        {
            for (var i = 0; i < Count; ++i) {
                dest[i] = Converter.ToDouble(this[i]);
            }
        }

        public int[] ToIntArray()
        {
            var result = new int[Count];

            for (var i = 0; i < Count; ++i) {
                result[i] = Convert.ToInt32(this[i]);
            }

            return result;
        }

        public DataFrame ToDummyValues(DataFrame dataFrame, string baseName, CodificationType codificationType = CodificationType.OneHotDropFirst)
        {
            HashSet<object> set = new HashSet<object>(new ObjectKeyComparer());
            foreach (var e in this) {
                set.Add(e);
            }

            var factors = new List<object>();
            foreach (var e in set) {
                factors.Add(e);
            }
            factors.Sort();

            var comparer = new ObjectKeyComparer();
            if (codificationType == CodificationType.Multilevel) {
                var values = new List<object>(Count);
                foreach (var e in this) {
                    var index = factors.FindIndex((x) => comparer.Equals(x, e));
                    values.Add(index);
                }
                dataFrame.AddColumn(baseName, values);
            }
            else {
                bool excludeFirst = codificationType == CodificationType.OneHotDropFirst;
                bool asBoolean = codificationType == CodificationType.Boolean;

                var values = new List<object>[factors.Count - (excludeFirst ? 1 : 0)];
                for (var i = 0; i < values.Length; ++i) {
                    values[i] = new List<object>(Count);
                }

                foreach (var e in this) {
                    var index = factors.FindIndex((x) => comparer.Equals(x, e));
                    if (excludeFirst) {
                        --index;
                    }
                    for (var i = 0; i < values.Length; ++i) {
                        if (asBoolean) {
                            values[i].Add(i == index);
                        }
                        else {
                            values[i].Add(i == index ? 1 : 0);
                        }
                    }
                }

                for (var i = 0; i < values.Length; ++i) {
                    var name = baseName + factors[i + (excludeFirst ? 1 : 0)].ToString();
                    dataFrame.AddColumn(name, values[i]);
                }
            }

            return dataFrame;
        }

        public DataFrame ToDummyValues(string baseName, CodificationType codificationType = CodificationType.OneHotDropFirst)
        {
            var dataFrame = new DataFrame();
            return ToDummyValues(dataFrame, baseName, codificationType);
        }

        #endregion

        #region Inplace convertions

        public void AsDouble()
        {
            for (var i = 0; i < this.Count; ++i) {
                this[i] = Converter.ToDouble(this[i]);
            }
        }

        public void AsBoolean()
        {
            for (var i = 0; i < this.Count; ++i) {
                this[i] = Convert.ToBoolean(this[i]);
            }
        }

        public void AsString()
        {
            for (var i = 0; i < this.Count; ++i) {
                this[i] = Convert.ToString(this[i]);
            }
        }

        public void AsDateTime()
        {
            for (var i = 0; i < this.Count; ++i) {
                this[i] = Converter.ToDateTime(this[i]);
            }
        }

        public void AsDateTime(string format)
        {
            for (var i = 0; i < this.Count; ++i) {
                this[i] = Converter.ToDateTime(this[i], format);
            }
        }

        public void AsDateTimeFromUnixTime()
        {
            for (var i = 0; i < this.Count; ++i) {
                var sec = Converter.ToDouble(this[i]);
                this[i] = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(sec);
            }
        }

        public void AsDateTimeOffset()
        {
            for (var i = 0; i < this.Count; ++i) {
                this[i] = Converter.ToDateTimeOffset(this[i]);
            }
        }

        public void AsDateTimeOffset(string format)
        {
            for (var i = 0; i < this.Count; ++i) {
                this[i] = Converter.ToDateTimeOffset(this[i], format);
            }
        }

        public void AsDateTimeOffsetFromUnixTime()
        {
            var epoch = new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);
            for (var i = 0; i < this.Count; ++i) {
                var sec = Converter.ToDouble(this[i]);
                this[i] = epoch.AddSeconds(sec);
            }
        }

        public void AsUnixTime()
        {
            var epoch = new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);
            for (var i = 0; i < this.Count; ++i) {
                var d = Converter.ToDateTimeOffset(this[i]);
                if (d == null) {
                    continue;
                }

                this[i] = ((DateTimeOffset)d - epoch).TotalSeconds;
            }
        }

        #endregion

        #region Data manupilations

        public void AddRange<T>(IEnumerable<T> data)
        {
            foreach (var d in data) {
                Add(d);
            }
        }

        public void AddRange<T>(T[] data)
        {
            foreach (var d in data) {
                Add(d);
            }
        }

        public void Replace<T>(IEnumerable<T> data)
        {
            Clear();
            foreach (var d in data) {
                Add(d);
            }
        }

        public void Replace<T>(T[] data)
        {
            Clear();
            foreach (var d in data) {
                Add(d);
            }
        }

        public void Replace(string pattern, string replacement)
        {
            var re = new Regex(pattern);

            for (var i = 0; i < this.Count; ++i) {
                this[i] = re.Replace(this[i].ToString(), replacement);
            }
        }

        public void Replace(Regex re, string replacement)
        {
            for (var i = 0; i < this.Count; ++i) {
                this[i] = re.Replace(this[i].ToString(), replacement);
            }
        }

        #endregion
    }

}