using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;

namespace O2JamUtils
{
    public class OsuBeatmap
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
            public float MsMarking { get; set; }
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
            public float MsStart { get; set; }
            public int Channel { get; set; }
            public string SampleFile { get; set; }
        }

        private class SampleTiming
        {
            public float MeasureStart { get; set; }
        }

        private class TimingComparer : IComparer<MSTiming>
        {
            public int Compare(MSTiming x, MSTiming y)
            {
                return x.MeasureStart.CompareTo(y.MeasureStart);
            }
        }

        private Boolean ext_renderer;
        private Boolean keysound_flag = false;

        private List<MSTiming> OsuTimings { get; set; } = new List<MSTiming>();
        private List<OsuNote> OsuNotes { get; set;  } = new List<OsuNote>();
        private List<OsuSample> OsuSamples { get; set;  } = new List<OsuSample>();

        //dumps file contents and returns the new directory with the contents
        public string BeatmapDump(string ojn_path, string out_dir, String renderer_path)
        {
            ext_renderer = renderer_path != null ? true : false;

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

            //dump image
            ojnHeader.DumpImage(outFolder);

            //write HX
            CreateDiff(outFolder, ojnHeader, Diff.HX);

            //write NX
            if(ojnHeader.level[1] < ojnHeader.level[2])
                CreateDiff(outFolder, ojnHeader, Diff.NX);

            //write EX
            if(ojnHeader.level[0] < ojnHeader.level[1] || ojnHeader.level[0] < ojnHeader.level[2])
                CreateDiff(outFolder, ojnHeader, Diff.EX);

            //dump the media contents
            if (!ext_renderer) OJMDump.DumpFile(ojmPath, outFolder);
            else
            {
                //this isn't used yet
                string render = keysound_flag ? "realtime" : "quick";
                var p = new Process();
                p.StartInfo = new ProcessStartInfo(renderer_path, $"{ojn_path} --format mp3 --outfile audio --rendermode quick")
                {
                    UseShellExecute = false
                };
                p.StartInfo.WorkingDirectory = outFolder;
                p.Start();
                p.WaitForExit();
            }

            return outFolder;
        }

        private void CreateDiff(string path, OJNData ojn_header, Diff diff)
        {
            NotePackage.Chart chart;

            String diffname = null;
            String diffex = null;

            switch (diff)
            {
                default:
                //case Diff.EX:
                    chart = ojn_header.DumpEXPackage();
                    diffex = $"_EX_LVL{ojn_header.level[0]}";
                    break;
                case Diff.NX:
                    chart = ojn_header.DumpNXPackage();
                    diffex = $"_NX_LVL{ojn_header.level[1]}";
                    break;
                case Diff.HX:
                    chart = ojn_header.DumpHXPackage();
                    diffex = $"_HX_LVL{ojn_header.level[2]}";
                    break;
            }
            diffname = $"{ojn_header.Title}{diffex}.osu";
            diffname = Helpers.GetSafeFilename(diffname);
            diffname = Path.Combine(path, diffname);

            //some chart logic
            if (chart.Samples.Count() == 1) ext_renderer = true;

            string audio_file = ext_renderer ? "audio.mp3" : "virtual";

            string[] general =
            {
                "[General]",
                $"AudioFilename: {audio_file}",
                "AudioLeadIn: 0",
                "PreviewTime: 6969",
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

            //clear the globals
            OsuTimings.Clear();
            OsuSamples.Clear();
            OsuNotes.Clear();

            //populate the osu lists
            GenTimings(chart.Timings);
            GenNotes(chart.Notes);
            GenSamples(chart.Samples);

            //as per osu specification
            int[] column_map = new int[7]{ 36,109,182,255,328,401,474};
            using (StreamWriter w = new StreamWriter(File.Open(diffname, FileMode.Create)))
            {
                w.Write("osu file format v14\n\n");
                foreach (var l in general) w.WriteLine(l);
                foreach (var l in metadata) w.WriteLine(l);
                foreach (var l in difficulty) w.WriteLine(l);
                foreach (var l in events) w.WriteLine(l);

                if (!ext_renderer)
                {
                    foreach (var hit in OsuSamples)
                    {
                        w.Write($"5,{(int)hit.MsStart},0,\"{hit.SampleFile}\",100\n");
                    }
                }

                w.WriteLine("\n\n[TimingPoints]");
                foreach (var timing in OsuTimings)
                {
                    float bpm = 240 / timing.MsPerMeasure * 1000;
                    float ms_per_beat = 60000 / bpm;
                    if (ms_per_beat < 0.01) ms_per_beat = 0.01f;
                    string line = $"{(int)timing.MsMarking},{ms_per_beat},4,2,2,100,1,0\n";
                    w.Write(line);
                }

                w.WriteLine("\n\n[HitObjects]");


                foreach (var note in OsuNotes)
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

        private void GenTimings(List<NotePackage.BPMChange> timings)
        {
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
                    OsuTimings.Add(ms_time);
                    continue;
                }
                float ms_per_measure = 240 / prev.NewBPM * 1000;
                float delta = timing.MeasureStart - prev.MeasureStart;

                milliseconds += delta * ms_per_measure;
                prev = timing;
                float prev_ms = OsuTimings[OsuTimings.Count() - 1].MsMarking;
                if (milliseconds - prev_ms < 1) OsuTimings[OsuTimings.Count() - 1].MsMarking--;

                ms_time.MeasureStart = timing.MeasureStart;
                ms_time.MsMarking = milliseconds;
                ms_time.MsPerMeasure = 240 / timing.NewBPM * 1000;
                OsuTimings.Add(ms_time);
            }
        }

        private void GenSamples(List<NotePackage.NoteEvent> samples)
        {
            foreach (var sample in samples)
            {
                OsuSample hitsound = new OsuSample
                {
                    Channel = sample.Channel
                };

                //get most recent bpm mark
                int index = OsuTimings.BinarySearch(new MSTiming { MeasureStart = sample.MeasureStart }, new TimingComparer());
                if (index < 0) index = ~index - 1;
                MSTiming last_bpm = OsuTimings[index];

                float offset = sample.MeasureStart - last_bpm.MeasureStart;
                offset *= last_bpm.MsPerMeasure;
                hitsound.MsStart = offset + last_bpm.MsMarking;

                //custom sounds, doesn't really work...
                if (!ext_renderer)
                {
                    if (sample.Value > 1000) hitsound.SampleFile = $"M{sample.Value - 999}.ogg";
                    else hitsound.SampleFile = $"M{sample.Value + 1}.ogg";
                }
                OsuSamples.Add(hitsound);
            }
        }

        private void GenNotes(List<NotePackage.NoteEvent> notes)
        {
            foreach(var note in notes)
            {
                //Console.WriteLine($"channel {note.channel} start {note.measure_start} end {note.measure_end}");
                //grab the last bpm change
                MSTiming last_bpm = OsuTimings[0];

                //incase the end of a LN is after a bpm change
                MSTiming next_bpm = null;

                int index = OsuTimings.BinarySearch(new MSTiming { MeasureStart = note.MeasureStart }, new TimingComparer());
                if (index < 0) index = ~index - 1;
                last_bpm = OsuTimings[index];

                //check for bpm changes throughout a long note
                if (note.NoteType == 3)
                {
                    index = OsuTimings.BinarySearch(new MSTiming { MeasureStart = note.MeasureEnd }, new TimingComparer());
                    if (index < 0) index = ~index - 1;
                    next_bpm = OsuTimings[index];

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
                if (!ext_renderer)
                {
                    if (note.Value > 1000) osu_note.SampleFile = $"M{note.Value - 999}.ogg";
                    else if (note.Value > 2) osu_note.SampleFile = $"M{note.Value + 1}.ogg";
                }

                OsuNotes.Add(osu_note);
            }
        }
    }
}
