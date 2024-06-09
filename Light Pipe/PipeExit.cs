using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DynaModel_v2.Light_Pipe
{
    public class PipeExit
    {
        public Voxel location { get; set; }
        public Boolean isTaken { get; set; }

        public PipeExit(Voxel location)
        {
            this.location = location;
            this.isTaken = false;
        }
    }
}
