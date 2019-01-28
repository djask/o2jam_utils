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
            public float ms_marking;
            public float ms_per_measure;
            public float measure_start;
        }

        private class OsuNote
        {
            public bool LN;
            public float ms_start;
            public float ms_end = -1;
            public string sample_file = null;
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


            //write EX
            writeDiff(out_folder, ojn_header, Diff.EX);

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
                "LetterboxInBreaks: 0",
                "SpecialStyle: 0",
                "WidescreenStoryboard: 0",
            };

            string[] editor =
            {
                "[Editor]",
                "DistanceSpacing: 1.3",
                "BeatDivisor: 4",
                "GridSize: 8",
                "TimelineZoom: 1",
                "\n"
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

            File.WriteAllText(diffname, "osu file format v14\n\n");
            File.AppendAllLines(diffname, general);
            File.AppendAllLines(diffname, editor);
            File.AppendAllLines(diffname, metadata);
            File.AppendAllLines(diffname, difficulty);

            File.AppendAllText(diffname, "[Events]\n");
            //genEvents(chart.samples);

            File.AppendAllText(diffname, "[TimingPoints]\n");
            List<MSTiming> ms_timing = genTimings(chart.timings);

            File.AppendAllText(diffname, "[HitObjects]\n");
            genNotes(chart.notes, ms_timing);

        }

        private static string[] genEvents(List<NotePackage.AutoplaySample> samples)
        {
            foreach (var sample in samples)
            {
                Console.WriteLine($"Sample,{sample.measure_start},0,\"OGG{sample.sample_no}.ogg\",70");
            }
            return null;
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
                ms_time.ms_per_measure = ms_per_measure;
                osu_timings.Add(ms_time);
            }
            return osu_timings;
        }

        private static string[] genNotes(List<NotePackage.NoteEvent> notes, List<MSTiming> timings)
        {
            foreach(var note in notes)
            {
                //grab the last bpm change
                MSTiming last_bpm = timings[0];

                //incase the end of a LN is after a bpm change
                MSTiming next_bpm_change = null;
                bool ln_bpm_change = false;

                foreach(var timing in timings)
                {
                    if (timing.measure_start < note.measure_start)
                    {
                        last_bpm = timing;
                        break;
                    }
                }

                //check for bpm changes throughout a long note
                if (note.note_type == 3)
                {
                    foreach (var timing in timings)
                    {
                        if (note.measure_end > timing.measure_start)
                        {
                            next_bpm_change = timing;
                            ln_bpm_change = true;
                            break;
                        }
                    }
                }

                //difference between our note and the start of the last bpm
                float start_delta = note.measure_start - last_bpm.measure_start;

                //grab the ms base for the start of the bpm change and add 
                float start_offset = last_bpm.ms_per_measure * start_delta;
                float hit_start = last_bpm.ms_marking + start_offset;

                //grab the offset for the end of a long note (if needed)
                if(note.note_type == 3)
                {

                }
            }
            return null;
        }
    }
}
