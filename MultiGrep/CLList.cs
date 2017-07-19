// //
// //    CLList.cs
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
using System.Linq;
using System.Text;

namespace MultiGrep
{
    /// <summary>
    /// A circular linked list
    /// </summary>
    public class CLList
    {
        private readonly byte[] Entries = Enumerable.Repeat((byte)0, 65).ToArray();
        public readonly Encoding Encoding;
        public int Count { get; private set; }
        public int Start { get; private set; }
        public int Length { get { return (Count % 64) - Start; } }

        public string Word()
        {
            if(Length > 0)
                return Encoding.GetString(Entries, Start, Length);
            return "";
        }

        public void Mark() { Start = Count % 64; }

        public CLList(Encoding enc) { Encoding = enc; }

        public void Add(int data) { Entries[Count++ % 64] = (byte)data; }

        public void Clear()
        {
            Count = 0;
            Array.Clear(Entries, 0, Entries.Length);
        }
    }
}