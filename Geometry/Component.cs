using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Grasshopper.Kernel.Components;
using Grasshopper.Kernel;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.DocObjects;
using Rhino.Collections;
using Rhino.Input.Custom;
using Rhino;
using Grasshopper.Kernel.Types.Transforms;

namespace DynaModel_v2
{
    namespace Geometry
    {
        /// <summary>
        /// This is the base class of all mechanical components, providing all basic support methods
        /// </summary>
        public class Component : Entity
        {
            private bool isKinetic = false;//Currently only spring and spiral are kinetic.
            private Curve baseCurve;

            public bool IsKinetic { get => isKinetic; protected set => isKinetic = value; }
            public Curve BaseCurve { get => baseCurve; protected set => baseCurve = value; }

            public Component(bool kinetic = false, string name = "") : base(false, name)
            {
                baseCurve = null;
                isKinetic = kinetic;
                base.Model = null;

            }
            protected virtual void GenerateBaseCurve()
            {

            }
            public virtual void Generate()
            {

            }
            public virtual bool IsEngaged(Component obj)
            {
                return false;
            }
        }
    }

}