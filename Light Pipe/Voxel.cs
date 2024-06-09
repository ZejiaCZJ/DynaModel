using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Rhino.Geometry;

namespace DynaModel_v2.Light_Pipe
{
    public class Voxel
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public double Cost { get; set; } //True cost from start point, AKA. g
        public double Distance { get; set; } //Estimated distance to end point, AKA. h
        public double CostDistance => Cost + Distance; //True cost + estimated distance, AKA. f
        public Voxel Parent { get; set; }

        public bool isTaken { get; set; }

        public Index Index { get; set; }

        public Vector3d vector { get; set; }

        public Voxel()
        {
            X = 0; Y = 0; Z = 0; Cost = 0; Distance = 0; isTaken = false; vector = Vector3d.Unset;
        }

        //This function set the distance with Manhattan distance
        public void SetDistance(double targetX, double targetY, double targetZ)
        {
            this.Distance = Math.Abs(targetX - X) + Math.Abs(targetY - Y) + Math.Abs(targetZ - Z);
        }

        //This function get the distance with Manhattan distance
        public double GetDistance(double targetX, double targetY, double targetZ)
        {
            //return Math.Abs(targetX - X) + Math.Abs(targetY - Y) + Math.Abs(targetZ - Z);//Manhattan
            return Math.Sqrt(Math.Pow(targetX - X, 2) + Math.Pow(targetY - Y, 2) + Math.Pow(targetZ - Z, 2)); //Euclidean
        }

        public bool Equal(Voxel voxel)
        {
            return voxel.X == this.X && voxel.Y == this.Y && voxel.Z == this.Z;
        }
    }
}
