using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Rhino;
using Rhino.Geometry;

namespace DynaModel_v2.UI
{
    public class ButtonListenerForChildWindows : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the ButtonListenerForChildWindows class.
        /// </summary>
        public ButtonListenerForChildWindows()
          : base("ButtonListenerForChildWindows (Deprecated)", "ButtonListener",
              "This component is a button listener that designs for launching windows",
              "DynaModel_v2", "UI")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddBooleanParameter("Button Value", "V", "The value of the button", GH_ParamAccess.item, false);
            pManager.AddTextParameter("Window State", "S", "The current state of the window", GH_ParamAccess.item, "Hide");
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddBooleanParameter("New Window State", "S", "The new state of the window", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            bool v = false;
            string s = "Hidden";

            if (!DA.GetData(0, ref v))
                DA.SetData(0, false);
            if (!DA.GetData(1, ref s))
                DA.SetData(0, false);
            else
                RhinoApp.WriteLine("here");

            if (v == false && s == "Hide")
                DA.SetData(0, false);
            else
                DA.SetData(0, true);
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
            get { return new Guid("4DE6135F-5A6D-4714-BFAB-18E5843D5C87"); }
        }
    }
}