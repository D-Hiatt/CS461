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
    public static class MultiByteSR
    {
        /// <summary>
        /// A list containing all possible pattern replacements
        /// </summary>
        private static readonly List<string> m_Replacements = new List<string>();
        public static readonly string TreeSave = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Tree.bin");

        /// <summary>
        /// Work queue containing files and their respective matches to be written.
        /// </summary>
        private static readonly BlockingCollection<Tuple<string, List<Bookmark>>> Replacer =
            new BlockingCollection<Tuple<string, List<Bookmark>>>(new ConcurrentBag<Tuple<string, List<Bookmark>>>());


        public static Trie Tree { get; private set; }

        /// <summary>
        /// Determines whether files are directly written to or a backup copy is made.
        /// </summary>
        public static bool Backup { get; set; }

        /// <summary>
        /// The identifier surrounding each pattern and replacement
        /// </summary>
        public static char Grouping { get; set; } = '"';


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
                Tree = new Trie(new BinaryReader(new FileStream(fn, FileMode.Open, FileAccess.Read, FileShare.Read)));
            }
            else
            {
              //  ConcurrentDictionary<string, HashSet<int>> dict1 = new ConcurrentDictionary<string, HashSet<int>>();
                //Otherwise build a new suffix tree
              //  ;
               // HashSet<int> del;
               // dict1.TryRemove("", out del);//Remove any blank strings
                Tree = new Trie(ReadData(fn));
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
        /// Saves the <see cref="Trie"/>.  Useful when loading large pattern files, to avoid rebuilding the tree each time.
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
        /// <param name="fn">A filename containing patterns and replacements</param>
        private static IEnumerable<Tuple<byte[], int>> ReadData(string fn)
        {
            if(!File.Exists(fn))
            {
                Console.WriteLine("Pattern file does not exist at '{0}'", fn);
            }
            else
            {
                Console.WriteLine("Reading patterns");
                using(StreamReader bin = new StreamReader(new FileStream(fn, FileMode.Open, FileAccess.Read, FileShare.Read)))
                {
                    string line;
                    Encoding enc = GetEncoding(bin.BaseStream);
                    while(!string.IsNullOrEmpty(line = bin.ReadLine()))
                    {
                        int startidx = line.IndexOf(Grouping) + 1;
                        string pattern = line.Substring(startidx, line.IndexOf(Grouping, startidx) - startidx);
                        startidx = line.IndexOf(Grouping, startidx + pattern.Length + 2) + 1;
                        string replace = line.Substring(startidx, line.IndexOf(Grouping, startidx + 1) - startidx);
                        if(!m_Replacements.Contains(replace))
                            m_Replacements.Add(replace);
                        yield return new Tuple<byte[], int>(enc.GetBytes(pattern), m_Replacements.IndexOf(replace));
                    }
                }
            }
        }


        /// <summary>
        /// Uses the <see cref="Trie"/> to search through all the files requested in <see cref="MainClass.Work"/>
        /// </summary>
        /// <returns>A list of tasks that should be waited on</returns>
        public static IEnumerable<Task> StartSearch()
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
                        Tuple<string, List<Bookmark>> file;
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
        /// <param name="file">The file to replace patterns in</param>
        /// <param name="work">A list of <see cref="Bookmark"/> detailing where to replace patterns</param>
        /// <returns></returns>
        private static async Task WriteAwait(string file, List<Bookmark> work)
        {
            await Task.Run(() =>
            {
                Interlocked.Increment(ref MainClass.Active);

                string tmp = Path.GetTempFileName();
                if(Backup)
                {
                    string back = file + ".bak";
                    int c = 1;
                    while(File.Exists(back))
                        back = Path.ChangeExtension(back, ".bak" + c++);
                    File.Copy(file, back);
                }

                byte[] buffer = new byte[2056];
                long current = 2056;
                work.Sort((a,b)=>a.Start.CompareTo(b.Start));
                using(FileStream writer = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.Write))
                {
                    using(FileStream reader = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        Encoding enc = GetEncoding(reader);
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

                            string replace = m_Replacements[loc.Id];
                            writer.Write(enc.GetBytes(replace), 0, replace.Length);
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
                string tmp2 = Path.GetTempFileName() + Task.CurrentId;
                File.Move(file, tmp2);
                File.Move(tmp, file);
                File.Delete(tmp2);
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
                //List<Translation> result = await FilterResults(fn, list).ConfigureAwait(false);
                if(list.Count > 0)
                {
                    Replacer.Add(new Tuple<string, List<Bookmark>>(fn, list));
                    MainClass.Log.Add(list.Aggregate("",
                                                       (a, b) => (string.IsNullOrEmpty(a) ? "" : a + Environment.NewLine) +
                                                                 $"{fn}:  {m_Replacements[b.Id]} <- '{list.Find(r => r.Start == b.Start).Text}'"));
                }
                Interlocked.Decrement(ref MainClass.Active);
            }, MainClass.Token).ConfigureAwait(false);
        }

        /// <summary>
        /// Determines a text file's encoding by analyzing its byte order mark (BOM).
        /// Defaults to ASCII when detection of the text file's endianness fails.
        /// Obtained from: https://stackoverflow.com/questions/3825390/effective-way-to-find-any-files-encoding
        /// </summary>
        /// <param name="file">The text file to analyze.</param>
        /// <returns>The detected encoding.</returns>
        private static Encoding GetEncoding(Stream file)
        {
            // Read the BOM
            byte[] bom = new byte[4];
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
            List<Bookmark> list = new List<Bookmark>();
            using (FileStream reader = new FileStream(fn, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                Encoding enc = GetEncoding(reader);
                int count = 0;
                int c;
                long start = 0;
                Node ele = Tree.Root;
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
                        bbuff[count++] = (byte)c;
                        list.Add(new Bookmark(start, count, enc.GetString(bbuff, 0, count), ele.Value));
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
    }
}