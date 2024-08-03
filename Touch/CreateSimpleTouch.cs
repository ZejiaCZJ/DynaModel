using System;
using System.Collections.Generic;
using DynaModel_v2.SharedData;
using Grasshopper.Kernel;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using Rhino.Input;

namespace DynaModel_v2.Touch
{
    public class CreateSimpleTouch : GH_Component
    {
        private Brep currModel;
        private Guid currModelObjId;
        private RhinoDoc myDoc;
        private List<Point3d> surfacePts;



        /// <summary>
        /// Initializes a new instance of the CreateSimpleTouch class.
        /// </summary>
        public CreateSimpleTouch()
          : base("CreateSimpleTouch", "CreateSimpleTouch",
              "This component creates a simple touch pipe",
              "DynaModel_v2", "Touch")
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

                    myDoc.Objects.Hide(currModelObjId, true);

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
                                    if (c.GetLength() > curve.GetLength())
                                        curve = c;
                                }
                                if (!curve.Equals(line.ToNurbsCurve()))
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
                        Item savedItem = new Item();
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

                        
                        Sphere customized_part_sphere = new Sphere(tempPt, 5);
                        Brep customized_part = customized_part_sphere.ToBrep();
                        Intersection.BrepBrep(currModel, customized_part, myDoc.ModelAbsoluteTolerance, out Curve[] intersectionCurves, out Point3d[] intersectionPts);
                        Curve customized_part_curve = intersectionCurves[0];

                        //Create a straight route from the base center to the selected point
                        Curve route = new Line(centroid, tempPt).ToNurbsCurve();
                        route = route.Trim(CurveEnd.Both, route.GetLength() / 12);
                        Brep conductive_pipe = Brep.CreatePipe(route, 3, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];

                        Curve conductive_source_circle = new Circle(new Plane(centroid, new Vector3d(0, 0, 1)), 3).ToNurbsCurve();
                        Curve pipe_start_circle = new Circle(new Plane(route.PointAtStart, route.TangentAtStart), 3).ToNurbsCurve();

                        if (!Curve.DoDirectionsMatch(conductive_source_circle, pipe_start_circle))
                            pipe_start_circle.Reverse();
                        Point3d start = conductive_source_circle.PointAtStart;
                        pipe_start_circle.ClosestPoint(start, out double t);
                        pipe_start_circle.ChangeClosedCurveSeam(t);
                        Curve[] crossSectionCurves = new Curve[] { conductive_source_circle, pipe_start_circle };
                        Brep[] loftBreps = Brep.CreateFromLoft(crossSectionCurves, Point3d.Unset, Point3d.Unset, LoftType.Normal, false);
                        Brep source_extension = loftBreps[0];
                        source_extension = source_extension.CapPlanarHoles(myDoc.ModelAbsoluteTolerance);

                        Curve pipe_end_circle = new Circle(new Plane(route.PointAtEnd, route.TangentAtEnd), 3).ToNurbsCurve();
                        if(customized_part_curve.IsClosed && pipe_end_circle.IsClosed)
                        {
                            if (!Curve.DoDirectionsMatch(customized_part_curve, pipe_end_circle))
                                pipe_end_circle.Reverse();
                            start = customized_part_curve.PointAtStart;
                            pipe_end_circle.ClosestPoint(start, out t);
                            pipe_end_circle.ChangeClosedCurveSeam(t);
                            crossSectionCurves = new Curve[] { customized_part_curve, pipe_end_circle };
                            loftBreps = Brep.CreateFromLoft(crossSectionCurves, Point3d.Unset, Point3d.Unset, LoftType.Normal, false);
                            customized_part = loftBreps[0];
                            customized_part = customized_part.CapPlanarHoles(myDoc.ModelAbsoluteTolerance);

                            Brep[] faces = currModel.Split(new[] { customized_part_curve }, myDoc.ModelAbsoluteTolerance);
                            Brep patch = new Brep();
                            if (faces.Length > 0)
                            {
                                patch = faces[0];
                                foreach (var face in faces)
                                {
                                    if (face.Faces.Count < patch.Faces.Count)
                                        patch = face;
                                }
                            }
                            myDoc.Objects.Add(customized_part);
                            myDoc.Objects.Add(patch);
                            customized_part = Brep.MergeBreps(new[] { customized_part, patch }, myDoc.ModelAbsoluteTolerance);
                            savedItem.customized_part_patch = new List<Brep> { patch };
                        }

                        myDoc.Objects.Add(conductive_pipe);
                        myDoc.Objects.Add(source_extension);
                        myDoc.Objects.Add(customized_part);

                        savedItem.EndPoint = tempPt;
                        savedItem.Name = "Touch";
                        savedItem.EndPointModel = new List<Brep>{ customized_part_sphere.ToBrep()};
                        DA.SetData(0, savedItem);
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
            get { return new Guid("45DA2EF8-0DF3-4CA2-BFB8-A27745CA75B5"); }
        }
    }
}