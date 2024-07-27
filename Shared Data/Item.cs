using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DynaModel_v2.Geometry;
using Rhino.Geometry;

namespace DynaModel_v2.SharedData
{
    public class Item
    {
        public string Name { get; set; }

        public string Description { get; set; }

        public Point3d EndPoint { get; set; }

        public Point3d StartPoint { get; set; }

        public Brep customized_part_patch { get; set; }

        public List<Brep> EndPointModel { get; set; }

        public Essentials gearEssentials { get; set; }

        public List<Brep> ButtonSet { get; set; }

        public Curve Button_Spring_Circle { get; set; }

        public Item()
        {
            Name = string.Empty;
            EndPointModel = new List<Brep>();
        }
    }
}
