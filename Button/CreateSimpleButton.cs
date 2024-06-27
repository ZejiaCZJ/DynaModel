using System;
using System.Collections.Generic;
using System.IO;
using Grasshopper.Kernel;
using Rhino;
using Rhino.Collections;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.FileIO;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using Rhino.Input;

namespace DynaModel_v2.Button
{
    public class CreateSimpleButton : GH_Component
    {
        private Brep currModel;
        private Guid currModelObjId;
        private RhinoDoc myDoc;
        private List<Point3d> surfacePts;


        /// <summary>
        /// Initializes a new instance of the CreateSimpleButton class.
        /// </summary>
        public CreateSimpleButton()
          : base("CreateSimpleButton", "CreateSimpleButton",
              "This component makes a button on the model",
              "DynaModel_v2", "Button")
        {
            currModel = new Brep();
            currModelObjId = Guid.Empty;
            myDoc = RhinoDoc.ActiveDoc;
            surfacePts = new List<Point3d>();
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddBooleanParameter("Start Button", "SB", "The button if user want to create a parameter", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Item", "I", "The Item to be saved", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            bool startButtonClicked = false;
            if (!DA.GetData(0, ref startButtonClicked))
                return;

            if (startButtonClicked)
            {
                var rc = RhinoGet.GetOneObject("Select a model (geometry): ", false, ObjectType.AnyObject, out ObjRef currObjRef);
                if (rc == Rhino.Commands.Result.Success)
                {
                    currModelObjId = currObjRef.ObjectId;
                    currModel = currObjRef.Brep();

                    #region Convert the current object to Brep if needed
                    if (currObjRef.Geometry().ObjectType == ObjectType.Mesh)
                    {
                        Mesh currModel_Mesh = currObjRef.Mesh();

                        //TODO: Convert Mesh into Brep; or just throw an error to user saying that only breps are allowed 
                        currModel = Brep.CreateFromMesh(currModel_Mesh, false);
                        if (currModel.IsValid && currModel.IsSolid && !currModel.IsManifold)
                        {
                            currModelObjId = myDoc.Objects.AddBrep(currModel);
                            myDoc.Objects.Delete(currObjRef.ObjectId, true);

                            myDoc.Views.Redraw();
                        }
                        else
                        {
                            RhinoApp.WriteLine("Your model cannot be fixed to become manifold and closed, please try to fix it manually");
                            return;
                        }
                    }

                    if (currModel == null)
                    {
                        RhinoApp.WriteLine("Your model cannot be fixed to become manifold and closed, please try to fix it manually");
                        return;
                    }
                    #endregion

                    #region Display points for user to choose
                    BoundingBox boundingBox = currModel.GetBoundingBox(true);

                    double w = boundingBox.Max.X - boundingBox.Min.X;
                    double l = boundingBox.Max.Y - boundingBox.Min.Y;
                    double h = boundingBox.Max.Z - boundingBox.Min.Z;
                    double offset = 5;

                    // Create a x-y plane to intersect with the current model from top to bottom
                    for (int i = 0; i < h + 10; i += 1)
                    {
                        Point3d Origin = new Point3d(w / 2, l / 2, i);
                        Point3d xPoint = new Point3d(boundingBox.Max.X + offset, l / 2, i);
                        Point3d yPoint = new Point3d(w / 2, boundingBox.Max.Y + offset, i);

                        Plane plane = new Plane(Origin, xPoint, yPoint);
                        PlaneSurface planeSurface = PlaneSurface.CreateThroughBox(plane, boundingBox);

                        Intersection.BrepSurface(currModel, planeSurface, myDoc.ModelAbsoluteTolerance, out Curve[] intersectionCurves, out Point3d[] intersectionPoints);

                        //Create Points on the Curve
                        if (intersectionCurves != null)
                        {
                            if (intersectionCurves.Length != 0)
                            {
                                foreach (Curve curve in intersectionCurves)
                                {
                                    Double[] curveParams = curve.DivideByLength(2, true, out Point3d[] points);
                                    if (curveParams != null && curveParams.Length > 0)
                                        surfacePts.AddRange(points);
                                }
                            }
                        }
                    }

                    myDoc.Objects.Hide(currModelObjId, true);
                    currModelObjId = myDoc.Objects.Add(currModel);

                    // Put dots on the view
                    List<Guid> pts_Guid = new List<Guid>();
                    foreach (Point3d point in surfacePts)
                    {
                        Guid pointID = myDoc.Objects.AddPoint(point);
                        pts_Guid.Add(pointID);
                    }
                    myDoc.Views.Redraw();
                    #endregion

                    #region Ask the user to select point to generate the area of the parameter
                    var getSelectedPts = RhinoGet.GetOneObject("Please select points for a pipe exit, press ENTER when finished", false, ObjectType.Point, out ObjRef pointRef);
                    #endregion

                    if (getSelectedPts == Rhino.Commands.Result.Success)
                    {
                        Point3d tempPt = new Point3d(pointRef.Point().Location);
                        double x = tempPt.X;
                        double y = tempPt.Y;
                        double z = tempPt.Z;

                        //Delete all points on the view
                        foreach (var ptsID in pts_Guid)
                        {
                            myDoc.Objects.Delete(ptsID, true);
                        }

                        string filePath = "./Orthoplanar_Spring.3dm";
                        string absolutePath = Path.GetFullPath(filePath);

                        if (!File.Exists(absolutePath))
                        {
                            RhinoApp.WriteLine("The file does not exist: " + absolutePath);
                            return;
                        }

                        File3dm file = File3dm.Read(absolutePath);
                        if (file == null)
                        {
                            RhinoApp.WriteLine("No current support for button feature");
                            return;
                        }
                        foreach (var obj in file.Objects)
                        {
                            myDoc.Objects.Add(obj.Geometry, obj.Attributes);
                        }
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
            get { return new Guid("FCB15846-300A-4639-93C8-92B3F63242C0"); }
        }
    }
}