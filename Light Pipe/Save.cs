using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Rhino.DocObjects;
using Rhino;
using Rhino.Geometry;
using DynaModel_v2.SharedData;

namespace DynaModel_v2.Light_Pipe
{
    public class Save : GH_Component
    {
        private static Item essentials;

        /// <summary>
        /// Initializes a new instance of the Save class.
        /// </summary>
        public Save()
          : base("Save", "Save",
              "This component save the neccessary things to create a pipe",
              "DynaModel_v2", "LED Light")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddBooleanParameter("Save Button", "S", "The save button", GH_ParamAccess.item);
            pManager.AddGenericParameter("Item", "I", "The item to be save", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            bool save = false;
            if (!DA.GetData(0, ref save))
                return;
            if (!DA.GetData(1, ref essentials))
                if (!save)
                    return;

            if (save)
            {
                if (essentials.Name != string.Empty)
                    SavedItems.items.Add(essentials);
                //Return the Rhino view to its original look
                var allObjects = new List<RhinoObject>(RhinoDoc.ActiveDoc.Objects.GetObjectList(ObjectType.AnyObject));
                foreach (var singleObject in allObjects)
                    RhinoDoc.ActiveDoc.Objects.Delete(singleObject.Id, true);
                foreach (var singleObject in SavedItems.originalModelGuids)
                    RhinoDoc.ActiveDoc.Objects.Show(singleObject, true);

                //Triggers the list to update itself
                GH_Document ghDoc = Grasshopper.Instances.ActiveCanvas.Document;
                foreach (IGH_DocumentObject obj in ghDoc.Objects)
                {
                    Guid id = SavedItems.instanceID;
                    Guid id2 = obj.InstanceGuid;
                    string name = obj.Name;
                    if (name.Equals("SavedItems"))
                    {
                        obj.ExpireSolution(true);
                        break;
                    }

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
            get { return new Guid("DD3CB02C-1B2F-488C-BEC4-507DEEDE08DF"); }
        }
    }
}