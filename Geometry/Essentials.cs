using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Rhino;
using Rhino.Collections;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using Rhino.Input;
using Plane = Rhino.Geometry.Plane;

namespace DynaModel_v2.Geometry
{
    public class Essentials
    {
        private List<Guid> models;
        private Guid mainModel;
        private Guid cutter;
        private Guid endEffector;
        private Plane cutterPlane;
        private double cutterThickness;

        
        
        public List<Guid> Models { get => models; set => models = value; }
        public Guid MainModel { get => mainModel; set => mainModel = value; }
        public Guid Cutter { get => cutter; set => cutter = value; }
        public Plane CutterPlane { get => cutterPlane; set => cutterPlane = value; }
        public double CutterThickness { get => cutterThickness; set => cutterThickness = value; }

        public Guid EndEffector { get => endEffector; set => endEffector = value; }

        public Essentials()
        {
            models = new List<Guid>();  //To store BooleanDifference(Main Modle, Cutter)
            mainModel = new Guid();
            cutter = new Guid();
            endEffector = new Guid();
            cutterPlane = new Plane();
            cutterThickness = 0.0;
        }

        public Essentials(List<Guid> models, Guid mainModel, Guid cutter, Guid endEffector, Plane cutterPlane, double cutterThickness)
        {
            this.models = models;
            this.mainModel = mainModel;
            this.cutter = cutter;
            this.endEffector = endEffector;
            this.cutterPlane = cutterPlane; 
            this.cutterThickness = cutterThickness;
        }

        public Essentials Duplicate(Essentials old)
        {
            Essentials duplicate = new Essentials();
            foreach (var model in old.Models) { duplicate.Models.Add(model); }
            duplicate.mainModel = old.mainModel;
            duplicate.cutter = old.cutter;
            duplicate.endEffector = old.endEffector;
            duplicate.cutterPlane = old.cutterPlane.Clone();
            duplicate.cutterThickness= old.cutterThickness;

            return duplicate;
        }
    }
}
