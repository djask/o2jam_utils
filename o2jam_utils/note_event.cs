using System;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace o2jam_utils
{
    public class NotePackage
    {
        private class NoteHeader
        {
            //which "bar" the package is on
            public int measure;

            //channel meaning
            //0 	measure fraction
            //1 	BPM change
            //2 	note on 1st lane
            //3 	note on 2nd lane
            //4 	note on 3rd lane
            //5 	note on 4th lane(middle button)
            //6 	note on 5th lane
            //7 	note on 6th lane
            //8 	note on 7th lane
            //9~22 	auto-play samples(?)
            public short channel;

            //the number of events
            public short events;
        }

        public static void read_package(MemoryMappedFile ojn_file, int start, int end)
        {
            //package size = 12 + (4*no of events)
            long pos = 0;
            using (var buf = ojn_file.CreateViewAccessor(start, end, MemoryMappedFileAccess.Read))
            {
                while(pos < (buf.Capacity - buf.PointerOffset))
                {
                    NoteHeader header = new NoteHeader();
                    header.measure = buf.ReadInt32(pos); pos += 4;
                    header.channel = buf.ReadInt16(pos); pos += 2;
                    header.events = buf.ReadInt16(pos); pos += 2;

                }
            }
        }
    }
}
