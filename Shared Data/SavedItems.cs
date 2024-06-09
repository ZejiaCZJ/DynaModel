using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;

namespace DynaModel_v2.SharedData
{
    public class SavedItems : GH_Component
    {
        public static List<Item> items;
        private static int itemsCount;
        public static List<String> itemsNames;
        public static List<Guid> originalModelGuids;


        /// <summary>
        /// Initializes a new instance of the MyComponent1 class.
        /// </summary>
        public SavedItems()
          : base("SavedItems", "Saved",
              "This component contains the saved parameters that the user wants to add to the model",
              "DynaModel_v2", "Main")
        {
            items = new List<Item>();
            itemsCount = 0;
            itemsNames = new List<String>();
            originalModelGuids = new List<Guid>();
            foreach (var guid in RhinoDoc.ActiveDoc.Objects.GetObjectList(ObjectType.Brep))
                originalModelGuids.Add(guid.Id);
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("List of saved items", "L", "This contains a list of saved items", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {

            

            if (items.Count == 0)
            {
                DA.SetDataList(0, itemsNames);
            }

            

            if (items.Count > 0)
            {
                if(items.Count > itemsCount)
                {
                    for (int i = itemsCount; i < items.Count; i++)
                    {
                        itemsNames.Add(items[i].Name);
                    }
                    itemsCount = items.Count;
                }

                //TODO: Sort both the item and itemNames lists, based on the item type

                DA.SetDataList(0, itemsNames);
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
            get { return new Guid("AF8EF4EE-8F58-461B-873C-4AA83F2065BF"); }
        }
    }
}