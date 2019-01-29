using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.IO.Compression;

namespace O2JamUtils
{
    public class OsuConverter
    {
        private enum Diff
        {
            EX,
            NX,
            HX
        };

        private enum Sig
        {
            M30,
            OJM
        };

        //timings for bpm changes plus their ms markings for osu format
        private class MSTiming
        {
            public float MsMarking { get; set;  }
            public float MsPerMeasure { get; set; }
            public float MeasureStart { get; set; }
        }

        private class OsuNote
        {
            public bool LN { get; set; }
            public int Channel { get; set; }
            public float MsStart { get; set; }
            public float MsEnd = -1;
            public string SampleFile { get; set; }
        }

        private class OsuSample
        {
            public float MsStart {get; set; }
            public int Channel { get; set; }
            public string SampleFile { get; set; }
        }

        private class SampleTiming
        {
            public float MeasureStart { get; set; }
        }

        public static void OSUDump(string ojn_path, string out_dir)
        {
            //read ojn headers
            OJNData ojnHeader = new OJNData(ojn_path);

            //output folder for beatmap
            string folderName = $"{ojnHeader.SongID} {ojnHeader.Artist} - {ojnHeader.Title}";
            folderName = Helpers.GetSafeFilename(folderName);
            string outFolder = Path.Combine(out_dir, folderName);
            DirectoryInfo directory = Directory.CreateDirectory(outFolder);

            //get the ojm path, we assume it is in the same directory
            DirectoryInfo o2jFolder = Directory.GetParent(ojn_path);
            string ojmPath = Path.Combine(o2jFolder.FullName, $"{Path.GetFileNameWithoutExtension(ojn_path)}.ojm");

            //dump the media contents
            OJMDump.DumpFile(ojmPath, outFolder);

            //dump image
            ojnHeader.DumpImage(outFolder);

            //write HX
            WriteDiff(outFolder, ojnHeader, Diff.HX);

            //write NX
            if(ojnHeader.level[1] < ojnHeader.level[2]) WriteDiff(outFolder, ojnHeader, Diff.NX);

            //write EX
            if(ojnHeader.level[0] < ojnHeader.level[1]) WriteDiff(outFolder, ojnHeader, Diff.EX);

            ZipOSZ(outFolder);
            Directory.Delete(outFolder, true);

        }

        private static void WriteDiff(string path, OJNData ojn_header, Diff diff)
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
            diffname = $"{ojn_header.Title}{diffex}.osu";
            diffname = Helpers.GetSafeFilename(diffname);
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
                $"Title:{ojn_header.Title}",
                $"TitleUnicode:{ojn_header.Title}",
                $"Artist:{ojn_header.Artist}",
                $"ArtistUnicode:{ojn_header.Artist}",
                $"Creator:{ojn_header.Noter}",
                $"Version:7k{diffex}",
                "Source:o2jam",
                $"Tags:o2jam {ojn_header.OJMFile}",
                $"BeatmapID:{ojn_header.SongID}",
                $"BeatmapSetID:{ojn_header.SongID}",
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

            List<MSTiming> ms_timing = GenTimings(chart.Timings);
            List<OsuNote> note_list = GenNotes(chart.Notes, ms_timing);
            List<OsuSample> sample_list = GenEvents(chart.Samples, ms_timing);

            //as per osu specification
            int[] column_map = new int[7]{ 36,109,182,255,328,401,474};
            using (StreamWriter w = new StreamWriter(File.Open(diffname, FileMode.Create)))
            {
                w.Write("osu file format v14\n\n");
                foreach (var l in general) w.WriteLine(l);
                foreach (var l in metadata) w.WriteLine(l);
                foreach (var l in difficulty) w.WriteLine(l);
                foreach (var l in events) w.WriteLine(l);

                foreach(var hit in sample_list)
                {
                    w.Write($"5,{(int)hit.MsStart},0,\"{hit.SampleFile}\",100\n");
                }

                w.WriteLine("\n\n[TimingPoints]");
                foreach (var timing in ms_timing)
                {
                    float bpm = 240 / timing.MsPerMeasure * 1000;
                    float ms_per_beat = 60000 / bpm;
                    string line = $"{(int)timing.MsMarking},{ms_per_beat},4,2,2,100,1,0\n";
                    w.Write(line);
                }

                w.WriteLine("\n\n[HitObjects]");


                foreach (var note in note_list)
                {
                    string line;
                    int vol = 100;
                    if (note.SampleFile == null) vol = 0;
                    if (note.LN)
                    {
                        line = $"{column_map[note.Channel]},0,{(int)note.MsStart},128,0,{(int)note.MsEnd}:0:0:0:{vol}:{note.SampleFile}\n";
                    }
                    else
                    {
                        line = $"{column_map[note.Channel]},0,{(int)note.MsStart},1,0,0:0:0:{vol}:{note.SampleFile}\n";
                    }
                    w.Write(line);
                }
            }
        }

        private static void ZipOSZ(String path)
        {
            //get the ojm path, we assume it is in the same directory
            string beatmapName = Path.GetFileName(path) + ".osz";
            DirectoryInfo beatmapParentFolder = Directory.GetParent(path);
            string outputName = Path.Combine(beatmapParentFolder.FullName, beatmapName);
            if (File.Exists(outputName)) File.Delete(outputName);
            ZipFile.CreateFromDirectory(path, outputName);
        }

        private static List<OsuSample> GenEvents(List<NotePackage.NoteEvent> samples, List<MSTiming> timings)
        {
            List<OsuSample> osu_samples = new List<OsuSample>();
            foreach (var sample in samples)
            {
                OsuSample hitsound = new OsuSample
                {
                    Channel = sample.Channel
                };

                //get most recent bpm mark
                int index = timings.BinarySearch(new MSTiming { MeasureStart = sample.MeasureStart }, new TimingComparer());
                if (index < 0) index = ~index - 1;
                MSTiming last_bpm = timings[index];

                float offset = sample.MeasureStart - last_bpm.MeasureStart;
                offset *= last_bpm.MsPerMeasure;
                hitsound.MsStart = offset + last_bpm.MsMarking;

                if(sample.Value > 1000) hitsound.SampleFile = $"M{sample.Value - 999}.ogg";
                else hitsound.SampleFile = $"M{sample.Value + 1}.ogg";
                osu_samples.Add(hitsound);
            }
            return osu_samples;
        }

        private static List<MSTiming> GenTimings(List<NotePackage.BPMChange> timings)
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
                    ms_time.MeasureStart = timing.MeasureStart;
                    ms_time.MsMarking = 0;
                    ms_time.MsPerMeasure = 240 / timing.NewBPM * 1000;
                    osu_timings.Add(ms_time);
                    continue;
                }
                float ms_per_measure = 240 / prev.NewBPM * 1000;
                float delta = timing.MeasureStart - prev.MeasureStart;
                milliseconds += delta * ms_per_measure;
                prev = timing;

                ms_time.MeasureStart = timing.MeasureStart;
                ms_time.MsMarking = milliseconds;
                ms_time.MsPerMeasure = 240 / timing.NewBPM * 1000;
                osu_timings.Add(ms_time);
            }
            return osu_timings;
        }

        private class TimingComparer : IComparer<MSTiming>
        {

            public int Compare(MSTiming x, MSTiming y)
            {
                return x.MeasureStart.CompareTo(y.MeasureStart);
            }

        }

        private static List<OsuNote> GenNotes(List<NotePackage.NoteEvent> notes, List<MSTiming> timings)
        {
            List<OsuNote> note_list = new List<OsuNote>();
            foreach(var note in notes)
            {
                //Console.WriteLine($"channel {note.channel} start {note.measure_start} end {note.measure_end}");
                //grab the last bpm change
                MSTiming last_bpm = timings[0];

                //incase the end of a LN is after a bpm change
                MSTiming next_bpm = null;

                int index = timings.BinarySearch(new MSTiming { MeasureStart = note.MeasureStart }, new TimingComparer());
                if (index < 0) index = ~index - 1;
                last_bpm = timings[index];

                //check for bpm changes throughout a long note
                if (note.NoteType == 3)
                {
                    index = timings.BinarySearch(new MSTiming { MeasureStart = note.MeasureEnd }, new TimingComparer());
                    if (index < 0) index = ~index - 1;
                    next_bpm = timings[index];

                }

                OsuNote osu_note = new OsuNote
                {
                    Channel = note.Channel
                };

                //difference between our note and the start of the last bpm
                float start_delta = note.MeasureStart - last_bpm.MeasureStart;

                //grab the ms base for the start of the bpm change and add 
                float start_offset = last_bpm.MsPerMeasure * start_delta;
                float hit_start = last_bpm.MsMarking + start_offset;

                osu_note.MsStart = hit_start;

                //grab the offset for the end of a long note (if needed)
                if(note.NoteType == 3)
                {
                    float end_delta = note.MeasureEnd - next_bpm.MeasureStart;
                    float end_offset = next_bpm.MsPerMeasure * end_delta;
                    float hit_end = next_bpm.MsMarking + end_offset;
                    osu_note.LN = true;
                    osu_note.MsEnd = hit_end;
                }
                if (note.Value > 1000) osu_note.SampleFile = $"M{note.Value - 999}.ogg";
                else if(note.Value > 2)osu_note.SampleFile = $"M{note.Value + 1}.ogg";

                note_list.Add(osu_note);
            }
            return note_list;
        }
    }
}
