using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Rhino.DocObjects;
using Rhino;
using Rhino.Geometry;
using DynaModel_v2.Light_Pipe;
using System.Drawing;
using Rhino.Collections;
using Rhino.Geometry.Intersect;
using Rhino.Input;
using Priority_Queue;
using System.Threading.Tasks;
using System.Linq;
using DynaModel_v2.SharedData;

namespace DynaModel_v2.AirPipe
{
    public class CreateSimplePipe : GH_Component
    {
        private Voxel[,,] voxelSpace;
        double pcbWidth;
        double pcbHeight;
        double pipeRadius;
        private static List<PipeExit> pipeExitPts;
        int voxelSpace_offset;
        private static List<Brep> allPipes;
        private List<Guid> allTempBoxesGuid;
        private static List<Curve> combinableLightPipeRoute;
        private static List<Brep> combinableLightPipe;
        private static List<InViewObject> conductiveObjects;

        private Brep currModel;
        private List<Point3d> surfacePts;
        private List<Point3d> selectedPts;
        private Guid currModelObjId;
        private RhinoDoc myDoc;
        private bool endButtonClicked;
        ObjectAttributes solidAttribute, lightGuideAttribute, redAttribute, yellowAttribute, soluableAttribute;


        /// <summary>
        /// Initializes a new instance of the CreateSimplePipe class.
        /// </summary>
        public CreateSimplePipe()
          : base("CreateSimplePipe", "CreateSimplePipe",
              "This create a simple air pipe",
              "DynaModel_v2", "Air Pipe")
        {
            myDoc = RhinoDoc.ActiveDoc;
            voxelSpace = null;
            pipeExitPts = new List<PipeExit>();
            allPipes = new List<Brep>();
            combinableLightPipe = new List<Brep>();
            combinableLightPipeRoute = new List<Curve>();
            conductiveObjects = new List<InViewObject>();

            allTempBoxesGuid = new List<Guid>();
            currModelObjId = Guid.Empty;
            selectedPts = new List<Point3d>();
            surfacePts = new List<Point3d>();

            pcbWidth = 20;
            pcbHeight = 20;
            pipeRadius = 3.5;
            voxelSpace_offset = 1;

            int solidIndex = myDoc.Materials.Add();
            Rhino.DocObjects.Material solidMat = myDoc.Materials[solidIndex];
            solidMat.DiffuseColor = System.Drawing.Color.White;
            solidMat.SpecularColor = System.Drawing.Color.White;
            solidMat.Transparency = 0;
            solidMat.CommitChanges();
            solidAttribute = new ObjectAttributes();
            //solidAttribute.LayerIndex = 2;
            solidAttribute.MaterialIndex = solidIndex;
            solidAttribute.MaterialSource = Rhino.DocObjects.ObjectMaterialSource.MaterialFromObject;
            solidAttribute.ObjectColor = Color.White;
            solidAttribute.ColorSource = ObjectColorSource.ColorFromObject;

            int lightGuideIndex = myDoc.Materials.Add();
            Rhino.DocObjects.Material lightGuideMat = myDoc.Materials[lightGuideIndex];
            lightGuideMat.DiffuseColor = System.Drawing.Color.Orange;
            lightGuideMat.Transparency = 0.3;
            lightGuideMat.SpecularColor = System.Drawing.Color.Orange;
            lightGuideMat.CommitChanges();
            lightGuideAttribute = new ObjectAttributes();
            //orangeAttribute.LayerIndex = 3;
            lightGuideAttribute.MaterialIndex = lightGuideIndex;
            lightGuideAttribute.MaterialSource = Rhino.DocObjects.ObjectMaterialSource.MaterialFromObject;
            lightGuideAttribute.ObjectColor = Color.Orange;
            lightGuideAttribute.ColorSource = ObjectColorSource.ColorFromObject;

            int redIndex = myDoc.Materials.Add();
            Rhino.DocObjects.Material redMat = myDoc.Materials[redIndex];
            redMat.DiffuseColor = System.Drawing.Color.Red;
            redMat.Transparency = 0.3;
            redMat.SpecularColor = System.Drawing.Color.Red;
            redMat.CommitChanges();
            redAttribute = new ObjectAttributes();
            //redAttribute.LayerIndex = 4;
            redAttribute.MaterialIndex = redIndex;
            redAttribute.MaterialSource = Rhino.DocObjects.ObjectMaterialSource.MaterialFromObject;
            redAttribute.ObjectColor = Color.Red;
            redAttribute.ColorSource = ObjectColorSource.ColorFromObject;

            int yellowIndex = myDoc.Materials.Add();
            Rhino.DocObjects.Material yellowMat = myDoc.Materials[yellowIndex];
            yellowMat.DiffuseColor = System.Drawing.Color.Yellow;
            yellowMat.Transparency = 0.3;
            yellowMat.SpecularColor = System.Drawing.Color.Yellow;
            yellowMat.CommitChanges();
            yellowAttribute = new ObjectAttributes();
            //yellowAttribute.LayerIndex = 4;
            yellowAttribute.MaterialIndex = yellowIndex;
            yellowAttribute.MaterialSource = Rhino.DocObjects.ObjectMaterialSource.MaterialFromObject;
            yellowAttribute.ObjectColor = Color.Yellow;
            yellowAttribute.ColorSource = ObjectColorSource.ColorFromObject;

            int soluableIndex = myDoc.Materials.Add();
            Rhino.DocObjects.Material soluableMat = myDoc.Materials[soluableIndex];
            soluableMat.DiffuseColor = System.Drawing.Color.Green;
            soluableMat.Transparency = 0.3;
            soluableMat.SpecularColor = System.Drawing.Color.Green;
            soluableMat.CommitChanges();
            soluableAttribute = new ObjectAttributes();
            //yellowAttribute.LayerIndex = 4;
            soluableAttribute.MaterialIndex = soluableIndex;
            soluableAttribute.MaterialSource = Rhino.DocObjects.ObjectMaterialSource.MaterialFromObject;
            soluableAttribute.ObjectColor = Color.Green;
            soluableAttribute.ColorSource = ObjectColorSource.ColorFromObject;
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddBooleanParameter("Start Button", "SB", "The button if user want to create a parameter", GH_ParamAccess.item);
            pManager.AddGenericParameter("Input Type", "Input", "The input of the parameter", GH_ParamAccess.item);
            pManager.AddGenericParameter("Output Type", "Output", "The output of the parameter", GH_ParamAccess.item);
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
            string inputType = "NA";
            string outputType = "NA";
            endButtonClicked = false;

            if (!DA.GetData(0, ref startButtonClicked))
                return;
            if (!DA.GetData(1, ref inputType))
                return;
            if (!DA.GetData(2, ref outputType))
                return;

            if (startButtonClicked && outputType.Equals("Air Pipe"))
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
                    var getSelectedPts = RhinoGet.GetOneObject("Please select a point for a air pipe, press ENTER when finished", false, ObjectType.Point, out ObjRef pointRef);
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
                        myDoc.Objects.Delete(currModel_hollowed_guid, true);

                        //Create a line between the base center of the model and the selected pt
                        Circle circle = new Circle(new Point3d(0, 0, currModel.GetBoundingBox(true).Min.Z + 0.1), 1000);
                        Brep planarSurface = Brep.CreatePlanarBreps(new[] { circle.ToNurbsCurve() }, myDoc.ModelAbsoluteTolerance)[0];
                        Brep[] a = planarSurface.Trim(currModel, myDoc.ModelAbsoluteTolerance);
                        Brep currModel_bottom = new Brep();
                        if (a.Length > 0)
                            currModel_bottom = a[0];
                        Point3d centroid = AreaMassProperties.Compute(currModel_bottom.Faces[0]).Centroid;

                        bool success = currModel_hollowed.ClosestPoint(tempPt, out Point3d closestPoint, out ComponentIndex ci, out double s, out double t, 0.1, out Vector3d normal);
                        if(success)
                        {
                            Brep customized_part = new Cylinder(new Circle(new Point3d(0, 0, 0), 3.2), 7).ToBrep(true, true);
                            Curve customized_part_outer_circle = new Circle(new Point3d(0, 0, 0), 3.2).ToNurbsCurve();
                            Curve customized_part_inner_circle = new Circle(new Point3d(0, 0, 0), 3).ToNurbsCurve();
                            Transform rotation = Transform.Rotation(new Vector3d(0, 0, 1), normal, customized_part.GetBoundingBox(true).Center);
                            customized_part.Transform(rotation);
                            customized_part_outer_circle.Transform(rotation);
                            customized_part_inner_circle.Transform(rotation);

                            Vector3d translationVector = tempPt - customized_part.GetBoundingBox(true).Center;
                            Transform translation = Transform.Translation(translationVector);
                            customized_part.Transform(translation);
                            customized_part_outer_circle.Transform(translation);
                            customized_part_inner_circle.Transform(translation);

                            //Create a straight route from the base center to the selected point
                            Point3d customized_part_center = tempPt;
                            if (voxelSpace == null)
                                GetVoxelSpace(currModel, 1);

                            Point3d pipeExit = centroid;
                            List<Point3d> bestRoute1 = FindShortestPath(customized_part_center, pipeExit, currModel, 1);
                            Curve route = Curve.CreateInterpolatedCurve(bestRoute1, 1);
                            route = route.Trim(CurveEnd.Both, route.GetLength() / 10);

                            Brep main_air_pipe = Brep.CreateThickPipe(route, 3, 3.2, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];

                            #region source extension
                            Curve source_outer_circle = new Circle(new Plane(centroid, new Vector3d(0, 0, 1)), 3.2).ToNurbsCurve();
                            Curve pipe_start_outer_circle = new Circle(new Plane(route.PointAtStart, route.TangentAtStart), 3.2).ToNurbsCurve();

                            if (!Curve.DoDirectionsMatch(source_outer_circle, pipe_start_outer_circle))
                                pipe_start_outer_circle.Reverse();
                            Point3d start = source_outer_circle.PointAtStart;
                            pipe_start_outer_circle.ClosestPoint(start, out t);
                            pipe_start_outer_circle.ChangeClosedCurveSeam(t);
                            Curve[] crossSectionCurves = new Curve[] { pipe_start_outer_circle, source_outer_circle };
                            Brep[] loftBreps = Brep.CreateFromLoft(crossSectionCurves, Point3d.Unset, Point3d.Unset, LoftType.Normal, false);
                            Brep source_outer_extension = new Brep();
                            if (loftBreps != null && loftBreps.Length > 0)
                                source_outer_extension = loftBreps[0];

                            Curve source_inner_circle = new Circle(new Plane(centroid, new Vector3d(0, 0, 1)), 3).ToNurbsCurve();
                            Curve pipe_start_inner_circle = new Circle(new Plane(route.PointAtStart, route.TangentAtStart), 3).ToNurbsCurve();

                            if (!Curve.DoDirectionsMatch(source_inner_circle, pipe_start_inner_circle))
                                pipe_start_inner_circle.Reverse();
                            start = source_inner_circle.PointAtStart;
                            pipe_start_inner_circle.ClosestPoint(start, out t);
                            pipe_start_inner_circle.ChangeClosedCurveSeam(t);
                            crossSectionCurves = new Curve[] { pipe_start_inner_circle, source_inner_circle };
                            loftBreps = Brep.CreateFromLoft(crossSectionCurves, Point3d.Unset, Point3d.Unset, LoftType.Normal, false);
                            Brep source_inner_extension = new Brep();
                            if (loftBreps != null && loftBreps.Length > 0)
                                source_inner_extension = loftBreps[0];

                            if (!Curve.DoDirectionsMatch(source_inner_circle, source_outer_circle))
                                source_outer_circle.Reverse();
                            start = source_inner_circle.PointAtStart;
                            source_outer_circle.ClosestPoint(start, out t);
                            source_outer_circle.ChangeClosedCurveSeam(t);
                            crossSectionCurves = new Curve[] { source_outer_circle, source_inner_circle };
                            loftBreps = Brep.CreateFromLoft(crossSectionCurves, Point3d.Unset, Point3d.Unset, LoftType.Normal, false);
                            Brep source_cap = new Brep();
                            if (loftBreps != null && loftBreps.Length > 0)
                                source_cap = loftBreps[0];

                            if (!Curve.DoDirectionsMatch(pipe_start_outer_circle, pipe_start_inner_circle))
                                pipe_start_inner_circle.Reverse();
                            start = pipe_start_outer_circle.PointAtStart;
                            pipe_start_inner_circle.ClosestPoint(start, out t);
                            pipe_start_inner_circle.ChangeClosedCurveSeam(t);
                            crossSectionCurves = new Curve[] { pipe_start_inner_circle, pipe_start_outer_circle };
                            loftBreps = Brep.CreateFromLoft(crossSectionCurves, Point3d.Unset, Point3d.Unset, LoftType.Normal, false);
                            Brep pipe_start_cap = new Brep();
                            if (loftBreps != null && loftBreps.Length > 0)
                                pipe_start_cap = loftBreps[0];

                            Brep source_extension = new Brep();
                            Brep[] solid = Brep.CreateSolid(new[] { pipe_start_cap, source_inner_extension, source_outer_extension, source_cap }, myDoc.ModelAbsoluteTolerance);
                            if(solid != null && solid.Length > 0)
                                source_extension= solid[0];
                            else
                            {
                                Brep[] breps = new Brep[] { pipe_start_cap, source_inner_extension, source_outer_extension, source_cap };
                                foreach(var item in breps)
                                    myDoc.Objects.Add(item);
                                Curve[] curves = new Curve[] { source_inner_circle, source_outer_circle, pipe_start_inner_circle, pipe_start_outer_circle };
                                foreach(var curve in curves)
                                    myDoc.Objects.AddCurve(curve);
                            }
                            #endregion

                            #region customized_part_extension
                            Curve pipe_end_outer_circle = new Circle(new Plane(route.PointAtEnd, route.TangentAtEnd), 3.2).ToNurbsCurve();

                            if (!Curve.DoDirectionsMatch(customized_part_outer_circle, pipe_end_outer_circle))
                                pipe_end_outer_circle.Reverse();
                            start = customized_part_outer_circle.PointAtStart;
                            pipe_end_outer_circle.ClosestPoint(start, out t);
                            pipe_end_outer_circle.ChangeClosedCurveSeam(t);
                            crossSectionCurves = new Curve[] { customized_part_outer_circle, pipe_end_outer_circle };
                            loftBreps = Brep.CreateFromLoft(crossSectionCurves, Point3d.Unset, Point3d.Unset, LoftType.Normal, false);
                            Brep customized_part_outer_extension = new Brep();
                            if (loftBreps != null && loftBreps.Length > 0)
                                customized_part_outer_extension = loftBreps[0];

                            Curve pipe_end_inner_circle = new Circle(new Plane(route.PointAtEnd, route.TangentAtEnd), 3).ToNurbsCurve();

                            if (!Curve.DoDirectionsMatch(customized_part_inner_circle, pipe_end_inner_circle))
                                pipe_end_inner_circle.Reverse();
                            start = customized_part_inner_circle.PointAtStart;
                            pipe_end_inner_circle.ClosestPoint(start, out t);
                            pipe_end_inner_circle.ChangeClosedCurveSeam(t);
                            crossSectionCurves = new Curve[] { pipe_end_inner_circle, customized_part_inner_circle };
                            loftBreps = Brep.CreateFromLoft(crossSectionCurves, Point3d.Unset, Point3d.Unset, LoftType.Normal, false);
                            Brep customized_part_inner_extension = new Brep();
                            if (loftBreps != null && loftBreps.Length > 0)
                                customized_part_inner_extension = loftBreps[0];

                            if (!Curve.DoDirectionsMatch(customized_part_inner_circle, customized_part_outer_circle))
                                customized_part_outer_circle.Reverse();
                            start = customized_part_inner_circle.PointAtStart;
                            customized_part_outer_circle.ClosestPoint(start, out t);
                            customized_part_outer_circle.ChangeClosedCurveSeam(t);
                            crossSectionCurves = new Curve[] { customized_part_outer_circle, customized_part_inner_circle };
                            loftBreps = Brep.CreateFromLoft(crossSectionCurves, Point3d.Unset, Point3d.Unset, LoftType.Normal, false);
                            Brep customized_part_cap = new Brep();
                            if (loftBreps != null && loftBreps.Length > 0)
                                customized_part_cap = loftBreps[0];

                            if (!Curve.DoDirectionsMatch(pipe_end_inner_circle, pipe_end_outer_circle))
                                pipe_end_outer_circle.Reverse();
                            start = pipe_end_inner_circle.PointAtStart;
                            pipe_end_outer_circle.ClosestPoint(start, out t);
                            pipe_end_outer_circle.ChangeClosedCurveSeam(t);
                            crossSectionCurves = new Curve[] { pipe_end_outer_circle, pipe_end_inner_circle };
                            loftBreps = Brep.CreateFromLoft(crossSectionCurves, Point3d.Unset, Point3d.Unset, LoftType.Normal, false);
                            Brep pipe_end_cap = new Brep();
                            if (loftBreps != null && loftBreps.Length > 0)
                                pipe_end_cap = loftBreps[0];

                            Brep customized_part_extension = new Brep();
                            solid = Brep.CreateSolid(new[] { pipe_end_cap, customized_part_cap, customized_part_inner_extension, customized_part_outer_extension }, myDoc.ModelAbsoluteTolerance);
                            if (solid != null && solid.Length > 0)
                            {
                                customized_part_extension = solid[0];
                            }
                                
                            else
                            {
                                Brep[] breps = new Brep[] { pipe_end_cap, customized_part_cap, customized_part_inner_extension, customized_part_outer_extension };
                                foreach (var item in breps)
                                    myDoc.Objects.Add(item);
                                Curve[] curves = new Curve[] { pipe_end_inner_circle, pipe_end_outer_circle, customized_part_inner_circle, customized_part_outer_circle };
                                foreach (var curve in curves)
                                    myDoc.Objects.AddCurve(curve);
                            }

                            myDoc.Objects.Add(customized_part_extension);
                            myDoc.Objects.Add(main_air_pipe);
                            myDoc.Objects.Add(source_extension);

                            Brep[] differences = Brep.CreateBooleanDifference(currModel_hollowed, customized_part, myDoc.ModelAbsoluteTolerance);
                            if(differences != null && differences.Length > 0)
                            {
                                GetSimilarVolumeBrep(differences, currModel_hollowed, out currModel_hollowed);
                            }
                            myDoc.Objects.Add(currModel_hollowed);

                            savedItem.EndPoint = tempPt;
                            savedItem.Name = "Air Pipe";
                            savedItem.EndPointModel = new List<Brep> { customized_part };
                            savedItem.Normal = normal;
                            DA.SetData(0, savedItem);

                            #endregion

                        }





                        //Sphere customized_part_sphere = new Sphere(tempPt, 5);
                        //Brep customized_part = customized_part_sphere.ToBrep();

                        ////Create a straight route from the base center to the selected point
                        //Point3d customized_part_center = tempPt;
                        //if (voxelSpace == null)
                        //    GetVoxelSpace(currModel, 1);

                        //Point3d pipeExit = centroid;
                        //List<Point3d> bestRoute1 = FindShortestPath(customized_part_center, pipeExit, currModel, 1);
                        //Curve route = Curve.CreateInterpolatedCurve(bestRoute1, 1);
                        //route = route.Trim(CurveEnd.Start, route.GetLength() / 10);

                        //Brep main_air_pipe = Brep.CreateThickPipe(route, 3, 3.2, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];

                        //Curve source_circle = new Circle(new Plane(centroid, new Vector3d(0, 0, 1)), 3).ToNurbsCurve();
                        //Curve pipe_start_circle = new Circle(new Plane(route.PointAtStart, route.TangentAtStart), 3).ToNurbsCurve();

                        //if (!Curve.DoDirectionsMatch(source_circle, pipe_start_circle))
                        //    pipe_start_circle.Reverse();
                        //Point3d start = source_circle.PointAtStart;
                        //pipe_start_circle.ClosestPoint(start, out double t);
                        //pipe_start_circle.ChangeClosedCurveSeam(t);
                        //Curve[] crossSectionCurves = new Curve[] { pipe_start_circle, source_circle };
                        //Brep[] loftBreps = Brep.CreateFromLoft(crossSectionCurves, Point3d.Unset, Point3d.Unset, LoftType.Normal, false);
                        //Brep source_extension = loftBreps[0];
                        //source_extension = source_extension.CapPlanarHoles(myDoc.ModelAbsoluteTolerance);

                        //Curve pipe_end_circle = new Circle(new Plane(tempPt, new Vector3d(0, 0, 1)), 3).ToNurbsCurve();
                        //Curve pipe_start_circle = new Circle(new Plane(route.PointAtStart, route.TangentAtStart), 3).ToNurbsCurve();

                        //Brep[] difference = Brep.CreateBooleanDifference(currModel_hollowed, customized_part_sphere.ToBrep(), myDoc.ModelAbsoluteTolerance);
                        //if (difference != null && difference.Length > 0)
                        //{
                        //    GetSimilarVolumeBrep(difference, currModel_hollowed, out currModel_hollowed);
                        //}

                        //difference = Brep.CreateBooleanDifference(main_air_pipe, currModel_hollowed, myDoc.ModelAbsoluteTolerance);
                        //if (difference != null && difference.Length > 0)
                        //    GetSimilarVolumeBrep(difference, main_air_pipe, out main_air_pipe);

                        //myDoc.Objects.Add(currModel_hollowed);
                        //myDoc.Objects.Add(main_air_pipe);
                        //myDoc.Objects.Add(source_extension);
                    }
                }
            }
        }


        /// <summary>
        /// This method obtains the 3D grid of the current model
        /// </summary>
        /// <param name="customized_part">The customized part that user wants</param>
        /// <param name="currModel">current model that the user wants to add pipe into</param>
        /// <param name="mode">1 = don't combine gears, 2 = combine gears</param>
        private void GetVoxelSpace(Brep currModel, int mode, Brep customized_part = null, List<Brep> tempBoxes = null, Brep ignorePart = null)
        {
            if (mode == 1) // For initializing the voxelSpace
            {
                BoundingBox boundingBox = currModel.GetBoundingBox(true);

                int w = (int)Math.Abs(boundingBox.Max.X - boundingBox.Min.X) / 2; //width
                int l = (int)Math.Abs(boundingBox.Max.Y - boundingBox.Min.Y) / 2; //length
                int h = (int)Math.Abs(boundingBox.Max.Z - boundingBox.Min.Z) / 2; //height



                voxelSpace = new Voxel[w, l, h];

                #region Initialize the voxel space element-wise
                Parallel.For(0, w, i =>
                {
                    for (int j = 0; j < l; j++)
                    {
                        double baseX = i * 2 + boundingBox.Min.X;
                        double baseY = j * 2 + boundingBox.Min.Y;


                        Point3d basePoint = new Point3d(baseX, baseY, boundingBox.Min.Z - 1);
                        Point3d topPoint = new Point3d(baseX, baseY, boundingBox.Max.Z + 1);
                        Curve intersector = (new Line(basePoint, topPoint)).ToNurbsCurve();
                        int num_region = 0;
                        if (Intersection.CurveBrep(intersector, currModel, myDoc.ModelAbsoluteTolerance, out Curve[] overlapCurves, out Point3d[] intersectionPoints))
                        {
                            num_region = intersectionPoints.Length % 2;
                        }

                        //Fix cases where the intersector intersect the brep at an edge
                        if (intersectionPoints != null && intersectionPoints.Length > 1)
                        {
                            Point3dList hit_points = new Point3dList(intersectionPoints);
                            hit_points.Sort();
                            Point3d hit = hit_points.Last();
                            List<Point3d> toRemoved_hit_points = new List<Point3d>();
                            for (int p = hit_points.Count - 2; p >= 0; p--)
                            {
                                if (hit_points[p].DistanceTo(hit) < myDoc.ModelAbsoluteTolerance)
                                {
                                    toRemoved_hit_points.Add(hit);
                                    toRemoved_hit_points.Add(hit_points[p]);
                                }
                                else
                                    hit = hit_points[p];
                            }
                            foreach (var replicate in toRemoved_hit_points)
                                hit_points.Remove(replicate);
                            intersectionPoints = hit_points.ToArray();
                            Sort.Quicksort(intersectionPoints, 0, intersectionPoints.Length - 1);
                        }

                        for (int k = 0; k < h; k++)
                        {
                            double currentZ = 2 * k + boundingBox.Min.Z;
                            Point3d currentPt = new Point3d(baseX, baseY, currentZ);
                            voxelSpace[i, j, k] = new Voxel
                            {
                                X = baseX,
                                Y = baseY,
                                Z = currentZ,
                                isTaken = true,
                                Index = new Index(i, j, k),
                                vector = new Vector3d(baseX, baseY, currentZ)
                            };

                            //Check if the current point is in the current model
                            if (num_region == 0)
                            {
                                for (int r = 0; r < intersectionPoints.Length; r += 2)
                                {
                                    if (currentZ < intersectionPoints[r + 1].Z && currentZ > intersectionPoints[r].Z)
                                    {
                                        voxelSpace[i, j, k].isTaken = false;
                                        break;
                                    }
                                }
                            }
                            else
                            {
                                voxelSpace[i, j, k].isTaken = !currModel.IsPointInside(currentPt, myDoc.ModelAbsoluteTolerance, true);
                            }



                            //Traverse all objects in the Rhino View to check for intersection. 
                            Boolean intersected = false;
                            Double maximumDistance = 3;//TODO: Try different distance metric to get better result. Euclidean: Math.Sqrt(Math.Pow((voxelSpace_offset + 1), 2) * 3)


                            if (voxelSpace[i, j, k].isTaken == false)
                            {
                                if (currModel.ClosestPoint(currentPt).DistanceTo(currentPt) < maximumDistance + 4)
                                {
                                    voxelSpace[i, j, k].isTaken = true;
                                    continue;
                                }

                            }
                        }
                    }
                });
                #endregion

            }

        }

        /// <summary>
        /// This method generates a list of possible pipe exits for the PCB
        /// </summary>
        /// <param name="base_part_center">The base part center of the current model</param>
        /// <param name="currModel">current model that the user wants to add the pipe</param>
        private void GetPipeExits(Point3d base_part_center, Brep currModel)
        {
            BoundingBox boundingBox = currModel.GetBoundingBox(true);
            double offset = 2; //This offset stands for the gap between the edge of the PCB and the pipe exit

            //Left upper corner of the PCB
            Point3d leftUpperCorner = new Point3d(base_part_center.X - pcbWidth / 2 + pipeRadius + offset, base_part_center.Y + pcbHeight / 2 - pipeRadius - offset, base_part_center.Z + 10);

            //Right upper corner of the PCB
            Point3d rightUpperCorner = new Point3d(base_part_center.X + pcbWidth / 2 - pipeRadius - offset, base_part_center.Y + pcbHeight / 2 - pipeRadius - offset, base_part_center.Z + 10);

            //Left lower corner of the PCB
            Point3d leftLowerCorner = new Point3d(base_part_center.X - pcbWidth / 2 + pipeRadius + offset, base_part_center.Y - pcbHeight / 2 + pipeRadius + offset, base_part_center.Z + 10);

            //Right lower corner of the PCB
            Point3d rightLowerCorner = new Point3d(base_part_center.X + pcbWidth / 2 - pipeRadius - offset, base_part_center.Y - pcbHeight / 2 + pipeRadius + offset, base_part_center.Z + 10);

            Index lu = FindClosestPointIndex(leftUpperCorner, currModel);
            Index ru = FindClosestPointIndex(rightUpperCorner, currModel);
            Index ll = FindClosestPointIndex(leftLowerCorner, currModel);
            Index rl = FindClosestPointIndex(rightLowerCorner, currModel);

            List<Voxel> voxels = new List<Voxel>();
            voxels.Add(voxelSpace[lu.i, lu.j, lu.k]);
            voxels.Add(voxelSpace[ru.i, ru.j, ru.k]);
            voxels.Add(voxelSpace[ll.i, ll.j, ll.k]);
            voxels.Add(voxelSpace[rl.i, rl.j, rl.k]);

            List<Point3d> voxels_location = new List<Point3d>();
            voxels_location.Add(leftUpperCorner);
            voxels_location.Add(rightUpperCorner);
            voxels_location.Add(leftLowerCorner);
            voxels_location.Add(rightLowerCorner);



            var allObjects = new List<RhinoObject>(myDoc.Objects.GetObjectList(ObjectType.Brep));

            Parallel.For(0, voxels.Count, i =>
            {
                foreach (var item in allObjects)
                {
                    Guid guid = item.Id;
                    ObjRef currObj = new ObjRef(guid);
                    Brep brep = currObj.Brep();



                    //See if the point is strictly inside of the brep
                    if (brep != null)
                    {
                        //Check if the current brep is the 3D model main body
                        if (guid == currModelObjId)
                        {
                            voxels[i].isTaken = !currModel.IsPointInside(voxels_location[i], 1, false);
                            continue;
                        }

                        if (brep.IsPointInside(voxels_location[i], myDoc.ModelAbsoluteTolerance, true))
                        {
                            voxels[i].isTaken = true;
                            break;
                        }
                        Double maximumDistance = 4;
                        //See if the point is too close to the brep and will cause intersection after creating the pipe
                        if (brep.ClosestPoint(voxels_location[i]).DistanceTo(voxels_location[i]) <= maximumDistance)
                        {
                            voxels[i].isTaken = true;
                            break;
                        }
                    }
                }
            });



            pipeExitPts.Add(new PipeExit(voxelSpace[lu.i, lu.j, lu.k], leftUpperCorner));
            pipeExitPts.Add(new PipeExit(voxelSpace[ru.i, ru.j, ru.k], rightUpperCorner));
            pipeExitPts.Add(new PipeExit(voxelSpace[ll.i, ll.j, ll.k], leftLowerCorner));
            pipeExitPts.Add(new PipeExit(voxelSpace[rl.i, rl.j, rl.k], rightLowerCorner));

            myDoc.Objects.AddPoint(leftUpperCorner);
            myDoc.Objects.AddPoint(rightUpperCorner);
            myDoc.Objects.AddPoint(leftLowerCorner);
            myDoc.Objects.AddPoint(rightLowerCorner);
        }

        /// <summary>
        /// Finds surrounding neighbors in 5*5*5 region
        /// </summary>
        /// <param name="current">current index in the 3D grid of the current model</param>
        /// <param name="goal">the goal voxel of the A* algorithm</param>
        /// <param name="voxelSpace"> the 3D grid of the current model</param>
        /// <returns></returns>
        private Queue<Voxel> GetNeighbors(Index current, int mode, ref Voxel goal, ref Voxel[,,] voxelSpace)
        {
            #region Get all neighbors
            Queue<Voxel> neighbors = new Queue<Voxel>();

            //up,down,left,right,front,back voxels of the current
            if (mode == 1)
            {
                for (int i = current.i - 2; i < current.i + 3; i++)
                {
                    for (int j = current.j - 2; j < current.j + 3; j++)
                    {
                        for (int k = current.k - 2; k < current.k + 3; k++)
                        {
                            if (i < voxelSpace.GetLength(0) && j < voxelSpace.GetLength(1) && k < voxelSpace.GetLength(2) && i >= 0 && j >= 0 && k >= 0)
                            {
                                if (goal.Equal(voxelSpace[i, j, k]))
                                    neighbors.Enqueue(voxelSpace[i, j, k]);
                                if (voxelSpace[i, j, k].isTaken == false)
                                    neighbors.Enqueue(voxelSpace[i, j, k]);
                            }
                        }
                    }
                }
            }
            else if (mode == 2)
            {
                for (int i = current.i - 1; i < current.i + 2; i++)
                {
                    for (int j = current.j - 1; j < current.j + 2; j++)
                    {
                        for (int k = current.k - 1; k < current.k + 2; k++)
                        {
                            if (i < voxelSpace.GetLength(0) && j < voxelSpace.GetLength(1) && k < voxelSpace.GetLength(2) && i >= 0 && j >= 0 && k >= 0)
                            {
                                if (goal.Equal(voxelSpace[i, j, k]))
                                    neighbors.Enqueue(voxelSpace[i, j, k]);
                                if (voxelSpace[i, j, k].isTaken == false)
                                    neighbors.Enqueue(voxelSpace[i, j, k]);
                            }
                        }
                    }
                }
            }
            #endregion

            return neighbors;
        }

        /// <summary>
        /// This method finds the estimated index in the 3D grid of the current model that has the closest location to the given point
        /// </summary>
        /// <param name="point">A point that needs to be estimated</param>
        /// <param name="currModel">current model that the user wants to add pipe into</param>
        /// <returns>the estimated index in the 3D grid of the current model</returns>
        private Index FindClosestPointIndex(Point3d point, Brep currModel)
        {
            Index index = new Index();

            //Calculate the approximate index of Point3d. Then obtain the precise index that has the smallest distance within the 2*2*2 bounding box of the Point3d
            BoundingBox boundingBox = currModel.GetBoundingBox(true);

            #region Calculate an estimated index
            double w = boundingBox.Max.X - boundingBox.Min.X; //width
            double h = boundingBox.Max.Y - boundingBox.Min.Y; //length
            double l = boundingBox.Max.Z - boundingBox.Min.Z; //height


            int estimated_i = (int)Math.Abs((point.X - boundingBox.Min.X) / 2);
            int estimated_j = (int)Math.Abs((point.Y - boundingBox.Min.Y) / 2);
            int estimated_k = (int)Math.Abs((point.Z - boundingBox.Min.Z) / 2);

            if (estimated_i >= voxelSpace.GetLength(0))
                estimated_i = voxelSpace.GetLength(0) - 1;
            if (estimated_j >= voxelSpace.GetLength(1))
                estimated_j = voxelSpace.GetLength(1) - 1;
            if (estimated_k >= voxelSpace.GetLength(2))
                estimated_k = voxelSpace.GetLength(2) - 1;
            if (estimated_i < 0)
                estimated_i = 0;
            if (estimated_j < 0)
                estimated_j = 0;
            if (estimated_k < 0)
                estimated_k = 0;
            #endregion


            double smallestDistance = double.MaxValue;
            index.i = estimated_i;
            index.j = estimated_j;
            index.k = estimated_k;
            bool found = false;
            #region Traverse the 5*5*5 bounding box of the estimated index to see if there is a better one
            if (voxelSpace[estimated_i, estimated_j, estimated_k].isTaken)
            {
                for (int i = estimated_i - 8; i < estimated_i + 9; i++)
                {
                    for (int j = estimated_j - 8; j < estimated_j + 9; j++)
                    {
                        for (int k = estimated_k - 8; k < estimated_k + 9; k++)
                        {
                            if (i < voxelSpace.GetLength(0) && j < voxelSpace.GetLength(1) && k < voxelSpace.GetLength(2) && i >= 0 && j >= 0 && k >= 0)
                            {
                                double distance = voxelSpace[i, j, k].GetDistance(point.X, point.Y, point.Z);

                                if (voxelSpace[i, j, k].isTaken == false && distance < smallestDistance)
                                {
                                    smallestDistance = distance;
                                    index.i = i;
                                    index.j = j;
                                    index.k = k;
                                    found = true;
                                    break;
                                }
                            }
                            if (found == true)
                                break;
                        }
                        if (found == true)
                            break;
                    }
                    if (found == true)
                        break;
                }
            }

            #endregion

            return index;
        }

        /// <summary>
        /// This method finds the route from customized part to the pipe exit using A*
        /// </summary>
        /// <param name="customized_part_center">The customized part bounding box center</param>
        /// <param name="base_part_center">The PCB part center</param>
        /// <param name="customized_part">The actual Brep object of the customized part</param>
        /// <param name="currModel">The current model that user wants to add pipe to</param>
        /// <returns></returns>
        private List<Point3d> FindShortestPath(Point3d customized_part_center, Point3d base_part_center, Brep currModel, int mode)
        {
            Line temp = new Line();
            Curve pipepath = temp.ToNurbsCurve();


            //Guid customized_guid = myDoc.Objects.AddPoint(customized_part_center);
            //Guid guid2 = myDoc.Objects.AddPoint(base_part_center);
            //myDoc.Views.Redraw();


            #region Preprocessing before A* algorithm
            Index customized_part_center_index = FindClosestPointIndex(customized_part_center, currModel);
            Index base_part_center_index = FindClosestPointIndex(base_part_center, currModel);

            Voxel start = voxelSpace[customized_part_center_index.i, customized_part_center_index.j, customized_part_center_index.k];
            Voxel goal = voxelSpace[base_part_center_index.i, base_part_center_index.j, base_part_center_index.k];

            start.Cost = 0; //TODO: Start is null, please FIX
            start.Distance = 0;
            start.SetDistance(goal.X, goal.Y, goal.Z);

            Queue<Voxel> voxelRoute = new Queue<Voxel>();

            #endregion

            int count = 0;
            foreach (var item in voxelSpace)
            {
                if (item.isTaken == false)
                    count++;
            }


            #region Perform A* algorithm
            Voxel current;
            if (mode == 1) //For light pipe
            {
                SimplePriorityQueue<Voxel, double> frontier = new SimplePriorityQueue<Voxel, double>();
                List<Voxel> searchedVoxels = new List<Voxel>();

                frontier.Enqueue(start, 0);

                Line straight_line = new Line(start.X, start.Y, start.Z, goal.X, goal.Y, goal.Z);

                while (frontier.Count != 0)
                {
                    current = frontier.Dequeue();

                    if (current.Equal(goal))
                    {
                        break;
                    }

                    foreach (var next in GetNeighbors(current.Index, 1, ref goal, ref voxelSpace))
                    {
                        double new_cost = current.Cost + 1;
                        if (new_cost < next.Cost || !searchedVoxels.Contains(next))
                        {
                            next.Cost = new_cost;
                            double distance = straight_line.DistanceTo(new Point3d(next.X, next.Y, next.Z), false);
                            double priority = new_cost + next.GetDistance(goal.X, goal.Y, goal.Z) + distance;
                            frontier.Enqueue(next, priority);
                            searchedVoxels.Add(next);
                            next.Parent = current;
                        }
                    }
                }
            }
            #endregion

            #region Retrieve to get the searched best route
            Stack<Voxel> bestRoute_Voxel = new Stack<Voxel>();
            List<Point3d> bestRoute_Point3d = new List<Point3d>();


            current = goal;
            while (current != start)
            {
                Point3d currentPoint = new Point3d(current.X, current.Y, current.Z);
                bestRoute_Voxel.Push(current);
                bestRoute_Point3d.Add(currentPoint);
                current = current.Parent;
                current.isTaken = true;
            }
            bestRoute_Voxel.Push(current);
            bestRoute_Point3d.Add(new Point3d(current.X, current.Y, current.Z));
            #endregion

            //TODO: Set the accurate location of the start and end
            //bestRoute_Point3d[0] = base_part_center;
            double min_distance = double.MaxValue;
            int closestIndex = 0;
            for (int i = 0; i < bestRoute_Point3d.Count; i++)
            {
                if (bestRoute_Point3d[i].DistanceToSquared(customized_part_center) < min_distance)
                {
                    min_distance = bestRoute_Point3d[i].DistanceToSquared(customized_part_center);
                    closestIndex = i;
                }
            }

            bestRoute_Point3d.RemoveRange(closestIndex, bestRoute_Point3d.Count - closestIndex - 1);

            List<int> indexes = new List<int>();
            for (int i = 0; i < bestRoute_Point3d.Count; i++)
            {
                if (bestRoute_Point3d[i].Z > customized_part_center.Z)
                {
                    indexes.Add(i);
                }
            }
            indexes.Sort((a, b) => b.CompareTo(a));
            foreach (int i in indexes)
            {
                if (i >= 0 && i < bestRoute_Point3d.Count)
                    bestRoute_Point3d.RemoveAt(i);
            }

            Line line = new Line(bestRoute_Point3d[bestRoute_Point3d.Count - 1], customized_part_center);
            bestRoute_Point3d.Add(line.ToNurbsCurve().PointAtLength(line.Length / 2));

            #region Use the result of A* to generate routes that are less curvy

            #region Method 1: Retrive route from the start to the end, it checks if the pipe generated by the straight line from current point to end point is not causing intersection
            List<Point3d> interpolatedRoute_Point3d = new List<Point3d>();
            interpolatedRoute_Point3d.Add(bestRoute_Point3d[0]);

            Boolean isIntersected = false;
            int index = 1;
            while (isIntersected == true || !interpolatedRoute_Point3d[interpolatedRoute_Point3d.Count - 1].Equals(bestRoute_Point3d[bestRoute_Point3d.Count - 1]))
            {
                Point3d endPoint = bestRoute_Point3d[bestRoute_Point3d.Count - index];
                //Create a pipe for the current section of the line
                Curve betterRoute = (new Line(interpolatedRoute_Point3d[interpolatedRoute_Point3d.Count - 1], endPoint)).ToNurbsCurve();

                if (betterRoute == null)
                {
                    interpolatedRoute_Point3d.Add(bestRoute_Point3d[bestRoute_Point3d.Count - index + 1]);
                    index = 1;
                    isIntersected = false;
                    continue;
                }
                Brep[] pipe = Brep.CreatePipe(betterRoute, 3.2, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians);


                //Check if the Pipe is intersecting with other breps
                var allObjects = new List<RhinoObject>(myDoc.Objects.GetObjectList(ObjectType.Brep));
                foreach (var item in allObjects)
                {
                    Guid guid = item.Id;
                    ObjRef currObj = new ObjRef(guid);
                    Brep brep = currObj.Brep();

                    if (brep != null)
                    {
                        //Check for intersection, go to the next brep if no intersection is founded, else, break and report intersection found
                        if (Intersection.BrepBrep(brep, pipe[0], myDoc.ModelAbsoluteTolerance, out Curve[] intersectionCurves, out Point3d[] intersectionPoints))
                        {
                            if (intersectionCurves.Length == 0 && intersectionPoints.Length == 0)
                            {
                                isIntersected = false;
                                continue;
                            }
                            else
                            {
                                isIntersected = true;
                                index += 1;
                                break;
                            }
                        }
                    }
                }

                if (isIntersected)
                {
                    continue;
                }

                interpolatedRoute_Point3d.Add(endPoint);
                index = 1;
            }
            #endregion
            return interpolatedRoute_Point3d;

            #endregion
        }

        private bool GetMaxVolumeBrep(IEnumerable<Brep> breps, out Brep brep)
        {
            brep = null;
            double maxVolume = double.MinValue;
            if (breps != null && breps.Count() > 0)
            {
                foreach (var b in breps)
                {
                    if (b.GetVolume() > maxVolume)
                    {
                        maxVolume = b.GetVolume();
                        brep = b;
                    }
                }
                return true;
            }
            return false;
        }

        private bool GetSimilarVolumeBrep(IEnumerable<Brep> breps, Brep originalBrep, out Brep brep)
        {
            brep = null;
            double minDifference = double.MaxValue;
            double original_volume = originalBrep.GetVolume();
            if (breps != null && breps.Count() > 0)
            {
                foreach (var b in breps)
                {
                    if (Math.Abs(original_volume - b.GetVolume()) < minDifference)
                    {
                        minDifference = Math.Abs(original_volume - b.GetVolume());
                        brep = b;
                    }
                }
                return true;
            }
            return false;
        }




        /// <summary>
        /// This method listen to ENTER button
        /// </summary>
        /// <param name="key">User's keyboard event</param>
        public void OnKeyboardEvent(int key)
        {
            if (key == 13)
            {
                endButtonClicked = true;
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
            get { return new Guid("236FB0D6-EA7A-4005-9A0D-4242B5286787"); }
        }
    }
}