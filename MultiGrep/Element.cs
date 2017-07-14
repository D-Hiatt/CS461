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
    public class Element : IEquatable<string>, IComparable<string>, IEquatable<Element>, IComparable<Element>
    {
        public static bool Loaded;
        private readonly int Id;

        private readonly HashSet<int> IdSet;
        /// <summary>
        /// The word represented by this element
        /// </summary>
        public string Word { get; }

        /// <summary>
        /// A list of elements that may come after this element
        /// </summary>
        private List<Element> Links { get; }
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
        public bool Terminal => Id > -1 && (string.IsNullOrEmpty(Word) || Id > 0 || Links.Any(s => string.IsNullOrEmpty(s.Word)));

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
                        return Links.First(u => string.IsNullOrEmpty(u.Word)).Value;
                    throw new TerminalException("No terminal value");
                }
                return 0;
            }
        }

        private Element(BinaryReader reader)
        {
            Links = new List<Element>();
            int version = reader.ReadInt32();
            int len;
            IdSet = new HashSet<int>();
            switch(version) //version
            {
                case 2:
                    string word = reader.ReadString();
                    Word = !string.IsNullOrEmpty(word) && word.Length < 5 ? string.Intern(word) : word;
                    Id = reader.ReadInt32();
                    len = reader.ReadInt32();
                    for(int i = 0; i < len; ++i)
                        IdSet.Add(reader.ReadInt32());
                    len = reader.ReadInt32();
                    for(int i = 0; i < len; ++i)
                        Links.Add(new Element(reader));
                    break;
                default:
                    Word = reader.ReadString();
                    /*Start =*/
                    if(version == 0)
                        reader.ReadBoolean();
                    Id = reader.ReadInt32();
                    len = reader.ReadInt32();
                    for(int i = 0; i < len; ++i)
                        Links.Add(new Element(reader));
                    break;
            }
        }

        /// <summary>
        /// Create a new root element
        /// </summary>
        public Element()
        {
            Id = -1;
            Word = "Root Element";
            IdSet = new HashSet<int>();
            Links = new List<Element>();
        }

        public Element(string word, int id)
        {
            IdSet = new HashSet<int> {id};
            Word = !string.IsNullOrEmpty(word) && word.Length < 5 ? string.Intern(word) : word;
            Links = new List<Element>();
            Id = 0;
        }

        private Element(int id)
        {
            Word = "";
            IdSet = new HashSet<int>();
            Id = id;
            Links = new List<Element>();
        }

        public void End(int id)
        {
            if(!Terminal && Id > -1)
                Links.Add(new Element(id));
        }

        public bool Combine(int id) { return IdSet.Add(id); }

        /// <exception cref="TerminalException">Cannot link off of terminals</exception>
        public bool Add(string word, int id, out Element ele)
        {
            if(string.IsNullOrEmpty(Word))
                throw new TerminalException("Cannot link off of terminals");
            ele = Links.FirstOrDefault(u => u.IsMatch(word));   //Check to see if an element matching this word already exists in our links
            if(ele == null)
            {
                Links.Add(ele = new Element(word, id));//If it doesnt, add the new element to our link list
                return true;
            }
            return ele.Combine(id);//If it does, add the id to the linked element
        }

        private static int InnerDepth(IReadOnlyCollection<Element> links)
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
            writer.Write(Word);
            //writer.Write(Start);
            writer.Write(Id);
            writer.Write(IdSet.Count);
            IdSet.ForEach(writer.Write);
            writer.Write(Breadth);
            Links.ForEach(s => s.Save(writer));
        }

        public static Element Load(BinaryReader reader)
        {
            Element result = new Element(reader);
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
            return Word;
        }

        #endregion

        /// <summary>
        /// Check if this element has any links matching the argument
        /// </summary>
        /// <param name="word"></param>
        /// <returns></returns>
        public bool HasMatch(string word) { return Links.Any(u => u.IsMatch(word)); }

        public bool GetMatch(int id, out Element ele)
        {
            ele = Links.Find(u => u.IdSet.Contains(id) || u.Id == id);
            return ele != null;
        }

        public bool GetMatch(string word, out Element ele)
        {
            if(!Loaded)
                return (ele = Links.FirstOrDefault(u => u.IsMatch(word))) != null;
            int low = 2;
            ele = null;
            foreach(Element u in Links)
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

        public Element GetMatch(string word)
        {
            if(!Loaded)
                return Links.FirstOrDefault(u => u.IsMatch(word));
            Element ele = null;
            int low = 2;
            foreach(Element u in Links)
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

        public bool IsMatch(string word)
        {
            if(Id == -1)
                return false;
            if(string.IsNullOrEmpty(word))
                return string.IsNullOrEmpty(Word);
            if(string.IsNullOrEmpty(Word))
                return false;
            if(Word[0] == '~')
                return word[0] == '{';

            return Word.Equals(word) || Loaded && EditDistance.PerformMeasure(Word, word) < 2;
        }

        public int MatchLevel(string word)
        {
            if(Id == -1)//Root element does not match any words
                return 99;
            if(string.IsNullOrEmpty(word))
                return string.IsNullOrEmpty(Word) ? 0 : 99;
            if(string.IsNullOrEmpty(Word))
                return 99;
            if(Word[0] == '~')
                return word[0] == '{' ? 0 : word.Length + Word.Length;

            return Loaded ? EditDistance.PerformMeasure(Word, word) : string.CompareOrdinal(Word, word);
        }

        public static explicit operator Element(string suffix) { return new Element(suffix, 0); }

        public static explicit operator string(Element element) { return element.Word; }

        #region IEquatable implementation

        public bool Equals(string other) { return Id != -1 && Word.Equals(other); }

        public bool Equals(Element obj) { return Id != -1 && obj != null && obj.Word.Equals(Word); }

        #endregion

        #region IComparable implementation

        public int CompareTo(Element other)
        {
            return other == null || Id == -1 ? 1 : (Loaded ? EditDistance.PerformMeasure(Word, other.Word) : string.CompareOrdinal(Word, other.Word));
        }

        public int CompareTo(string other) { return Id == -1 ? 1 : (Loaded ? EditDistance.PerformMeasure(Word, other) : string.CompareOrdinal(Word, other)); }

        #endregion
    }
}