using System;
using System.Collections.Generic;
using DynaModel_v2.SharedData;
using Grasshopper.Kernel;
using Rhino;
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
            bool generate = false;

            if(!DA.GetData(0, ref generate))
                return;

            if(generate)
            {
                GenerateHelper generateHelper = new GenerateHelper(out bool success);
                for (int i = 0; i < SavedItems.items.Count; i++)
                {
                    if (SavedItems.items[i].Name == "Rotational Motion")
                    {
                        Item item = SavedItems.items[i];
                        generateHelper.GenerateRotationalMotion(ref item, out List<Brep> subtrahends);
                    }
                    if (SavedItems.items[i].Name == "LED Light")
                    {
                        Item item = SavedItems.items[i];
                        generateHelper.GenerateLightPipe(ref item, out List<Brep> subtrahends);
                    }
                    if (SavedItems.items[i].Name == "Air Pipe")
                    {
                        Item item = SavedItems.items[i];
                        generateHelper.GenerateAirPipe(ref item, out List<Brep> subtrahends);
                    }
                }
                RhinoDoc.ActiveDoc.Objects.Add(generateHelper.foundation);
                foreach(var item in generateHelper.toDelete)
                {
                    RhinoDoc.ActiveDoc.Objects.Add(item);
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