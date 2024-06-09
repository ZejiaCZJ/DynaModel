using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Rhino.DocObjects;
using Rhino;
using Rhino.Geometry;
using DynaModel_v2.SharedData;

namespace DynaModel_v2.Light_Pipe
{
    public class Cancel : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the Cancel class.
        /// </summary>
        public Cancel()
          : base("Cancel", "Cancel",
              "This component cancel the created sketch and return the rhino view to its original state",
              "DynaModel_v2", "LED light")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddBooleanParameter("Cancel Button", "C", "The save button", GH_ParamAccess.item);
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
            bool cancel = false;
            if (!DA.GetData(0, ref cancel))
                return;

            if (cancel)
            {
                var allObjects = new List<RhinoObject>(RhinoDoc.ActiveDoc.Objects.GetObjectList(ObjectType.AnyObject));
                foreach (var singleObject in allObjects)
                    RhinoDoc.ActiveDoc.Objects.Delete(singleObject.Id, true);
                foreach (var singleObject in SavedItems.originalModelGuids)
                    RhinoDoc.ActiveDoc.Objects.Show(singleObject, true);
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
            get { return new Guid("11072C45-6978-46D7-82BD-55F3EFC77327"); }
        }
    }
}