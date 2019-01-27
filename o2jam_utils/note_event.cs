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

        public enum channels
        {
            MEASURE,
            BPM_change,
            NOTE_ONE,
            NOTE_TWO,
            NOTE_THREE,
            NOTE_FOUR,
            NOTE_FIVE,
            NOTE_SIX,
            NOTE_SEVEN
        };

        public class NoteHeader
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

            public NoteEvent[] event_list = null;

            //non-note package
            public float payload = -1;
        }

        public class NoteEvent
        {
            public short value;
            public byte volume;
            public byte pan;
            public byte note_type;
        }

        public static NoteHeader[] ReadPackage(MemoryMappedFile ojn_file, int start, int end, int N)
        {
            long pos = 0;
            //an array of packages
            NoteHeader[] packages = new NoteHeader[N];

            using (var buf = ojn_file.CreateViewAccessor(start, end, MemoryMappedFileAccess.Read))
            {
                for(int i = 0; i < N; i++)
                {
                    packages[i] = new NoteHeader();
                    packages[i].measure = buf.ReadInt32(pos); pos += 4;
                    packages[i].channel = buf.ReadInt16(pos); pos += 2;
                    packages[i].events = buf.ReadInt16(pos); pos += 2;
                    int events = packages[i].events;

                    //populate note events
                    if(packages[i].channel >= 2 && packages[i].channel <= 8)
                    {
                        Console.WriteLine((channels)packages[i].channel + " detected note package, measure" + packages[i].measure);
                        packages[i].event_list = new NoteEvent[packages[i].events];
                        for(int j = 0; j < events; j++)
                        {
                            packages[i].event_list[j] = new NoteEvent();
                            packages[i].event_list[j].value = buf.ReadInt16(pos); pos += 2;
                            byte raw_byte = buf.ReadByte(pos); pos++;
                            packages[i].event_list[j].note_type = buf.ReadByte(pos); pos++;

                            //split the 2nd byte into 2 nybbles
                            packages[i].event_list[j].volume = (byte)(raw_byte & 0x0F);
                            packages[i].event_list[j].pan = (byte)(raw_byte & 0xf0 >> 4);
                        }
                    }
                    else if(packages[i].channel < 2)
                    {
                        packages[i].payload = buf.ReadSingle(pos);
                        pos += packages[i].events * 4;
                        Console.WriteLine((channels)packages[i].channel + " detected change package, measure " + packages[i].measure + " with payload " + packages[i].payload);

                    }

                    //still have no idea how the samples work this is just a placeholder
                    else if(packages[i].channel >= 9 && packages[i].channel <= 22)
                    {
                        pos += packages[i].events * 4;
                    }
                }
            }
            return packages;
        }
    }
}
