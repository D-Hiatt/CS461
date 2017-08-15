// //
// //    BaseNode.cs
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
using System.IO;
using System.Linq;

namespace MultiGrep
{
    public abstract class BaseNode
    {
        public static bool Loaded;

        protected readonly int Id;

        protected readonly HashSet<int> IdSet;

        /// <summary>
        /// A list of elements that may come after this element
        /// </summary>
        protected List<BaseNode> Links { get; }

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
        public bool Terminal => Id > -1;// && (Data == 0 || Id > 0 || Links.Any(s => s.Data == 0));

        /// <exception cref="TerminalException" accessor="get">No terminal value</exception>
        public int Value
        {
            get
            {
                if (Terminal)
                {
                    if (Id > -1)
                        return Id;
                    if (IdSet.Count == 1)
                        return IdSet.First();
                    if (Links.Count > 0)
                        return Links.First(u => u.Terminal).Value;
                    return -1;
                    //throw new TerminalException("No terminal value");
                }
                return 0;
            }
        }

        /// <summary>
        /// Create a new root element
        /// </summary>
        protected BaseNode()
        {
            Id = -1;
            IdSet = new HashSet<int>();
            Links = new List<BaseNode>();
        }

        protected BaseNode(int id, bool term = false)
        {
            IdSet = term ? new HashSet<int> { id } : new HashSet<int>();
            Links = new List<BaseNode>();
            Id = term ? id : 0;
        }

        /// <summary>
        /// Represents a terminal node
        /// </summary>
        /// <param name="id"></param>
        protected BaseNode(int id)
        {
            IdSet = new HashSet<int>();
            Id = id;
            Links = new List<BaseNode>();
        }


        /// <summary>
        /// Add a terminal node to this one
        /// </summary>
        /// <param name="id"></param>
        public  void End(int id)
        {
            if (!Terminal && Id > -1)
                Links.Add(new Node(id));
        }

        public bool Combine(int id) { return IdSet.Add(id); }

        /// <exception cref="TerminalException">Cannot link off of terminals</exception>
        public abstract bool Add(byte word, int id, out BaseNode ele);

        private static int InnerDepth(IReadOnlyCollection<BaseNode> links)
        {
            if (links == null)
                return 0;
            if (links.Count > 0)
                return 1 + links.Max(s => InnerDepth(s.Links));
            return 1;
        }

        /// <summary>
        /// Check if this element has any links matching the argument
        /// </summary>
        /// <param name="word"></param>
        /// <returns></returns>
        public bool HasMatch(byte word)
        {
            return Links.Any(u => u.IsMatch(word));
        }

        public bool GetMatch(int id, out BaseNode ele)
        {
            ele = Links.Find(u => u.IdSet.Contains(id) || u.Id == id);
            return ele != null;
        }

        public bool GetMatch(byte word, out BaseNode ele)
        {
            //if(!Loaded)
            return (ele = Links.FirstOrDefault(u => u.IsMatch(word))) != null;
            // ele = Links.FirstOrDefault(u => u.Data == word);
            // return ele != null;
        }

        public BaseNode GetMatch(byte word)
        {
     //       if (!Loaded)
                return Links.FirstOrDefault(u => u.IsMatch(word));
   /*         BaseNode ele = null;
            int low = 2;
            foreach (BaseNode u in Links)
            {
                int tmp = u.MatchLevel(word);
                if (tmp == 0)
                    return u;
                if (low > tmp)
                {
                    low = tmp;
                    ele = u;
                }
            }
            return ele;*/
        }

        public bool IsMatch(int id) { return Id == id || IdSet.Contains(id); }

        public bool IsMatch(byte word) { return !Terminal && CheckMatch(word); }

        protected abstract bool CheckMatch(byte word);

       // public abstract int MatchLevel(byte word);
    }
}