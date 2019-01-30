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
        private static void test_dir(string path, string output)
        {
            string[] files = System.IO.Directory.GetFiles(path, "*.ojn");
            for (int i = 0; i < files.Length; i++)
            {
                Console.WriteLine(files[i]);
                try
                {
                    OsuConverter.OSUDump(files[i], output, true);
                }
                catch
                {
                    return;
                }
            }
        }

        static void Main(string[] args)
        {
            //OJN_Data test = new OJN_Data(@"D:\temp\sampleo2jm\o2ma1183.ojn");
            //test.DumpImage("D:\\temp");

            //testing converting to osu
            //OsuConverter.OSUDump(@"D:\temp\sampleo2jm\o2ma1374.ojn", @"D:\Temp\output");

            //o2jam_utils.OJM_Dump.dumpFile(@"D:\temp\sampleo2jm\o2ma1183.ojm", @"D:\temp\sampleo2jm");
            if(args.Length != 2)
            {
                Console.WriteLine("Usage: program.exe [ojnfolder] [outputfolder]");
                return;
            }
            try
            {
                System.IO.Directory.CreateDirectory(args[1]);
            }
            catch
            {
                Console.WriteLine("Error creating or finding output directory, please check your arguments");
                return;
            }
            test_dir(args[0], args[1]);
        }
    }
}
