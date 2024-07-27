using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DynaModel_v2.SharedData;
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
        private Brep conductive_part;
        private Guid conductive_part_guid;
        private Brep spring_part;
        private Guid spring_part_guid;
        private Curve spring_part_circle;
        private static List<Brep> button_parts;

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

            string filePath = "./Button.3dm";
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

            button_parts = new List<Brep>();
            foreach (var obj in file.Objects)
            {
                if(obj.Geometry.ObjectType == ObjectType.Brep)
                    button_parts.Add(obj.Geometry as Brep);
                else if(obj.Geometry.ObjectType == ObjectType.Curve)
                    spring_part_circle = obj.Geometry as Curve;
            }
            if (button_parts[0].GetVolume() > button_parts[1].GetVolume())
            {
                conductive_part = button_parts[1].Duplicate() as Brep;
                spring_part = button_parts[0].Duplicate() as Brep;
                //conductive_part_guid = myDoc.Objects.Add(conductive_part);
                //spring_part_guid = myDoc.Objects.Add(spring_part);
            }
            else
            {
                conductive_part = button_parts[0].Duplicate() as Brep;
                spring_part = button_parts[1].Duplicate() as Brep;
                //conductive_part_guid = myDoc.Objects.Add(conductive_part);
                //spring_part_guid = myDoc.Objects.Add(spring_part);
            }
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

                    myDoc.Objects.Hide(currModelObjId, true);
                    //currModelObjId = myDoc.Objects.Add(currModel);

                    Brep currModel_hollowed = myDoc.Objects.Find(SavedItems.originalModelGuids[1]).Geometry as Brep;
                    Guid currModel_hollowed_guid = myDoc.Objects.Add(currModel_hollowed);

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
                    BoundingBox boundingBox = currModel_hollowed.GetBoundingBox(true);

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

                        Intersection.BrepSurface(currModel_hollowed, planeSurface, myDoc.ModelAbsoluteTolerance, out Curve[] intersectionCurves, out Point3d[] intersectionPoints);

                        //Create Points on the Curve
                        if (intersectionCurves != null)
                        {
                            if (intersectionCurves.Length != 0)
                            {
                                Line line = new Line(new Point3d(0, 0, 0), new Point3d(0, 0, 1));
                                Curve curve = line.ToNurbsCurve();
                                foreach (Curve c in intersectionCurves)
                                {
                                    if(c.GetLength() > curve.GetLength())
                                        curve = c;
                                }
                                if(!curve.Equals(line.ToNurbsCurve()))
                                {
                                    Double[] curveParams = curve.DivideByLength(2, true, out Point3d[] points);
                                    if (curveParams != null && curveParams.Length > 0)
                                        surfacePts.AddRange(points);
                                }
                            }
                        }
                    }

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


                        //Create a line between the base center of the model and the selected pt
                        Circle circle = new Circle(new Point3d(0, 0, currModel.GetBoundingBox(true).Min.Z + 0.1), 1000);
                        Brep planarSurface = Brep.CreatePlanarBreps(new[] { circle.ToNurbsCurve() }, myDoc.ModelAbsoluteTolerance)[0];
                        Brep[] a = planarSurface.Trim(currModel, myDoc.ModelAbsoluteTolerance);
                        Brep currModel_bottom = new Brep();
                        if (a.Length > 0)
                            currModel_bottom = a[0];
                        Point3d centroid = AreaMassProperties.Compute(currModel_bottom.Faces[0]).Centroid;

                        //Create a button on the surface of the Model
                        bool success = currModel_hollowed.ClosestPoint(tempPt, out Point3d closestPoint, out ComponentIndex ci, out double s, out double t, 0.1, out Vector3d normal);
                        if (success)
                        {
                            //Fit the button onto the model
                            Brep union = Brep.CreateBooleanUnion(new[] { conductive_part, spring_part }, myDoc.ModelAbsoluteTolerance)[0];

                            Transform rotation = Transform.Rotation(new Vector3d(0, 0, 1), normal, union.GetBoundingBox(true).Center);
                            conductive_part.Transform(rotation);
                            spring_part.Transform(rotation);
                            spring_part_circle.Transform(rotation);
                            union.Transform(rotation);

                            Vector3d translationVector = tempPt - union.GetBoundingBox(true).Center;
                            Transform translation = Transform.Translation(translationVector);
                            conductive_part.Transform(translation);

                            translation = Transform.Translation(translationVector);
                            spring_part.Transform(translation);
                            spring_part_circle.Transform(translation);
                            union.Transform(translation);

                            Extrusion pipe = Extrusion.Create(spring_part_circle, 5.5, true);

                            conductive_part_guid = myDoc.Objects.Add(conductive_part);
                            spring_part_guid = myDoc.Objects.Add(spring_part);
                            Brep[] differences = Brep.CreateBooleanDifference(currModel_hollowed, pipe.ToBrep(), myDoc.ModelAbsoluteTolerance);

                            //Show button on the model if possible
                            if (differences != null && differences.Length > 0)
                            {
                                currModel_hollowed = differences[0];
                                myDoc.Objects.Delete(currModel_hollowed_guid, true);
                                currModel_hollowed_guid = myDoc.Objects.Add(currModel_hollowed);
                            }
                            else
                            {
                                RhinoApp.WriteLine("Fail to create a button on the selected area");
                                var allObjects = new List<RhinoObject>(RhinoDoc.ActiveDoc.Objects.GetObjectList(ObjectType.AnyObject));
                                foreach (var singleObject in allObjects)
                                    if (SavedItems.originalModelGuids.All(guid => guid != singleObject.Id))
                                        RhinoDoc.ActiveDoc.Objects.Delete(singleObject.Id, true);
                                foreach (var singleObject in SavedItems.originalModelGuids)
                                    RhinoDoc.ActiveDoc.Objects.Show(singleObject, true);
                                return;
                            }

                            //Create a straight route from the base center to the selected point
                            Curve route = new Line(centroid, tempPt).ToNurbsCurve();
                            route = route.Trim(CurveEnd.Both, route.GetLength() / 12);
                            Brep conductive_pipe = Brep.CreatePipe(route, 3, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];

                            Curve conductive_source_circle = new Circle(new Plane(centroid, new Vector3d(0, 0, 1)), 3).ToNurbsCurve();
                            Curve pipe_start_circle = new Circle(new Plane(route.PointAtStart, route.TangentAtStart), 3).ToNurbsCurve();

                            if (!Curve.DoDirectionsMatch(conductive_source_circle, pipe_start_circle))
                                pipe_start_circle.Reverse();
                            Point3d start = conductive_source_circle.PointAtStart;
                            pipe_start_circle.ClosestPoint(start, out t);
                            pipe_start_circle.ChangeClosedCurveSeam(t);
                            Curve[] crossSectionCurves = new Curve[] { conductive_source_circle, pipe_start_circle };
                            Brep[] loftBreps = Brep.CreateFromLoft(crossSectionCurves, Point3d.Unset, Point3d.Unset, LoftType.Normal, false);
                            Brep source_extension = loftBreps[0];

                            Curve pipe_end_circle = new Circle(new Plane(route.PointAtEnd, route.TangentAtEnd), 3).ToNurbsCurve();
                            if (!Curve.DoDirectionsMatch(spring_part_circle, pipe_end_circle))
                                pipe_start_circle.Reverse();
                            start = spring_part_circle.PointAtStart;
                            pipe_end_circle.ClosestPoint(start, out t);
                            pipe_end_circle.ChangeClosedCurveSeam(t);
                            crossSectionCurves = new Curve[] { spring_part_circle, pipe_end_circle };
                            loftBreps = Brep.CreateFromLoft(crossSectionCurves, Point3d.Unset, Point3d.Unset, LoftType.Normal, false);
                            Brep spring_extension = loftBreps[0];

                            source_extension = source_extension.CapPlanarHoles(myDoc.ModelAbsoluteTolerance);
                            spring_extension = spring_extension.CapPlanarHoles(myDoc.ModelAbsoluteTolerance);
                            myDoc.Objects.Add(conductive_pipe);
                            myDoc.Objects.Add(source_extension);
                            myDoc.Objects.Add(spring_extension);

                            Item savedItem = new Item();
                            savedItem.Name = "Button";
                            savedItem.EndPoint = tempPt;
                            savedItem.ButtonSet = new List<Brep>();
                            savedItem.ButtonSet.Add(conductive_part);
                            savedItem.ButtonSet.Add(spring_part);
                            savedItem.Button_Spring_Circle = spring_part_circle;

                            DA.SetData(0, savedItem);
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