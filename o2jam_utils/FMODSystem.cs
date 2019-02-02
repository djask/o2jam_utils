using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace O2JamUtils
{
    public class FMODSystem
    {
        public FMOD.System FmodSys;
        public List<OJMDump.FMODSample> Samples { get; set; }
        public Boolean Stream = false;

        public FMODSystem(Boolean nrt = true)
        {
            FmodSys = InitFMOD(nrt: true);
        }

        public FMOD.System InitFMOD(Boolean nrt = true)
        {
            Stream = nrt;

            FMOD.RESULT result;


            char[] name = new char[256];

            /* 
                Create a System object and initialize. 
            */
            result = FMOD.Factory.System_Create(out var system);

            system.getNumDrivers(out int numdrivers);
            if (numdrivers == 0)
            {
                system.setOutput(FMOD.OUTPUTTYPE.NOSOUND);
            }
            system.init(100, FMOD.INITFLAGS.NORMAL, IntPtr.Zero);
            return system;
        }

        public void PlaySample(int refID)
        {
            var found = Samples.Where(sample => sample.RefID == refID).FirstOrDefault();
            FmodSys.playSound(found.Data, null, false, out var channel);
        }

        public void LoadSamples(string path, Boolean stream)
        {
            Samples = OJMDump.ExtractSamples(path, FmodSys, stream);
        }
    }
}
