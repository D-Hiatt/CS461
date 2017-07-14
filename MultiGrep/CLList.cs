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
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MultiGrep
{
    /// <summary>
    /// A circular linked list
    /// </summary>
    public class CLList
    {
        private static readonly byte[] Targ = {0x28, 0x65, 0x67, 0x61, 0x73, 0x73, 0x65, 0x4D};

        private static readonly HashSet<byte> Exempt = new HashSet<byte>
            {0x20, 0x0A, 0x0D, 0x09, 0x40, 0x2B, 0x3F, 0x3A, 0x26, 0x3C, 0x3E, 0x3D, 0x25, 0x2F, 0x2D, 0x2E, 0x5B, 0x5D};

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
        static CLList()
        {
            Exempt.UnionWith(Enumerable.Range(0x2D, 0x3A).Select(i => (byte)i));
            Exempt.UnionWith(Enumerable.Range(0x3C, 0x40).Select(i => (byte)i));
        }

        public CLList(Encoding enc) { Encoding = enc; }

        public void Add(int data) { Entries[Count++ % 64] = (byte)data; }

        public void Clear()
        {
            Count = 0;
            Array.Clear(Entries, 0, Entries.Length);
        }

        public int Check()
        {
            if(Count < Targ.Length)
                return -1;
            int correct = 0;
            int skipped = 0;
            int length = Count > 64 ? 64 : Count;
            for(int i = 1; i < length; ++i)
            {
                byte b = Entries[(Count - i) % 64];

                if(b == Targ[correct])
                {
                    if(++correct == Targ.Length)
                        return skipped + correct;
                }
                else
                {
                    if(correct > 0 || !Exempt.Contains(b))
                        return -1;
                    ++skipped;
                }
            }
            return -1;
        }
    }
}