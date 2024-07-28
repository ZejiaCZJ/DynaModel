using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;

namespace DynaModel_v2.SharedData
{
    public class SavedItems : GH_Component
    {
        public static List<Item> items;
        private static int itemsCount;
        public static List<String> itemsNames;
        public static List<Guid> originalModelGuids = new List<Guid>();
        public static Guid instanceID;
        private RhinoDoc myDoc = RhinoDoc.ActiveDoc;


        /// <summary>
        /// Initializes a new instance of the MyComponent1 class.
        /// </summary>
        public SavedItems()
          : base("SavedItems", "Saved",
              "This component contains the saved parameters that the user wants to add to the model",
              "DynaModel_v2", "Main")
        {
            instanceID = this.InstanceGuid;
            items = new List<Item>();
            itemsCount = 0;
            itemsNames = new List<String>();
            
            if(originalModelGuids.Count == 0)
            {
                var allObjects = RhinoDoc.ActiveDoc.Objects.GetObjectList(ObjectType.AnyObject);

                List<Guid> allObjectsGuids = new List<Guid>();
                foreach(var item in allObjects)
                {
                    allObjectsGuids.Add(item.Id);
                }

                if(allObjectsGuids.Count > 1)
                {
                    RhinoApp.WriteLine("The file you provided contains more than one object. Please provide a file that has only one model");
                    return;
                }

                Guid currModel_Hollowed_ObjId = allObjectsGuids[0];
                ObjRef currModel_Hollowed_ObjRef = new ObjRef(RhinoDoc.ActiveDoc, currModel_Hollowed_ObjId);
                Brep currModel_Hollowed;

                if (currModel_Hollowed_ObjRef.Geometry().ObjectType == ObjectType.Mesh)
                {
                    Mesh currModel_Mesh = currModel_Hollowed_ObjRef.Mesh();
                    bool isit = currModel_Mesh.IsManifold();

                    //TODO: Convert Mesh into Brep; or just throw an error to user saying that only breps are allowed 
                    currModel_Hollowed = Brep.CreateFromMesh(currModel_Mesh, false);
                    myDoc.Objects.Delete(currModel_Hollowed_ObjId, true);
                }
                else
                {
                    currModel_Hollowed = currModel_Hollowed_ObjRef.Brep();
                }

                //Adjust the Model's base center to Origin position
                BoundingBox currModel_box = currModel_Hollowed.GetBoundingBox(true);
                Circle circle = new Circle(new Point3d(currModel_box.Center.X, currModel_box.Center.Y, currModel_box.Min.Z + 2), 1000);
                Brep planarSurface = Brep.CreatePlanarBreps(new[] { circle.ToNurbsCurve() }, myDoc.ModelAbsoluteTolerance)[0];
                Curve bottom = null;
                if (Intersection.BrepBrep(planarSurface, currModel_Hollowed, myDoc.ModelAbsoluteTolerance, out Curve[] intersectionCurves, out Point3d[] intersectionPoint))
                {
                    foreach (var c in intersectionCurves)
                    {
                        if (c.IsClosed)
                            bottom = c;
                    }
                    if (bottom != null)
                    {
                        Brep bottomFace = Brep.CreatePlanarBreps(new[] { bottom.ToNurbsCurve() }, myDoc.ModelAbsoluteTolerance)[0];
                        Point3d centroid = AreaMassProperties.Compute(bottomFace.Faces[0]).Centroid;
                        Transform translation = Transform.Translation(-centroid.X, -centroid.Y, -centroid.Z);
                        currModel_Hollowed.Transform(translation);
                        myDoc.Objects.Delete(currModel_Hollowed_ObjId, false);
                        currModel_Hollowed_ObjId = myDoc.Objects.Add(currModel_Hollowed);
                    }
                }
                else 
                {
                    RhinoApp.WriteLine("The model's base is not planar. This software only support models that're planar at their base");
                    return;
                }

                currModel_box = currModel_Hollowed.GetBoundingBox(true);
                circle = new Circle(new Point3d(0, 0, currModel_box.Min.Z + 0.1), 1000);
                planarSurface = Brep.CreatePlanarBreps(new[] { circle.ToNurbsCurve() }, myDoc.ModelAbsoluteTolerance)[0];
                Intersection.BrepBrep(planarSurface, currModel_Hollowed, myDoc.ModelAbsoluteTolerance, out intersectionCurves, out intersectionPoint);
                if (GetLongestCurve(intersectionCurves, out Curve curve))
                    planarSurface = Brep.CreatePlanarBreps(new[] { curve }, myDoc.ModelAbsoluteTolerance)[0];

                Brep currModel;
                Brep[] union = Brep.CreateBooleanUnion(new[] { currModel_Hollowed, planarSurface }, myDoc.ModelAbsoluteTolerance, true);
                if(union != null && union.Length == 1)
                {
                    currModel = union[0];
                }
                else
                {
                    RhinoApp.WriteLine("The model's base is not planar. This software only support models that're planar at their base");
                    return;
                }

                Guid currModel_guid = myDoc.Objects.Add(currModel);

                originalModelGuids.Add(currModel_guid);
                originalModelGuids.Add(currModel_Hollowed_ObjId);

                RhinoDoc.ActiveDoc.Objects.Hide(originalModelGuids[1], true);
            }
            
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
                itemsNames = new List<String>();
                DA.SetDataList(0, itemsNames);
            }

            

            if (items.Count > 0)
            {
                if(items.Count != itemsCount)
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

        private bool GetLongestCurve(Curve[] curves, out Curve curve)
        {
            if (curves != null && curves.Length > 0)
            {
                curve = curves[0];
                foreach (var c in curves)
                    if (curve.GetLength() < c.GetLength())
                        curve = c;
                return true;
            }
            else
            {
                curve = null; return false;
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