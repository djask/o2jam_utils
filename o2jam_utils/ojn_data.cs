using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace o2jam_utils
{
    public class OJN_Data
    {
        public int songid;
        private static char[] signature = new char[4];
        private static float encode_version;
        public int genre;
        public float bpm;
        private static short[] level = new short[4];
        private static int[] event_count = new int[4];
        private static int[] note_count = new int[4];
        private static int[] measure_count = new int[4];
        private static int[] package_count = new int[4];
        private static short old_encode_version;
        private static short old_songid;
        private static char[] old_genre = new char[20];
        private static int bmp_size;
        private static int old_file_version;
        public string title;
        public string artist;
        public string noter;
        public string ojm_file;
        private static int cover_size;
        private static int[] time = new int[3];
        private static int[] note_offset = new int[3];
        private static int cover_offset;

        private static MemoryMappedFile ojn_file;

        //safer memory mapping i guess...
        private static MemoryMappedFile MemFile(string path)
        {
            return MemoryMappedFile.CreateFromFile(
                      File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read),
                      //not mapping to a name
                      null,
                      //use the file's actual size
                      0L,
                      //read only access
                      MemoryMappedFileAccess.Read,
                      //not configuring security
                      null,
                      //adjust as needed
                      HandleInheritability.None,
                      //close the previously passed in stream when done
                      false);

        }

        //populate our class variables
        public OJN_Data(String path)
        {
            ojn_file = MemFile(path);

            //312 bytes long i think
            MemoryMappedViewAccessor buf = ojn_file.CreateViewAccessor(0, 312, MemoryMappedFileAccess.Read);
            long offset = 0;
            songid = buf.ReadInt32(offset); offset += 4;

            byte[] unk_raw1 = new byte[4];
            buf.ReadArray(offset, unk_raw1, 0, 4); offset += 4;
            signature = System.Text.Encoding.UTF8.GetString(unk_raw1).ToCharArray();
            encode_version = buf.ReadSingle(offset); offset += 4;
            genre = buf.ReadInt32(offset); offset += 4;
            bpm = buf.ReadSingle(offset); offset += 4;

            //some level stuff
            buf.ReadArray(offset, level, 0, 4); offset += 8;
            buf.ReadArray(offset, event_count, 0, 3); offset += 12;
            buf.ReadArray(offset, note_count, 0, 3); offset += 12;
            buf.ReadArray(offset, measure_count, 0, 3); offset += 12;
            buf.ReadArray(offset, package_count, 0, 3); offset += 12;

            //skip the old variables
            offset += 24;

            bmp_size = buf.ReadInt32(offset); offset += 4;

            //skip old file version
            offset += 4;

            //title and stuff, just assuming it's utf8 for now, but will
            //have to deal with gbk and big5
            byte[] raw_title = new byte[64];
            byte[] raw_artist = new byte[32];
            byte[] raw_noter = new byte[32];
            buf.ReadArray(offset, raw_title, 0, 64); offset += 64;
            buf.ReadArray(offset, raw_artist, 0, 32); offset += 32; 
            buf.ReadArray(offset, raw_noter, 0, 32); offset += 32;

            raw_title = raw_title.Where(i => i != 0).ToArray();
            raw_artist = raw_artist.Where(i => i != 0).ToArray();
            raw_noter = raw_noter.Where(i => i != 0).ToArray();

            title = Encoding.GetEncoding(936).GetString(raw_title);
            artist = Encoding.GetEncoding(936).GetString(raw_title);
            noter = Encoding.GetEncoding(936).GetString(raw_title);

            byte[] raw_file = new byte[32];
            buf.ReadArray(offset, raw_file, 0, 32); offset += 32;
            raw_file = raw_file.Where(i => i != 0).ToArray();

            //assuming file names should be in unicode...
            ojm_file = System.Text.Encoding.UTF8.GetString(raw_file);

            cover_size = buf.ReadInt32(offset); offset += 4;
            buf.ReadArray(offset, time, 0, 3); offset += 12;
            buf.ReadArray(offset, note_offset, 0, 3); offset += 12;
            cover_offset = buf.ReadInt32(offset);
        }

        public void DumpImage(String out_dir)
        {
            if (cover_size == 0) return;
            MemoryMappedViewAccessor buf = ojn_file.CreateViewAccessor(cover_offset, cover_size, MemoryMappedFileAccess.Read);
            //jpeg image dump
            String filename = "bg.jpg";
            String path = Path.Combine(out_dir, filename);
            BinaryWriter writer = new BinaryWriter(File.Open(path, FileMode.Create));
            byte[] tmp = new byte[cover_offset];
            buf.ReadArray(0, tmp, 0, cover_size);
            writer.Write(tmp);
        }

        public NotePackage.NoteHeader[] DumpEXPackage()
        {
            return NotePackage.ReadPackage(ojn_file, note_offset[0], note_offset[1], package_count[0]);
        }

        public NotePackage.NoteHeader[] DumpNXPackage()
        {
            return NotePackage.ReadPackage(ojn_file, note_offset[1], note_offset[2], package_count[1]);
        }

        //third difficulty ends at cover offset
        public NotePackage.NoteHeader[] DumpHXPackage()
        {
            return NotePackage.ReadPackage(ojn_file, note_offset[2], cover_offset, package_count[2]);
        }
    };
}
