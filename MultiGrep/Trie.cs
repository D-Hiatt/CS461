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
    public class Trie : IEquatable<byte>, IComparable<byte>, IEquatable<Trie>, IComparable<Trie>
    {
        public static bool Loaded;
        private readonly int Id;

        private readonly HashSet<int> IdSet;

        /// <summary>
        /// The word represented by this element
        /// </summary>
        public byte Data { get; }

        /// <summary>
        /// A list of elements that may come after this element
        /// </summary>
        private List<Trie> Links { get; }

        /// <summary>
        /// The number of branches linking off this element
        /// </summary>
        public int Breadth => Links.Count;

        /// <summary>
        /// The max depth linking off this element
        /// </summary>
        public int Depth => Links?.Max(s => InnerDepth(s.Links)) ?? 0;

        /// <summary>
        /// Indicates if this element can be ended on
        /// </summary>
        public bool Terminal => Id > -1 && (Data == 0 || Id > 0 || Links.Any(s => s.Data == 0));

        /// <exception cref="TerminalException" accessor="get">No terminal value</exception>
        public int Value
        {
            get
            {
                if(Terminal)
                {
                    if(Id > 0)
                        return Id;
                    if(IdSet.Count == 1)
                        return IdSet.First();
                    if(Links.Count > 0)
                        return Links.First(u => u.Data == 0).Value;
                    throw new TerminalException("No terminal value");
                }
                return 0;
            }
        }

        private Trie(BinaryReader reader)
        {
            int version = reader.ReadInt32();
            int len;
            IdSet = new HashSet<int>();
            switch(version) //version
            {
                default:
                    Data = reader.ReadByte();

                    Id = reader.ReadInt32();
                    len = reader.ReadInt32();
                    for(int i = 0; i < len; ++i)
                        IdSet.Add(reader.ReadInt32());
                    len = reader.ReadInt32();
                    Links = new List<Trie>(len);
                    for(int i = 0; i < len; ++i)
                        Links.Add(new Trie(reader));
                    break;
            }
        }

        /// <summary>
        /// Create a new root element
        /// </summary>
        public Trie()
        {
            Id = -1;
            Data = 0;
            IdSet = new HashSet<int>();
            Links = new List<Trie>();
        }

        public Trie(byte word, int id)
        {
            IdSet = new HashSet<int> {id};
            Data = word;
            Links = new List<Trie>();
            Id = 0;
        }

        /// <summary>
        /// Represents a terminal node
        /// </summary>
        /// <param name="id"></param>
        private Trie(int id)
        {
            Data = 0;
            IdSet = new HashSet<int>();
            Id = id;
            Links = new List<Trie>();
        }

        /// <summary>
        /// Add a terminal node to this one
        /// </summary>
        /// <param name="id"></param>
        public void End(int id)
        {
            if(!Terminal && Id > -1)
                Links.Add(new Trie(id));
        }

        public bool Combine(int id) { return IdSet.Add(id); }

        /// <exception cref="TerminalException">Cannot link off of terminals</exception>
        public bool Add(byte word, int id, out Trie ele)
        {
            if(Data == 0)
                throw new TerminalException("Cannot link off of terminals");
            ele = Links.FirstOrDefault(u => u.IsMatch(word)); //Check to see if an element matching this word already exists in our links
            if(ele == null)
            {
                Links.Add(ele = new Trie(word, id)); //If it doesnt, add the new element to our link list
                return true;
            }
            return ele.Combine(id); //If it does, add the id to the linked element
        }

        private static int InnerDepth(IReadOnlyCollection<Trie> links)
        {
            if(links == null)
                return 0;
            if(links.Count > 0)
                return 1 + links.Max(s => InnerDepth(s.Links));
            return 1;
        }

        public void Save(BinaryWriter writer)
        {
            writer.Write(2); //version
            writer.Write(Data);
            //writer.Write(Start);
            writer.Write(Id);
            writer.Write(IdSet.Count);
            IdSet.ForEach(writer.Write);
            writer.Write(Breadth);
            Links.ForEach(s => s.Save(writer));
        }

        public static Trie Load(BinaryReader reader)
        {
            Trie result = new Trie(reader);
            Loaded = true;
            return result;
        }

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

        /// <summary>
        /// Check if this element has any links matching the argument
        /// </summary>
        /// <param name="word"></param>
        /// <returns></returns>
        public bool HasMatch(byte word)
        {
            return Links.Any(u => u.IsMatch(word));
        }

        public bool GetMatch(int id, out Trie ele)
        {
            ele = Links.Find(u => u.IdSet.Contains(id) || u.Id == id);
            return ele != null;
        }

        public bool GetMatch(byte word, out Trie ele)
        {
            if(!Loaded)
                return (ele = Links.FirstOrDefault(u => u.IsMatch(word))) != null;
            int low = 2;
            ele = null;
            foreach(Trie u in Links)
            {
                int tmp = u.MatchLevel(word);
                if(tmp == 0)
                {
                    ele = u;
                    return true;
                }
                if(low > tmp)
                {
                    low = tmp;
                    ele = u;
                }
            }
            return ele != null;
        }

        public Trie GetMatch(byte word)
        {
            if(!Loaded)
                return Links.FirstOrDefault(u => u.IsMatch(word));
            Trie ele = null;
            int low = 2;
            foreach(Trie u in Links)
            {
                int tmp = u.MatchLevel(word);
                if(tmp == 0)
                    return u;
                if(low > tmp)
                {
                    low = tmp;
                    ele = u;
                }
            }
            return ele;
        }

        public bool IsMatch(int id) { return Id == id || IdSet.Contains(id); }

        public bool IsMatch(byte word)
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

        public int MatchLevel(byte word)
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
        }

        public static explicit operator Trie(byte suffix) { return new Trie(suffix, -1); }

        public static explicit operator byte(Trie element) { return element.Data; }

        #region IEquatable implementation

        public bool Equals(byte other) { return Id != -1 && Data.Equals(other); }

        public bool Equals(Trie obj) { return Id != -1 && obj != null && obj.Data.Equals(Data); }

        #endregion

        #region IComparable implementation

        public int CompareTo(Trie other)
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