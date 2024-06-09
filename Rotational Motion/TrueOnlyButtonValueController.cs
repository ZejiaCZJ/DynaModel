using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Rhino.Geometry;

namespace DynaModel_v2.Rotational_Motion
{
    public class TrueOnlyButtonValueController : GH_Component
    {
        public static int finished = 1; //0 = false, 1 = true, 2 = in progress
        /// <summary>
        /// Initializes a new instance of the TrueOnlyButtonValueController class.
        /// </summary>
        public TrueOnlyButtonValueController()
          : base("TrueButtonValueController", "TBVC",
              "This component control the true button value",
              "DynaModel_v2", "UI")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddBooleanParameter("True Button Value", "TB value", "The true button value", GH_ParamAccess.item, false);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddBooleanParameter("Start Button", "B", "The start button value", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            bool buttonClicked = false;
            if (!DA.GetData(0, ref buttonClicked))
                return;


            if (buttonClicked)
                DA.SetData(0, true);
            else
                DA.SetData(0, false);
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
            get { return new Guid("DB84C5D3-04F6-4FE3-A746-FC1ED70449AE"); }
        }
    }
}