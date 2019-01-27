using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using o2jam_utils;

namespace o2jam_debug
{
    class Program
    {
        private static void test_dir(string path)
        {
            string[] files = System.IO.Directory.GetFiles(path, "*.ojm");
            for (int i = 0; i < files.Length; i++)
            {
                String signature = o2jam_utils.OJM_Dump.getType(files[i]);
                Console.WriteLine(files[i]);
                Console.WriteLine(signature);
                o2jam_utils.OJM_Dump.dumpFile(files[i], "D:\\temp\\ojm");
            }
        }

        static void Main(string[] args)
        {
            OJN_Data test = new OJN_Data(@"D:\temp\sampleo2jm\o2ma1183.ojn");
            //o2jam_utils.OJM_Dump.dumpFile(@"D:\temp\sampleo2jm\o2ma1183.ojm", @"D:\temp\sampleo2jm");
        }
    }
}
