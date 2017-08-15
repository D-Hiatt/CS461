// //
// //    MoreNode.cs
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
namespace MultiGrep
{
    public class MoreNode : BaseNode
    {

        private readonly BaseNode m_ToMactch;

        public MoreNode(BaseNode node, int id) : base(id)
        {
            m_ToMactch = node;
            Links.Add(this);
        }

        #region Overrides of BaseNode

        /// <inheritdoc />
        public override bool Add(byte word, int id, out BaseNode ele) { throw new System.NotImplementedException(); }

        /// <inheritdoc />
        protected override bool CheckMatch(byte word)
        {
            return m_ToMactch.IsMatch(word);
        }

        #endregion
    }
}