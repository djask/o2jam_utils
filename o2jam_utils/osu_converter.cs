using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace o2jam_utils
{
    public class OsuConverter
    {
        private enum Diff
        {
            EX,
            NX,
            HX
        };

        public static void OSU_dump(string ojn_path, string out_dir)
        {
            //read ojn headers
            OJN_Data ojn_header = new OJN_Data(ojn_path);

            //output folder for beatmap
            string out_folder = Path.Combine(out_dir, ojn_header.songid.ToString());
            DirectoryInfo directory = Directory.CreateDirectory(out_folder);

            //get the ojm path, we assume it is in the same directory
            DirectoryInfo o2j_folder = Directory.GetParent(ojn_path);
            string ojm_path = Path.Combine(o2j_folder.FullName, ojn_header.ojm_file);

            //dump the media contents
            OJM_Dump.dumpFile(ojm_path, out_folder);

            //dump image
            ojn_header.DumpImage(out_folder);


            //write EX
            writeDiff(out_folder, ojn_header, Diff.EX);

        }

        private static void writeDiff(string path, OJN_Data ojn_header, Diff diff)
        {
            NotePackage.Chart chart;
            String diffname = null;

            switch (diff)
            {
                case Diff.EX:
                    chart = ojn_header.DumpEXPackage();
                    diffname = ojn_header.title + "_EX_" + ojn_header.level[0];
                    break;
                case Diff.NX:
                    chart = ojn_header.DumpNXPackage();
                    diffname = ojn_header.title + "_NX_" + ojn_header.level[1];
                    break;
                case Diff.HX:
                    chart = ojn_header.DumpHXPackage();
                    diffname = ojn_header.title + "_HX_" + ojn_header.level[2];
                    break;
                default:
                    chart = ojn_header.DumpEXPackage();
                    diffname = ojn_header.title + "_EX_" + ojn_header.level[0];
                    break;
            }
            diffname += ".osu";
            diffname = Path.Combine(path, diffname);

        }

        private static string[] genTimings(NotePackage.NoteHeader[] packages)
        {
            //elapsed milliseconds
            int ms_counter = 0;
            float currbpm = 0;
            List<string> timings = new List<string>();
            for(int i = 0; i < packages.Length; i++)
            {
                //filter out bpm timings only
                if(packages[i].channel == 1)
                {
                    String temp = " " + ",4,2,2,100,1,0";
                }
            }
            return null;
        }
    }
}
