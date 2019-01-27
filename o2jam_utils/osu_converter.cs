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

            //write the osu file
            NotePackage.NoteHeader[] EXPackage = ojn_header.DumpEXPackage();
        }
    }
}
