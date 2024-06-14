using System;
using System.Collections.Generic;
using DynaModel_v2.SharedData;
using Grasshopper.Kernel;
using Rhino.Geometry;

namespace DynaModel_v2.Final_Stage
{
    public class Generate : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the Generate class.
        /// </summary>
        public Generate()
          : base("Generate", "Nickname",
              "This component generate all the user-defined items on the model in a 3D printer friendly fashion",
              "DynaModel_v2", "Final Stage")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddBooleanParameter("Generate Button", "G", "The generate button", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddBooleanParameter("Save Button", "S", "Show the save button", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            for(int i = 0; i < SavedItems.items.Count; i++)
            {
                GenerateHelper generateHelper = new GenerateHelper(out bool success);
                if (SavedItems.items[i].Name == "Rotational Motion")
                {
                    Item item = SavedItems.items[i];
                    generateHelper.GenerateRotationalMotion(ref item, out List<Brep> subtrahends);
                }
            }
        
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
            get { return new Guid("643BDDFC-AF86-49E9-AF34-717D8539FC35"); }
        }
    }
}