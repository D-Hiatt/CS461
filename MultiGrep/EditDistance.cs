// //
// //    EditDistance.cs
// //
// //    Created on: 13 07 2017
// //        Author: David Hiatt - dhiatt89@gmail.com
// //
// //
// // Copyright (c) 2017 David Hiatt
// //
// //  This program is free software: you can redistribute it and/or modify
// //  it under the terms of the GNU Lesser General Public License as published by
// //  the Free Software Foundation, either version 3 of the License, or
// //  (at your option) any later version.
// //
// //  This program is distributed in the hope that it will be useful,
// //  but WITHOUT ANY WARRANTY; without even the implied warranty of
// //  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// //  GNU Lesser General Public License for more details.
// //
// //  You should have received a copy of the GNU Lesser General Public License
// //  along with this program.  If not, see <http://www.gnu.org/licenses/>.
// //

using System;
using System.Collections.Concurrent;
using System.Linq;

namespace MultiGrep
{
    /// <summary>
    ///     Calculates the Edit distance between strings.
    /// </summary>
    public static class EditDistance
    {
        private static readonly ConcurrentBag<int[]> IndexPool = new ConcurrentBag<int[]>();

        private static char[] Filter(string s) { return string.IsNullOrEmpty(s) ? new char[] {' '} : s.Where(c => c >= 0x20 && c <= 0x7E).ToArray(); }

        private sealed class EditMatrix
        {
            private static readonly ConcurrentBag<EditMatrix> BufferPool = new ConcurrentBag<EditMatrix>();

            private bool m_Active;

            /// <summary> The matrix. </summary>
            private int[] m_Matrix;

            /// <summary> Gets the row. </summary>
            /// <value> The row. </value>
            private int Row { get; set; }

            /// <summary> Gets the column. </summary>
            /// <value> The column. </value>
            private int Col { get; set; }

            /// <summary>
            ///     Indexer to get or set items within this collection using array index syntax.
            /// </summary>
            /// <param name="i"> Zero-based index of the entry to access. </param>
            /// <returns> The indexed item. </returns>
            public int this[int i] { get => this[0, i]; set => this[0, i] = value; }

            /// <summary>
            ///     Indexer to get or set items within this collection using array index syntax.
            /// </summary>
            /// <param name="i"> Zero-based row of the entry to access. </param>
            /// <param name="j"> Zero-based column of the entry to access. </param>
            /// <returns> The indexed item. </returns>
            public int this[int i, int j]
            {
                get { return m_Active ? m_Matrix[i * (Col + 1) + j] : 0; }
                set
                {
                    if(m_Active)
                        m_Matrix[i * (Col + 1) + j] = value;
                }
            }

            /// <summary> Constructor. </summary>
            private EditMatrix()
            {
                Row = 0;
                Col = 0;
                m_Active = false;
                m_Matrix = null; // r > 0 && c > 0 ? new int[(r + 1) * (c + 1)] : new int[1];
            }

            /// <summary> Returns the fully qualified type name of this instance. </summary>
            /// <returns> A <see cref="T:System.String" /> containing a fully qualified type name. </returns>
            /// <remarks>This is only useful for debugging</remarks>
            public override string ToString()
            {
                string result = "";
                for(int i = 0; i <= Row; ++i)
                {
                    for(int j = 0; j <= Col; ++j)
                        result += $"{this[i, j]} ";
                    result += Environment.NewLine;
                }
                return result;
            }

            /// <summary> Resets this Server.Misc.EditDistance&lt;T&gt; </summary>
            /// <param name="r"> The row to process. </param>
            /// <param name="c"> The column to process. </param>
            public void Reset(int r, int c)
            {
                if(Row > r && Col > c && m_Matrix.Length > (r + 1) * (c + 1))
                {
                    Array.Clear(m_Matrix, 0, m_Matrix.Length);
                    Row = r;
                    Col = c;
                }
                else
                {
                    Row = r;
                    Col = c;
                    m_Matrix = r > 0 && c > 0 ? new int[(r + 1) * (c + 1)] : new int[1];
                }
                if(m_Matrix.Length > 1)
                {
                    int max = r + c;
                    for(int i = 1; i <= r; ++i)
                    {
                        m_Matrix[i * (c + 1) + c] = max;
                        m_Matrix[i * (c + 1)] = i;
                    }
                    for(int i = 1; i <= c; ++i)
                    {
                        m_Matrix[r * (c + 1) + i] = max;
                        m_Matrix[i] = i;
                    }
                    m_Active = true;
                }
            }

            public int Result()
            {
                int result = this[Row, Col];
                m_Active = false;
                BufferPool.Add(this);
                return result;
            }

            public static EditMatrix GetMatrix()
            {
                EditMatrix field;
                if(!BufferPool.TryTake(out field))
                    field = new EditMatrix();
                return field;
            }
        }

        #region Action

        /// <summary>   Performs the measure action. </summary>
        /// <remarks>   X, 5/30/2015. </remarks>
        /// <param name="actual"></param>
        /// <param name="comp"> The component. </param>
        /// <returns>   An int. </returns>
        public static int PerformMeasure(string actual, string comp)
        {
            if(string.IsNullOrEmpty(comp) || string.IsNullOrEmpty(actual))
                return int.MaxValue;
            char[] C1array = Filter(actual.ToUpperInvariant());
            char[] C2array = Filter(comp.ToUpperInvariant());
            int L1 = C1array.Length;
            int L2 = C2array.Length;
            int[] index;
            if(!IndexPool.TryTake(out index))
                index = new int[0x7F - 0x20];
            Array.Clear(index, 0, index.Length);
            EditMatrix field = EditMatrix.GetMatrix();
            field.Reset(L1, L2);
            for(int i = 1; i <= L1; ++i)
            {
                int beta = 0;
                for(int j = 1; j <= L2; ++j)
                {
                    int C1 = index[C2array[j - 1] - 0x20];
                    int C2 = beta;
                    int cost = 1;
                    if(C1array[i - 1] == C2array[j - 1])
                    {
                        cost = 0;
                        beta = j;
                    }
                    field[i, j] = Min(field[i - 1, j - 1] + cost, field[i, j - 1] + 1, field[i - 1, j] + 1,
                                      field[C1 > 0 ? C1 - 1 : 0, C2 > 0 ? C2 - 1 : 0] + (i - C1 - 1) + 1 + (j - C2 - 1));
                }
                index[C1array[i - 1] - 0x20] = i;
            }
            IndexPool.Add(index);
            int result = field.Result();
            double perc = (L1 + L2 / (double)result) * 100d;
            double perca = result / (double)L1 * 100d;
            if(perc < 50)
                Console.WriteLine("{3}: '{0}' & '{1}' -> {2:F2}", actual, comp, perc, result);
            return result;
        }

        public static double PerformMeasureTest(string actual, string comp)
        {
            if(string.IsNullOrEmpty(comp) || string.IsNullOrEmpty(actual))
                return int.MaxValue;
            char[] C1array = Filter(actual.ToUpperInvariant());
            char[] C2array = Filter(comp.ToUpperInvariant());
            int L1 = C1array.Length;
            int L2 = C2array.Length;
            int[] index;
            if(!IndexPool.TryTake(out index))
                index = new int[0x7F - 0x20];
            Array.Clear(index, 0, index.Length);
            EditMatrix field = EditMatrix.GetMatrix();
            field.Reset(L1, L2);
            for(int i = 1; i <= L1; ++i)
            {
                int beta = 0;
                for(int j = 1; j <= L2; ++j)
                {
                    int C1 = index[C2array[j - 1] - 0x20];
                    int C2 = beta;
                    int cost = 1;
                    if(C1array[i - 1] == C2array[j - 1])
                    {
                        cost = 0;
                        beta = j;
                    }
                    field[i, j] = Min(field[i - 1, j - 1] + cost, field[i, j - 1] + 1, field[i - 1, j] + 1,
                                      field[C1 > 0 ? C1 - 1 : 0, C2 > 0 ? C2 - 1 : 0] + (i - C1 - 1) + 1 + (j - C2 - 1));
                }
                index[C1array[i - 1] - 0x20] = i;
            }
            IndexPool.Add(index);
            int result = field.Result();
            double perc = (L1 + L2 / (double)result) * 100d;
            double perca = result / (double)L1 * 100d;
            if(perc < 50)
                Console.WriteLine("{3}: '{0}' & '{1}' -> {2:F2}", actual, comp, perc, result);
            return perc;
        }

        #endregion

        #region Internal

        // private static int Min(params int[] args) => args.Min();
        private static int Min(int a, int b) { return a > b ? b : a; }

        private static int Min(int a, int b, int c) { return Min(a > b ? b : a, c); }

        private static int Min(int a, int b, int c, int d) { return Min(a > b ? b : a, c, d); }

        #endregion
    }
}