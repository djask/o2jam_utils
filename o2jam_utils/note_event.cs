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

        public class Chart
        {
            //chart has timings, note events
            public List<NoteEvent> ch_one = new List<NoteEvent>();
            public List<NoteEvent> ch_two = new List<NoteEvent>();
            public List<NoteEvent> ch_three = new List<NoteEvent>();
            public List<NoteEvent> ch_four = new List<NoteEvent>();
            public List<NoteEvent> ch_five = new List<NoteEvent>();
            public List<NoteEvent> ch_six = new List<NoteEvent>();
            public List<NoteEvent> ch_seven = new List<NoteEvent>();

            //timings list
            public List<BPMChange> timings = new List<BPMChange>();
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
            public int measure;
        }

        public static Chart ReadPackage(MemoryMappedFile ojn_file, int start, int end, int N)
        {
            long pos = 0;
            //an array of packages
            Chart chart = new Chart();

            //some variables to keep track of stuff
            //start of long holds
            NoteEvent[] start_holds = new NoteEvent[7];
            float measure_mult = 1.0f;


            using (var buf = ojn_file.CreateViewAccessor(start, end, MemoryMappedFileAccess.Read))
            {
                for(int i = 0; i < N; i++)
                {
                    int measure = buf.ReadInt32(pos); pos += 4;
                    short channel = buf.ReadInt16(pos); pos += 2;
                    short events = buf.ReadInt16(pos); pos += 2;

                    if (channel == 0)
                    {
                        pos += events * 4;
                        continue;
                    }

                    //get bpm change timing
                    if (channel == 1)
                    {
                        BPMChange timing = new BPMChange();
                        timing.val = buf.ReadSingle(pos);
                        timing.measure = measure;
                        pos += events * 4;
                        Console.WriteLine((channels)channel + " detected change package, measure " + measure + " with payload " + timing.val);
                        chart.timings.Add(timing);

                    }
                    //populate note events
                    else if (channel >= 2 && channel <= 8)
                    {
                        Console.WriteLine((channels)channel + " detected note package, measure" + measure);
                        for(int j = 0; j < events; j++)
                        {
                            short val = buf.ReadInt16(pos); pos += 2;
                            //skip empty events (but also add the offset)
                            if(val == 0) { pos += 2;  continue;  }

                            //new noteevent object
                            NoteEvent note_event = new NoteEvent();
                            note_event.value = val;

                            //the measure time (e.g. 2.5 is measure 2, halfway throught the bar)
                            float time = measure;
                            time += ((j + 1) / ((float)events));
                            note_event.measure_start = time;

                            //get the two half chars
                            byte raw_byte = buf.ReadByte(pos); pos++;

                            //split the 2nd byte into 2 nybbles
                            note_event.volume = (byte)(raw_byte & 0x0F);
                            note_event.pan = (byte)(raw_byte & 0xf0 >> 4);

                            //get the note_type
                            note_event.note_type = buf.ReadByte(pos); pos++;

                            //start of a long note, we record the measure time
                            if (note_event.note_type == 2)
                            {
                                start_holds[channel - 2] = note_event;
                                continue;
                            }

                            //the end of a long note, we can put this into the note list
                            else if(note_event.note_type == 3)
                            {
                                note_event.measure_end = time;
                                note_event.measure_start = start_holds[channel - 2].measure_start;
                            }


                            //normal note procedure
                            //not a long note so no need for end measure
                            else if(note_event.note_type == 0)
                                note_event.measure_end = -1;

                            //add to chart object
                            switch (channel)
                            {
                                case 2:
                                    chart.ch_one.Add(note_event);
                                    break;
                                case 3:
                                    chart.ch_two.Add(note_event);
                                    break;
                                case 4:
                                    chart.ch_three.Add(note_event);
                                    break;
                                case 5:
                                    chart.ch_four.Add(note_event);
                                    break;
                                case 6:
                                    chart.ch_five.Add(note_event);
                                    break;
                                case 7:
                                    chart.ch_six.Add(note_event);
                                    break;
                                case 8:
                                    chart.ch_seven.Add(note_event);
                                    break;
                                default:
                                    break;
                            }
                        }
                    }

                    //still have no idea how the samples work this is just a placeholder
                    else if(channel >= 9 && channel <= 22)
                    {
                        pos += events * 4;
                    }
                }
            }
            return chart;
        }
    }
}
