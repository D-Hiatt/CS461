// //
// //    Element.cs
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
using System.IO;
using System.Linq;

namespace MultiGrep
{
    public class Node : BaseNode, IEquatable<byte>, IComparable<byte>, IEquatable<Node>, IComparable<Node>
    {

        /// <summary>
        /// The word represented by this element
        /// </summary>
        public byte Data { get; }

        /// <summary>
        /// Create a node by reading in a pattern
        /// </summary>
        /// <param name="reader"></param>
        private Node(BinaryReader reader)
        {
            int version = reader.ReadInt32();
            switch(version) //version
            {
                default:
                    //Data = reader.ReadByte();
                    int len = reader.ReadInt32();
                    for(int i = 0; i < len; ++i)
                        IdSet.Add(reader.ReadInt32());
                    len = reader.ReadInt32();
                    for(int i = 0; i < len; ++i)
                        Links.Add(new Node(reader));
                    break;
            }
        }

        /// <summary>
        /// Create a new root element
        /// </summary>
        public Node()
        {
            Data = 0;
        }

        public Node(byte word, int id) : base(id) { Data = word; }


        /// <summary>
        /// Represents a terminal node
        /// </summary>
        /// <param name="id"></param>
        public Node(int id) : base(id)
        {
            Data = 0;
        }

        /// <exception cref="TerminalException">Cannot link off of terminals</exception>
        public override bool Add(byte word, int id, out BaseNode ele)
        {
            if(Data == 0 && Id != -1)
                throw new TerminalException("Cannot link off of terminals");
            ele = Links.FirstOrDefault(u => u.IsMatch(word)); //Check to see if an element matching this word already exists in our links
            if(ele == null)
            {
                Links.Add(ele = new Node(word, id)); //If it doesnt, add the new element to our link list
                return true;
            }
            return ele.Combine(id); //If it does, add the id to the linked element
        }

        private static int InnerDepth(IReadOnlyCollection<Node> links)
        {
            if(links == null)
                return 0;
            //if(links.Count > 0)
               // return 1 + links.Max(s => InnerDepth(s.Links));
            return 1;
        }
/*
        public void Save(BinaryWriter writer)
        {
            writer.Write(2); //version
            //writer.Write(Start);
            writer.Write(Id);
            writer.Write(IdSet.Count);
            IdSet.ForEach(writer.Write);
            writer.Write(Breadth);
            Links.ForEach(s => s.Save(writer));
        }

        public static Node Load(BinaryReader reader)
        {
            Node result = new Node(reader);
            Loaded = true;
            return result;
        }
        */
        #region Overrides of Object

        /// <summary>
        ///     Returns a string that represents the current object.
        /// </summary>
        /// <returns>
        ///     A string that represents the current object.
        /// </returns>
        public override string ToString()
        {
            return new string(new char[] {(char)Data});
        }

        #endregion

        protected override bool CheckMatch(byte word)
        {
            if(Id == -1)
                return false;
            if(word == 0)
                return Data == 0;
            if(Data == 0)
                return false;
            if(Data == '~')
                return word == '{';

            return Data.Equals(word); // || Loaded && EditDistance.PerformMeasure(Data, word) < 2;
        }

  /*      public override int MatchLevel(byte word)
        {
            if(Id == -1) //Root element does not match any words
                return 99;
            if(word == 0)
                return (Data == 0) ? 0 : 99;
            if(Data == 0)
                return 99;
            if(Data == '~')
                return word == '{' ? 0 : 2;

            return Data.CompareTo(word); //EditDistance.PerformMeasure(Word, word) : string.CompareOrdinal(Word, word);
        }*/

        public static explicit operator Node(byte suffix) { return new Node(suffix, -1); }

        public static explicit operator byte(Node element) { return element.Data; }

        #region IEquatable implementation

        public bool Equals(byte other) { return Id != -1 && Data.Equals(other); }

        public bool Equals(Node obj) { return Id != -1 && obj != null && obj.Data.Equals(Data); }

        #endregion

        #region IComparable implementation

        public int CompareTo(Node other)
        {
            return
                other == null || Id == -1
                    ? 1
                    : Data.CompareTo(other.Data); //(Loaded ? EditDistance.PerformMeasure(Word, other.Word) : string.CompareOrdinal(Word, other.Word));
        }

        public int CompareTo(byte other)
        {
            return Id == -1 ? 1 : Data.CompareTo(other); //(Loaded ? EditDistance.PerformMeasure(Word, other) : string.CompareOrdinal(Word, other)); }
        }

        #endregion
    }
}