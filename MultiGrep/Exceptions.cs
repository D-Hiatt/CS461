// //
// //    Exceptions.cs
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

namespace MultiGrep
{
    public class TerminalException : Exception
    {
        public TerminalException(string msg) : base(msg) { }
    }

    public class TreeException : Exception
    {
        public int Id { get; }
        public string Words { get; }

        public TreeException(string msg, string word, int id = 0) : base(msg)
        {
            Words = word;
            Id = id;
        }

        public TreeException(string msg, int id, string word = "") : base(msg)
        {
            Id = id;
            Words = word;
        }
    }

    public class ParseException : Exception
    {
        public long Position { get; }
        public char[] LastBytes { get; }
        public bool Found { get; }
        public bool Block { get; }
        public CLList History { get; }

        public ParseException(string msg, long pos = 0, bool found = false, bool block = false, char[] last = null, int length = 0,
                              CLList list = null) : base(msg)
        {
            Position = pos;
            Found = found;
            Block = block;
            if(last != null)
            {
                LastBytes = new char[length];
                Buffer.BlockCopy(last, 0, LastBytes, 0, length);
            }
            else
            {
                LastBytes = new char[] {'0'};
            }
            History = list;
        }
    }
}