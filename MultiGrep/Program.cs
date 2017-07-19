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
    public static class MainClass
    {
        public static readonly byte[] NewLine = Encoding.Default.GetBytes(Environment.NewLine);

        private static readonly string Temp = Path.Combine(Path.GetTempPath(), "LocalizedData");
        private static readonly string LogPath = Path.Combine(Temp, "results.log");

        public static long Active;
        public static long Found;

        private static readonly CancellationTokenSource Source = new CancellationTokenSource();
        private static readonly ManualResetEvent Toggle = new ManualResetEvent(false);

        /// <summary>
        /// Contains a list of filepaths that still need to be searched for patterns
        /// </summary>
        public static readonly BlockingCollection<string> Work = new BlockingCollection<string>(new ConcurrentBag<string>());

        /// <summary>
        /// A list of strings to be written to the log file
        /// </summary>
        public static readonly BlockingCollection<string> Log = new BlockingCollection<string>(new ConcurrentBag<string>());

        /// <summary>
        /// Contains a list of files that have been altered
        /// </summary>
        public static readonly ConcurrentBag<string> Writes = new ConcurrentBag<string>();

        /// <summary>
        /// A Token used to quickly cancel all tasks if necessary
        /// </summary>
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

            Queue<string> work = new Queue<string>();
            args.ForEach(s =>
            {
                switch(s.Substring(0, 2))
                {

                    case "-g":
                        MultiByteSR.Grouping = s[2];
                        break;
                    default:
                        if(Directory.Exists(s))
                            Directory.GetFiles(s).ForEach(work.Enqueue);
                        else if(File.Exists(s))
                            work.Enqueue(s);
                        else
                            Console.WriteLine("Unknown argument {0}", s);
                        break;
                }
            });
            string fn = work.Count > 0 ? work.Dequeue() : "";

            if(!MultiByteSR.Initialize(fn) && work.Count < 2)
            {
                ExitMessage("Must enter a directory or file to parse.");
                return;
            }
            Task.WaitAll(StartAll(MultiByteSR.StartSearch(), work.ToArray()), Monitor().ContinueWith(t => Console.WriteLine("Completed in {0}.  Changes: {1}", t.Result, Writes.Count)));

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
        }

        public static void ExitMessage(string format, params object[] args) { ExitMessage(string.Format(format, args)); }

        private static void ExitMessage(string message)
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
            await Task.Run(() => Parallel.ForEach(Partitioner.Create(fn.SelectMany(s => Directory.Exists(s) ? Directory.GetFiles(s) : new string[]{s})), Work.Add),
                           Source.Token).ContinueWith(t =>
            {
                Work.CompleteAdding();
                Task.WaitAll(tasks.ToArray());
            }).ContinueWith(c => Toggle.Set()).ConfigureAwait(false);
        }

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
                int writes = 0;
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
                        Console.WriteLine("{0,-8}   |   {1,-5}   |   {2,-6}   ", writes = rc, found = fc, act = ac); //, work=wc);
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

    }
}