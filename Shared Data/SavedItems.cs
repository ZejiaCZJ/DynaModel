using System;
using System.Collections.Generic;
using DynaModel_v2.Geometry;
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

        //Foundation parameters
        public static double foundation_length = 64;
        public static double foundation_width = 52;
        public static double foundation_height = 18;
        public static Point3d foundation_origin;
        public static Point3d foundation_center;
        public static Brep foundation { get; set; }
        public static Guid foundation_guid {  get; set; }
        public static double pcb_width = 50;
        public static double pcb_length = 50;
        public static double pcb_origin_x = 1; //Relative coordinate to foundation
        public static double pcb_origin_y = 1 + 13;//Relative coordinate to foundation
        public static Point3d pcb_origin;
        public static Point3d pcb_center;
        private static double start_gear_elevation = 14;

        //Initial gear parameters
        public static double module = 1.5;
        public static double pressure_angle = 20;
        public static double thickness = 5;

        public static SpurGear start_gear_horizontal { get; set; }
        public static SpurGear start_gear_vertical { get; set; }

        public static Guid start_gear_horizontal_guid { get; set; }

        public static Guid start_gear_vertical_guid { get; set; }

        public static (SpurGear, Guid) start_gear_horizontal_pair { get; set; }

        public static (SpurGear, Guid) start_gear_vertical_pair { get; set; }

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
                Point3d centroid = Point3d.Unset;
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
                        centroid = AreaMassProperties.Compute(bottomFace.Faces[0]).Centroid;
                        Transform translation = Transform.Translation(-centroid.X, -centroid.Y, -centroid.Z);
                        currModel_Hollowed.Transform(translation);
                        myDoc.Objects.Delete(currModel_Hollowed_ObjId, false);
                        currModel_Hollowed_ObjId = myDoc.Objects.Add(currModel_Hollowed);
                    }
                }
                else 
                {
                    myDoc.Objects.Add(planarSurface);
                    foreach (var curve1 in intersectionCurves)
                        myDoc.Objects.Add(curve1);
                    myDoc.Objects.Add(currModel_Hollowed);
                    RhinoApp.WriteLine("The model's base is not planar. This software only support models that're planar at their base");
                    return;
                }

                currModel_box = currModel_Hollowed.GetBoundingBox(true);
                circle = new Circle(new Point3d(0, 0, currModel_box.Min.Z + 1), 1000);
                planarSurface = Brep.CreatePlanarBreps(new[] { circle.ToNurbsCurve() }, myDoc.ModelAbsoluteTolerance)[0];
                //myDoc.Objects.Add(planarSurface);
                Intersection.BrepBrep(planarSurface, currModel_Hollowed, myDoc.ModelAbsoluteTolerance, out intersectionCurves, out intersectionPoint);
                
                if (GetLongestCurve(intersectionCurves, out Curve curve))
                {
                    if(curve.IsClosed)
                        planarSurface = Brep.CreatePlanarBreps(new[] { curve }, myDoc.ModelAbsoluteTolerance)[0];
                    else
                    {
                        myDoc.Objects.Add(planarSurface);
                        foreach (var curve1 in intersectionCurves)
                            myDoc.Objects.Add(curve1);
                        myDoc.Objects.Add(currModel_Hollowed);
                    }
                }
                    

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

                //Generate Foundation and PCB if possible
                currModel_box = currModel.GetBoundingBox(true);

                circle = new Circle(new Point3d(0, 0, currModel_box.Min.Z + 0.1), 1000);
                planarSurface = Brep.CreatePlanarBreps(new[] { circle.ToNurbsCurve() }, myDoc.ModelAbsoluteTolerance)[0];
                Brep[] a = planarSurface.Trim(currModel, myDoc.ModelAbsoluteTolerance);
                Brep currModel_bottom = new Brep();
                if (a.Length > 0)
                    currModel_bottom = a[0];
                centroid = AreaMassProperties.Compute(currModel_bottom.Faces[0]).Centroid;

                foundation_center = new Point3d(centroid.X, centroid.Y, currModel_box.Min.Z + foundation_height / 2);
                foundation_origin = new Point3d(centroid.X - foundation_width / 2, centroid.Y - foundation_length / 2, currModel_box.Min.Z);
                foundation = new BoundingBox(foundation_origin, new Point3d(foundation_center.X + foundation_width / 2, foundation_center.Y + foundation_length / 2, currModel_box.Min.Z + foundation_height)).ToBrep();
                pcb_origin = new Point3d(foundation_origin.X + pcb_origin_x, foundation_origin.Y + pcb_origin_y, currModel_box.Min.Z + foundation_height);
                pcb_center = new Point3d(pcb_origin.X + pcb_width, pcb_origin.Y + pcb_length, pcb_origin.Z);

                foundation_guid = myDoc.Objects.Add(foundation);
                myDoc.Objects.Hide(foundation_guid, true);

                Point3d start_gear_vertical_centerPoint = new Point3d(foundation_origin.X + 6, foundation_origin.Y + 67.5, foundation_origin.Z + 7);
                Vector3d start_gear_vertical_Direction = new Vector3d(0, 1, 0);
                Vector3d start_gear_vertical_xDir = new Vector3d(0, 0, 0);
                int start_gear_vertical_teethNum = 20;
                double start_gear_vertical_selfRotAngle = 0;

                start_gear_vertical = new SpurGear(start_gear_vertical_centerPoint, start_gear_vertical_Direction, start_gear_vertical_xDir, start_gear_vertical_teethNum, module, pressure_angle, thickness, start_gear_vertical_selfRotAngle, true);

                //Find central point of the base of the bounding box
                Point3d start_gear_horizontal_centerPoint = new Point3d(pcb_origin.X + pcb_width / 2, foundation_origin.Y + 6.5, foundation_origin.Z + foundation_height + start_gear_elevation);
                Vector3d start_gear_horizontal_Direction = new Vector3d(0, 0, 1);
                Vector3d start_gear_horizontal_xDir = new Vector3d(0, 0, 0);
                int start_gear_horizontal_teethNum = 20;
                double start_gear_horizontal_selfRotAngle = 0;

                start_gear_horizontal = new SpurGear(start_gear_horizontal_centerPoint, start_gear_horizontal_Direction, start_gear_horizontal_xDir, start_gear_horizontal_teethNum, module, pressure_angle, thickness, start_gear_horizontal_selfRotAngle, true);

                start_gear_horizontal_guid = myDoc.Objects.Add(start_gear_horizontal.Model);

                start_gear_vertical_guid = myDoc.Objects.Add(start_gear_vertical.Model);

                myDoc.Objects.Hide(start_gear_horizontal_guid, true);

                myDoc.Objects.Hide(start_gear_vertical_guid, true);

                start_gear_horizontal_pair = (start_gear_horizontal, start_gear_horizontal_guid);

                start_gear_vertical_pair = (start_gear_vertical, start_gear_vertical_guid);
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
                if(items.Count > itemsCount)
                {
                    for (int i = itemsCount; i < items.Count; i++)
                    {
                        itemsNames.Add(items[i].Name);
                    }
                    itemsCount = items.Count;
                    DA.SetDataList(0, itemsNames);
                }
                else if (items.Count < itemsCount)
                {
                    itemsNames.Clear();
                    for (int i = 0; i < items.Count; i++)
                    {
                        itemsNames.Add(items[i].Name);
                    }
                    itemsCount = items.Count;
                    DA.SetDataList(0, itemsNames);
                }
                //TODO: Sort both the item and itemNames lists, based on the item type


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