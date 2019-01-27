using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace o2jam_utils
{
    class OsuConverter
    {
        public static void OSU_dump(string ojn_path, string out_dir)
        {
            OJN_Data ojn_header = new OJN_Data(ojn_path);
            string out_folder = Path.Combine(out_dir, ojn_header.songid.ToString());
            Directory.CreateDirectory(out_folder);
            DirectoryInfo o2j_folder = Directory.GetParent(ojn_path);
            string ojm_path = Path.Combine(o2j_folder.FullName, ojn_header.ojm_file);
            OJM_Dump.dumpFile(ojm_path, out_folder);
            
        }
    }
}
