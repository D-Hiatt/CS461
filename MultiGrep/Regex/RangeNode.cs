// //
// //    RangeNode.cs
// //
// //    Created on: 19 07 2017
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
    public class RangeNode : BaseNode
    {
        private readonly byte m_Min;
        private readonly byte m_Max;

        public RangeNode(byte min, byte max, int id) :  base(id)
        {
            m_Min = min;
            m_Max = max;
        }

        #region Overrides of Node

        /// <inheritdoc />
        public override bool Add(byte word, int id, out BaseNode ele) { throw new System.NotImplementedException(); }

        /// <inheritdoc />
        protected override bool CheckMatch(byte word)
        {
            return word <= m_Max && word >= m_Min;
        }

        #endregion
    }
}