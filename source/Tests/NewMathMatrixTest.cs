﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Horker.DataAnalysis;
using System.Management.Automation;

namespace Tests
{
    [TestClass]
    public class NewMathMatrixTest
    {
        [TestMethod]
        public void TestCreate()
        {
            using (var ps = PowerShell.Create()) {
                var ci = new CmdletInfo("New-Math.Matrix", typeof(NewMathMatrix));
                ps.AddCommand(ci);
                ps.AddParameters(new object[] { new double[] { 1, 2, 3 }, 2, 3 });
                var results = ps.Invoke();

                Assert.AreEqual(1, results.Count);
                var matrix = (Matrix)results[0].BaseObject;

                Assert.AreEqual(2, matrix.RowCount);
                Assert.AreEqual(3, matrix.ColumnCount);
                Assert.AreEqual(2, matrix[0, 2]);
            }
        }

        [TestMethod]
        public void TestAutosizing()
        {
            using (var ps = PowerShell.Create()) {
                var ci = new CmdletInfo("New-Math.Matrix", typeof(NewMathMatrix));
                ps.AddCommand(ci);
                ps.AddParameters(new Dictionary<string, object>() {
                    { "RowCount", 2 },
                    { "Values", new double[] { 1, 2, 3, 4, 5 } }
                });
                var results = ps.Invoke();

                Assert.AreEqual(1, results.Count);
                var matrix = (Matrix)results[0].BaseObject;

                Assert.AreEqual(2, matrix.RowCount);
                Assert.AreEqual(3, matrix.ColumnCount);
                Assert.AreEqual(5, matrix[0, 2]);
            }
        }

        [TestMethod]
        public void TestFromJagged()
        {
            using (var ps = PowerShell.Create()) {
                var ci = new CmdletInfo("New-Math.Matrix", typeof(NewMathMatrix));
                ps.AddCommand(ci);
                ps.AddParameters(new Dictionary<string, object>() {
                    { "FromJagged", true },
                    { "Values", new double[][] { new double[] { 1, 2 }, new double[] { 3, 4, 5 } } }
                });
                var results = ps.Invoke();

                Assert.AreEqual(1, results.Count);
                var matrix = (Matrix)results[0].BaseObject;

                Assert.AreEqual(2, matrix.RowCount);
                Assert.AreEqual(3, matrix.ColumnCount);
                Assert.AreEqual(0, matrix[0, 2]);
            }
        }
    }
}