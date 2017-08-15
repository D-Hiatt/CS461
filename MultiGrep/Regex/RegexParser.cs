// //
// //    RegexParser.cs
// //
// //    Created on: 24 07 2017
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

using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;

namespace MultiGrep
{
    public static class RegexParser
    {
        private enum RegexIdentity
        {
            OpenParen = 0x28,
            CloseParen = 0x29,
            ZeroOrMore = 0x2A,
            OneOrMore = 0x2C,
            Dot = 0x2E,
            OpenBracket = 0x5B,
            Escape = 0x5C,
            CloseBracket = 0x5D,
            Caret = 0x5E
        }
        private static readonly byte m_OpenBracket = 0x5B;
        private static readonly byte m_CloseBracket = 0x5D;
        private static readonly byte m_Dash = 0x2D;
        private const byte m_Dot = 0x2E;
        private const byte m_Escape = 0x5C;
        private const byte m_ZeroOrMore = 0x2A;
        private const byte m_OneOrMore = 0x2C;
        private const byte m_OpenParen = 0x28;
        private const byte m_CloseParen = 0x29;
        private const byte m_Caret = 0x5E;

        public static void ParseBytes(byte[] bytes, int id)
        {
            bool inRange = false;
            bool escape = false;
            Stack<BaseNode> list = new Stack<BaseNode>();
            foreach(byte b in bytes)
            {
                if (escape)
                {
                    list.Push(new Node(b, id));
                    escape = false;
                }
                else if (b == m_Dot)
                {
                   list.Push(new AnyNode());
                }
                else if(b == m_OpenBracket)
                {
                    if(inRange)
                        list.Push(new Node(b, id));
                    else
                        inRange = true;
                }
                else if(b == m_CloseBracket)
                {
                    if (inRange)
                        inRange = false;
                    else
                        list.Push(new Node(b, id));
                }
                else if(b == m_Escape)
                {
                        escape = true;
                }
                else if(b == m_OneOrMore)
                {
                    BaseNode node = list.Pop();

                }
            }
        }
    }
}