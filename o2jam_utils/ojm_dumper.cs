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

namespace o2jam_utils
{
    public class OJM_Dump
    {
        //according to ojm documentation
        private static readonly byte[] nami = new byte[] { 0x6E, 0x61, 0x6D, 0x69 };


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

        /** some weird encryption */
        private static int acc_keybyte = 0xFF;
        private static int acc_counter = 0;
        private static byte[] acc_xor(byte[] buf)
        {
            int temp = 0;
            byte this_byte = 0;
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

        public static String getType(string path)
        {
            byte[] signature = new byte[3];
            using (MemoryMappedFile f = MemFile(path))
            using (MemoryMappedViewStream vs = f.CreateViewStream(0, 0, MemoryMappedFileAccess.Read))
            {
                vs.Read(signature, 0, signature.Length);
            }
            return System.Text.Encoding.Default.GetString(signature);
        }

        public static void dumpFile(string path, string outdir)
        {
            int signature;
            MemoryMappedFile f = MemFile(path);

            using (MemoryMappedViewStream vs = f.CreateViewStream(0L, 0L, MemoryMappedFileAccess.Read))
            using (BinaryReader reader = new BinaryReader(vs))
                signature = reader.ReadInt32();

            switch (signature)
            {
                case M30_SIGNATURE:
                    parseM30(f, path, outdir);
                    break;
                case OMC_SIGNATURE:
                case OJM_SIGNATURE:
                    parseOMC(f, outdir);
                    break;
            }
        }

        private static void parseM30(MemoryMappedFile f, String path, String out_dir)
        {
            MemoryMappedViewStream buf = f.CreateViewStream(4,24, MemoryMappedFileAccess.Read);
            BinaryReader reader = new BinaryReader(buf);

            // header
            byte[] unk_fixed = reader.ReadBytes(4);
            byte nami_encoded = reader.ReadByte();
            byte[] unk_fixed2 = reader.ReadBytes(3);
            short sample_count = reader.ReadInt16();
            byte[] unk_fixed3 = reader.ReadBytes(6);
            int payload_size = reader.ReadInt32();
            int padding = reader.ReadInt32();

            buf = f.CreateViewStream(28, 0, MemoryMappedFileAccess.Read);
            reader = new BinaryReader(buf);

            for (int i = 0; i < sample_count; i++)
            {
                long remaining_bytes = buf.Capacity - buf.Position;
                if(remaining_bytes < 52)
                {
                    throw new System.ArgumentException("OJM Header sample size is incorrect");
                }

                byte[] sample_name = reader.ReadBytes(32);
                int sample_size = reader.ReadInt32();
                byte unk_sample_type = reader.ReadByte();
                byte unk_off = reader.ReadByte();
                short fixed_2 = reader.ReadInt16();
                int unk_flag = reader.ReadInt32();
                short note_ref = reader.ReadInt16();
                short unk_zero = reader.ReadInt16();
                byte[] unk_wut = reader.ReadBytes(3);
                byte unk_counter = reader.ReadByte();
                byte[] sample_data = reader.ReadBytes(sample_size);

                if (nami_encoded > 0) namiXOR(sample_data);

                int value = note_ref;

                //background note
                if(unk_sample_type == 0)
                {
                    value = 1000 + note_ref;
                }

                //unknown sound
                else if(unk_sample_type != 5)
                {
                    Console.WriteLine("not recognized sample type");
                }

                //write the filename
                String filename = unk_sample_type + "-" + note_ref + ".ogg";
                String out_file = Path.Combine(out_dir, filename);
                BinaryWriter writer = new BinaryWriter(File.Open(out_file, FileMode.Create));
                writer.Write(sample_data);
            }
        }

        private static void parseOMC(MemoryMappedFile f, String out_dir)
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
            int sample_id = 0;

            acc_keybyte = 0xFF;
            acc_counter = 0;
            
            //read wav data first
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
                wav_data = rearrange(wav_data);
                wav_data = acc_xor(wav_data);

                //write the filename can't be bothered finding out the encoding
                sample_name = sample_name.Where(i => i != 0).ToArray();
                string filename = sample_id + ".wav";
                String out_file = Path.Combine(out_dir, filename);
                BinaryWriter writer = new BinaryWriter(File.Open(out_file, FileMode.Create));

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

                writer.Close();

            }

            sample_id = 1000; //ogg uses 1000+
            byte[] tmp_buffer = new byte[1024];
            while(file_offset < filesize) //read ogg data
            {
                buf = f.CreateViewStream(file_offset, 36, MemoryMappedFileAccess.Read);
                reader = new BinaryReader(buf);
                file_offset += 36;

                byte[] sample_name = reader.ReadBytes(32);
                int sample_size = reader.ReadInt32();

                if(sample_size == 0)
                {
                    sample_id++;
                    continue;
                }

                buf = f.CreateViewStream(file_offset, sample_size, MemoryMappedFileAccess.Read);
                reader = new BinaryReader(buf);
                file_offset += sample_size;

                //write the filename
                sample_name = sample_name.Where(i => i != 0).ToArray();
                bool gb2312 = false;
                string decname = Encoding.GetEncoding(936).GetString(sample_name);
                string filename = sample_id + ".ogg";
                String out_file = Path.Combine(out_dir, filename);
                BinaryWriter writer = new BinaryWriter(File.Open(out_file, FileMode.Create));

                while (reader.BaseStream.Position != reader.BaseStream.Length)
                {
                    tmp_buffer = reader.ReadBytes(1024);
                    writer.Write(tmp_buffer);
                }
                writer.Close();
                sample_id++;

            }

        }

        private static byte[] rearrange(byte[] encoded_data)
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

        private static void namiXOR(byte[] array)
        {
            for(int i = 0; i+3 < array.Length; i+=4)
            {
                for(int d = 0; d < 4; d++)
                {
                    array[i + d] ^= nami[d];
                }
            }
        }
    }
}
