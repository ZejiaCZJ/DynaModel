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
                    currModelObjId = myDoc.Objects.Add(currModel);

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
                    CurveList selected_area = new CurveList();
                    List<Brep> selected_box = new List<Brep>();
                    List<Guid> rectanglesGuid = new List<Guid>();
                    var getSelectedPts = RhinoGet.GetOneObject("Please select points for a pipe exit, press ENTER when finished", false, ObjectType.Point, out ObjRef pointRef);
                    #endregion


                    if (pointRef != null)
                    {
                        Point3d tempPt = new Point3d(pointRef.Point().Location);
                        double x = tempPt.X;
                        double y = tempPt.Y;
                        double z = tempPt.Z;

                        //Check if the selected point is in selectedPts
                        //1. If so, Get rid of the bounding box
                        //2. If not, store the selected point and display bounding box

                        selectedPts.Add(tempPt);

                        // Display the selected area with red 2D bounding box
                        BoundingBox box = new BoundingBox(x - 0.5, y - 0.5, z - 0.5, x + 0.5, y + 0.5, z + 0.5);
                        Intersection.BrepBrep(box.ToBrep(), currModel, myDoc.ModelAbsoluteTolerance, out Curve[] curve, out Point3d[] point);
                        foreach (var c in curve)
                        {
                            selected_area.Add(c);
                            Guid temp = myDoc.Objects.AddCurve(c, redAttribute);
                            rectanglesGuid.Add(temp);
                        }

                        selected_box.Add(box.ToBrep());

                        myDoc.Views.Redraw();

                        //Delete all points on the view
                        foreach (var ptsID in pts_Guid)
                        {
                            myDoc.Objects.Delete(ptsID, true);
                        }

                        //Delete all rectangle
                        foreach (var ptsID in rectanglesGuid)
                        {
                            myDoc.Objects.Delete(ptsID, true);
                        }

                        pts_Guid.Clear();
                        surfacePts.Clear();


                        //Show the User interact parameter
                        Brep customized_part = Brep.MergeBreps(selected_box.ToArray(), myDoc.ModelAbsoluteTolerance);
                        Guid newPartGuid = myDoc.Objects.AddBrep(customized_part, redAttribute);
                        myDoc.Views.Redraw();


                        #region preparation before pipe creation
                        Point3d customized_part_center = customized_part.GetBoundingBox(true).Center;
                        GetVoxelSpace(currModel, 1, customized_part);

                        //Find the Pipe exit location
                        if (pipeExitPts.Count == 0)
                        {
                            double base_z = boundingBox.Min.Z;
                            double base_x = (boundingBox.Max.X - boundingBox.Min.X) / 2 + boundingBox.Min.X;
                            double base_y = (boundingBox.Max.Y - boundingBox.Min.Y) / 2 + boundingBox.Min.Y;
                            Point3d basePartCenter = new Point3d(base_x, base_y, base_z);
                            GetPipeExits(basePartCenter, currModel);
                        }

                        Point3d pipeExit = new Point3d();
                        bool allTaken = true;
                        foreach (var item in pipeExitPts)
                        {
                            if (item.isTaken == false && item.location.isTaken == false)
                            {
                                pipeExit = new Point3d(item.location.X, item.location.Y, item.location.Z);
                                item.isTaken = true;
                                allTaken = false;
                                break;
                            }
                        }
                        if (allTaken)
                        {
                            RhinoApp.WriteLine("All pipe exits are taken, or covered. Unable to create anymore LED light parameters");
                            return;
                        }
                        #endregion


                        #region Find pipe path
                        //Method 1: use A* directly
                        List<Point3d> bestRoute1 = FindShortestPath(customized_part_center, pipeExit, customized_part, currModel, 1);
                        Curve bestRoute = Curve.CreateInterpolatedCurve(bestRoute1, 1);

                        //Method 2: use a portion of the lightPipe directly.
                        Curve bestStartRoute = bestRoute;
                        Curve bestEndRoute = bestRoute;

                        if (combinableLightPipeRoute.Count > 0)
                        {
                            Curve bestMiddleRoute = bestRoute;
                            int closestIndex = -1;
                            double closestDistance = pipeExit.DistanceToSquared(customized_part_center);
                            for (int i = 0; i < combinableLightPipe.Count; i++)
                            {
                                Point3d head = combinableLightPipeRoute[i].PointAtEnd;
                                double thisDistance = head.DistanceToSquared(customized_part_center);
                                if (thisDistance < closestDistance)
                                {
                                    closestIndex = i;
                                    closestDistance = thisDistance;
                                    bestMiddleRoute = combinableLightPipeRoute[i];
                                }
                            }
                            myDoc.Objects.Delete(newPartGuid, true);
                            voxelSpace = null;
                            myDoc.Objects.Delete(conductiveObjects[closestIndex].guid, true);
                            GetVoxelSpace(currModel, 1, combinableLightPipe[closestIndex]);
                            bestMiddleRoute = bestMiddleRoute.Trim(CurveEnd.End, 7);
                            bestMiddleRoute = bestMiddleRoute.Trim(CurveEnd.Start, 7);



                            List<Point3d> bestStartPath = FindShortestPath(customized_part_center, bestMiddleRoute.PointAtEnd, customized_part, currModel, 2);
                            List<Point3d> bestEndPath = FindShortestPath(bestMiddleRoute.PointAtStart, pipeExit, customized_part, currModel, 2);
                            bestStartRoute = Curve.CreateInterpolatedCurve(bestStartPath, 1);
                            bestEndRoute = Curve.CreateInterpolatedCurve(bestEndPath, 1);
                            myDoc.Objects.Add(bestStartRoute, soluableAttribute);
                            myDoc.Objects.Add(bestEndRoute, lightGuideAttribute);
                            conductiveObjects[closestIndex].guid = myDoc.Objects.Add(conductiveObjects[closestIndex].brep, redAttribute);
                        }

                        #endregion

                        #region Find the shortest route to create the air pipe
                        double totalDistance = bestStartRoute.GetLength() + bestEndRoute.GetLength();
                        if (bestRoute.GetLength() < totalDistance)
                        {
                            //Create method 1 pipe 
                            Brep[] airPipe = Brep.CreatePipe(bestRoute, 2, true, PipeCapMode.Flat, false, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians);
                            airPipe = Brep.CreateBooleanSplit(airPipe[0], currModel, myDoc.ModelAbsoluteTolerance);
                            myDoc.Objects.Add(airPipe[0], solidAttribute);
                        }
                        else
                        {
                            Brep[] airPipe1 = Brep.CreatePipe(bestStartRoute, 2, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians);
                            Brep[] airPipe2 = Brep.CreatePipe(bestEndRoute, 2, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians);
                            airPipe1 = Brep.CreateBooleanSplit(airPipe1[0], currModel, myDoc.ModelAbsoluteTolerance);
                            airPipe2 = Brep.CreateBooleanSplit(airPipe2[0], currModel, myDoc.ModelAbsoluteTolerance);
                            myDoc.Objects.Add(airPipe1[0], solidAttribute);
                            myDoc.Objects.Add(airPipe2[0], solidAttribute);
                        }

                        //Pass the result as Item to the Save button
                        Item saveItem = new Item();
                        saveItem.StartPoint = pipeExit;
                        saveItem.EndPoint = customized_part_center;
                        saveItem.EndPointModel.Add(customized_part);
                        saveItem.Name = "Air Pipe";
                        DA.SetData(0, saveItem);

                        #endregion
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
                var allObjects = new List<RhinoObject>(myDoc.Objects.GetObjectList(ObjectType.Brep));
                Guid customized_part_Guid = Guid.Empty;
                Guid currModel_Guid = Guid.Empty;

                foreach (var item in allObjects)
                {
                    Guid guid = item.Id;
                    ObjRef currObj = new ObjRef(myDoc, guid);
                    Brep brep = currObj.Brep();
                    if (brep != null)
                    {
                        if (brep.IsDuplicate(currModel, myDoc.ModelAbsoluteTolerance))
                        {
                            currModel_Guid = guid;
                        }
                        if (brep.IsDuplicate(customized_part, myDoc.ModelAbsoluteTolerance))
                        {
                            customized_part_Guid = guid;
                        }
                    }

                }



                BoundingBox boundingBox = currModel.GetBoundingBox(true);

                int w = (int)Math.Abs(boundingBox.Max.X - boundingBox.Min.X) * voxelSpace_offset; //width
                int l = (int)Math.Abs(boundingBox.Max.Y - boundingBox.Min.Y) * voxelSpace_offset; //length
                int h = (int)Math.Abs(boundingBox.Max.Z - boundingBox.Min.Z) * voxelSpace_offset; //height



                voxelSpace = new Voxel[w, l, h];

                double offset_spacer = 1 / voxelSpace_offset;

                #region Initialize the voxel space element-wise
                Parallel.For(0, w, i =>
                {
                    for (int j = 0; j < l; j++)
                    {
                        double baseX = i + boundingBox.Min.X;
                        double baseY = j + boundingBox.Min.Y;


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
                            double currentZ = offset_spacer * k + boundingBox.Min.Z;
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
                                foreach (var item in allObjects)
                                {
                                    Guid guid = item.Id;
                                    ObjRef currObj = new ObjRef(myDoc, guid);
                                    Brep brep = currObj.Brep();



                                    //See if the point is strictly inside of the brep
                                    if (brep != null)
                                    {
                                        //Check if the current brep is the 3D model main body
                                        if (guid == customized_part_Guid || guid == currModel_Guid)
                                        {
                                            continue;
                                        }

                                        if (brep.IsPointInside(currentPt, myDoc.ModelAbsoluteTolerance, true))
                                        {
                                            voxelSpace[i, j, k].isTaken = true;
                                            break;
                                        }

                                        //See if the point is too close to the brep and will cause intersection after creating the pipe
                                        if (brep.ClosestPoint(currentPt).DistanceTo(currentPt) <= maximumDistance)
                                        {
                                            voxelSpace[i, j, k].isTaken = true;
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                    }
                });
                #endregion

            }
            else if (mode == 2) //For Light Pipe bestRoute2
            {
                double maximumDistance = 4;
                BoundingBox boundingBox = currModel.GetBoundingBox(true);

                int w = (int)Math.Abs(boundingBox.Max.X - boundingBox.Min.X) * voxelSpace_offset; //width
                int l = (int)Math.Abs(boundingBox.Max.Y - boundingBox.Min.Y) * voxelSpace_offset; //length
                int h = (int)Math.Abs(boundingBox.Max.Z - boundingBox.Min.Z) * voxelSpace_offset; //height


                Parallel.For(0, w, i =>
                {
                    for (int j = 0; j < l; j++)
                    {
                        for (int k = 0; k < h; k++)
                        {
                            foreach (var brep in tempBoxes)
                            {
                                Point3d currentPt = new Point3d(voxelSpace[i, j, k].X, voxelSpace[i, j, k].Y, voxelSpace[i, j, k].Z);
                                if (brep.IsPointInside(currentPt, myDoc.ModelAbsoluteTolerance, true))
                                {
                                    voxelSpace[i, j, k].isTaken = true;
                                    break;
                                }

                                //See if the point is too close to the brep and will cause intersection after creating the pipe
                                if (brep.ClosestPoint(currentPt).DistanceTo(currentPt) <= maximumDistance)
                                {
                                    voxelSpace[i, j, k].isTaken = true;
                                    break;
                                }
                            }
                        }
                    }
                });
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
            Point3d leftUpperCorner = new Point3d(base_part_center.X - pcbWidth / 2 + pipeRadius + offset, base_part_center.Y + pcbHeight / 2 - pipeRadius - offset, base_part_center.Z);

            //Right upper corner of the PCB
            Point3d rightUpperCorner = new Point3d(base_part_center.X + pcbWidth / 2 - pipeRadius - offset, base_part_center.Y + pcbHeight / 2 - pipeRadius - offset, base_part_center.Z);

            //Left lower corner of the PCB
            Point3d leftLowerCorner = new Point3d(base_part_center.X - pcbWidth / 2 + pipeRadius + offset, base_part_center.Y - pcbHeight / 2 + pipeRadius + offset, base_part_center.Z);

            //Right lower corner of the PCB
            Point3d rightLowerCorner = new Point3d(base_part_center.X + pcbWidth / 2 - pipeRadius - offset, base_part_center.Y - pcbHeight / 2 + pipeRadius + offset, base_part_center.Z);

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

            int estimated_i = (int)((point.X - boundingBox.Min.X) * voxelSpace_offset);
            int estimated_j = (int)((point.Y - boundingBox.Min.Y) * voxelSpace_offset);
            int estimated_k = (int)((point.Z - boundingBox.Min.Z) * voxelSpace_offset);

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

            #region Traverse the 5*5*5 bounding box of the estimated index to see if there is a better one
            double smallestDistance = voxelSpace[estimated_i, estimated_j, estimated_k].GetDistance(point.X, point.Y, point.Z);
            index.i = estimated_i;
            index.j = estimated_j;
            index.k = estimated_k;

            for (int i = estimated_i - 5; i < estimated_i + 6; i++)
            {
                for (int j = estimated_j - 5; j < estimated_j + 6; j++)
                {
                    for (int k = estimated_k - 5; k < estimated_k + 6; k++)
                    {
                        if (i < voxelSpace.GetLength(0) && j < voxelSpace.GetLength(1) && k < voxelSpace.GetLength(2) && i >= 0 && j >= 0 && k >= 0)
                        {
                            double distance = voxelSpace[i, j, k].GetDistance(point.X, point.Y, point.Z);

                            if (distance < smallestDistance)
                            {
                                smallestDistance = distance;
                                index.i = i;
                                index.j = j;
                                index.k = k;
                            }
                        }
                    }
                }
            }
            #endregion

            return index;
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
        /// This method finds the route from customized part to the pipe exit using A*
        /// </summary>
        /// <param name="customized_part_center">The customized part bounding box center</param>
        /// <param name="base_part_center">The PCB part center</param>
        /// <param name="customized_part">The actual Brep object of the customized part</param>
        /// <param name="currModel">The current model that user wants to add pipe to</param>
        /// <returns></returns>
        private List<Point3d> FindShortestPath(Point3d customized_part_center, Point3d base_part_center, Brep customized_part, Brep currModel, int mode)
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
            else if (mode == 2) //For air pipe
            {
                SimplePriorityQueue<Voxel, double> frontier = new SimplePriorityQueue<Voxel, double>();
                List<Voxel> searchedVoxels = new List<Voxel>();

                frontier.Enqueue(start, 0);

                while (frontier.Count != 0)
                {
                    current = frontier.Dequeue();

                    if (current.Equal(goal))
                    {
                        break;
                    }

                    foreach (var next in GetNeighbors(current.Index, 2, ref goal, ref voxelSpace))
                    {
                        double new_cost = current.Cost + 1;
                        if (new_cost < next.Cost || !searchedVoxels.Contains(next))
                        {
                            next.Cost = new_cost;
                            double priority = new_cost + next.GetDistance(goal.X, goal.Y, goal.Z);
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
            bestRoute_Point3d.Add(customized_part_center);

            //Parallel.For(0, bestRoute_Point3d.Count, i =>{
            //    myDoc.Objects.AddPoint(bestRoute_Point3d[i]);
            //});

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
                Brep[] pipe = Brep.CreatePipe(betterRoute, 2, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians);


                //Check if the Pipe is intersecting with other breps
                var allObjects = new List<RhinoObject>(myDoc.Objects.GetObjectList(ObjectType.Brep));
                foreach (var item in allObjects)
                {
                    Guid guid = item.Id;
                    ObjRef currObj = new ObjRef(guid);
                    Brep brep = currObj.Brep();

                    if (brep != null)
                    {
                        //Ignore the current model and customized part
                        if (brep.IsDuplicate(currModel, myDoc.ModelAbsoluteTolerance) || brep.IsDuplicate(customized_part, myDoc.ModelAbsoluteTolerance))
                        {
                            continue;
                        }

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