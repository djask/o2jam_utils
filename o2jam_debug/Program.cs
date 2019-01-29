using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using O2JamUtils;

namespace O2JamDebug
{
    class Program
    {
        private static void test_dir(string path, int jobs)
        {
            string[] files = System.IO.Directory.GetFiles(path, "*.ojn");
            Parallel.For(0, files.Length, i =>
             {
                 Console.WriteLine(files[i]);
                 try
                 {
                     OsuConverter.OSUDump(files[i], @"D:\Temp\output");
                 }
                 catch
                 {
                     return;
                 }
             });
        }

        static void Main(string[] args)
        {
            //OJN_Data test = new OJN_Data(@"D:\temp\sampleo2jm\o2ma1183.ojn");
            //test.DumpImage("D:\\temp");

            //testing converting to osu
            //OsuConverter.OSUDump(@"D:\temp\sampleo2jm\o2ma1374.ojn", @"D:\Temp\output");

            //o2jam_utils.OJM_Dump.dumpFile(@"D:\temp\sampleo2jm\o2ma1183.ojm", @"D:\temp\sampleo2jm");

            test_dir(@"d:\Games\o2servers\dpv3\Music",12);
        }
    }
}
