﻿using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Management.Automation;
using Accord.Math;
using Accord.Math.Decompositions;
using System.Text;
using Accord.Math.Random;

namespace Horker.Math
{
    public enum Direction
    {
        Horizontal,
        Vertical
    }

    public class DataFrame : IEnumerable<PSObject>
    {
        private List<string> _names;
        private Dictionary<string, DataFrameColumn> _columns;
        private PSObject _link;

        #region Constructors

        public DataFrame()
        {
            _names = new List<string>();
            _columns = new Dictionary<string, DataFrameColumn>(new StringKeyComparer());
            _link = new PSObject(this);
        }

        public DataFrame(IEnumerable<PSObject> objects)
            : this()
        {
            foreach (var obj in objects) {
                AddRow(obj);
            }
        }

        #endregion

        #region Factory methods

        public static DataFrame Create<T>(T[][] jagged)
        {
            var df = new DataFrame();

            int rowCount = jagged.Length;

            int columnCount = 0;
            foreach (var j in jagged) {
                if (columnCount < j.Length) {
                    columnCount = j.Length;
                }
            }

            var data = new DataFrameColumn[columnCount];
            for (var column = 0; column < columnCount; ++column) {
                data[column] = new DataFrameColumn(rowCount);
            }

            for (var row = 0; row < rowCount; ++row) {
                for (var column = 0; column < jagged[row].Length; ++column) {
                    data[column].Add(jagged[row][column]);
                }
                for (var column = jagged[row].Length; column < columnCount; ++column) {
                    data[column].Add(null);
                }
            }

            for (var column = 0; column < columnCount; ++column) {
                df.DefineNewColumn("c" + column, data[column]);
            }

            return df;
        }

        public static DataFrame Create<T>(T[] array, int rowCount = int.MaxValue, int columnCount = int.MaxValue, bool transpose = false)
        {
            var df = new DataFrame();

            if (columnCount == int.MaxValue) {
                if (rowCount == int.MaxValue) {
                    columnCount = 1;
                    rowCount = array.Length;
                }
                else {
                    columnCount = array.Length / rowCount;
                    if (columnCount == 0 || array.Length % rowCount != 0) {
                        ++columnCount;
                    }
                }
            }
            else {
                if (rowCount == int.MaxValue) {
                    rowCount = array.Length / columnCount;
                    if (rowCount == 0 || array.Length % columnCount != 0) {
                        ++rowCount;
                    }
                }
            }

            var data = new DataFrameColumn[columnCount];
            for (int column = 0; column < columnCount; ++column) {
                data[column] = new DataFrameColumn(rowCount);
            }

            for (int column = 0; column < columnCount; ++column) {
                var v = data[column];
                for (int row = 0; row < rowCount; ++row) {
                    int index;
                    if (transpose) {
                        index = (row * columnCount + column) % array.Length;
                    }
                    else {
                        index = (column * rowCount + row) % array.Length;
                    }
                    v.Add(array[index]);
                }
            }

            for (var column = 0; column < columnCount; ++column) {
                df.DefineNewColumn("c" + column, data[column]);
            }

            return df;
        }

        public static DataFrame Create<T>(T[,] matrix)
        {
            var df = new DataFrame();

            int rowCount = matrix.GetLength(0);
            int columnCount = matrix.GetLength(1);

            for (var column = 0; column < columnCount; ++column) {
                var v = new DataFrameColumn(rowCount);
                for (var row = 0; row < rowCount; ++row) {
                    v.Add(matrix[row, column]);
                }
                df.DefineNewColumn("c" + column, v);
            }

            return df;
        }

        public DataFrame Clone()
        {
            var df = new DataFrame();

            for (var i = 0; i < ColumnCount; ++i) {
                df.DefineNewColumn(ColumnNames[i], new DataFrameColumn(GetColumn(i)));
            }

            return df;
        }

        #endregion

        #region Properties

        public PSObject LinkedPSObject
        {
            get => _link;
        }

        public int Count
        {
            get
            {
                if (_columns.Count == 0) {
                    return 0;
                }
                return _columns[_names[0]].Count;
            }
        }

        public int ColumnCount
        {
            get => _columns.Count;
        }

        public int RowCount
        {
            get => Count;

        }

        public List<String> ColumnNames => _names;

        public DataFrameColumn this[string name] {
            get => GetColumn(name);
            set => SetColumn(name, value);
        }

        public PSObject this[int index]
        {
            get => GetRow(index);
            set => SetRow(value);
        }

        public object this[int? row, int? column]
        {
            get
            {
                if (row == null) {
                    return GetColumn(column.Value);
                }
                if (column == null) {
                    return GetRow(row.Value);
                }
                return GetColumn(column.Value)[row.Value];
            }

            set
            {
                if (row == null || column == null) {
                    throw new ArgumentException("row and column should not be null");
                }
                GetColumn(column.Value)[row.Value] = value;
            }
        }

        public object this[int? row, string column]
        {
            get
            {
                if (row == null) {
                    return GetColumn(column);
                }
                return GetColumn(column)[row.Value];
            }

            set
            {
                if (row == null) {
                    throw new ArgumentException("row should not be null");
                }
                GetColumn(column)[row.Value] = value;
            }
        }

        #endregion

        #region  Enumerator<PSObject>

        private class Enumerator : IEnumerator<PSObject>
        {
            private DataFrame _dataFrame;
            private int _index;

            public Enumerator(DataFrame dataFrame)
            {
                _dataFrame = dataFrame;
                _index = -1;
            }

            public PSObject Current
            {
                get { return _dataFrame.GetRow(_index); }
            }

            object IEnumerator.Current
            {
                get { return _dataFrame.GetRow(_index);  }
            }

            public void Dispose()
            {
                // Do nothing
            }

            public bool MoveNext()
            {
                ++_index;
                return _index < _dataFrame.Count;
            }

            public void Reset()
            {
                _index = 0;
            }
        }

        public IEnumerator<PSObject> GetEnumerator()
        {
            return new Enumerator(this);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return new Enumerator(this);
        }

        #endregion

        #region Conversions

        public double[][] ToDoubleJaggedArray()
        {
            var data = new double[this.Count][];
            for (var i = 0; i < data.Length; ++i) {
                data[i] = new double[_names.Count];
            }

            var columnCount = 0;
            foreach (var entry in _columns) {
                var column = entry.Value;
                for (var rowCount = 0; rowCount < column.Count; ++rowCount) {
                    data[rowCount][columnCount] = Convert.ToDouble(column[rowCount]);
                }
                ++columnCount;
            }

            return data;
        }

        public double[,] ToDoubleMatrix()
        {
            var data = new double[RowCount, ColumnCount];
            for (var column = 0; column < ColumnCount; ++column) {
                var v = GetColumn(column);
                for (var row = 0; row < RowCount; ++row) {
                    data[row, column] = Converter.ToDouble(v[row]);
                }
            }

            return data;
        }

        public int[,] ToIntMatrix()
        {
            var data = new int[this.Count, _names.Count];
            for (var column = 0; column < _names.Count; ++column) {
                var v = GetColumn(column);
                for (var row = 0; row < this.Count; ++row) {
                    data[row, column] = Convert.ToInt32(v[row]);
                }
            }

            return data;
        }

        public double[] ToDoubleArray(Direction direction = Direction.Horizontal)
        {
            var data = new double[RowCount * ColumnCount];
            if (direction == Direction.Horizontal) {
                for (var column = 0; column < ColumnCount; ++column)
                {
                    var v = GetColumn(column);
                    for (var row = 0; row < RowCount; ++row)
                    {
                        data[row * ColumnCount + column] = Converter.ToDouble(v[row]);
                    }
                }
            }
            else {
                for (var column = 0; column < ColumnCount; ++column)
                {
                    var v = GetColumn(column);
                    for (var row = 0; row < RowCount; ++row)
                    {
                        data[row  + column * RowCount] = Converter.ToDouble(v[row]);
                    }
                }
            }

            return data;
        }

        #endregion

        #region Inplace conversions

        public void ColumnToDummyValues(string columnName, CodificationType codificationType = CodificationType.OneHotDropFirst)
        {
            var baseName = columnName;
            if (codificationType != CodificationType.Multilevel) {
                baseName += "_";
            }

            var column = this[columnName];
            RemoveColumn(columnName);

            column.ToDummyValues(this, baseName, codificationType);
        }

        #endregion

        #region Data manipulations

        public bool HasColumn(string name)
        {
            // Use this method to find if a column name exists in the object
            // because column names are case-incensitive.
            return _columns.ContainsKey(name);
        }

        public void Add(string name, object value)
        {
            _columns[name].Add(value);
        }

        public PSObject GetRow(int index)
        {
            var obj = new PSObject();
            foreach (var name in _names) {
                var column = _columns[name];
                object value = null;
                if (index < column.Count) {
                    value = column[index];
                }
                var prop = new PSNoteProperty(name, value);
                obj.Properties.Add(prop);
            }

            return obj;
        }

        public DataFrameColumn GetColumn(string name)
        {
            if (name.ToLower() == "__line__") {
                var count = this.Count;
                var column = new DataFrameColumn(Count);
                for (var i = 0; i < count; ++i) {
                    column.Add(i);
                }
                return column;
            }
            return _columns[name];
        }

        public DataFrameColumn GetColumn(int index)
        {
            return _columns[_names[index]];
        }

        public void SetRow(PSObject obj)
        {
            throw new NotImplementedException();
        }

        public void AddRow(PSObject obj)
        {
            var names = new HashSet<string>(new StringKeyComparer());

            var count = this.Count;
            foreach (var p in obj.Properties) {
                names.Add(p.Name);
                if (_columns.ContainsKey(p.Name)) {
                    _columns[p.Name].Add(p.Value);
                }
                else {
                    var l = new DataFrameColumn();

                    for (var i = 0; i < count; ++i) {
                        l.Add(null);
                    }

                    l.Add(p.Value);

                    DefineNewColumn(p.Name, l);
                }
            }

            foreach (var entry in _columns) {
                if (!names.Contains(entry.Key)) {
                    _columns[entry.Key].Add(null);
                }
            }
        }

        public void SetColumn<T>(string name, IEnumerable<T> values)
        {
            if (!_columns.ContainsKey(name)) {
                AddColumn(name, values);
            }
            else {
                _columns[name].AddRange(values);
            }
        }

        public void SetColumn<T>(string name, T[] values)
        {
            if (!_columns.ContainsKey(name)) {
                AddColumn(name, values);
            }
            else {
                _columns[name].AddRange(values);
            }
        }

        public void DefineNewColumn(string name, DataFrameColumn data)
        {
            if (_columns.ContainsKey(name)) {
                throw new RuntimeException("Column already exists");
            }

            _names.Add(name);
            _columns[name] = data;
            _link.Properties.Add(new PSNoteProperty(name, data));
        }

        public void AddColumn<T>(string name, IEnumerable<T> values)
        {
            DefineNewColumn(name, DataFrameColumn.Create(values));
        }

        public void AddColumn<T>(string name, T[] values)
        {
            DefineNewColumn(name, DataFrameColumn.Create(values));
        }

        public void InsertColumn<T>(int index, string name, IEnumerable<T> values)
        {
            DefineNewColumn(name, DataFrameColumn.Create(values));
            MoveColumn(name, index);
        }

        public void InsertColumn<T>(string before, string name, T[] values)
        {
            DefineNewColumn(name, DataFrameColumn.Create(values));
            MoveColumn(name, before);
        }

        public void Aggregate(DataFrame df)
        {
            for (var i = 0; i < df.ColumnCount; ++i) {
                DefineNewColumn(df.ColumnNames[i], new DataFrameColumn(df.GetColumn(i)));
            }
        }

        public void RemoveColumn(string name)
        {
            if (!_names.Contains(name)) {
                throw new RuntimeException("No such a column");
            }

            _columns.Remove(name);
            _names.Remove(name);
            _link.Properties.Remove(name);
        }

        public void RenameColumn(string name, string newName)
        {
            var index = _names.FindIndex((x) => StringKeyComparer.Compare(x, name));
            if (index == -1) {
                throw new RuntimeException("No such a column");
            }

            _names[index] = newName;
            _columns[newName] = _columns[name];
            _columns.Remove(name);

            UpdateLinkedObject();
        }

        public void MoveColumn(string name, int index)
        {
            _names.Remove(name);
            _names.Insert(index, name);

            UpdateLinkedObject();
        }

        public void MoveColumn(string name, string location)
        {
            var index = _names.FindIndex((x) => StringKeyComparer.Compare(x, location));
            if (index == -1) {
                throw new RuntimeException("No such a column");
            }

            MoveColumn(name, index);
        }

        private void UpdateLinkedObject()
        {
            var names = new List<string>();
            foreach (var p in _link.Properties) {
                names.Add(p.Name);
            }

            foreach (var n in names) {
                _link.Properties.Remove(n);
            }

            foreach (var n in _names) {
                _link.Properties.Add(new PSNoteProperty(n, _columns[n]));
            }
        }

        public void FillShortColumns()
        {
            var maxCount = _columns.Values.Max(x => x.Count);

            foreach (var column in _columns.Values) {
                while (column.Count < maxCount) {
                    column.Add(null);
                }
            }
        }

        public void Clear()
        {
            _names.Clear();
            _columns.Clear();
        }

        public void Shuffle()
        {
            // ref. https://en.wikipedia.org/wiki/Fisher%E2%80%93Yates_shuffle

            int count = this.Count;
            for (var i = 0; i < count - 1; i++) {
                var j = Generator.Random.Next(i, count);
                foreach (var column in _columns.Values) {
                    var temp = column[i];
                    column[i] = column[j];
                    column[j] = temp;
                }
            }
        }

        public void Append(DataFrame other)
        {
            if (ColumnCount != other.ColumnCount) {
                throw new ArgumentException("Column sizes mismatch");
            }

            for (var i = 0; i < ColumnCount; ++i) {
                GetColumn(i).AddRange(other.GetColumn(i));
            }
        }

        #endregion

        #region Elementwise operations

        public DataFrame Apply(Func<object, object> f)
        {
            var df = new DataFrame();

            for (var column = 0; column < ColumnCount; ++column) {
                var v = GetColumn(column);
                var newVec = new DataFrameColumn(v.Count);
                for (var row = 0; row < RowCount; ++row) {
                    newVec.Add(f.Invoke(v[row]));
                }
                df.DefineNewColumn(ColumnNames[column], newVec);
            }

            return df;
        }

        public DataFrame Apply(Func<object, object, object> f, object arg1)
        {
            var df = new DataFrame();

            for (var column = 0; column < ColumnCount; ++column) {
                var v = GetColumn(column);
                var newVec = new DataFrameColumn(v.Count);
                for (var row = 0; row < RowCount; ++row) {
                    newVec.Add(f.Invoke(v[row], arg1));
                }
                df.DefineNewColumn(ColumnNames[column], newVec);
            }

            return df;
        }

        public DataFrame Apply(ScriptBlock f)
        {
            var df = new DataFrame();

            for (var column = 0; column < ColumnCount; ++column) {
                var v = GetColumn(column);
                var newVec = new DataFrameColumn(v.Count);
                for (var row = 0; row < RowCount; ++row) {
                    var va = new List<PSVariable>() { new PSVariable("_") };
                    va[0].Value = v[row];
                    newVec.Add(f.InvokeWithContext(null, va, null).Last());
                }
                df.DefineNewColumn(ColumnNames[column], newVec);
            }

            return df;
        }

        public void ApplyInPlace(Func<object, object> f)
        {
            for (var column = 0; column < ColumnCount; ++column) {
                var v = GetColumn(column);
                for (var row = 0; row < RowCount; ++row) {
                    v[row] = f.Invoke(v[row]);
                }
            }
        }

        public void ApplyInPlace(Func<object, object, object> f, object arg1)
        {
            for (var column = 0; column < ColumnCount; ++column) {
                var v = GetColumn(column);
                for (var row = 0; row < RowCount; ++row) {
                    v[row] = f.Invoke(v[row], arg1);
                }
            }
        }

        public void ApplyInPlace(ScriptBlock f)
        {
            for (var column = 0; column < ColumnCount; ++column) {
                var v = GetColumn(column);
                for (var row = 0; row < RowCount; ++row) {
                    var va = new List<PSVariable>() { new PSVariable("_") };
                    va[0].Value = v[row];
                    v[row] = f.InvokeWithContext(null, va, null).Last();
                }
            }
        }

        public DataFrame Apply(DataFrame b, Func<object, object, object> f)
        {
            if (ColumnCount > b.ColumnCount || RowCount > b.RowCount) {
                throw new RuntimeException("Data sizes are not the same");
            }

            var df = new DataFrame();

            for (var column = 0; column < ColumnCount; ++column) {
                var v = GetColumn(column);
                var v2 = b.GetColumn(column);
                var newVec = new DataFrameColumn(v.Count);
                for (var row = 0; row < RowCount; ++row) {
                    newVec.Add(f.Invoke(v[row], v2[row]));
                }
                df.DefineNewColumn(ColumnNames[column], newVec);
            }

            return df;
        }

        public DataFrame Apply(DataFrame b, ScriptBlock f)
        {
            if (ColumnCount > b.ColumnCount || RowCount > b.RowCount) {
                throw new RuntimeException("Data sizes are not the same");
            }

            var df = new DataFrame();

            for (var column = 0; column < ColumnCount; ++column) {
                var v = GetColumn(column);
                var v2 = b.GetColumn(column);
                var newVec = new DataFrameColumn(v.Count);
                for (var row = 0; row < RowCount; ++row) {
                    var va = new List<PSVariable>() { new PSVariable("a"), new PSVariable("b") };
                    va[0].Value = v[row];
                    va[1].Value = v2[row];
                    newVec.Add(f.InvokeWithContext(null, va, null).Last());
                }
                df.DefineNewColumn(ColumnNames[column], newVec);
            }

            return df;
        }

        #endregion

        #region Aggregation methods

        public DataFrameGroup GroupBy(string columnName)
        {
            var result = new DataFrameGroup();
            foreach (var row in this) {
                var group = row.Properties[columnName].Value;
                if (!result.Contains(group)) {
                    result[group] = new DataFrame();
                }
                ((DataFrame)result[group]).AddRow(row);
            }

            return result;
        }

        public DataFrameGroup GroupBy(params object[] columnNames)
        {
            var result = new DataFrameGroup();
            foreach (var row in this) {
                var builder = new StringBuilder();
                for (var i = 0; i < columnNames.Length - 1; ++i) {
                    builder.Append(row.Properties[columnNames[i].ToString()].Value.ToString());
                    builder.Append(",");
                }
                builder.Append(row.Properties[columnNames[columnNames.Length - 1].ToString()].Value.ToString());

                var group = builder.ToString();

                if (!result.Contains(group)) {
                    result[group] = new DataFrame();
                }
                ((DataFrame)result[group]).AddRow(row);
            }

            return result;
        }

        #endregion
    }
}