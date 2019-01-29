using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

/* a c# implementation of the ojm dumper based on information from open2jam */

namespace O2JamUtils
{
    public class OJMDump
    {
        /** the xor mask used in the M30 format */
        private readonly static byte[] mask_nami = new byte[] { 0x6E, 0x61, 0x6D, 0x69 }; // nami
        private readonly static byte[] mask_0412 = new byte[] { 0x30, 0x34, 0x31, 0x32 }; // 0412


        /** the M30 signature, "M30\0" in little endian */
        private const int M30_SIGNATURE = 0x0030334D;

        /** the OMC signature, "OMC\0" in little endian */
        private const int OMC_SIGNATURE = 0x00434D4F;

        /** the OJM signature, "OJM\0" in little endian */
        private const int OJM_SIGNATURE = 0x004D4A4F;

        //????????? from the notetool
        private static readonly byte[] REARRANGE_TABLE = new byte[]{
            0x10, 0x0E, 0x02, 0x09, 0x04, 0x00, 0x07, 0x01,
            0x06, 0x08, 0x0F, 0x0A, 0x05, 0x0C, 0x03, 0x0D,
            0x0B, 0x07, 0x02, 0x0A, 0x0B, 0x03, 0x05, 0x0D,
            0x08, 0x04, 0x00, 0x0C, 0x06, 0x0F, 0x0E, 0x10,
            0x01, 0x09, 0x0C, 0x0D, 0x03, 0x00, 0x06, 0x09,
            0x0A, 0x01, 0x07, 0x08, 0x10, 0x02, 0x0B, 0x0E,
            0x04, 0x0F, 0x05, 0x08, 0x03, 0x04, 0x0D, 0x06,
            0x05, 0x0B, 0x10, 0x02, 0x0C, 0x07, 0x09, 0x0A,
            0x0F, 0x0E, 0x00, 0x01, 0x0F, 0x02, 0x0C, 0x0D,
            0x00, 0x04, 0x01, 0x05, 0x07, 0x03, 0x09, 0x10,
            0x06, 0x0B, 0x0A, 0x08, 0x0E, 0x00, 0x04, 0x0B,
            0x10, 0x0F, 0x0D, 0x0C, 0x06, 0x05, 0x07, 0x01,
            0x02, 0x03, 0x08, 0x09, 0x0A, 0x0E, 0x03, 0x10,
            0x08, 0x07, 0x06, 0x09, 0x0E, 0x0D, 0x00, 0x0A,
            0x0B, 0x04, 0x05, 0x0C, 0x02, 0x01, 0x0F, 0x04,
            0x0E, 0x10, 0x0F, 0x05, 0x08, 0x07, 0x0B, 0x00,
            0x01, 0x06, 0x02, 0x0C, 0x09, 0x03, 0x0A, 0x0D,
            0x06, 0x0D, 0x0E, 0x07, 0x10, 0x0A, 0x0B, 0x00,
            0x01, 0x0C, 0x0F, 0x02, 0x03, 0x08, 0x09, 0x04,
            0x05, 0x0A, 0x0C, 0x00, 0x08, 0x09, 0x0D, 0x03,
            0x04, 0x05, 0x10, 0x0E, 0x0F, 0x01, 0x02, 0x0B,
            0x06, 0x07, 0x05, 0x06, 0x0C, 0x04, 0x0D, 0x0F,
            0x07, 0x0E, 0x08, 0x01, 0x09, 0x02, 0x10, 0x0A,
            0x0B, 0x00, 0x03, 0x0B, 0x0F, 0x04, 0x0E, 0x03,
            0x01, 0x00, 0x02, 0x0D, 0x0C, 0x06, 0x07, 0x05,
            0x10, 0x09, 0x08, 0x0A, 0x03, 0x02, 0x01, 0x00,
            0x04, 0x0C, 0x0D, 0x0B, 0x10, 0x05, 0x06, 0x0F,
            0x0E, 0x07, 0x09, 0x0A, 0x08, 0x09, 0x0A, 0x00,
            0x07, 0x08, 0x06, 0x10, 0x03, 0x04, 0x01, 0x02,
            0x05, 0x0B, 0x0E, 0x0F, 0x0D, 0x0C, 0x0A, 0x06,
            0x09, 0x0C, 0x0B, 0x10, 0x07, 0x08, 0x00, 0x0F,
            0x03, 0x01, 0x02, 0x05, 0x0D, 0x0E, 0x04, 0x0D,
            0x00, 0x01, 0x0E, 0x02, 0x03, 0x08, 0x0B, 0x07,
            0x0C, 0x09, 0x05, 0x0A, 0x0F, 0x04, 0x06, 0x10,
            0x01, 0x0E, 0x02, 0x03, 0x0D, 0x0B, 0x07, 0x00,
            0x08, 0x0C, 0x09, 0x06, 0x0F, 0x10, 0x05, 0x0A,
            0x04, 0x00
        };

        public static String GetType(string path)
        {
            byte[] signature = new byte[3];
            using (MemoryMappedFile f = Helpers.MemFile(path))
            using (MemoryMappedViewStream vs = f.CreateViewStream(0, 0, MemoryMappedFileAccess.Read))
            {
                vs.Read(signature, 0, signature.Length);
            }
            return Encoding.Default.GetString(signature);
        }

        public static void DumpFile(string path, string outdir)
        {
            int signature;
            MemoryMappedFile f = Helpers.MemFile(path);

            using (MemoryMappedViewStream vs = f.CreateViewStream(0L, 0L, MemoryMappedFileAccess.Read))
            using (BinaryReader reader = new BinaryReader(vs))
                signature = reader.ReadInt32();

            switch (signature)
            {
                case M30_SIGNATURE:
                    ParseM30(f, path, outdir);
                    break;
                case OMC_SIGNATURE:
                case OJM_SIGNATURE:
                    ParseOMC(f, outdir);
                    break;
            }
        }

        private static void ParseM30(MemoryMappedFile f, String path, String out_dir)
        {
            MemoryMappedViewStream buf = f.CreateViewStream(4,24, MemoryMappedFileAccess.Read);
            BinaryReader reader = new BinaryReader(buf);

            // header
            int file_format_version = reader.ReadInt32();
            int encryption_flag = reader.ReadInt32();
            int sample_count = reader.ReadInt32();
            int sample_offset = reader.ReadInt32();
            int payload_size = reader.ReadInt32();
            int padding = reader.ReadInt32();

            using (buf = f.CreateViewStream(28, 0, MemoryMappedFileAccess.Read))
            using (reader = new BinaryReader(buf))
            {

                for (int i = 0; i < sample_count; i++)
                {
                    long remaining_bytes = buf.Capacity - buf.Position;
                    if (remaining_bytes < 52)
                    {
                        throw new System.ArgumentException("OJM Header sample size is incorrect");
                    }

                    byte[] sample_name = reader.ReadBytes(32);
                    int sample_size = reader.ReadInt32();

                    short codec_code = reader.ReadInt16();
                    short codec_code2 = reader.ReadInt16();

                    int music_flag = reader.ReadInt32();
                    short note_ref = reader.ReadInt16();
                    short unk_zero = reader.ReadInt16();
                    int pcm_samples = reader.ReadInt32();

                    byte[] sample_data = reader.ReadBytes(sample_size);

                    switch (encryption_flag)
                    {
                        case 0: break; //Let it pass
                        case 16: M30_xor(sample_data, mask_nami); break;
                        case 32: M30_xor(sample_data, mask_0412); break;
                        default: break;
                    }

                    //normal note
                    int value = note_ref + 2;
                    String filename = $"M{value}.ogg";

                    //background note
                    if (codec_code == 0)
                    {
                        value = 1000 + note_ref;
                        filename = $"W{value}.ogg";
                    }

                    //unknown sound
                    else if (codec_code != 5)
                    {
                        Console.WriteLine("not recognized sample type");
                    }

                    //write the filename
                    String out_file = Path.Combine(out_dir, filename);
                    using (BinaryWriter writer = new BinaryWriter(File.Open(out_file, FileMode.Create)))
                        writer.Write(sample_data);
                }
            }
        }

        private static void M30_xor(byte[] array, byte[] mask)
        {
            for (int i = 0; i + 3 < array.Length; i += 4)
            {
                array[i + 0] ^= mask[0];
                array[i + 1] ^= mask[1];
                array[i + 2] ^= mask[2];
                array[i + 3] ^= mask[3];
            }
        }

        private static void ParseOMC(MemoryMappedFile f, String out_dir)
        {
            MemoryMappedViewStream buf = f.CreateViewStream(4, 16, MemoryMappedFileAccess.Read);
            BinaryReader reader = new BinaryReader(buf);

            // header
            short wav_count = reader.ReadInt16(); //wav count
            short ogg_count = reader.ReadInt16(); //ogg count
            int wav_start = reader.ReadInt32();
            int ogg_start = reader.ReadInt32();
            int filesize = reader.ReadInt32();

            int file_offset = 20;
            int sample_id = 2;

            acc_keybyte = 0xFF;
            acc_counter = 0;
            
            //read wav data first this doesn't really work yet
            while(file_offset < ogg_start)
            {
                buf = f.CreateViewStream(file_offset, 56, MemoryMappedFileAccess.Read);
                reader = new BinaryReader(buf);

                //advance 56 bytes per header
                file_offset += 56;

                //WAV DATA HEADERS
                //name of sample
                byte[] sample_name = reader.ReadBytes(32);

                //wav metadata
                short audio_format = reader.ReadInt16();
                short num_channels = reader.ReadInt16();
                int sample_rate = reader.ReadInt32();
                int bit_rate = reader.ReadInt32();
                short block_align = reader.ReadInt16();
                short bits_per_sample = reader.ReadInt16();
                int unk_data = reader.ReadInt32();

                //sample size
                int chunk_size = reader.ReadInt32();

                //0 size wav, go to next
                if (chunk_size == 0)
                {
                    sample_id++;
                    continue;
                }

                buf = f.CreateViewStream(file_offset, chunk_size, MemoryMappedFileAccess.Read);
                file_offset += chunk_size;
                reader = new BinaryReader(buf);

                //basically get remaining data
                byte[] wav_data = new byte[chunk_size];
                buf.Read(wav_data, 0, chunk_size);
                wav_data = Rearrange(wav_data);
                wav_data = OMC_xor(wav_data);

                //write the filename can't be bothered finding out the encoding
                sample_name = sample_name.Where(i => i != 0).ToArray();
                string filename = $"M{sample_id}.wav";
                String out_file = Path.Combine(out_dir, filename);
                using (BinaryWriter writer = new BinaryWriter(File.Open(out_file, FileMode.Create)))
                {
                    writer.Write("RIFF");
                    writer.Write(chunk_size + 36);
                    writer.Write("WAVE");
                    writer.Write("fmt");
                    writer.Write(0x10);
                    writer.Write(audio_format);
                    writer.Write(num_channels);
                    writer.Write(sample_rate);
                    writer.Write(bit_rate);
                    writer.Write(block_align);
                    writer.Write(bits_per_sample);
                    writer.Write("data");
                    writer.Write(chunk_size);
                    writer.Write(wav_data);
                }

            }

            sample_id = 1002; //ogg uses 1000+
            byte[] tmp_buffer = new byte[1024];
            while (file_offset < filesize) //read ogg data
            {
                buf = f.CreateViewStream(file_offset, 36, MemoryMappedFileAccess.Read);
                reader = new BinaryReader(buf);
                file_offset += 36;

                byte[] sample_name = reader.ReadBytes(32);
                int sample_size = reader.ReadInt32();

                if (sample_size == 0)
                {
                    sample_id++;
                    continue;
                }

                using (buf = f.CreateViewStream(file_offset, sample_size, MemoryMappedFileAccess.Read))
                {
                    reader = new BinaryReader(buf);
                    file_offset += sample_size;

                    //write the filename
                    sample_name = sample_name.Where(i => i != 0).ToArray();
                    string decname = Encoding.GetEncoding(936).GetString(sample_name);
                    string filename = $"M{sample_id - 1000}.ogg";
                    String out_file = Path.Combine(out_dir, filename);
                    using (BinaryWriter writer = new BinaryWriter(File.Open(out_file, FileMode.Create)))
                    {

                        while (reader.BaseStream.Position != reader.BaseStream.Length)
                        {
                            tmp_buffer = reader.ReadBytes(1024);
                            writer.Write(tmp_buffer);
                        }
                        writer.Close();
                        sample_id++;
                    }
                }

            }
        }

        /** some weird encryption */
        private static int acc_keybyte = 0xFF;
        private static int acc_counter = 0;
        private static byte[] OMC_xor(byte[] buf)
        {
            int temp;
            byte this_byte;
            for (int i = 0; i < buf.Length; i++)
            {
                temp = this_byte = buf[i];

                if (((acc_keybyte << acc_counter) & 0x80) != 0)
                {
                    this_byte = (byte)~this_byte;
                }

                buf[i] = this_byte;
                acc_counter++;
                if (acc_counter > 7)
                {
                    acc_counter = 0;
                    acc_keybyte = temp;
                }
            }
            return buf;
        }

        private static byte[] Rearrange(byte[] encoded_data)
        {
            int len = encoded_data.Length;
            int key = ((len % 17) << 4) + (len % 17);

            int block_size = len / 17;

            byte[] raw_data = new byte[len];
            System.Array.Copy(encoded_data, raw_data, 0);

            for(int i = 0; i < 17; i++)
            {
                //i think this works properly
                System.Array.ConstrainedCopy(encoded_data, i, raw_data, REARRANGE_TABLE[key], block_size);
                key++;
            }
            return raw_data;
        }
    }
}
