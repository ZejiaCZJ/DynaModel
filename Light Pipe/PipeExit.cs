using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Rhino.Geometry;

namespace DynaModel_v2.Light_Pipe
{
    public class PipeExit
    {
        public Voxel location { get; set; }
        public Boolean isTaken { get; set; }

        public Point3d actualLocation { get; set; }

        public PipeExit()
        {
            Point3d actualLocation = Point3d.Unset;
        }

        public PipeExit(Voxel location, Point3d actualLocation)
        {
            this.location = location;
            this.isTaken = false;
            this.actualLocation = actualLocation;
        }
    }
}
