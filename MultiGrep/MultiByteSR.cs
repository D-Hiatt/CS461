// //
// //    ClilocTasks.cs
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MultiGrep
{
    public class MultiByteSR
    {
        private static readonly byte[] LocalConst = Encoding.Default.GetBytes("LocalizedMessage(");
        private static readonly byte[] Comment = Encoding.Default.GetBytes("  //");
        private static readonly string Temp = Path.Combine(Path.GetTempPath(), "LocalizedData");
        private static readonly string m_Path = "";
        public static char Grouping = '"';
        private static readonly List<string> m_Replacements = new List<string>();
        public static readonly string TreeSave = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Tree.bin");

        private static readonly BlockingCollection<Tuple<string, List<Translation>>> Replacer =
            new BlockingCollection<Tuple<string, List<Translation>>>(new ConcurrentBag<Tuple<string, List<Translation>>>());

        public static ByteTree Tree { get; private set; }
        public static List<byte[]> Ignore { get;  } = new List<byte[]>();


        /// <summary>
        /// 
        /// </summary>
        /// <param name="fn">A file containing a list of patterns followed by a replacement string</param>
        /// <returns></returns>
        public static bool Initialize(string fn = "")
        {
            if(Tree != null)
                return true;
            if(string.IsNullOrEmpty(fn))
                fn = TreeSave;
            if(!File.Exists(fn))
            {
                fn = TreeSave;
                if(!File.Exists(fn))    //Pattern file does not exist
                    return false;
            }
            if(fn == TreeSave || fn.EndsWith(".bin", StringComparison.OrdinalIgnoreCase))
            {
                //Load the tree from a saved file
                Tree = new ByteTree(new BinaryReader(new FileStream(fn, FileMode.Open, FileAccess.Read, FileShare.Read)));
            }
            else
            {
                ConcurrentDictionary<string, HashSet<int>> dict1 = new ConcurrentDictionary<string, HashSet<int>>();
                //Otherwise build a new suffix tree
                ReadData(fn, (n, s) => dict1.AddOrUpdate(s, new HashSet<int> {n}, (k, v) =>
                {
                    v.Add(n);
                    return v;
                }));
                HashSet<int> del;
                dict1.TryRemove("", out del);//Remove any blank strings
                Tree = new ByteTree(dict1);
            }
            return Tree != null;
        }

        public static string ConvertIDToString(int id)
        {
            if(id >= 0 && id < m_Replacements.Count)
                return m_Replacements[id];
            return "";
        }

        /// <summary>
        /// Saves the <see cref="SuffixTree"/>.  Useful when loading large pattern files, to avoid rebuilding the tree each time.
        /// </summary>
        /// <param name="fn"></param>
        public static void Save(string fn)
        {
            if(!string.IsNullOrEmpty(fn))
                Tree?.Save(fn);
        }

        /// <summary>
        /// Read the list of patterns and replacement patterns
        /// </summary>
        /// <param name="fn"></param>
        /// <param name="action"></param>
        public static void ReadData(string fn, Action<int, string> action)
        {
            if(!File.Exists(fn))
            {
                Console.WriteLine("Pattern file does not exist at '{0}'", fn);
                return;
            }
            Console.WriteLine("Reading patterns");
            using(StreamReader bin = new StreamReader(new FileStream(fn, FileMode.Open, FileAccess.Read, FileShare.Read)))
            {
                string line = bin.ReadLine();
                int startidx = line.IndexOf(Grouping) + 1;
                string pattern = line.Substring(startidx, line.IndexOf(Grouping, startidx) - startidx);
                startidx = line.IndexOf(Grouping, startidx + pattern.Length + 2, Grouping) + 1;
                string replace = line.Substring(startidx, line.IndexOf(Grouping, startidx + 1) - startidx);
                if(!m_Replacements.Contains(replace))
                    m_Replacements.Add(replace);
                action(m_Replacements.IndexOf(replace), pattern);
            }
        }


        public static Task[] StartSearch()
        {
            return new Task[]
            {
                Task.Run(() =>
                {
                    ConcurrentBag<Task> list = new ConcurrentBag<Task>();
                    while(!MainClass.Work.IsCompleted)
                    {
                        string file;
                        if(MainClass.Work.TryTake(out file))
                            list.Add(RunAwait(file));
                    }
                    return list;
                }, MainClass.Token).ContinueWith(a =>
                {
                    Task.WaitAll(a.Result.ToArray());
                    Replacer.CompleteAdding();
                }),
                Task.Run(() =>
                {
                    ConcurrentBag<Task> list = new ConcurrentBag<Task>();
                    while(!Replacer.IsCompleted)
                    {
                        Tuple<string, List<Translation>> file;
                        if(Replacer.TryTake(out file))
                            list.Add(WriteAwait(file.Item1, file.Item2));
                    }
                    return list;
                }, MainClass.Token).ContinueWith(b => Task.WaitAll(b.Result.ToArray()))
            };
        }

        /// <summary>
        /// An async file writer.  It replaces patterns with their respective matches
        /// </summary>
        /// <param name="file"></param>
        /// <param name="work"></param>
        /// <returns></returns>
        private static async Task WriteAwait(string file, List<Translation> work)
        {
            await Task.Run(() =>
            {
                Interlocked.Increment(ref MainClass.Active);

                string atmp = file.Remove(0, file.IndexOf(Path.DirectorySeparatorChar) + 1);
                atmp = Path.GetDirectoryName(atmp.Replace(m_Path, ""));
                Directory.CreateDirectory(Path.Combine(Temp, atmp));
                string tmp = Path.Combine(Temp, atmp, Path.ChangeExtension(Path.GetFileName(file), ".tmp"));

                byte[] buffer = new byte[2056];
                long current = 2056;
                work.Sort(new TranslationComparer());
                using(FileStream writer = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.Write))
                {
                    using(FileStream reader = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        bool nxt = false;
                        work.ForEach(loc =>
                        {
                            int length = (int)(loc.Start - reader.Position - 1);
                            if(length > current)
                            {
                                current = loc.Start - reader.Position;
                                buffer = new byte[current];
                            }
                            reader.Read(buffer, 0, length);
                            if(nxt)
                            {
                                int correct = 0;
                                int n = 0;
                                while(n < length)
                                {
                                    byte bit = buffer[n++];
                                    if(bit == MainClass.NewLine[correct])
                                    {
                                        if(++correct == MainClass.NewLine.Length)
                                            break;
                                    }
                                    else if(bit != 0x20 || correct > 0)
                                    {
                                        writer.Write(MainClass.NewLine, 0, MainClass.NewLine.Length);
                                        break;
                                    }
                                }
                            }
                            writer.Write(buffer, 0, length);
                            writer.Write(LocalConst, 0, LocalConst.Length);


                            int id = loc.Id;
                            int i = 0;
                            int div = id;
                            while(div > 9) //Convert the id into digits in the localization
                            {
                                buffer[10 - i] = (byte)((div % 10 + 48) & 0xFF);
                                div /= 10;
                                ++i;
                            }
                            buffer[10 - i] = (byte)((div + 48) & 0xFF); //Convert the final digit
                            writer.Write(buffer, 10 - i, i + 1); //Write the id.
                            reader.Position = loc.Start + loc.Length + 1; //Skip the the end of this line.
                            length = (int)(loc.End - reader.Position); //Get the length of the remaining arguments.
                            reader.Read(buffer, 0, length); //Read the arguments
                            writer.Write(buffer, 0, length); //Write the arguments.
                            //writer.Write(Comment, 0, Comment.Length); //Add a comment
                            byte[] local = Encoding.Default.GetBytes(Tree.FindId(loc.Id));
                            writer.Write(local, 0, local.Length);
                            nxt = true;
                        });
                        int pos;
                        while((pos = (int)(reader.Length - reader.Position)) != 0)
                        {
                            reader.Read(buffer, 0, current > pos ? pos : (int)current);
                            writer.Write(buffer, 0, current > pos ? pos : (int)current);
                        }
                    }
                }
                MainClass.Writes.Add(file);
                Interlocked.Decrement(ref MainClass.Active);
            }, MainClass.Token).ConfigureAwait(false);
        }

        private static async Task RunAwait(string fn)
        {
            await Task.Run(async () =>
            {
                Interlocked.Increment(ref MainClass.Active);
                List<Bookmark> list = ParseFile(fn);
                List<Translation> result = await FilterResults(fn, list).ConfigureAwait(false);
                if(result.Count > 0)
                {
                    Replacer.Add(new Tuple<string, List<Translation>>(fn, result));
                    MainClass.Log.Add(result.Aggregate("",
                                                       (a, b) => (string.IsNullOrEmpty(a) ? "" : a + Environment.NewLine) +
                                                                 $"{fn}:  {b.Id} <- '{list.Find(r => r.Start == b.Start).Text}'"));
                }
                Interlocked.Decrement(ref MainClass.Active);
            }, MainClass.Token).ConfigureAwait(false);
        }

        public static async Task<List<Translation>> FilterResults(string fn, List<Bookmark> list)
        {
            return await Task.Run(async () =>
            {
                List<Translation> result = new List<Translation>();
                if(list.Count > 0)
                {
                    Interlocked.Increment(ref MainClass.Active);//Increment the number of active tasks
                    foreach(Bookmark t in list)
                    {
                        int res;
                        if((res = await Match(t.Text).ConfigureAwait(false)) != 0)
                        {
                            result.Add(t.Convert(res));
                            Interlocked.Increment(ref MainClass.Found);//Increment the number of matching patterns found
                        }
                    }
                    Interlocked.Decrement(ref MainClass.Active);//Decrement the number of active tasks
                }
                return result;
            }, MainClass.Token).ConfigureAwait(false);
        }

        /// <summary>
        /// Determines a text file's encoding by analyzing its byte order mark (BOM).
        /// Defaults to ASCII when detection of the text file's endianness fails.
        /// Obtained from: https://stackoverflow.com/questions/3825390/effective-way-to-find-any-files-encoding
        /// </summary>
        /// <param name="file">The text file to analyze.</param>
        /// <returns>The detected encoding.</returns>
        public static Encoding GetEncoding(FileStream file)
        {
            // Read the BOM
            var bom = new byte[4];
            file.Read(bom, 0, 4);


            // Analyze the BOM
            if(bom[0] == 0x2b && bom[1] == 0x2f && bom[2] == 0x76)
            {
                file.Seek(2, SeekOrigin.Begin);
                return Encoding.UTF7;
            }
            if(bom[0] == 0xef && bom[1] == 0xbb && bom[2] == 0xbf)
            {
                file.Seek(2, SeekOrigin.Begin);
                return Encoding.UTF8;
            }
            if (bom[0] == 0xff && bom[1] == 0xfe)
            {
                file.Seek(2, SeekOrigin.Begin);
                return Encoding.Unicode; //UTF-16LE
            }
            if (bom[0] == 0xfe && bom[1] == 0xff)
            {
                file.Seek(2, SeekOrigin.Begin);
                return Encoding.BigEndianUnicode; //UTF-16BE
            }
            if (bom[0] == 0 && bom[1] == 0 && bom[2] == 0xfe && bom[3] == 0xff) return Encoding.UTF32;
            file.Seek(0, SeekOrigin.Begin);
            return Encoding.ASCII;
        }

        private static List<Bookmark> ParseFile(string fn)
        {
            // List<Tuple<long, int, string>> list = new List<Tuple<long, int, string>>();
            List<Bookmark> list = new List<Bookmark>();
            using (FileStream reader = new FileStream(fn, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                Encoding enc = GetEncoding(reader);
                int count = 0;
                int c;
                long start = 0;
                Trie ele = Tree.Root;
                byte[] bbuff = new byte[2056];
                while((c = reader.ReadByte()) != -1)
                {
                    if(!ele.GetMatch((byte)c, out ele))
                    {
                        ele = Tree.Root;
                        count = 0;
                    }
                    else if(ele.Terminal)
                    {
                        bbuff[count] = (byte)c;
                        list.Add(new Bookmark(start, count, enc.GetString(bbuff, 0, count)));
                        ele = Tree.Root;
                        count = 0;
                    }
                    else
                    {
                        bbuff[count++] = (byte)c;
                        if(count == 1)
                            start = reader.Position;
                        if (count >= bbuff.Length)
                        {
                            byte[] tmp = new byte[count + 256];
                            Buffer.BlockCopy(bbuff, 0, tmp, 0, count);
                            bbuff = tmp;
                        }
                    }
                }
            }
            return list;
        }

        private static async Task<int> Match(string line) { return await Task.Run(() => Tree.GetLocalization(line)).ConfigureAwait(false); }

        public sealed class TranslationComparer : IComparer<Translation>
        {
            #region Implementation of IComparer<in Tuple<long,int,int>>

            /// <summary>
            ///     Compares two objects and returns a value indicating whether one is less than, equal to, or greater than the other.
            /// </summary>
            /// <returns>
            ///     A signed integer that indicates the relative values of <paramref name="x" /> and <paramref name="y" />, as shown in
            ///     the following table.Value Meaning Less than zero<paramref name="x" /> is less than <paramref name="y" />.Zero
            ///     <paramref name="x" /> equals <paramref name="y" />.Greater than zero<paramref name="x" /> is greater than
            ///     <paramref name="y" />.
            /// </returns>
            /// <param name="x">The first object to compare.</param>
            /// <param name="y">The second object to compare.</param>
            public int Compare(Translation x, Translation y)
            {
                return (int)(x.Start - y.Start);
            }

            #endregion
        }
    }
}