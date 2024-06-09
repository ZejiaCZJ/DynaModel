using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Rhino.Geometry;

namespace DynaModel_v2.Rotational_Motion
{
    public class RadioButtonListener : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the MyComponent1 class.
        /// </summary>
        public RadioButtonListener()
          : base("RadioButtonListener", "RadioListener",
              "This component listens to the RadioButtonListener",
              "DynaModel_v2", "Rotational Motion")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddBooleanParameter("Slow", "S", "Slow radio button", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Normal", "N", "Normal radio button", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Fast", "F", "Fast radio button", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Gear Speed", "S", "Speed for the end gear", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            bool slow = false;
            bool normal = false;
            bool fast = false;

            if (!DA.GetData(0, ref slow))
                return;
            if (!DA.GetData(1, ref normal))
                return;
            if (!DA.GetData(2, ref fast))
                return;

            if (slow)
                DA.SetData(0, "0.5");
            else if (normal)
                DA.SetData(0, "1");
            else if(fast)
                DA.SetData(0, "2");

        }

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                //You can add image files to your project resources and access them like this:
                // return Resources.IconForThisComponent;
                return null;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("316C379F-1145-4B91-8C0C-2683B8709EB9"); }
        }
    }
}