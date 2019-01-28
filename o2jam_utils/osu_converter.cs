using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.IO.MemoryMappedFiles;

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

        //timings for bpm changes plus their ms markings for osu format
        private class MSTiming
        {
            public float ms_marking { get; set;  }
            public float ms_per_measure { get; set; }
            public float measure_start { get; set; }
        }

        private class OsuNote
        {
            public bool LN { get; set; }
            public int channel { get; set; }
            public float ms_start { get; set; }
            public float ms_end = -1;
            public string sample_file { get; set; }
        }

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




            //write HX
            writeDiff(out_folder, ojn_header, Diff.HX);

            //write NX
            if(ojn_header.level[1] < ojn_header.level[2]) writeDiff(out_folder, ojn_header, Diff.NX);

            //write EX
            if(ojn_header.level[0] < ojn_header.level[1]) writeDiff(out_folder, ojn_header, Diff.EX);

        }

        private static string GetSafeFilename(string filename)
        {
            return string.Join("_", filename.Split(Path.GetInvalidFileNameChars()));
        }

        private static void writeDiff(string path, OJN_Data ojn_header, Diff diff)
        {
            NotePackage.Chart chart;
            String diffname = null;
            String diffex = null;

            switch (diff)
            {
                default:
                case Diff.EX:
                    chart = ojn_header.DumpEXPackage();
                    diffex = $"_EX_LVL{ojn_header.level[0]}";
                    break;
                case Diff.NX:
                    chart = ojn_header.DumpNXPackage();
                    diffex = $"_NX_LVL{ojn_header.level[1]}";
                    break;
                case Diff.HX:
                    chart = ojn_header.DumpHXPackage();
                    diffex = $"HX_LVL{ojn_header.level[2]}";
                    break;
            }
            diffname = $"{ojn_header.title}{diffex}.osu";
            diffname = GetSafeFilename(diffname);
            diffname = Path.Combine(path, diffname);

            string[] general =
            {
                "[General]",
                "AudioFilename: virtual",
                "AudioLeadIn: 0",
                "PreviewTime: 0",
                "Countdown: 0",
                "SampleSet: Soft",
                "StackLeniency: 0.7",
                "Mode: 3",
                "LetterboxInBreaks: 0\n\n",
            };


            string[] metadata =
            {
                "[Metadata]",
                $"Title:{ojn_header.title}",
                $"TitleUnicode:{ojn_header.title}",
                $"Artist:{ojn_header.artist}",
                $"ArtistUnicode:{ojn_header.artist}",
                $"Creator:{ojn_header.noter}",
                $"Version:7k{diffex}",
                "Source:o2jam",
                $"Tags:o2jam {ojn_header.ojm_file}",
                $"BeatmapID:{ojn_header.songid}",
                $"BeatmapSetID:{ojn_header.songid}",
                "\n"
            };

            string[] difficulty =
            {
                "[Difficulty]",
                "HPDrainRate:5",
                "CircleSize:7",
                "OverallDifficulty:5",
                "ApproachRate:5",
                "SliderMultiplier:1.4",
                "SliderTickRate:1",
                "\n"
            };

            string[] events =
{
                "[Events]",
                "0,0,\"bg.jpg\"",
                "\n"
            };

            List<MSTiming> ms_timing = genTimings(chart.timings);
            List<OsuNote> note_list = genNotes(chart.notes, ms_timing);

            //as per osu specification
            int[] column_map = new int[7]{ 36,109,182,255,328,401,474};
            using (StreamWriter w = new StreamWriter(File.Open(diffname, FileMode.Create)))
            {
                w.Write("osu file format v14\n\n");
                foreach (var l in general) w.WriteLine(l);
                foreach (var l in metadata) w.WriteLine(l);
                foreach (var l in difficulty) w.WriteLine(l);
                foreach (var l in events) w.WriteLine(l);

                w.WriteLine("[TimingPoints]");
                foreach (var timing in ms_timing)
                {
                    float bpm = 240 / timing.ms_per_measure * 1000;
                    float ms_per_beat = 60000 / bpm;
                    string line = $"{(int)timing.ms_marking},{ms_per_beat},4,2,2,100,1,0\n";
                    w.Write(line);
                }

                w.WriteLine("\n\n[HitObjects]");


                foreach (var note in note_list)
                {
                    string line;
                    if (note.LN)
                    {
                        line = $"{column_map[note.channel]},0,{(int)note.ms_start},128,0,{(int)note.ms_end}:0:0:0:0:{note.sample_file}\n";
                    }
                    else
                    {
                        line = $"{column_map[note.channel]},0,{(int)note.ms_start},1,0,0:0:0:0:0:{note.sample_file}\n";
                    }
                    w.Write(line);
                }
            }
        }

        private static List<MSTiming> genTimings(List<NotePackage.BPMChange> timings)
        {
            List<MSTiming> osu_timings = new List<MSTiming>();
            //elapsed milliseconds
            NotePackage.BPMChange prev = null;
            float milliseconds = 0.0f;

            foreach (var timing in timings)
            {
                // Console.WriteLine($"!timing change {timing.val} on {timing.measure_start}");
                MSTiming ms_time = new MSTiming();

                if (prev == null)
                {
                    prev = timing;
                    ms_time.measure_start = timing.measure_start;
                    ms_time.ms_marking = 0;
                    ms_time.ms_per_measure = 240 / timing.val * 1000;
                    osu_timings.Add(ms_time);
                    continue;
                }
                float ms_per_measure = 240 / prev.val * 1000;
                float delta = timing.measure_start - prev.measure_start;
                milliseconds += delta * ms_per_measure;
                prev = timing;

                ms_time.measure_start = timing.measure_start;
                ms_time.ms_marking = milliseconds;
                ms_time.ms_per_measure = 240 / timing.val * 1000;
                osu_timings.Add(ms_time);
            }
            return osu_timings;
        }

        private class TimingComparer : IComparer<MSTiming>
        {

            public int Compare(MSTiming x, MSTiming y)
            {
                return x.measure_start.CompareTo(y.measure_start);
            }

        }

        private static List<OsuNote> genNotes(List<NotePackage.NoteEvent> notes, List<MSTiming> timings)
        {
            List<OsuNote> note_list = new List<OsuNote>();
            foreach(var note in notes)
            {
                //Console.WriteLine($"channel {note.channel} start {note.measure_start} end {note.measure_end}");
                //grab the last bpm change
                MSTiming last_bpm = timings[0];

                //incase the end of a LN is after a bpm change
                MSTiming next_bpm = null;

                int index = timings.BinarySearch(new MSTiming { measure_start = note.measure_start }, new TimingComparer());
                if (index < 0) index = ~index - 1;
                last_bpm = timings[index];

                //check for bpm changes throughout a long note
                if (note.note_type == 3)
                {
                    index = timings.BinarySearch(new MSTiming { measure_start = note.measure_end }, new TimingComparer());
                    if (index < 0) index = ~index - 1;
                    next_bpm = timings[index];

                }

                OsuNote osu_note = new OsuNote();
                osu_note.channel = note.channel;

                //difference between our note and the start of the last bpm
                float start_delta = note.measure_start - last_bpm.measure_start;

                //grab the ms base for the start of the bpm change and add 
                float start_offset = last_bpm.ms_per_measure * start_delta;
                float hit_start = last_bpm.ms_marking + start_offset;

                osu_note.ms_start = hit_start;

                //grab the offset for the end of a long note (if needed)
                if(note.note_type == 3)
                {
                    float end_delta = note.measure_end - next_bpm.measure_start;
                    float end_offset = next_bpm.ms_per_measure * end_delta;
                    float hit_end = next_bpm.ms_marking + end_offset;
                    osu_note.LN = true;
                    osu_note.ms_end = hit_end;
                }


                note_list.Add(osu_note);
            }
            return note_list;
        }
    }
}
