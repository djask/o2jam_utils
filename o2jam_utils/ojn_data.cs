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
        private int songid;
        private char[] signature = new char[4];
        private float encode_version;
        private int genre;
        private float bpm;
        private short[] level = new short[4];
        private int[] event_count = new int[4];
        private int[] note_count = new int[4];
        private int[] measure_count = new int[4];
        private int[] package_count = new int[4];
        private short old_encode_version;
        private short old_songid;
        private char[] old_genre = new char[20];
        private int bmp_size;
        private int old_file_version;
        private string title;
        private string artist;
        private string noter;
        private string ojm_file;
        private int cover_size;
        private int[] time = new int[3];
        private int[] note_offset = new int[3];
        private int cover_offset;

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
            MemoryMappedFile ojn_file = MemFile(path);

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
            buf.ReadArray(offset, level, 0, 4); offset += 4;
            buf.ReadArray(offset, event_count, 0, 3); offset += sizeof(int) * 3;
            buf.ReadArray(offset, note_count, 0, 3); offset += sizeof(int) * 3;
            buf.ReadArray(offset, measure_count, 0, 3); offset += sizeof(int) * 3;
            buf.ReadArray(offset, package_count, 0, 3); offset += sizeof(int) * 3;

            //skip the old variables
            offset += 24;

            bmp_size = buf.ReadInt32(offset); offset += 4;

            //skip old file version
            offset += 4;

            //title and stuff, just assuming it's utf8 for now
            byte[] raw_title = new byte[64];
            byte[] raw_artist = new byte[32];
            byte[] raw_noter = new byte[32];
            buf.ReadArray(offset, raw_title, 0, 64); offset += 64;
            buf.ReadArray(offset, raw_artist, 0, 32); offset += 32; 
            buf.ReadArray(offset, raw_noter, 0, 32); offset += 32;

            byte[] raw_file = new byte[32];
            buf.ReadArray(offset, raw_file, 0, 32); offset += 32;

            //assuming file names should be in unicode...
            ojm_file = System.Text.Encoding.UTF8.GetString(raw_file);

            cover_size = buf.ReadInt32(offset); offset += 4;
            buf.ReadArray(offset, time, 0, 3); offset += 12;
            buf.ReadArray(offset, note_offset, 0, 3); offset += 12;
            cover_offset = buf.ReadInt32(offset);
            
        }
    };
}
