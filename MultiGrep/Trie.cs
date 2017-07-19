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
    public class Trie
    {
        public Node Root { get; }

        /// <summary>
        /// Builds a suffix tree.  Every string is split into individual words, and each word associated with an <see cref="Node"/>.
        /// </summary>
        /// <param name="reader"></param>
        public Trie(IDictionary<byte[], HashSet<int>> reader)
        {
            Console.WriteLine("Building new tree from {0} words.", reader.Count());
            Root = new Node();
            reader.ForEach(kv =>
            {
                Node current = Root;
                int val = kv.Value.First();
                kv.Key.ForEach(b=> current.Add(b, val, out current));   //Build the branch using each byte as a node
                current?.End(val);      //Mark the final node as a terminal
            });
            Node.Loaded = true;
        }

        public Trie(IEnumerable<Tuple<byte[], int>> tuples)
        {
            Root = new Node();
            Console.WriteLine("Building new tree from {0} words.", tuples.Count());
            tuples.ForEach(t =>
            {
                Node current = Root;
                t.Item1.ForEach(b=>current.Add(b, t.Item2, out current));
                current?.End(t.Item2);
            });
            Node.Loaded = true;
        }

        public Trie(BinaryReader reader)
        {
            Console.WriteLine("Loading Tree");
            switch(reader.ReadInt32()) //version
            {
                case 1:
                    Root = Node.Load(reader);
                    break;
                default:
                    Root = new Node();
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
    }
}