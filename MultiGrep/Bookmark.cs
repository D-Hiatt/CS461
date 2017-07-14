// //
// //    Bookmark.cs
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

namespace MultiGrep
{
    /// <summary>
    /// Stores the location of a match within a file
    /// </summary>
    public struct Bookmark
    {
        public long Start { get; }
        public long End { get; set; }
        public int Length { get; }
        public string Text { get; }

        public Bookmark(long s, int length, int size, char[] text)
        {
            Start = s;
            End = s + length;
            Length = length;
            Text = new string(text, 0, size);
        }

        public Bookmark(long s, int length, string text)
        {
            Start = s;
            End = s + length;
            Length = length;
            Text = text;
        }

        public Translation Convert(int id) { return new Translation(this, id); }
    }

    /// <summary>
    /// 
    /// </summary>
    public struct Translation
    {
        public long Start { get; }
        public long End { get; }
        public int Length { get; }
        public int Id { get; }

        public Translation(Bookmark mark, int id)
        {
            Start = mark.Start;
            End = mark.End;
            Length = mark.Length;
            Id = id;
        }
    }
}