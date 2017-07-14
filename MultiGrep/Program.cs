using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MultiGrep
{
    public class MainClass
    {
        //   public static readonly string TreeSave = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Tree.bin");
        //   private static readonly byte[] LocalConst = Encoding.Default.GetBytes("LocalizedMessage(");
        //   private static readonly byte[] Comment = Encoding.Default.GetBytes("  //");
        public static readonly byte[] NewLine = Encoding.Default.GetBytes(Environment.NewLine);

        private static readonly string Temp = Path.Combine(Path.GetTempPath(), "LocalizedData");
        private static readonly string LogPath = Path.Combine(Temp, "results.log");

        public static long Active;
        public static long Found;

        private static string m_Path = "";
        private static readonly CancellationTokenSource Source = new CancellationTokenSource();
        private static readonly ManualResetEvent Toggle = new ManualResetEvent(false);

        public static readonly BlockingCollection<string> Work = new BlockingCollection<string>(new ConcurrentBag<string>());
        public static readonly BlockingCollection<string> Log = new BlockingCollection<string>(new ConcurrentBag<string>());

        public static readonly ConcurrentBag<string> Writes = new ConcurrentBag<string>();
        public static CancellationToken Token => Source.Token;

        public static void Main(string[] args)
        {
            if(Directory.Exists(Temp))
            {
                Directory.EnumerateFiles(Temp).ForEach(s => File.SetAttributes(s, FileAttributes.Normal));
                try
                {
                    Directory.Delete(Temp, true);   //Delete any files in our temporary folder
                }
                catch(IOException) { }
            }
            Directory.CreateDirectory(Temp); //Create the temporary folder
            AppDomain.CurrentDomain.UnhandledException += (s, e) => Source.Cancel();        //Set up crash handler to cancel any async tasks
            Console.CancelKeyPress += (s, e) => Source.Cancel();                            //Same thing with ctrl+C
            //Set up a task to save any data obtained before exiting
            Source.Token.Register(() =>
            {
                if(Log.Count > 0)
                    using(StreamWriter writer = new StreamWriter(new FileStream(LogPath, FileMode.Create, FileAccess.Write, FileShare.None)))
                    {
                        string data;
                        while(Log.TryTake(out data))
                            writer.Write(data);
                    }
            }, false);
            string config = "";
            WorkMode workmode = WorkMode.Normal;
            Queue<string> work = new Queue<string>();
            args.ForEach(s =>
            {
                switch(s.Substring(0, 2))
                {

                    case "-g":
                        MultiByteSR.Grouping = s[2];
                        break;
                    case "-c":
                        config = s.Substring(2);
                        break;
                    case "-m":
                        if(!Enum.TryParse(s.Substring(2), out workmode))
                        {
                            int mode;
                            if(int.TryParse(s.Substring(2), out mode))
                                workmode = (WorkMode)mode;
                            else
                                workmode = WorkMode.Invalid;
                        }
                        break;
                    default:
                        work.Enqueue(s);
                        break;
                }
            });
            string fn = work.Count > 0 ? work.Dequeue() : "";
            switch(workmode)
            {
                case WorkMode.Invalid:
                    ExitMessage("Enter a filename.");
                    break;
                case WorkMode.DecryptCliloc:
                    if(string.IsNullOrEmpty(fn))
                        goto case WorkMode.Invalid;
                    string saveto = work.Count > 0 ? work.Dequeue() : Path.ChangeExtension(fn, ".txt");
                    using(StreamWriter writer = new StreamWriter(new FileStream(saveto, FileMode.Create, FileAccess.Write, FileShare.None)))
                        MultiSR.ReadData(fn, (n, s) => writer.WriteLine("{0} : {1}", n, s));

                    break;
                case WorkMode.Normal:
                    if(!MultiByteSR.Initialize(fn))
                    {
                        if(work.Count < 2)
                        {
                            ExitMessage("Must enter a directory or file to parse.");
                            break;
                        }
                    }
                    else
                    {
                        goto case WorkMode.Invalid;
                    }
                    fn = work.Peek();
                    m_Path = fn.Remove(0, fn.IndexOf(Path.DirectorySeparatorChar) + 1);
                    m_Path = m_Path.Split(Path.DirectorySeparatorChar).TakeWhile(s => s != "Scripts")
                                   .Aggregate("", (a, b) => string.IsNullOrEmpty(a) ? b : Path.Combine(a, b));
                    if(!string.IsNullOrEmpty(m_Path))
                        m_Path = Path.Combine(m_Path, "Scripts\\");
                    //       MainTasks.Add(Task.Run(() => Parallel.ForEach(Partitioner.Create(work.SelectMany(s => Directory.GetFiles(s, "*.cs", SearchOption.AllDirectories))), Work.Add), Source.Token).ContinueWith(t => Work.CompleteAdding()));
                    StartAll(MultiSR.StartSearch(), work.ToArray());
                    Task task = Monitor().ContinueWith(t => Console.WriteLine("Completed in {0}.  Changes: {1}", t.Result, Writes.Count));
                    task.Wait();

                    if(Writes.Count > 0)
                    {
                        using(StreamWriter writer = new StreamWriter(new FileStream(LogPath, FileMode.Create, FileAccess.Write, FileShare.None)))
                        {
                            string data;
                            while(Log.TryTake(out data))
                                writer.Write(data);
                        }
                        Process proc = Process.Start("notepad.exe", LogPath);
                        proc.WaitForExit();
                    }
                    else
                    {
                        ExitMessage("No matches found.");
                    }
                    break;
                case WorkMode.RebuildTree:
                    if(!MultiSR.Initialize(fn))
                        goto case WorkMode.Invalid;
                    MultiSR.Save(work.Count > 0 ? work.Dequeue() : Path.ChangeExtension(fn, ".bin"));
                    break;
                case WorkMode.Syntax: break;
            }
        }

        public static void ExitMessage(string format, params object[] args) { ExitMessage(string.Format(format, args)); }

        public static void ExitMessage(string message)
        {
            Console.WriteLine(message);
            Console.WriteLine("Press any key to exit the program");
            Console.ReadKey();
            Source.Cancel();
        }

        /// <summary>
        /// Starts a task the recursively searches a directory for files to search and replace in.
        /// </summary>
        /// <param name="tasks"></param>
        /// <param name="fn"></param>
        /// <returns></returns>
        private static async Task StartAll(IEnumerable<Task> tasks, params string[] fn)
        {
            await Task.Run(() => Parallel.ForEach(Partitioner.Create(fn.SelectMany(s => Directory.GetFiles(s, "*.cs", SearchOption.AllDirectories))), Work.Add),
                           Source.Token).ContinueWith(t =>
            {
                Work.CompleteAdding();
                Task.WaitAll(tasks.ToArray());
            }).ContinueWith(c => Toggle.Set()).ConfigureAwait(false);
        }

        //        private Task.Run 
        //    (() =>
        //        {
        //            ConcurrentBag<Task> list = new ConcurrentBag<Task>();
        //            while(!Work.IsCompleted)
        //            {
        //                string file;
        //                if(Work.TryTake(out file))
        //                {
        //                    list.Add(RunAwait(file));
        //                }
        //            }
        //            return list;
        //        }
        //    ,

        //Source.Token).ContinueWith(a=>
        //{
        //    private Task.WaitAll 
        //(
        //    private a.Result.ToArray 
        //());
        //    private Replacer.CompleteAdding 
        //();
        //}
        //)
        //; Task.Run(() =>
        //{
        //    private ConcurrentBag<Task> list = new ConcurrentBag<Task>();
        //while(!
        //    private Replacer.IsCompleted 
        //)
        //    {
        //        Tuple<string, List<Translation>> file;
        //        if(Replacer.TryTake(out file))
        //        {
        //            list.Add(WriteAwait(file.Item1, file.Item2));
        //        }
        //    }
        //return
        //list;
        //}
        //, Source.Token).ContinueWith(b=> Task.WaitAll(b.Result.ToArray()))) ;

            /// <summary>
            /// A simple task that reports the current progress to the user.
            /// </summary>
            /// <returns></returns>
        private static async Task<TimeSpan> Monitor()
        {
            return await Task.Run(() =>
            {
                Stopwatch timer = new Stopwatch();
                timer.Start();
                bool done = false;
                bool second = false;
                int writes = 0, work = 0;
                long found = 0, act = 0;
                int count = 0;
                Console.WriteLine("Building directory tree..");
                while(!Toggle.WaitOne(350))
                {
                    int rc = Writes.Count;
                    long fc = Interlocked.Read(ref Found);
                    long ac = Interlocked.Read(ref Active);
                    // int wc = Work.Count + Replacer.Count;
                    if(rc != writes || fc != found || ac != act) // || wc != work)
                    {
                        if(count++ % 30 == 0)
                        {
                            Console.WriteLine();
                            Console.WriteLine("Complete   |   Found   |   Active   |   Todo");
                        }
                        Console.WriteLine("{0,-8}   |   {1,-5}   |   {2,-6}   |   {3,-4}", writes = rc, found = fc, act = ac); //, work=wc);
                        if(!done && Work.IsAddingCompleted)
                        {
                            Console.WriteLine("Adding complete at {0}.", timer.Elapsed);
                            done = true;
                            count = 30;
                        }
                        //else if(!second && Replacer.IsAddingCompleted)
                        //{
                        //    Console.WriteLine("Searching complete at {0}.", timer.Elapsed);
                        //    second = true;
                        //        count = 30;
                        //    }
                    }
                }

                timer.Stop();
                Log.CompleteAdding();
                return timer.Elapsed;
            }, Source.Token).ConfigureAwait(false);
        }

        private enum WorkMode
        {
            Invalid = -1,
            Normal,
            DecryptCliloc,
            RebuildTree,
            Syntax
        }
    }
}