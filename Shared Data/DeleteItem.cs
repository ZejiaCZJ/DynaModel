using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using DynaModel_v2.SharedData;
using Grasshopper.Kernel;
using Rhino.Geometry;

namespace DynaModel_v2.Shared_Data
{
    public class DeleteItem : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the DeleteItem class.
        /// </summary>
        public DeleteItem()
          : base("DeleteItem", "DeleteItem",
              "This component deletes an item in SavedItem",
              "DynaModel_v2", "Main")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddBooleanParameter("Delete Button", "D", "The delete button", GH_ParamAccess.item);
            pManager.AddGenericParameter("Item", "I", "The item to be delete", GH_ParamAccess.item);
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
            bool delete = false;
            string item = null;
            if (!DA.GetData(0, ref delete))
                return;
            if (!DA.GetData(1, ref item))
                if (!delete)
                    return;

            if(delete && item != null)
            {
                for(int i = 0; i < SavedItems.items.Count; i++)
                {
                    if (SavedItems.items[i].Name.Equals(item))
                    {
                        SavedItems.items.RemoveAt(i);
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
            get { return new Guid("55AC770F-B71E-4D5E-8659-8D4CDEBF6D99"); }
        }
    }
}