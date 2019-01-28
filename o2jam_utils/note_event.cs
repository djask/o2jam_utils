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
        public class Chart
        {
            //chart has timings, note events
            public List<NoteEvent> notes = new List<NoteEvent>();

            //timings list
            public List<BPMChange> timings = new List<BPMChange>();

            //autoplay samles
            public List<NoteEvent> samples = new List<NoteEvent>();

            //how many milliseconds since the start of the measure
            public int[] elapsed_time;
        }

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
        }

        public class NoteEvent
        {
            public int channel;

            public short value;
            public byte volume;
            public byte pan;
            public byte note_type;

            //measure pos is event index / no_events
            public float measure_start;

            //duration if it's a long note
            public float measure_end = -1;
        }

        public class BPMChange
        {
            public float val;
            public float measure_start;
        }

        public static Chart ReadPackage(MemoryMappedFile ojn_file, OJN_Data header, int diff)
        {
            long pos = 0;
            //an array of packages
            Chart chart = new Chart();

            //get the initial bpm timing (not sure if needed yet)...
            BPMChange start_bpm = new BPMChange();
            start_bpm.val = header.bpm;
            start_bpm.measure_start = 0;
            chart.timings.Add(start_bpm);

            //some variables to keep track of stuff
            //start of long holds
            NoteEvent[] start_holds = new NoteEvent[7];

            int start, end = -1;
            start = header.note_offset[diff];
            if (diff < 2) end = header.note_offset[diff + 1];
            else end = header.cover_offset;

            int N = header.package_count[diff];

            int sz = end - start;

            using (var buf = ojn_file.CreateViewAccessor(start, sz, MemoryMappedFileAccess.Read))
            {
                for(int i = 0; i < N; i++)
                {
                    int measure = buf.ReadInt32(pos); pos += 4;
                    short channel = buf.ReadInt16(pos); pos += 2;
                    short events = buf.ReadInt16(pos); pos += 2;

                    //hope that this doesnt occur
                    if (channel == 0)
                    {
                        pos += events * 4;
                    }

                    //get bpm change timing
                    else if (channel == 1)
                    {
                        for(int j = 0; j < events; j++)
                        {
                            float time = measure;
                            //PLEASE DO NOT ADD ONE TO J, IT BREAKS THE PROCESS
                            time += (j / ((float)events));

                            float val = buf.ReadSingle(pos); pos += 4;
                            if (val == 0) continue;

                            BPMChange timing = new BPMChange();
                            timing.val = val;
                            timing.measure_start = time;
                            chart.timings.Add(timing);

                        }
                    }
                    //populate note events
                    else
                    {
                        for(int j = 0; j < events; j++)
                        {
                            short val = buf.ReadInt16(pos); pos += 2;
                            //skip empty events (but also add the offset)
                            if(val == 0) { pos += 2;  continue;  }

                            //new noteevent object
                            NoteEvent note_event = new NoteEvent();
                            note_event.value = val;

                            //the measure time (e.g. 2.5 is measure 2, halfway throught the bar)
                            //logically with 8 events, the 4th one should be the middle right?
                            //but adding one to j breaks the program, and I don't know why
                            float time = measure;
                            time += (j / ((float)events));
                            note_event.measure_start = time;

                            //get the two half chars
                            byte raw_byte = buf.ReadByte(pos); pos++;

                            //split the 2nd byte into 2 nybbles
                            note_event.volume = (byte)(raw_byte & 0x0F);
                            note_event.pan = (byte)(raw_byte & 0xf0 >> 4);

                            //get the note_type
                            note_event.note_type = buf.ReadByte(pos); pos++;

                            //start of a long note, we record the measure time
                            if (note_event.note_type == 2 && start_holds[channel-2] == null)
                            {
                                start_holds[channel - 2] = note_event;
                                continue;
                            }

                            //the end of a long note, we can put this into the note list
                            else if(note_event.note_type == 3 && start_holds[channel-2] != null)
                            {
                                note_event.measure_end = time;
                                note_event.measure_start = start_holds[channel - 2].measure_start;
                                start_holds[channel - 2] = null;
                            }


                            //normal note procedure
                            //not a long note so no need for end measure
                            else if(note_event.note_type == 0)
                                note_event.measure_end = -1;


                            //add to chart object
                            if (channel < 9)
                            {
                                note_event.channel = channel - 2;
                                chart.notes.Add(note_event);
                            }
                            else
                            {
                                note_event.channel = channel;
                                chart.samples.Add(note_event);
                            }
                        }
                    }
                }
            }

            //sort chart by start time, multiple notes on the same timing sort from smallest to largest channel
            chart.notes.Sort(
                delegate (NoteEvent p1, NoteEvent p2)
                {
                    int time = p1.measure_start.CompareTo(p2.measure_start);
                    if (time == 0)
                    {
                        return p1.channel.CompareTo(p2.channel);
                    }
                    return time;
                }
            );
            return chart;
        }
    }
}
