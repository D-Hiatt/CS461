// //
// //    SuffixTree.cs
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
    public class SuffixTree
    {
        public Element Root { get; }

        /// <summary>
        /// Builds a suffix tree.  Every string is split into individual words, and each word associated with an <see cref="Element"/>.
        /// </summary>
        /// <param name="reader"></param>
        public SuffixTree(IDictionary<string, HashSet<int>> reader)
        {
            Console.WriteLine("Building new tree from {0} words.", reader.Count());
            Root = new Element();
            reader?.ForEach(kv =>
            {
                Element current = Root;
                int val = kv.Value.First();
                kv.Key.Split(' ').Where(w => !string.IsNullOrEmpty(w)).ForEach(w => current.Add(w, val, out current));
                current?.End(val);
            });
            Element.Loaded = true;
        }

        public SuffixTree(BinaryReader reader)
        {
            Console.WriteLine("Loading Tree");
            switch(reader.ReadInt32()) //version
            {
                case 1:
                    Root = Element.Load(reader);
                    break;
                default:
                    Root = new Element();
                    reader.ReadInt32();
                    break;
            }
        }

        public void Save(string nm)
        {
            Console.WriteLine("Saving Tree to {0}", nm);
            using(BinaryWriter writer = new BinaryWriter(new FileStream(nm, FileMode.Create, FileAccess.Write, FileShare.None)))
            {
                writer.Write(1); //version
                Root.Save(writer);
            }
        }

        /// <summary>
        /// Attempt to match the argument with any branch in the tree
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public int GetLocalization(string text)
        {
            if(string.IsNullOrEmpty(text))
                return -1;
            Element current = Root;
            return text.Split(' ').All(w => current.GetMatch(w, out current)) && current.Terminal ? current.Value : -1;
        }

        

        /// <summary>
        /// Locate a full pattern that matches to a specific replacement id.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public string FindId(int id)
        {
            string result = "";
            Element current;
            if(!Root.GetMatch(id, out current))
                throw new TreeException($"CANNOT FIND ROOT ID {id}", id);
            while(current != null)
            {
                if(string.IsNullOrEmpty(result))
                    result = current.Word;
                else
                    result += " " + current.Word;
                if(current.Terminal && current.Value == id)
                    break;
                string last = current.Word;
                if(!current.GetMatch(id, out current))
                    throw new TreeException($"CANNOT FIND ID {id}", id, last);
            }
            return result;
        }

        /// <summary>
        /// Check if a string matches any branch in this tree
        /// </summary>
        /// <param name="quote"></param>
        /// <returns></returns>
        public bool CheckMatch(string quote)
        {
            if(string.IsNullOrEmpty(quote))
                return false;
            Element current = Root;
            return quote.Split(' ').All(w => current.GetMatch(w, out current)) && current.Terminal;
        }
    }
}