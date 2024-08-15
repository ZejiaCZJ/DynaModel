using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DynaModel_v2.Geometry;
using System.Windows;
using DynaModel_v2.SharedData;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using DynaModel_v2.Rotational_Motion;
using DynaModel_v2.Light_Pipe;
using Rhino.Collections;
using Priority_Queue;
using System.Drawing;
using Rhino.Commands;
using Grasshopper.Kernel.Geometry.Delaunay;
using Grasshopper.Kernel.Geometry;
using Plane = Rhino.Geometry.Plane;
using System.Net;

namespace DynaModel_v2.Final_Stage
{
    public class GenerateHelper
    {
        //General parameter
        RhinoDoc myDoc = RhinoDoc.ActiveDoc;
        public Guid currModelObjId = Guid.Empty;
        public Guid currModel_Hollowed_ObjId = Guid.Empty;
        public Brep currModel = null;
        public Brep currModel_Hollowed = null;
        BoundingBox currModel_box;
        List<Brep> allBreps = new List<Brep>();
        List<Guid> allBreps_guid = new List<Guid>();
        Voxel[,,] voxelSpace = null;
        List<Brep> allPipes = new List<Brep>();
        public ObjectAttributes solidAttribute, lightGuideAttribute, redAttribute, yellowAttribute, soluableAttribute;
        double foundation_length = 60;
        double foundation_width = 90;
        double foundation_height = 30;
        Point3d foundation_origin;
        Point3d foundation_center;
        public Brep foundation { get; set; }
        double pcb_width = 50;
        double pcb_length = 50;
        double pcb_origin_x = 5; //Relative coordinate to foundation
        double pcb_origin_y = 5;//Relative coordinate to foundation
        Point3d pcb_origin;
        Point3d pcb_center;
        public List<Brep> toDelete = new List<Brep>();
        private List<Guid> specialPipes = new List<Guid>(); //Shafts that is not vertical or horizontal


        //Gear parameter
        private static double start_gear_elevation = 4;
        private static double module = 1.5;
        private static double pressure_angle = 20;
        private static double thickness = 5;
        private static double ratio;
        private static double clearance = 0.5;
        private static double clearance_shaft_radius = 3.4;
        private static double shaft_radius = 3;
        private static double shaft_extends_from_gear = 3;
        private static double gasket_inner_radius = 3.4; //shaft_radius + 0.4
        private static double gasket_outer_radius = 7;
        private static double gasket_clearance_inner_radius = 3.3;
        private static double gasket_clearance_outer_radius = clearance_shaft_radius;
        private static double clearance_gasket_radius = 1.7;
        private static double gasket_gear_gap = 0.6;
        private static double bottom_gasket_gear_height = 4;
        private static double top_gasket_gear_height = 3;
        public List<Curve> gasketCircles = new List<Curve>();
        public List<Guid> gaskets_guid = new List<Guid>();
        public List<Brep> conductive_pipes = new List<Brep>();
        public List<Guid> conductive_pipes_guid = new List<Guid>();



        //LED Light parameter
        private double pipeRadius = 3.5;
        private int voxelSpace_offset = 1;
        private List<Brep> ignorePipes = new List<Brep>();
        private List<Guid> ignorePipesGuid = new List<Guid>();
        private List<Guid> allTempBoxesGuid= new List<Guid>();
        private List<PipeExit> ledPipeExitPts = new List<PipeExit>();
        private List<Curve> combinableLightPipeRoute = new List<Curve>();
        private List<Brep> combinableLightPipe = new List<Brep>();
        private List<InViewObject> conductiveObjects = new List<InViewObject>();
        public List<Brep> led_pipes = new List<Brep>();
        public List<Guid> led_pipes_guid = new List<Guid>();
        public List<(Brep, Brep)> lightSourcePipePairs = new List<(Brep, Brep)>();
        public List<Brep> mainPipePairs = new List<Brep>();

        //Button parameter
        private List<PipeExit> conductivePipeExitPts = new List<PipeExit>();


        public GenerateHelper(out bool success)
        {
            currModelObjId = SavedItems.originalModelGuids[0];
            RhinoObject currModelObj = myDoc.Objects.FindId(currModelObjId);
            if (currModelObj != null && currModelObj.Geometry.ObjectType == ObjectType.Brep) 
            {
                currModel = currModelObj.Geometry as Brep;
                success = true;
            }
            else
            {
                RhinoApp.WriteLine("There are none or more than one models in Rhino document");
                success = false;
                return;
            }

            currModel_Hollowed_ObjId = SavedItems.originalModelGuids[1];
            RhinoObject currModel_Hollowed_Obj = myDoc.Objects.FindId(currModel_Hollowed_ObjId);
            if (currModel_Hollowed_Obj != null && currModel_Hollowed_Obj.Geometry.ObjectType == ObjectType.Brep)
            {
                currModel_Hollowed = currModel_Hollowed_Obj.Geometry as Brep;
                success = true;
            }
            else
            {
                RhinoApp.WriteLine("There are none or more than one models in Rhino document");
                success = false;
                return;
            }

            //Generate Foundation and PCB if possible
            currModel_box = currModel.GetBoundingBox(true);
            Circle circle = new Circle(new Point3d(0,0,currModel_box.Min.Z + 0.1), 1000);
            Brep planarSurface = Brep.CreatePlanarBreps(new[] { circle.ToNurbsCurve() }, myDoc.ModelAbsoluteTolerance)[0];
            Brep[] a = planarSurface.Trim(currModel, myDoc.ModelAbsoluteTolerance);
            Brep currModel_bottom = new Brep();
            if(a.Length > 0)
                currModel_bottom = a[0];
            Point3d centroid = AreaMassProperties.Compute(currModel_bottom.Faces[0]).Centroid;

            foundation_center = new Point3d(centroid.X, centroid.Y, currModel_box.Min.Z + foundation_height/2);
            foundation_origin = new Point3d(centroid.X - foundation_width/2, centroid.Y - foundation_length/2, currModel_box.Min.Z);
            foundation = new BoundingBox(foundation_origin, new Point3d(foundation_center.X + foundation_width / 2, foundation_center.Y + foundation_length / 2, currModel_box.Min.Z + foundation_height)).ToBrep();
            pcb_origin = new Point3d(foundation_origin.X + pcb_origin_x, foundation_origin.Y + pcb_origin_y, currModel_box.Min.Z + foundation_height);
            pcb_center = new Point3d(pcb_origin.X + pcb_width, pcb_origin.Y + pcb_length, pcb_origin.Z);

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


        public bool GenerateRotationalMotion(ref Item save_item, out List<Brep> subtrahends)
        {
            ratio = save_item.gearEssentials.Speed;
            subtrahends = new List<Brep>();

            //Load cutter
            Guid cutterObjId = save_item.gearEssentials.Cutter;
            RhinoObject cutterObj = myDoc.Objects.FindId(cutterObjId);
            Brep cutter = null;
            if (cutterObj != null && cutterObj.Geometry.ObjectType == ObjectType.Brep)
            {
                cutter = cutterObj.Geometry as Brep;
            }
            else
            {
                RhinoApp.WriteLine("This rotational motion item cannot be created. Due to impatible to other items");
                return false;
            }

            //Load end effector
            Guid endEffectorObjId = save_item.gearEssentials.EndEffector;
            RhinoObject endEffectorObj = myDoc.Objects.FindId(endEffectorObjId);
            Brep endEffector = null;
            if(endEffectorObj != null && endEffectorObj.Geometry.ObjectType == ObjectType.Brep)
            {
                endEffector = endEffectorObj.Geometry as Brep;
            }
            else
            {
                RhinoApp.WriteLine("This rotational motion item cannot be created. Due to impatible to other items");
                return false;
            }

            Brep[] cuttedBrep = Brep.CreateBooleanDifference(currModel, cutter, myDoc.ModelAbsoluteTolerance, false);
            Brep[] cuttedBrep2 = Brep.CreateBooleanDifference(currModel_Hollowed, cutter, myDoc.ModelAbsoluteTolerance, false);
            Brep endEffector_Hollowed = null;

            if (cuttedBrep.Length != 2)
                return false;

            //Update current model
            if (cuttedBrep[0].GetVolume() - endEffector.GetVolume() < 10)
            {
                currModel = cuttedBrep[1];

                if (currModel.GetVolume() > endEffector.GetVolume())
                {
                    if (cuttedBrep2[0].GetVolume() > cuttedBrep2[1].GetVolume())
                    {
                        currModel_Hollowed = cuttedBrep2[0];
                        endEffector_Hollowed = cuttedBrep2[1];
                    }
                    else
                    {
                        currModel_Hollowed = cuttedBrep2[1];
                        endEffector_Hollowed = cuttedBrep2[0];
                    }
                }
                else
                {
                    if (cuttedBrep2[0].GetVolume() > cuttedBrep2[0].GetVolume())
                    {
                        currModel_Hollowed = cuttedBrep2[1];
                        endEffector_Hollowed = cuttedBrep2[0];
                    }
                    else
                    {
                        currModel_Hollowed = cuttedBrep2[0];
                        endEffector_Hollowed = cuttedBrep2[1];
                    }
                }
            }
            else
            {
                currModel = cuttedBrep[0];

                if (cuttedBrep2.Length >= 2)
                {
                    if (currModel.GetVolume() > endEffector.GetVolume())
                    {
                        if (cuttedBrep2[0].GetVolume() > cuttedBrep2[1].GetVolume())
                        {
                            currModel_Hollowed = cuttedBrep2[0];
                            endEffector_Hollowed = cuttedBrep2[1];
                        }
                        else
                        {
                            currModel_Hollowed = cuttedBrep2[1];
                            endEffector_Hollowed = cuttedBrep2[0];
                        }
                    }
                    else
                    {
                        if (cuttedBrep2[0].GetVolume() > cuttedBrep2[0].GetVolume())
                        {
                            currModel_Hollowed = cuttedBrep2[1];
                            endEffector_Hollowed = cuttedBrep2[0];
                        }
                        else
                        {
                            currModel_Hollowed = cuttedBrep2[0];
                            endEffector_Hollowed = cuttedBrep2[1];
                        }
                    }
                }
                else
                {
                    RhinoApp.WriteLine("This rotational motion item cannot be created. Due to impatible to other items");
                    return false;
                }

            }

            myDoc.Objects.Delete(currModelObjId, true);
            myDoc.Views.Redraw();

            currModelObjId = myDoc.Objects.Add(currModel);
            //currModel_Hollowed_ObjId = myDoc.Objects.Add(currModel_Hollowed);
            myDoc.Objects.Hide(currModelObjId, true);
            myDoc.Views.Redraw();

            //myDoc.Objects.Show(endEffectorObjId, true);
            if(endEffector_Hollowed != null)
                myDoc.Objects.Add(endEffector_Hollowed);
            myDoc.Views.Redraw();

            List<Guid> cuttedBrepObjId = new List<Guid>();
            cuttedBrepObjId.Add(currModelObjId);
            cuttedBrepObjId.Add(endEffectorObjId);

            Brep mainModel = currModel.DuplicateBrep();

            allBreps.Clear();
            allBreps_guid.Clear();
            allBreps = getAllBreps();
            allBreps_guid = getAllBrepsGuid();

            #region Start gear(comes with motor)
            BoundingBox mainModel_bbox = mainModel.GetBoundingBox(true);

            //Find central point of the base of the bounding box
            Point3d start_gear_centerPoint = new Point3d(pcb_origin.X + pcb_width/2, foundation_origin.Y, foundation_origin.Z + foundation_height + start_gear_elevation);
            Vector3d start_gear_Direction = new Vector3d(0, 0, 1);
            Vector3d start_gear_xDir = new Vector3d(0, 0, 0);
            int start_gear_teethNum = 20;
            double start_gear_selfRotAngle = 0;

            SpurGear start_gear = new SpurGear(start_gear_centerPoint, start_gear_Direction, start_gear_xDir, start_gear_teethNum, module, pressure_angle, thickness, start_gear_selfRotAngle, true);

            BoundingBox start_gear_bBox = start_gear.Boundingbox;
            Point3d max = new Point3d(start_gear_bBox.Max.X + 1, start_gear_bBox.Max.Y + 1, start_gear_bBox.Max.Z + 3);
            Point3d min = new Point3d(start_gear_bBox.Min.X - 1, start_gear_bBox.Min.Y - 1, start_gear_bBox.Min.Z - 3);
            start_gear_bBox = new BoundingBox(min, max);
            #endregion

            #region Calculate the point of the end effector that extend the pipe to the main model
            Point3d pointConnection1 = new Point3d(0, 0, 0);
            if (Intersection.BrepBrep(endEffector, cutter, myDoc.ModelAbsoluteTolerance, out Curve[] intersectionCurves, out Point3d[] intersectionPoints))
            {
                if (intersectionCurves.Length == 0)
                {
                    RhinoApp.WriteLine("This rotational motion item cannot be created. Due to impatible to other items");
                    return false;
                }

                if (intersectionCurves.Length > 1)
                {
                    RhinoApp.WriteLine("This rotational motion item cannot be created. Due to impatible to other items");
                    return false;
                }

                AreaMassProperties areaMass = AreaMassProperties.Compute(intersectionCurves[0], myDoc.ModelAbsoluteTolerance);
                pointConnection1 = areaMass.Centroid;
            }
            else
            {
                RhinoApp.WriteLine("This rotational motion item cannot be created. Due to impatible to other items");
                return false;
            }
            #endregion

            #region Calculate the direction of the end gear that needs to be facing.
            Vector3d end_gear_dir = new Vector3d(save_item.gearEssentials.CutterPlane.Normal);
            //double distance = pointConnection1.DistanceTo(mainModel.GetBoundingBox(true).Center);
            double distance = pointConnection1.DistanceTo(mainModel.ClosestPoint(pointConnection1)) + 1;
            Line endEffector_rail = new Line(pointConnection1, end_gear_dir, distance);

            if (!currModel.IsManifold || !currModel.IsSolid)
            {
                RhinoApp.WriteLine("This rotational motion item cannot be created. Due to impatible to other items");
                return false;
            }


            if (!currModel.IsPointInside(endEffector_rail.To, myDoc.ModelAbsoluteTolerance, true)) // TODO: We need to make sure if the main model is closed and manifold. Perhaps write a function to fix the original model in the first place.
            {
                end_gear_dir.Reverse();
                endEffector_rail = new Line(pointConnection1, end_gear_dir, distance);
            }
            #endregion

            #region Calculate the cone angle of the end bevel gear and its driven gear
            double end_gear_coneAngle = RhinoMath.ToDegrees(Vector3d.VectorAngle(new Vector3d(0, 0, 1), end_gear_dir));

            myDoc.Views.Redraw();
            #endregion

            if(end_gear_coneAngle == 180 ||  end_gear_coneAngle == 0)
            {
                Brep split = endEffector.Split(cutter, myDoc.ModelAbsoluteTolerance)[0];
                Point3d centroid = AreaMassProperties.Compute(split).Centroid;

                distance = centroid.DistanceTo(new Point3d(mainModel.GetBoundingBox(true).Center.X, mainModel.GetBoundingBox(true).Center.Y, mainModel.GetBoundingBox(true).Min.Z)) / 2;
                endEffector_rail = new Line(centroid, end_gear_dir, distance);
                Curve endEffector_curve = endEffector_rail.ToNurbsCurve().Extend(CurveEnd.Start, CurveExtensionStyle.Line, new[] { endEffector_Hollowed });

                //Connector Gear
                Point3d connector_gear_centerPoint = new Point3d(endEffector_curve.PointAtStart.X, endEffector_curve.PointAtStart.Y, start_gear_centerPoint.Z);
                Vector3d connector_gear_Direction = new Vector3d(0, 0, 1);
                Vector3d connector_gear_xDir = new Vector3d(0, 0, 0);
                int connector_gear_teethNum = (int)(start_gear_teethNum * ratio);
                double connector_gear_selfRotAngle = 0;
                SpurGear connector_gear = new SpurGear(connector_gear_centerPoint, connector_gear_Direction, connector_gear_xDir, connector_gear_teethNum, module, pressure_angle, thickness, connector_gear_selfRotAngle, false);

                //Connect Gear Gaksets
                Point3d startPoint = new Point3d(connector_gear.CenterPoint.X, connector_gear.CenterPoint.Y, connector_gear.Boundingbox.Min.Z - bottom_gasket_gear_height);
                Point3d endPoint = new Point3d(connector_gear.CenterPoint.X, connector_gear.CenterPoint.Y, connector_gear.Boundingbox.Min.Z - gasket_gear_gap);
                Line connector_gear_bottom_gasket_rail = new Line(startPoint, endPoint);
                Brep connector_gear_bottom_gasket = Brep.CreateThickPipe(connector_gear_bottom_gasket_rail.ToNurbsCurve(), gasket_inner_radius, gasket_outer_radius, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];

                startPoint.Z = connector_gear.Boundingbox.Max.Z + top_gasket_gear_height;
                endPoint.Z = connector_gear.Boundingbox.Max.Z + gasket_gear_gap;
                Line connector_gear_top_gasket_rail = new Line(startPoint, endPoint);
                Brep connector_gear_top_gasket = Brep.CreateThickPipe(connector_gear_top_gasket_rail.ToNurbsCurve(), gasket_inner_radius, gasket_outer_radius, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];

                //Shaft between end effector and connector gear
                Line shaft_rail = new Line(endEffector_curve.PointAtStart, new Point3d(connector_gear.CenterPoint.X, connector_gear.CenterPoint.Y, connector_gear.Boundingbox.Min.Z));
                shaft_rail.Extend(0, shaft_extends_from_gear);
                Brep shaft = Brep.CreatePipe(shaft_rail.ToNurbsCurve(), shaft_radius, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];

                //Second Driven Gear
                Point3d second_driven_gear_centerPoint = new Point3d(0, 0, 0);
                Vector3d second_driven_gear_Direction = new Vector3d(0, 0, 1);
                Vector3d second_driven_gear_xDir = new Vector3d(0, 0, 0);
                double second_driven_gear_selfRotAngle = 0;
                double second_driven_gear_tipRadius = (connector_gear_centerPoint.DistanceTo(start_gear_centerPoint) - start_gear.BaseRadius - connector_gear.BaseRadius) / 2;
                int second_driven_gear_teethNum = getNumTeeth(second_driven_gear_tipRadius);


                SpurGear second_driven_gear = null;
                if (second_driven_gear_tipRadius > getTipRadius(4))
                {
                    Line start_gear_connection_rail = new Line(start_gear.CenterPoint, connector_gear.CenterPoint);
                    start_gear_connection_rail = new Line(start_gear.CenterPoint, start_gear_connection_rail.Direction, start_gear.BaseRadius + second_driven_gear_tipRadius);
                    second_driven_gear_centerPoint = start_gear_connection_rail.To;
                    second_driven_gear = new SpurGear(second_driven_gear_centerPoint, second_driven_gear_Direction, second_driven_gear_xDir, second_driven_gear_teethNum, module, pressure_angle, thickness, second_driven_gear_selfRotAngle, true);
                }
                else if (second_driven_gear_tipRadius <= getTipRadius(4) && second_driven_gear_tipRadius > getTipRadius(1))
                {
                    RhinoApp.WriteLine("Fail to create gear on your main model with the selected end effector.2");
                    return false;
                }
                else if (second_driven_gear_tipRadius <= getTipRadius(1))
                {
                    myDoc.Objects.Add(start_gear.Model);
                    myDoc.Objects.Add(shaft);
                    myDoc.Objects.Add(connector_gear.Model);


                    gaskets_guid.Add(myDoc.Objects.Add(connector_gear_bottom_gasket));
                    gaskets_guid.Add(myDoc.Objects.Add(connector_gear_top_gasket));

                    //Create holes on gaskets to allow more water flow
                    List<Curve> rails = new List<Curve>();
                    rails.Add(connector_gear_bottom_gasket_rail.ToNurbsCurve());
                    rails.Add(connector_gear_top_gasket_rail.ToNurbsCurve());


                    for (int i = 0; i < rails.Count; i++)
                    {
                        Curve c = rails[i];

                        Circle hole = new Circle(new Plane(c.PointAtLength(c.GetLength() / 2), c.TangentAtStart), (gasket_outer_radius + gasket_inner_radius) / 2);
                        gasketCircles.Add(hole.ToNurbsCurve());
                    }
                    if (Intersection.CurveBrep(shaft_rail.ToNurbsCurve(), currModel, myDoc.ModelAbsoluteTolerance, out Curve[] overlapCurves, out intersectionPoints, out Double[] curveParameters))
                    {
                        Curve cover_rail = (new Line(intersectionPoints[0], end_gear_dir, shaft_rail.ToNurbsCurve().GetLength() / 10)).ToNurbsCurve();
                        Brep cover = Brep.CreateThickPipe(cover_rail, gasket_inner_radius, gasket_outer_radius, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];

                        Guid guid = myDoc.Objects.Add(cover);
                        gaskets_guid.Add(guid);
                        Circle hole = new Circle(new Plane(cover_rail.PointAtLength(cover_rail.GetLength() / 2), cover_rail.TangentAtStart), (gasket_outer_radius + gasket_inner_radius) / 2);
                        gasketCircles.Add(hole.ToNurbsCurve());
                    }
                }
                else
                {
                    //Rotate the connector gear and second driven gear for perfect matching
                    if (Intersection.BrepBrep(connector_gear.Model, second_driven_gear.Model, myDoc.ModelAbsoluteTolerance, out intersectionCurves, out intersectionPoints))
                    {
                        if (connector_gear.CenterPoint.X > second_driven_gear.CenterPoint.X && connector_gear.CenterPoint.Y < second_driven_gear.CenterPoint.Y)
                        {
                            Line rail5 = new Line(connector_gear.CenterPoint, second_driven_gear.CenterPoint);
                            double angle = RhinoMath.ToDegrees(Vector3d.VectorAngle(new Vector3d(rail5.Direction.X, rail5.Direction.Y, 0), new Vector3d(0, 1, 0)));
                            connector_gear.Rotate(angle);
                            second_driven_gear.Rotate(-180 + angle);
                            connector_gear.Rotate(360 / connector_gear.NumTeeth / 2);
                        }
                        else if (connector_gear.CenterPoint.X > second_driven_gear.CenterPoint.X && connector_gear.CenterPoint.Y > second_driven_gear.CenterPoint.Y)
                        {
                            Line rail5 = new Line(connector_gear.CenterPoint, second_driven_gear.CenterPoint);
                            double angle = RhinoMath.ToDegrees(Vector3d.VectorAngle(new Vector3d(rail5.Direction.X, rail5.Direction.Y, 0), new Vector3d(0, 1, 0)));
                            connector_gear.Rotate(180 - angle);
                            second_driven_gear.Rotate(-angle);
                            connector_gear.Rotate(360 / connector_gear.NumTeeth / 2);
                        }
                        else if (connector_gear.CenterPoint.X < second_driven_gear.CenterPoint.X && connector_gear.CenterPoint.Y > second_driven_gear.CenterPoint.Y)
                        {
                            Line rail5 = new Line(connector_gear.CenterPoint, second_driven_gear.CenterPoint);
                            double angle = RhinoMath.ToDegrees(Vector3d.VectorAngle(new Vector3d(rail5.Direction.X, rail5.Direction.Y, 0), new Vector3d(0, 1, 0)));
                            connector_gear.Rotate(-180 + angle);
                            second_driven_gear.Rotate(angle);
                            second_driven_gear.Rotate(360 / second_driven_gear.NumTeeth / 2);
                        }
                        else if (connector_gear.CenterPoint.X < second_driven_gear.CenterPoint.X && connector_gear.CenterPoint.Y < second_driven_gear.CenterPoint.Y)
                        {
                            Line rail5 = new Line(connector_gear.CenterPoint, second_driven_gear.CenterPoint);
                            double angle = RhinoMath.ToDegrees(Vector3d.VectorAngle(new Vector3d(rail5.Direction.X, rail5.Direction.Y, 0), new Vector3d(0, 1, 0)));
                            connector_gear.Rotate(-angle);
                            second_driven_gear.Rotate(180 - angle);
                            second_driven_gear.Rotate(360 / second_driven_gear.NumTeeth / 2);
                        }
                        else if (connector_gear.CenterPoint.X == second_driven_gear.CenterPoint.X && connector_gear.CenterPoint.Y < second_driven_gear.CenterPoint.Y)
                        {
                            second_driven_gear.Rotate(180);
                            second_driven_gear.Rotate(360 / second_driven_gear.NumTeeth / 2);
                        }
                        else
                        {
                            connector_gear.Rotate(180);
                            connector_gear.Rotate(360 / connector_gear.NumTeeth / 2);
                        }
                    }

                    //Second driven gear gasket and shaft
                    Line second_driven_top_gasket_rail = new Line();
                    Line second_driven_bottom_gasket_rail = new Line();
                    Line second_driven_shaft_rail = new Line();
                    Line second_driven_shaft_clearance_rail = new Line();
                    if (second_driven_gear != null)
                    {
                        //Model
                        while (Intersection.BrepBrep(second_driven_gear.Model, connector_gear.Model, myDoc.ModelAbsoluteTolerance, out intersectionCurves, out intersectionPoints))
                        {
                            if (intersectionCurves != null || intersectionPoints != null)
                            {
                                if (intersectionCurves.Length > 0 || intersectionPoints.Length > 0)
                                {
                                    second_driven_gear.Rotate(1);
                                }
                                else
                                {
                                    break;
                                }
                            }
                            else
                            {
                                break;
                            }
                        }

                        //Gasket
                        Point3d start_point = new Point3d(second_driven_gear.CenterPoint);
                        start_point.Z = second_driven_gear.Model.GetBoundingBox(true).Min.Z - gasket_gear_gap;
                        Point3d end_point = new Point3d(second_driven_gear.CenterPoint);
                        end_point.Z = second_driven_gear.Model.GetBoundingBox(true).Min.Z - bottom_gasket_gear_height;
                        second_driven_bottom_gasket_rail = new Line(start_point, end_point);

                        Brep second_driven_gear_bottom_gasket = Brep.CreateThickPipe(second_driven_bottom_gasket_rail.ToNurbsCurve(), gasket_inner_radius, gasket_outer_radius, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];
                        Guid guid = myDoc.Objects.Add(second_driven_gear_bottom_gasket);
                        gaskets_guid.Add(guid);
                        Circle hole = new Circle(new Plane(second_driven_bottom_gasket_rail.PointAtLength(second_driven_bottom_gasket_rail.Length / 2), new Vector3d(0, 0, 1)), (gasket_outer_radius + gasket_inner_radius) / 2);
                        gasketCircles.Add(hole.ToNurbsCurve());

                        start_point.Z = second_driven_gear.Model.GetBoundingBox(true).Max.Z + gasket_gear_gap;
                        end_point.Z = second_driven_gear.Model.GetBoundingBox(true).Max.Z + top_gasket_gear_height;
                        second_driven_top_gasket_rail = new Line(start_point, end_point);
                        Brep second_driven_top_gasket = Brep.CreateThickPipe(second_driven_top_gasket_rail.ToNurbsCurve(), gasket_inner_radius, gasket_outer_radius, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];
                        guid = myDoc.Objects.Add(second_driven_top_gasket);
                        gaskets_guid.Add(guid);
                        //myDoc.Objects.AddCurve(second_driven_top_gasket_rail.ToNurbsCurve());
                        hole = new Circle(new Plane(second_driven_top_gasket_rail.PointAtLength(second_driven_top_gasket_rail.Length / 2), new Vector3d(0, 0, 1)), (gasket_outer_radius + gasket_inner_radius) / 2);

                        gasketCircles.Add(hole.ToNurbsCurve());


                        //shaft
                        start_point.Z = second_driven_gear.Model.GetBoundingBox(true).Max.Z + shaft_extends_from_gear;
                        end_point.Z = second_driven_gear.Model.GetBoundingBox(true).Min.Z - shaft_extends_from_gear;
                        second_driven_shaft_rail = new Line(start_point, end_point);
                        Brep second_driven_gear_shaft = Brep.CreatePipe(second_driven_shaft_rail.ToNurbsCurve(), shaft_radius, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];
                        myDoc.Objects.Add(second_driven_gear_shaft);
                    }

                    myDoc.Objects.Add(start_gear.Model);
                    myDoc.Objects.Add(shaft);
                    myDoc.Objects.Add(connector_gear.Model);
                    myDoc.Objects.Add(second_driven_gear.Model);

                    gaskets_guid.Add(myDoc.Objects.Add(connector_gear_bottom_gasket));
                    gaskets_guid.Add(myDoc.Objects.Add(connector_gear_top_gasket));

                    //Create holes on gaskets to allow more water flow
                    List<Curve> rails = new List<Curve>();
                    rails.Add(connector_gear_bottom_gasket_rail.ToNurbsCurve());
                    rails.Add(connector_gear_top_gasket_rail.ToNurbsCurve());


                    for (int i = 0; i < rails.Count; i++)
                    {
                        Curve c = rails[i];

                        Circle hole = new Circle(new Plane(c.PointAtLength(c.GetLength() / 2), c.TangentAtStart), (gasket_outer_radius + gasket_inner_radius) / 2);
                        gasketCircles.Add(hole.ToNurbsCurve());
                    }
                    if (Intersection.CurveBrep(shaft_rail.ToNurbsCurve(), currModel, myDoc.ModelAbsoluteTolerance, out Curve[] overlapCurves, out intersectionPoints, out Double[] curveParameters))
                    {
                        Curve cover_rail = (new Line(intersectionPoints[0], end_gear_dir, shaft_rail.ToNurbsCurve().GetLength() / 10)).ToNurbsCurve();
                        Brep cover = Brep.CreateThickPipe(cover_rail, gasket_inner_radius, gasket_outer_radius, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];

                        Guid guid = myDoc.Objects.Add(cover);
                        gaskets_guid.Add(guid);
                        Circle hole = new Circle(new Plane(cover_rail.PointAtLength(cover_rail.GetLength() / 2), cover_rail.TangentAtStart), (gasket_outer_radius + gasket_inner_radius) / 2);
                        gasketCircles.Add(hole.ToNurbsCurve());
                    }
                }
            }
            else
            {
                #region if cone angle is greater than 90, use a special set of bevel gear ---------> To be implemented
                bool isReversed = false;
                if (end_gear_coneAngle > 90)
                {
                    end_gear_coneAngle = 180 - end_gear_coneAngle;
                    isReversed = true;
                    //isReversed = false;
                }
                #endregion

                #region end gear
                //Create the gear
                Vector3d end_gear_Direction = new Vector3d(end_gear_dir);
                if (isReversed)
                {
                    end_gear_Direction.Reverse();
                    endEffector_rail.Extend(0, 2.6);
                }

                Point3d end_gear_centerPoint = endEffector_rail.To;
                Vector3d end_gear_xDir = new Vector3d(0, 0, 0);
                int end_gear_teethNum = 15;
                double end_gear_selfRotAngle = 0;
                BevelGear end_gear = new BevelGear(end_gear_centerPoint, end_gear_Direction, end_gear_xDir, end_gear_teethNum, module, pressure_angle, thickness, end_gear_selfRotAngle, end_gear_coneAngle, false);
                #endregion

                #region driven gear of end gear
                //Calculate the vector that is perpendicular to the end gear facing direction
                Vector3d orthogonal = GetOrthogonalWithMinZ(end_gear_Direction);

                //Get the line that is along the xy axis that has the same direction of the end gear facing direction
                Line rail1 = new Line(endEffector_rail.To, orthogonal, end_gear.PitchRadius); //A line that is paralle to the end gear and through the end gear's center point
                Line rail2 = new Line(end_gear_centerPoint, end_gear_Direction, 100); // A line that has the direction of the end gear facing direction
                Line rail3 = new Line(end_gear_centerPoint, new Vector3d(rail2.Direction.X, rail2.Direction.Y, 0));//A line that is along the xy plane that has the same direction of the end gear facing direction

                //Move rail3 to minimum z of the end gear
                double length = end_gear_centerPoint.Z - end_gear.Model.GetBoundingBox(true).Min.Z;
                Transform transform = Transform.Translation(new Vector3d(0, 0, -length));
                rail3.Transform(transform);

                //Find the center point of the gear
                int first_driven_gear_teethNum = 15;
                double first_driven_gear_pitchRadius = getPitchRadius(first_driven_gear_teethNum) + clearance;

                Line rail5 = new Line(rail1.To, new Vector3d(rail2.Direction.X, rail2.Direction.Y, 0), first_driven_gear_pitchRadius);


                Point3d first_driven_gear_centerPoint = rail5.To;
                Vector3d first_driven_gear_Direction = new Vector3d(0, 0, 1);
                Vector3d first_driven_gear_xDir = new Vector3d(0, 0, 0);
                double first_driven_gear_selfRotAngle = 0;
                if (first_driven_gear_centerPoint.X > end_gear_centerPoint.X && first_driven_gear_centerPoint.Y < end_gear_centerPoint.Y)
                {
                    //RhinoApp.WriteLine("first_driven_gear_centerPoint At fourth axis");
                    first_driven_gear_selfRotAngle = -RhinoMath.ToDegrees(Vector3d.VectorAngle(new Vector3d(rail5.Direction.X, rail5.Direction.Y, 0), new Vector3d(1, 0, 0)));
                    end_gear.Rotate(first_driven_gear_selfRotAngle - 360 / first_driven_gear_teethNum / 2);
                }
                else if (first_driven_gear_centerPoint.X > end_gear_centerPoint.X && first_driven_gear_centerPoint.Y > end_gear_centerPoint.Y)
                {
                    //RhinoApp.WriteLine("first_driven_gear_centerPoint At first axis");
                    first_driven_gear_selfRotAngle = RhinoMath.ToDegrees(Vector3d.VectorAngle(new Vector3d(rail5.Direction.X, rail5.Direction.Y, 0), new Vector3d(1, 0, 0)));
                    end_gear.Rotate(first_driven_gear_selfRotAngle - 360 / first_driven_gear_teethNum / 2);
                }
                else if (first_driven_gear_centerPoint.X < end_gear_centerPoint.X && first_driven_gear_centerPoint.Y < end_gear_centerPoint.Y)
                {
                    //RhinoApp.WriteLine("first_driven_gear_centerPoint At third axis");
                    first_driven_gear_selfRotAngle = -RhinoMath.ToDegrees(Vector3d.VectorAngle(new Vector3d(rail5.Direction.X, rail5.Direction.Y, 0), new Vector3d(1, 0, 0)));
                    end_gear.Rotate(first_driven_gear_selfRotAngle - 360 / first_driven_gear_teethNum / 2);
                }
                else if (first_driven_gear_centerPoint.X < end_gear_centerPoint.X && first_driven_gear_centerPoint.Y > end_gear_centerPoint.Y)
                {
                    //RhinoApp.WriteLine("first_driven_gear_centerPoint At second axis");
                    first_driven_gear_selfRotAngle = RhinoMath.ToDegrees(Vector3d.VectorAngle(new Vector3d(rail5.Direction.X, rail5.Direction.Y, 0), new Vector3d(1, 0, 0)));
                    end_gear.Rotate(first_driven_gear_selfRotAngle - 360 / first_driven_gear_teethNum / 2);
                }
                //RhinoApp.WriteLine($"First driven gear rotated {RhinoMath.ToDegrees(Vector3d.VectorAngle(new Vector3d(rail5.Direction.X, rail5.Direction.Y, 0), new Vector3d(1, 0, 0)))} degrees");
                double first_driven_gear_coneAngle = end_gear_coneAngle;
                BevelGear first_driven_gear = new BevelGear(first_driven_gear_centerPoint, first_driven_gear_Direction, first_driven_gear_xDir, first_driven_gear_teethNum, module, pressure_angle, thickness, first_driven_gear_selfRotAngle, first_driven_gear_coneAngle, false);
                #endregion

                #region connector gear (The gear that connects start gear and driven gear of end gear)
                Point3d connector_gear_centerPoint = new Point3d(rail5.To.X, rail5.To.Y, start_gear_centerPoint.Z);
                Vector3d connector_gear_Direction = new Vector3d(0, 0, 1);
                Vector3d connector_gear_xDir = new Vector3d(0, 0, 0);
                int connector_gear_teethNum = (int)(start_gear_teethNum * ratio);

                ////Calculate the connector_gear_teethNum
                //double connector_gear_tipRadius = connector_gear_centerPoint.DistanceTo(start_gear_centerPoint) - start_gear.BaseRadius;
                //connector_gear_teethNum = getNumTeeth(connector_gear_tipRadius);

                double connector_gear_selfRotAngle = 0;
                SpurGear connector_gear = new SpurGear(connector_gear_centerPoint, connector_gear_Direction, connector_gear_xDir, connector_gear_teethNum, module, pressure_angle, thickness, connector_gear_selfRotAngle, false);
                #endregion

                #region second driven gear
                Point3d second_driven_gear_centerPoint = new Point3d(0, 0, 0);
                Vector3d second_driven_gear_Direction = new Vector3d(0, 0, 1);
                Vector3d second_driven_gear_xDir = new Vector3d(0, 0, 0);
                double second_driven_gear_selfRotAngle = 0;
                double second_driven_gear_tipRadius = (connector_gear_centerPoint.DistanceTo(start_gear_centerPoint) - start_gear.BaseRadius - connector_gear.BaseRadius) / 2;
                int second_driven_gear_teethNum = getNumTeeth(second_driven_gear_tipRadius);

                SpurGear second_driven_gear = null;
                if (second_driven_gear_tipRadius > getTipRadius(8))
                {
                    Line start_gear_connection_rail = new Line(start_gear.CenterPoint, connector_gear.CenterPoint);
                    start_gear_connection_rail = new Line(start_gear.CenterPoint, start_gear_connection_rail.Direction, start_gear.BaseRadius + second_driven_gear_tipRadius);
                    second_driven_gear_centerPoint = start_gear_connection_rail.To;
                    second_driven_gear = new SpurGear(second_driven_gear_centerPoint, second_driven_gear_Direction, second_driven_gear_xDir, second_driven_gear_teethNum, module, pressure_angle, thickness, second_driven_gear_selfRotAngle, true);
                }
                #endregion

                #region shafts of first driven gear and connector gear
                Line shaft_rail = new Line(new Point3d(first_driven_gear.CenterPoint.X, first_driven_gear.CenterPoint.Y, first_driven_gear.Boundingbox.Max.Z), new Point3d(first_driven_gear.CenterPoint.X, first_driven_gear.CenterPoint.Y, connector_gear.Boundingbox.Min.Z));
                shaft_rail.Extend(shaft_extends_from_gear, shaft_extends_from_gear);
                Brep shaft = Brep.CreatePipe(shaft_rail.ToNurbsCurve(), shaft_radius, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];
                Line shaft_clearance_rail = shaft_rail;
                shaft_clearance_rail.Extend(shaft_extends_from_gear, shaft_extends_from_gear);
                Brep shaft_clearance = Brep.CreatePipe(shaft_clearance_rail.ToNurbsCurve(), clearance_shaft_radius, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];
                #endregion

                #region gaskets for first driven gear and connector gear
                //Gaskets of first driven gear
                Point3d startPoint = new Point3d(first_driven_gear.CenterPoint.X, first_driven_gear.CenterPoint.Y, first_driven_gear.Boundingbox.Min.Z - bottom_gasket_gear_height);
                Point3d endPoint = new Point3d(first_driven_gear.CenterPoint.X, first_driven_gear.CenterPoint.Y, first_driven_gear.Boundingbox.Min.Z - gasket_gear_gap);
                Line first_driven_gear_bottom_gasket_rail = new Line(startPoint, endPoint);
                Brep first_driven_gear_bottom_gasket = Brep.CreateThickPipe(first_driven_gear_bottom_gasket_rail.ToNurbsCurve(), gasket_inner_radius, gasket_outer_radius, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];

                startPoint.Z = first_driven_gear.Boundingbox.Max.Z + top_gasket_gear_height;
                endPoint.Z = first_driven_gear.Boundingbox.Max.Z + gasket_gear_gap;
                Line first_driven_gear_top_gasket_rail = new Line(startPoint, endPoint);
                Brep first_driven_gear_top_gasket = Brep.CreateThickPipe(first_driven_gear_top_gasket_rail.ToNurbsCurve(), gasket_inner_radius, gasket_outer_radius, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];

                //Gaskets of connector gear
                startPoint = new Point3d(connector_gear.CenterPoint.X, connector_gear.CenterPoint.Y, connector_gear.Boundingbox.Min.Z - bottom_gasket_gear_height);
                endPoint = new Point3d(connector_gear.CenterPoint.X, connector_gear.CenterPoint.Y, connector_gear.Boundingbox.Min.Z - gasket_gear_gap);
                Line connector_gear_bottom_gasket_rail = new Line(startPoint, endPoint);
                Brep connector_gear_bottom_gasket = Brep.CreateThickPipe(connector_gear_bottom_gasket_rail.ToNurbsCurve(), gasket_inner_radius, gasket_outer_radius, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];

                startPoint.Z = connector_gear.Boundingbox.Max.Z + top_gasket_gear_height;
                endPoint.Z = connector_gear.Boundingbox.Max.Z + gasket_gear_gap;
                Line connector_gear_top_gasket_rail = new Line(startPoint, endPoint);
                Brep connector_gear_top_gasket = Brep.CreateThickPipe(connector_gear_top_gasket_rail.ToNurbsCurve(), gasket_inner_radius, gasket_outer_radius, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];
                #endregion

                #region gaskets and shafts for end gear
                Line extended_endEffector_rail = endEffector_rail;
                extended_endEffector_rail.Extend(100, 100);

                Brep end_gear_bottom_gasket = null;
                Brep end_gear_top_gasket = null;
                Brep end_gear_shaft = null;
                Brep end_gear_clearance_shaft = null;
                Line end_gear_shaft_rail = new Line();
                Line end_gear_shaft_clearance_rail = new Line();
                Line end_gear_bottom_gakset_rail = new Line();
                Line end_gear_top_gasket_rail = new Line();
                if (Intersection.CurveBrep(extended_endEffector_rail.ToNurbsCurve(), end_gear.Boundingbox_big, myDoc.ModelAbsoluteTolerance, out _, out intersectionPoints))
                {
                    Vector3d dir = endEffector_rail.Direction;
                    end_gear_bottom_gakset_rail = new Line(intersectionPoints[0], dir, bottom_gasket_gear_height - gasket_gear_gap);
                    dir.Reverse();
                    end_gear_top_gasket_rail = new Line(intersectionPoints[1], dir, top_gasket_gear_height - gasket_gear_gap);
                    end_gear_bottom_gasket = Brep.CreateThickPipe(end_gear_bottom_gakset_rail.ToNurbsCurve(), gasket_inner_radius, gasket_outer_radius, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];
                    end_gear_top_gasket = Brep.CreateThickPipe(end_gear_top_gasket_rail.ToNurbsCurve(), gasket_inner_radius, gasket_outer_radius, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];
                    //myDoc.Objects.Add(end_gear_bottom_gasket);
                    //myDoc.Objects.Add(end_gear_top_gasket);

                    distance = pointConnection1.DistanceTo(intersectionPoints[1]) - 1;
                    end_gear_shaft_rail = new Line(pointConnection1, end_gear_dir, distance);
                    end_gear_shaft = Brep.CreatePipe(end_gear_shaft_rail.ToNurbsCurve(), shaft_radius, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];

                    distance += 2;
                    end_gear_shaft_clearance_rail = new Line(pointConnection1, end_gear_dir, distance);
                    end_gear_clearance_shaft = Brep.CreatePipe(end_gear_shaft_clearance_rail.ToNurbsCurve(), clearance_shaft_radius, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];
                    //mainModel = Brep.CreateBooleanDifference(mainModel, end_gear_shaft, myDoc.ModelAbsoluteTolerance, false)[0];
                    //mainModel = Brep.CreateBooleanDifference(mainModel, end_gear_clearance_shaft, myDoc.ModelAbsoluteTolerance, false)[0];
                }
                #endregion


                List<GearSet> workable_gearsets = new List<GearSet>();

                #region keep pushing the end gear inside of the model until it doesn't intersect with the model and it will be placed on the correct location where the connector gear will be appropriate in size and 
                while (Intersection.BrepBrep(end_gear.Boundingbox_big, mainModel, myDoc.ModelAbsoluteTolerance, out intersectionCurves, out intersectionPoints) && Intersection.BrepBrep(end_gear.Model, start_gear.Model, myDoc.ModelAbsoluteTolerance, out Curve[] intersectionCurves2, out Point3d[] intersectionPoints2)
                    && Intersection.BrepBrep(first_driven_gear.Boundingbox_big, mainModel, myDoc.ModelAbsoluteTolerance, out Curve[] intersectionCurves3, out Point3d[] intersectionPoints3) && Intersection.BrepBrep(connector_gear.Boundingbox_big, mainModel, myDoc.ModelAbsoluteTolerance, out Curve[] intersectionCurves4, out Point3d[] intersectionPoints4))
                {
                    //if all gears are in good condition, then stop the pushing action and show gears
                    if (intersectionCurves.Length == 0 && intersectionPoints.Length == 0 && intersectionCurves2.Length == 0 && intersectionPoints2.Length == 0 && intersectionCurves3.Length == 0 && intersectionPoints3.Length == 0 &&
                        intersectionCurves4.Length == 0 && intersectionPoints4.Length == 0 && (first_driven_gear.Boundingbox.Min.Z - start_gear_centerPoint.Z) > thickness && connector_gear_centerPoint.Equals(new Point3d(first_driven_gear.CenterPoint.X, first_driven_gear.CenterPoint.Y, start_gear_centerPoint.Z)) &&
                        !checkIntersection(shaft, cuttedBrepObjId) && !checkIntersection(first_driven_gear_bottom_gasket, cuttedBrepObjId) && !checkIntersection(first_driven_gear_top_gasket, cuttedBrepObjId) && !checkIntersection(connector_gear_bottom_gasket, cuttedBrepObjId) &&
                        !checkIntersection(connector_gear_top_gasket, cuttedBrepObjId) && !checkIntersection(end_gear_shaft, cuttedBrepObjId) && !checkIntersection(end_gear_top_gasket, cuttedBrepObjId) && !checkIntersection(end_gear_bottom_gasket, cuttedBrepObjId) &&
                        !checkIntersection(end_gear.Model, cuttedBrepObjId) && !checkIntersection(first_driven_gear.Model, cuttedBrepObjId) && !checkIntersection(connector_gear.Model, cuttedBrepObjId))
                    {
                        if (second_driven_gear != null)
                        {
                            Intersection.BrepBrep(second_driven_gear.Boundingbox_big, mainModel, myDoc.ModelAbsoluteTolerance, out intersectionCurves, out intersectionPoints);
                            if (intersectionCurves.Length == 0 && intersectionPoints.Length == 0 && !checkIntersection(second_driven_gear.Model, cuttedBrepObjId))
                            {
                                if (IsBrepInsideBrep(first_driven_gear.Boundingbox_big, mainModel) && IsBrepInsideBrep(connector_gear.Boundingbox_big, mainModel) && IsBrepInsideBrep(end_gear.Boundingbox_big, mainModel))
                                {
                                    GearSet gearSet = new GearSet();
                                    gearSet.EndGear = end_gear;
                                    gearSet.FirstDrivenGear = first_driven_gear;
                                    gearSet.ConnectorGear = connector_gear;
                                    gearSet.SecondDrivenGear = second_driven_gear;
                                    gearSet.EndGearShaft = end_gear_shaft;
                                    gearSet.EndGearTopGasket = end_gear_top_gasket;
                                    gearSet.EndGearBottomGasket = end_gear_bottom_gasket;
                                    gearSet.FirstDrivenGearTopGasket = first_driven_gear_top_gasket;
                                    gearSet.FirstDrivenGearBottomGasket = first_driven_gear_bottom_gasket;
                                    gearSet.ConnectorGearTopGasket = connector_gear_top_gasket;
                                    gearSet.ConnectorGearBottomGasket = connector_gear_bottom_gasket;
                                    gearSet.Shaft = shaft;
                                    gearSet.SecondDrivenGear = second_driven_gear;
                                    gearSet.EndGearShaftClearance = end_gear_clearance_shaft;
                                    gearSet.ShaftClearance = shaft_clearance;
                                    gearSet.ShaftRail = shaft_rail.ToNurbsCurve();
                                    gearSet.EndGearShaftRail = end_gear_shaft_rail.ToNurbsCurve();
                                    gearSet.EndGearBottomGasketRail = end_gear_bottom_gakset_rail.ToNurbsCurve();
                                    gearSet.EndGearTopGasketRail = end_gear_top_gasket_rail.ToNurbsCurve();
                                    gearSet.FirstDrivenGearBottomGasketRail = first_driven_gear_bottom_gasket_rail.ToNurbsCurve();
                                    gearSet.FirstDrivenGearTopGasketRail = first_driven_gear_top_gasket_rail.ToNurbsCurve();
                                    gearSet.ConnectorGearBottomGasketRail = connector_gear_bottom_gasket_rail.ToNurbsCurve();
                                    gearSet.ConnectorGearTopGasketRail = connector_gear_top_gasket_rail.ToNurbsCurve();
                                    gearSet.EndGearShaftClearanceRail = end_gear_shaft_clearance_rail.ToNurbsCurve();
                                    gearSet.ShaftClearanceRail = shaft_clearance_rail.ToNurbsCurve();
                                    workable_gearsets.Add(gearSet);
                                }
                            }
                        }
                        else
                        {
                            //Check if connector gear and start gear is matched
                            double tipRadius = connector_gear.CenterPoint.DistanceTo(start_gear_centerPoint) - start_gear.BaseRadius;


                            if (tipRadius == connector_gear.TipRadius && IsBrepInsideBrep(first_driven_gear.Boundingbox_big, mainModel) && IsBrepInsideBrep(connector_gear.Boundingbox_big, mainModel) && IsBrepInsideBrep(end_gear.Boundingbox_big, mainModel))
                            {
                                GearSet gearSet = new GearSet();
                                gearSet.EndGear = end_gear;
                                gearSet.FirstDrivenGear = first_driven_gear;
                                gearSet.ConnectorGear = connector_gear;
                                gearSet.SecondDrivenGear = second_driven_gear;
                                gearSet.EndGearShaft = end_gear_shaft;
                                gearSet.EndGearTopGasket = end_gear_top_gasket;
                                gearSet.EndGearBottomGasket = end_gear_bottom_gasket;
                                gearSet.FirstDrivenGearTopGasket = first_driven_gear_top_gasket;
                                gearSet.FirstDrivenGearBottomGasket = first_driven_gear_bottom_gasket;
                                gearSet.ConnectorGearTopGasket = connector_gear_top_gasket;
                                gearSet.ConnectorGearBottomGasket = connector_gear_bottom_gasket;
                                gearSet.Shaft = shaft;
                                gearSet.SecondDrivenGear = second_driven_gear;
                                gearSet.EndGearShaftClearance = end_gear_clearance_shaft;
                                gearSet.ShaftClearance = shaft_clearance;
                                gearSet.ShaftRail = shaft_rail.ToNurbsCurve();
                                gearSet.EndGearShaftRail = end_gear_shaft_rail.ToNurbsCurve();
                                gearSet.EndGearShaftRail = end_gear_shaft_rail.ToNurbsCurve();
                                gearSet.EndGearBottomGasketRail = end_gear_bottom_gakset_rail.ToNurbsCurve();
                                gearSet.EndGearTopGasketRail = end_gear_top_gasket_rail.ToNurbsCurve();
                                gearSet.FirstDrivenGearBottomGasketRail = first_driven_gear_bottom_gasket_rail.ToNurbsCurve();
                                gearSet.FirstDrivenGearTopGasketRail = first_driven_gear_top_gasket_rail.ToNurbsCurve();
                                gearSet.ConnectorGearBottomGasketRail = connector_gear_bottom_gasket_rail.ToNurbsCurve();
                                gearSet.ConnectorGearTopGasketRail = connector_gear_top_gasket_rail.ToNurbsCurve();
                                gearSet.EndGearShaftClearanceRail = end_gear_shaft_clearance_rail.ToNurbsCurve();
                                gearSet.ShaftClearanceRail = shaft_clearance_rail.ToNurbsCurve();
                                workable_gearsets.Add(gearSet);
                            }
                        }
                    }
                    if (!IsBrepInsideBrep(first_driven_gear.Boundingbox_big, mainModel) && !IsBrepInsideBrep(connector_gear.Boundingbox_big, mainModel) && !IsBrepInsideBrep(end_gear.Boundingbox_big, mainModel))
                    {
                        if (workable_gearsets.Count > 0)
                            break;
                        RhinoApp.WriteLine("This rotational motion item cannot be created. Due to impatible to other items");
                        return false;
                    }
                    if (!IsBrepInsideBrep(first_driven_gear.Boundingbox_big, mainModel) && !IsBrepInsideBrep(connector_gear.Boundingbox_big, mainModel) && IsBrepInsideBrep(end_gear.Boundingbox_big, mainModel) && !connector_gear.CenterPoint.Equals(new Point3d(first_driven_gear.CenterPoint.X, first_driven_gear.CenterPoint.Y, start_gear_centerPoint.Z)))
                    {
                        RhinoApp.WriteLine("This rotational motion item cannot be created. Due to impatible to other items");
                        return false;
                    }

                    //Pushing the end gear
                    endEffector_rail.Extend(0, 1);
                    end_gear_centerPoint = endEffector_rail.To;
                    end_gear = new BevelGear(end_gear);
                    end_gear.Translate(end_gear_centerPoint);

                    //Predict driven gear's location and see if the connector gear is appropriate
                    Vector3d orthogonal_temp = GetOrthogonalWithMinZ(end_gear_Direction);
                    rail1 = new Line(end_gear_centerPoint, orthogonal, end_gear.PitchRadius); //A line that is paralle to the end gear and through the end gear's center point
                    rail2 = new Line(end_gear_centerPoint, end_gear_Direction, 100); // A line that has the direction of the end gear facing direction
                    rail3 = new Line(end_gear_centerPoint, new Vector3d(rail2.Direction.X, rail2.Direction.Y, 0));

                    length = end_gear_centerPoint.Z - end_gear.Boundingbox.Min.Z;
                    transform = Transform.Translation(new Vector3d(0, 0, -length));
                    rail3.Transform(transform);

                    //Adjust first driven gear
                    rail5 = new Line(rail1.To, new Vector3d(rail2.Direction.X, rail2.Direction.Y, 0), first_driven_gear_pitchRadius);
                    first_driven_gear = new BevelGear(first_driven_gear);
                    first_driven_gear.Translate(rail5.To);

                    //Adjust connector gear
                    connector_gear_centerPoint = new Point3d(rail5.To.X, rail5.To.Y, start_gear_centerPoint.Z);
                    connector_gear = new SpurGear(connector_gear);
                    connector_gear.Translate(connector_gear_centerPoint);

                    //Adjust second driven gear if needed
                    second_driven_gear_tipRadius = (connector_gear.CenterPoint.DistanceTo(start_gear_centerPoint) - start_gear.BaseRadius - connector_gear.BaseRadius) / 2;
                    if (second_driven_gear_tipRadius > getTipRadius(8))
                    {
                        second_driven_gear_teethNum = getNumTeeth(second_driven_gear_tipRadius);
                        Line start_gear_connection_rail = new Line(start_gear.CenterPoint, connector_gear.CenterPoint);
                        start_gear_connection_rail = new Line(start_gear.CenterPoint, start_gear_connection_rail.Direction, start_gear.BaseRadius + second_driven_gear_tipRadius);
                        second_driven_gear_centerPoint = start_gear_connection_rail.To;
                        second_driven_gear = new SpurGear(second_driven_gear_centerPoint, second_driven_gear_Direction, second_driven_gear_xDir, second_driven_gear_teethNum, module, pressure_angle, thickness, second_driven_gear_selfRotAngle, true);
                    }
                    else
                    {
                        second_driven_gear = null;
                    }

                    //Adjust the shaft and gaskets for connector gear and first driven gear
                    shaft_rail = new Line(new Point3d(first_driven_gear.CenterPoint.X, first_driven_gear.CenterPoint.Y, first_driven_gear.Boundingbox.Max.Z), new Point3d(first_driven_gear.CenterPoint.X, first_driven_gear.CenterPoint.Y, connector_gear.Boundingbox.Min.Z));
                    shaft_rail.Extend(shaft_extends_from_gear, shaft_extends_from_gear);
                    shaft = Brep.CreatePipe(shaft_rail.ToNurbsCurve(), shaft_radius, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];
                    shaft_clearance_rail = shaft_rail;
                    shaft_clearance_rail.Extend(shaft_extends_from_gear, shaft_extends_from_gear);
                    shaft_clearance = Brep.CreatePipe(shaft_clearance_rail.ToNurbsCurve(), clearance_shaft_radius, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];

                    startPoint = new Point3d(first_driven_gear.CenterPoint.X, first_driven_gear.CenterPoint.Y, first_driven_gear.Boundingbox.Min.Z - bottom_gasket_gear_height);
                    endPoint = new Point3d(first_driven_gear.CenterPoint.X, first_driven_gear.CenterPoint.Y, first_driven_gear.Boundingbox.Min.Z - gasket_gear_gap);
                    first_driven_gear_bottom_gasket_rail = new Line(startPoint, endPoint);
                    first_driven_gear_bottom_gasket = Brep.CreateThickPipe(first_driven_gear_bottom_gasket_rail.ToNurbsCurve(), gasket_inner_radius, gasket_outer_radius, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];

                    startPoint.Z = first_driven_gear.Boundingbox.Max.Z + top_gasket_gear_height;
                    endPoint.Z = first_driven_gear.Boundingbox.Max.Z + gasket_gear_gap;
                    first_driven_gear_top_gasket_rail = new Line(startPoint, endPoint);
                    first_driven_gear_top_gasket = Brep.CreateThickPipe(first_driven_gear_top_gasket_rail.ToNurbsCurve(), gasket_inner_radius, gasket_outer_radius, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];

                    //Gaskets of connector gear
                    startPoint = new Point3d(connector_gear.CenterPoint.X, connector_gear.CenterPoint.Y, connector_gear.Boundingbox.Min.Z - bottom_gasket_gear_height);
                    endPoint = new Point3d(connector_gear.CenterPoint.X, connector_gear.CenterPoint.Y, connector_gear.Boundingbox.Min.Z - gasket_gear_gap);
                    connector_gear_bottom_gasket_rail = new Line(startPoint, endPoint);
                    connector_gear_bottom_gasket = Brep.CreateThickPipe(connector_gear_bottom_gasket_rail.ToNurbsCurve(), gasket_inner_radius, gasket_outer_radius, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];

                    startPoint.Z = connector_gear.Boundingbox.Max.Z + top_gasket_gear_height;
                    endPoint.Z = connector_gear.Boundingbox.Max.Z + gasket_gear_gap;
                    connector_gear_top_gasket_rail = new Line(startPoint, endPoint);
                    connector_gear_top_gasket = Brep.CreateThickPipe(connector_gear_top_gasket_rail.ToNurbsCurve(), gasket_inner_radius, gasket_outer_radius, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];

                    //Adjust the shaft and gasket for end gear 
                    extended_endEffector_rail = endEffector_rail;
                    extended_endEffector_rail.Extend(100, 100);
                    end_gear_bottom_gasket = null;
                    end_gear_top_gasket = null;
                    end_gear_shaft = null;
                    end_gear_clearance_shaft = null;
                    if (Intersection.CurveBrep(extended_endEffector_rail.ToNurbsCurve(), end_gear.Boundingbox_big, myDoc.ModelAbsoluteTolerance, out intersectionCurves, out intersectionPoints))
                    {
                        Vector3d dir = new Vector3d(endEffector_rail.Direction);

                        Point3d bottomPoint = intersectionPoints[0];
                        Point3d topPoint = intersectionPoints[1];
                        if (intersectionPoints[0].DistanceToSquared(pointConnection1) > intersectionPoints[1].DistanceToSquared(pointConnection1))
                        {
                            bottomPoint = intersectionPoints[1];
                            topPoint = intersectionPoints[0];
                        }


                        end_gear_bottom_gakset_rail = new Line(bottomPoint, dir, bottom_gasket_gear_height - gasket_gear_gap);
                        end_gear_bottom_gakset_rail.Extend(2, -2);
                        dir.Reverse();
                        end_gear_top_gasket_rail = new Line(topPoint, dir, top_gasket_gear_height - gasket_gear_gap);


                        if (isReversed)
                        {
                            dir.Reverse();
                            end_gear_bottom_gakset_rail = new Line(bottomPoint, dir, top_gasket_gear_height - gasket_gear_gap);
                            dir.Reverse();
                            end_gear_top_gasket_rail = new Line(topPoint, dir, bottom_gasket_gear_height - gasket_gear_gap);
                            end_gear_top_gasket_rail.Extend(2, -2);
                        }


                        end_gear_bottom_gasket = Brep.CreateThickPipe(end_gear_bottom_gakset_rail.ToNurbsCurve(), gasket_inner_radius, gasket_outer_radius, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];
                        end_gear_top_gasket = Brep.CreateThickPipe(end_gear_top_gasket_rail.ToNurbsCurve(), gasket_inner_radius, gasket_outer_radius, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];

                        distance = pointConnection1.DistanceTo(topPoint) - 1;
                        end_gear_shaft_rail = new Line(pointConnection1, end_gear_dir, distance);
                        end_gear_shaft = Brep.CreatePipe(end_gear_shaft_rail.ToNurbsCurve(), shaft_radius, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];

                        distance += 2;
                        end_gear_shaft_clearance_rail = new Line(pointConnection1, end_gear_dir, distance);
                        end_gear_clearance_shaft = Brep.CreatePipe(end_gear_shaft_clearance_rail.ToNurbsCurve(), clearance_shaft_radius, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];
                        //mainModel = Brep.CreateBooleanDifference(mainModel, end_gear_shaft, myDoc.ModelAbsoluteTolerance, false)[0];
                        //mainModel = Brep.CreateBooleanDifference(mainModel, end_gear_clearance_shaft, myDoc.ModelAbsoluteTolerance, false)[0];
                    }
                }
                #endregion


                GearSet bestGearSet = workable_gearsets[0];
                int difference = bestGearSet.ConnectorGear.NumTeeth;
                if (bestGearSet.SecondDrivenGear != null)
                {
                    difference = Math.Abs(bestGearSet.ConnectorGear.NumTeeth - bestGearSet.SecondDrivenGear.NumTeeth);
                }

                foreach (var gearset in workable_gearsets)
                {
                    if (gearset.SecondDrivenGear == null)
                    {
                        bestGearSet = gearset;
                        break;
                    }
                    else
                    {
                        if (bestGearSet.SecondDrivenGear != null && gearset.SecondDrivenGear.NumTeeth < bestGearSet.SecondDrivenGear.NumTeeth)
                            bestGearSet = gearset;
                        //if(bestGearSet.SecondDrivenGear != null)
                        //{
                        //    int temp_difference = Math.Abs(gearset.ConnectorGear.NumTeeth - gearset.SecondDrivenGear.NumTeeth);
                        //    if(temp_difference < difference)
                        //    {
                        //        difference = temp_difference;
                        //        bestGearSet = gearset;
                        //    }

                        //}
                    }
                }

                //Rotate the connector gear and second driven gear for perfect matching
                if (bestGearSet.SecondDrivenGear != null && Intersection.BrepBrep(bestGearSet.ConnectorGear.Model, bestGearSet.SecondDrivenGear.Model, myDoc.ModelAbsoluteTolerance, out intersectionCurves, out intersectionPoints))
                {
                    if (bestGearSet.ConnectorGear.CenterPoint.X > bestGearSet.SecondDrivenGear.CenterPoint.X && bestGearSet.ConnectorGear.CenterPoint.Y < bestGearSet.SecondDrivenGear.CenterPoint.Y)
                    {
                        rail5 = new Line(bestGearSet.ConnectorGear.CenterPoint, bestGearSet.SecondDrivenGear.CenterPoint);
                        double angle = RhinoMath.ToDegrees(Vector3d.VectorAngle(new Vector3d(rail5.Direction.X, rail5.Direction.Y, 0), new Vector3d(0, 1, 0)));
                        bestGearSet.ConnectorGear.Rotate(angle);
                        bestGearSet.SecondDrivenGear.Rotate(-180 + angle);
                        bestGearSet.ConnectorGear.Rotate(360 / bestGearSet.ConnectorGear.NumTeeth / 2);
                    }
                    else if (bestGearSet.ConnectorGear.CenterPoint.X > bestGearSet.SecondDrivenGear.CenterPoint.X && bestGearSet.ConnectorGear.CenterPoint.Y > bestGearSet.SecondDrivenGear.CenterPoint.Y)
                    {
                        rail5 = new Line(bestGearSet.ConnectorGear.CenterPoint, bestGearSet.SecondDrivenGear.CenterPoint);
                        double angle = RhinoMath.ToDegrees(Vector3d.VectorAngle(new Vector3d(rail5.Direction.X, rail5.Direction.Y, 0), new Vector3d(0, 1, 0)));
                        //RhinoApp.WriteLine("Rotating with angle" + angle);
                        bestGearSet.ConnectorGear.Rotate(180 - angle);
                        bestGearSet.SecondDrivenGear.Rotate(angle - 180);
                        bestGearSet.ConnectorGear.Rotate(360 / bestGearSet.ConnectorGear.NumTeeth / 2);
                    }
                    else if (bestGearSet.ConnectorGear.CenterPoint.X < bestGearSet.SecondDrivenGear.CenterPoint.X && bestGearSet.ConnectorGear.CenterPoint.Y > bestGearSet.SecondDrivenGear.CenterPoint.Y)
                    {
                        rail5 = new Line(bestGearSet.ConnectorGear.CenterPoint, bestGearSet.SecondDrivenGear.CenterPoint);
                        double angle = RhinoMath.ToDegrees(Vector3d.VectorAngle(new Vector3d(rail5.Direction.X, rail5.Direction.Y, 0), new Vector3d(0, 1, 0)));
                        bestGearSet.ConnectorGear.Rotate(-180 + angle);
                        bestGearSet.SecondDrivenGear.Rotate(angle);
                        bestGearSet.SecondDrivenGear.Rotate(360 / bestGearSet.SecondDrivenGear.NumTeeth / 2);
                    }
                    else if (bestGearSet.ConnectorGear.CenterPoint.X < bestGearSet.SecondDrivenGear.CenterPoint.X && bestGearSet.ConnectorGear.CenterPoint.Y < bestGearSet.SecondDrivenGear.CenterPoint.Y)
                    {
                        rail5 = new Line(bestGearSet.ConnectorGear.CenterPoint, bestGearSet.SecondDrivenGear.CenterPoint);
                        double angle = RhinoMath.ToDegrees(Vector3d.VectorAngle(new Vector3d(rail5.Direction.X, rail5.Direction.Y, 0), new Vector3d(0, 1, 0)));
                        bestGearSet.ConnectorGear.Rotate(-angle);
                        bestGearSet.SecondDrivenGear.Rotate(180 - angle);
                        bestGearSet.SecondDrivenGear.Rotate(360 / bestGearSet.SecondDrivenGear.NumTeeth / 2);
                    }
                    else if (bestGearSet.ConnectorGear.CenterPoint.X == bestGearSet.SecondDrivenGear.CenterPoint.X && bestGearSet.ConnectorGear.CenterPoint.Y < bestGearSet.SecondDrivenGear.CenterPoint.Y)
                    {
                        bestGearSet.SecondDrivenGear.Rotate(180);
                        bestGearSet.SecondDrivenGear.Rotate(360 / bestGearSet.SecondDrivenGear.NumTeeth / 2);
                    }
                    else
                    {
                        bestGearSet.ConnectorGear.Rotate(180);
                        bestGearSet.ConnectorGear.Rotate(360 / bestGearSet.ConnectorGear.NumTeeth / 2);
                    }
                }

                myDoc.Objects.Add(bestGearSet.EndGear.Model);
                myDoc.Objects.Add(bestGearSet.FirstDrivenGear.Model);
                myDoc.Objects.Add(bestGearSet.ConnectorGear.Model);

                Point3d pt1 = new Point3d(bestGearSet.ConnectorGear.Model.GetBoundingBox(true).Center.X, bestGearSet.ConnectorGear.Model.GetBoundingBox(true).Center.Y, bestGearSet.FirstDrivenGear.Model.GetBoundingBox(true).Max.Z);
                Point3d pt2 = new Point3d(bestGearSet.ConnectorGear.Model.GetBoundingBox(true).Center.X, bestGearSet.ConnectorGear.Model.GetBoundingBox(true).Center.Y, bestGearSet.FirstDrivenGear.Model.GetBoundingBox(true).Max.Z + gasket_gear_gap);
                Line line1 = new Line(pt1, pt2);
                Brep brep = Brep.CreatePipe(line1.ToNurbsCurve(), gasket_outer_radius, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];
                Brep[] breps = Brep.CreateBooleanDifference(new[] { bestGearSet.EndGearBottomGasket }, new[] { brep, bestGearSet.ShaftClearance }, myDoc.ModelAbsoluteTolerance);
                if (breps != null && breps.Length > 0)
                {
                    bestGearSet.EndGearBottomGasket = breps[0];
                    foreach (var temp_brep in breps)
                    {
                        if (temp_brep.GetVolume() > bestGearSet.EndGearBottomGasket.GetVolume())
                            bestGearSet.EndGearBottomGasket = temp_brep;
                    }
                }


                pt1 = bestGearSet.EndGearBottomGasketRail.PointAtEnd;
                pt2 = bestGearSet.EndGearBottomGasketRail.Extend(CurveEnd.End, gasket_gear_gap, CurveExtensionStyle.Line).PointAtEnd;
                line1 = new Line(pt1, pt2);
                brep = Brep.CreatePipe(line1.ToNurbsCurve(), gasket_outer_radius, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];
                breps = Brep.CreateBooleanDifference(new[] { bestGearSet.FirstDrivenGearTopGasket }, new[] { brep, bestGearSet.EndGearShaftClearance }, myDoc.ModelAbsoluteTolerance);
                if (breps != null && breps.Length > 0)
                {
                    bestGearSet.FirstDrivenGearTopGasket = breps[0];
                    foreach (var temp_brep in breps)
                    {
                        if (temp_brep.GetVolume() > bestGearSet.FirstDrivenGearTopGasket.GetVolume())
                            bestGearSet.FirstDrivenGearTopGasket = temp_brep;
                    }
                }

                breps = Brep.CreateBooleanDifference(new[] { bestGearSet.Shaft }, new[] { bestGearSet.EndGearShaftClearance }, myDoc.ModelAbsoluteTolerance);
                if (breps != null && breps.Length > 0)
                {
                    bestGearSet.Shaft = breps[0];
                    foreach (var temp_brep in breps)
                    {
                        if (temp_brep.GetVolume() > bestGearSet.Shaft.GetVolume())
                            bestGearSet.Shaft = temp_brep;
                    }
                }

                myDoc.Objects.Add(bestGearSet.Shaft);

                //myDoc.Objects.Add(bestGearSet.EndGearShaftRail);
                Curve end_gear_shaft_rail_curve = bestGearSet.EndGearShaftRail.ToNurbsCurve().Extend(CurveEnd.Start, CurveExtensionStyle.Line, new[] { endEffector_Hollowed });
                end_gear_shaft_rail_curve = end_gear_shaft_rail_curve.Extend(CurveEnd.End, 2, CurveExtensionStyle.Line);
                if (end_gear_shaft_rail_curve != null)
                {
                    bestGearSet.EndGearShaft = Brep.CreatePipe(end_gear_shaft_rail_curve, shaft_radius, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];
                    bestGearSet.EndGearShaft = Brep.CreateBooleanDifference(bestGearSet.EndGearShaft, endEffector_Hollowed, myDoc.ModelAbsoluteTolerance)[0];
                }


                specialPipes.Add(myDoc.Objects.Add(bestGearSet.EndGearShaft));

                //Second driven gear, gasket and shaft
                Line second_driven_top_gasket_rail = new Line();
                Line second_driven_bottom_gasket_rail = new Line();
                Line second_driven_shaft_rail = new Line();
                Line second_driven_shaft_clearance_rail = new Line();
                if (bestGearSet.SecondDrivenGear != null)
                {
                    //Model
                    while (Intersection.BrepBrep(bestGearSet.SecondDrivenGear.Model, bestGearSet.ConnectorGear.Model, myDoc.ModelAbsoluteTolerance, out intersectionCurves, out intersectionPoints))
                    {
                        if (intersectionCurves != null || intersectionPoints != null)
                        {
                            if (intersectionCurves.Length > 0 || intersectionPoints.Length > 0)
                            {
                                bestGearSet.SecondDrivenGear.Rotate(1);
                            }
                            else
                            {
                                break;
                            }
                        }
                        else
                        {
                            break;
                        }
                    }


                    myDoc.Objects.Add(bestGearSet.SecondDrivenGear.Model);

                    //Gasket
                    Point3d start_point = new Point3d(bestGearSet.SecondDrivenGear.CenterPoint);
                    start_point.Z = bestGearSet.SecondDrivenGear.Model.GetBoundingBox(true).Min.Z - gasket_gear_gap;
                    Point3d end_point = new Point3d(bestGearSet.SecondDrivenGear.CenterPoint);
                    end_point.Z = bestGearSet.SecondDrivenGear.Model.GetBoundingBox(true).Min.Z - bottom_gasket_gear_height;
                    second_driven_bottom_gasket_rail = new Line(start_point, end_point);

                    Brep second_driven_gear_bottom_gasket = Brep.CreateThickPipe(second_driven_bottom_gasket_rail.ToNurbsCurve(), gasket_inner_radius, gasket_outer_radius, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];
                    Guid guid = myDoc.Objects.Add(second_driven_gear_bottom_gasket);
                    gaskets_guid.Add(guid);
                    Circle hole = new Circle(new Plane(second_driven_bottom_gasket_rail.PointAtLength(second_driven_bottom_gasket_rail.Length / 2), new Vector3d(0, 0, 1)), (gasket_outer_radius + gasket_inner_radius) / 2);
                    gasketCircles.Add(hole.ToNurbsCurve());

                    start_point.Z = bestGearSet.SecondDrivenGear.Model.GetBoundingBox(true).Max.Z + gasket_gear_gap;
                    end_point.Z = bestGearSet.SecondDrivenGear.Model.GetBoundingBox(true).Max.Z + top_gasket_gear_height;
                    second_driven_top_gasket_rail = new Line(start_point, end_point);
                    Brep second_driven_top_gasket = Brep.CreateThickPipe(second_driven_top_gasket_rail.ToNurbsCurve(), gasket_inner_radius, gasket_outer_radius, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];
                    guid = myDoc.Objects.Add(second_driven_top_gasket);
                    gaskets_guid.Add(guid);
                    //myDoc.Objects.AddCurve(second_driven_top_gasket_rail.ToNurbsCurve());
                    hole = new Circle(new Plane(second_driven_top_gasket_rail.PointAtLength(second_driven_top_gasket_rail.Length / 2), new Vector3d(0, 0, 1)), (gasket_outer_radius + gasket_inner_radius) / 2);

                    gasketCircles.Add(hole.ToNurbsCurve());


                    //shaft
                    start_point.Z = bestGearSet.SecondDrivenGear.Model.GetBoundingBox(true).Max.Z + shaft_extends_from_gear;
                    end_point.Z = bestGearSet.SecondDrivenGear.Model.GetBoundingBox(true).Min.Z - shaft_extends_from_gear;
                    second_driven_shaft_rail = new Line(start_point, end_point);
                    Brep second_driven_gear_shaft = Brep.CreatePipe(second_driven_shaft_rail.ToNurbsCurve(), shaft_radius, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];
                    myDoc.Objects.Add(second_driven_gear_shaft);

                    second_driven_shaft_clearance_rail = second_driven_shaft_rail;
                    second_driven_shaft_clearance_rail.Extend(shaft_extends_from_gear, shaft_extends_from_gear);
                    Brep second_driven_gear_shaft_clearance = Brep.CreatePipe(second_driven_shaft_clearance_rail.ToNurbsCurve(), clearance_shaft_radius, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];
                }






                gaskets_guid.Add(myDoc.Objects.Add(bestGearSet.EndGearBottomGasket));
                gaskets_guid.Add(myDoc.Objects.Add(bestGearSet.EndGearTopGasket));
                gaskets_guid.Add(myDoc.Objects.Add(bestGearSet.FirstDrivenGearBottomGasket));
                gaskets_guid.Add(myDoc.Objects.Add(bestGearSet.FirstDrivenGearTopGasket));
                gaskets_guid.Add(myDoc.Objects.Add(bestGearSet.ConnectorGearBottomGasket));
                gaskets_guid.Add(myDoc.Objects.Add(bestGearSet.ConnectorGearTopGasket));


                //Create holes on gaskets to allow more water flow
                List<Curve> rails = new List<Curve>();
                rails.Add(bestGearSet.EndGearBottomGasketRail);
                rails.Add(bestGearSet.EndGearTopGasketRail);
                rails.Add(bestGearSet.FirstDrivenGearBottomGasketRail);
                rails.Add(bestGearSet.FirstDrivenGearTopGasketRail);
                rails.Add(bestGearSet.ConnectorGearBottomGasketRail);
                rails.Add(bestGearSet.ConnectorGearTopGasketRail);


                for (int i = 0; i < rails.Count; i++)
                {
                    Curve c = rails[i];

                    Circle hole = new Circle(new Plane(c.PointAtLength(c.GetLength() / 2), c.TangentAtStart), (gasket_outer_radius + gasket_inner_radius) / 2);
                    gasketCircles.Add(hole.ToNurbsCurve());
                }
                if (Intersection.CurveBrep(bestGearSet.EndGearShaftRail, currModel, myDoc.ModelAbsoluteTolerance, out Curve[] overlapCurves, out intersectionPoints, out Double[] curveParameters))
                {
                    Curve cover_rail = (new Line(intersectionPoints[0], end_gear_dir, bestGearSet.EndGearShaftRail.GetLength() / 5)).ToNurbsCurve();
                    Brep cover = Brep.CreateThickPipe(cover_rail, gasket_inner_radius, gasket_outer_radius, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];

                    Guid guid = myDoc.Objects.Add(cover);
                    gaskets_guid.Add(guid);
                    Circle hole = new Circle(new Plane(cover_rail.PointAtLength(cover_rail.GetLength() / 2), cover_rail.TangentAtStart), (gasket_outer_radius + gasket_inner_radius) / 2);
                    gasketCircles.Add(hole.ToNurbsCurve());
                }

                myDoc.Objects.Add(start_gear.Model);
            }
            

            myDoc.Objects.Delete(cutterObjId, true);
            myDoc.Objects.Delete(endEffectorObjId, true);
            myDoc.Views.Redraw();
            myDoc.Objects.Hide(currModelObjId, true);




            return true;
        }


        public bool GenerateTranslationalMotion(ref Item savedItem, out List<Brep> subtrahends)
        {
            subtrahends = new List<Brep>();



            return false;
        }

        public bool GenerateAirPipe(ref Item savedItem, out List<Brep> subtrahends)
        {
            subtrahends = new List<Brep>();

            allTempBoxesGuid.Clear();

            Point3d tempPt = savedItem.EndPoint;
            Brep customized_part = savedItem.EndPointModel[0];

            voxelSpace = null;
            Point3d pipeExit = new Point3d(foundation_origin.X + 72.5, foundation_origin.Y + 63, foundation_origin.Z + foundation_height - 1);

            GetVoxelSpace(currModel, 1, currModel);

            Index base_part_center_index = FindClosestPointIndex(pipeExit, currModel, "accurate");

            Voxel goal = voxelSpace[base_part_center_index.i, base_part_center_index.j, base_part_center_index.k];

            if (goal.isTaken)
            {
                RhinoApp.WriteLine("The air pipe exit is blocked. Cannot generate air pipe!");
                return false;
            }

            #region Find pipe path
            //Method 1: use A* directly
            List<Point3d> bestRoute1 = FindShortestPath(tempPt, pipeExit, currModel, currModel, 1);

            Curve route = Curve.CreateInterpolatedCurve(bestRoute1, 1);
            route = route.Trim(CurveEnd.Both, route.GetLength() / 10);


            Brep temp_customized_part = new Cylinder(new Circle(new Point3d(0, 0, 0), 3.2), 5).ToBrep(true, true);
            Curve customized_part_outer_circle = new Circle(new Point3d(0, 0, 0), 3.2).ToNurbsCurve();
            Curve customized_part_inner_circle = new Circle(new Point3d(0, 0, 0), 3).ToNurbsCurve();
            Transform rotation = Transform.Rotation(new Vector3d(0, 0, 1), savedItem.Normal, temp_customized_part.GetBoundingBox(true).Center);
            temp_customized_part.Transform(rotation);
            customized_part_outer_circle.Transform(rotation);
            customized_part_inner_circle.Transform(rotation);

            Vector3d translationVector = tempPt - temp_customized_part.GetBoundingBox(true).Center;
            Transform translation = Transform.Translation(translationVector);
            temp_customized_part.Transform(translation);
            customized_part_outer_circle.Transform(translation);
            customized_part_inner_circle.Transform(translation);

            Brep main_air_pipe = Brep.CreateThickPipe(route, 3, 3.2, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];
            #endregion

            #region source extension
            Curve source_outer_circle = new Circle(new Plane(pipeExit, new Vector3d(0, 0, 1)), 3.2).ToNurbsCurve();
            Curve pipe_start_outer_circle = new Circle(new Plane(route.PointAtStart, route.TangentAtStart), 3.2).ToNurbsCurve();

            if (!Curve.DoDirectionsMatch(source_outer_circle, pipe_start_outer_circle))
                pipe_start_outer_circle.Reverse();
            Point3d start = source_outer_circle.PointAtStart;
            pipe_start_outer_circle.ClosestPoint(start, out double t);
            pipe_start_outer_circle.ChangeClosedCurveSeam(t);
            Curve[] crossSectionCurves = new Curve[] { pipe_start_outer_circle, source_outer_circle };
            Brep[] loftBreps = Brep.CreateFromLoft(crossSectionCurves, Point3d.Unset, Point3d.Unset, LoftType.Normal, false);
            Brep source_outer_extension = new Brep();
            if (loftBreps != null && loftBreps.Length > 0)
                source_outer_extension = loftBreps[0];

            Curve source_inner_circle = new Circle(new Plane(pipeExit, new Vector3d(0, 0, 1)), 3).ToNurbsCurve();
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
            if (solid != null && solid.Length > 0)
                source_extension = solid[0];
            else
            {
                RhinoApp.WriteLine("Failed to create an air pipe due to unavailability of air pipe source");
                myDoc.Objects.Add(route);
                Brep[] breps = new Brep[] { pipe_start_cap, source_inner_extension, source_outer_extension, source_cap };
                foreach (var item in breps)
                    myDoc.Objects.Add(item);
                Curve[] curves = new Curve[] { source_inner_circle, source_outer_circle, pipe_start_inner_circle, pipe_start_outer_circle };
                foreach (var curve in curves)
                    myDoc.Objects.AddCurve(curve);
                return false;
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
                customized_part_extension = solid[0];
            else
            {
                RhinoApp.WriteLine("Failed to create an air pipe due to the selected area is too complex to create an airpipe");
                myDoc.Objects.Add(main_air_pipe);
                Brep[] breps = new Brep[] { pipe_end_cap, customized_part_cap, customized_part_inner_extension, customized_part_outer_extension };
                foreach (var item in breps)
                    myDoc.Objects.Add(item);
                Curve[] curves = new Curve[] { pipe_end_inner_circle, pipe_end_outer_circle, customized_part_inner_circle, customized_part_outer_circle };
                foreach (var curve in curves)
                    myDoc.Objects.AddCurve(curve);
                return false;
            }

            

            Guid a = myDoc.Objects.Add(customized_part_extension);
            specialPipes.Add(a);
            led_pipes.Add(customized_part_extension);
            led_pipes_guid.Add(a);

            a = myDoc.Objects.Add(main_air_pipe);
            specialPipes.Add(a);
            led_pipes.Add(main_air_pipe);
            led_pipes_guid.Add(a);
            Brep main_air_pipe_concrete = Brep.CreatePipe(route, 3.2, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];
            InViewObject conductiveObject = new InViewObject(main_air_pipe_concrete, a, "conductive pipe");

            a = myDoc.Objects.Add(source_extension);
            specialPipes.Add(a);
            led_pipes.Add(source_extension);
            led_pipes_guid.Add(a);

            Brep[] differences = Brep.CreateBooleanDifference(currModel_Hollowed, customized_part, myDoc.ModelAbsoluteTolerance);
            if (differences != null && differences.Length > 0)
            {
                GetSimilarVolumeBrep(differences, currModel_Hollowed, out currModel_Hollowed);
            }
            else
            {
                myDoc.Objects.Add(customized_part);
            }

            myDoc.Objects.Hide(currModelObjId, true);

            

            combinableLightPipeRoute.Add(route.Trim(CurveEnd.Both, route.GetLength()/4));
            combinableLightPipe.Add(main_air_pipe_concrete);
            conductiveObjects.Add(conductiveObject);

            ////Method 2: use a portion of the lightPipe directly.
            //Curve bestStartRoute = bestRoute;
            //Curve bestEndRoute = bestRoute;

            //if (combinableLightPipeRoute.Count > 0)
            //{
            //    Curve bestMiddleRoute = bestRoute;
            //    int closestIndex = -1;
            //    double closestDistance = pipeExit.DistanceToSquared(customized_part_center);
            //    for (int i = 0; i < combinableLightPipe.Count; i++)
            //    {
            //        Point3d head = combinableLightPipeRoute[i].PointAtEnd;
            //        double thisDistance = head.DistanceToSquared(customized_part_center);
            //        if (thisDistance < closestDistance)
            //        {
            //            closestIndex = i;
            //            closestDistance = thisDistance;
            //            bestMiddleRoute = combinableLightPipeRoute[i];
            //        }
            //    }
            //    voxelSpace = null;
            //    myDoc.Objects.Delete(conductiveObjects[closestIndex].guid, true);
            //    GetVoxelSpace(currModel, 1, combinableLightPipe[closestIndex]);
            //    bestMiddleRoute = bestMiddleRoute.Trim(CurveEnd.End, 7);
            //    bestMiddleRoute = bestMiddleRoute.Trim(CurveEnd.Start, 7);

            //    List<Point3d> bestStartPath = FindShortestPath(customized_part_center, bestMiddleRoute.PointAtEnd, customized_part, currModel, 2);
            //    List<Point3d> bestEndPath = FindShortestPath(bestMiddleRoute.PointAtStart, pipeExit, customized_part, currModel, 2);
            //    bestStartRoute = Curve.CreateInterpolatedCurve(bestStartPath, 1);
            //    bestEndRoute = Curve.CreateInterpolatedCurve(bestEndPath, 1);
            //    //myDoc.Objects.Add(bestStartRoute, soluableAttribute);
            //    //myDoc.Objects.Add(bestEndRoute, lightGuideAttribute);
            //    conductiveObjects[closestIndex].guid = myDoc.Objects.Add(conductiveObjects[closestIndex].brep, redAttribute);
            //}

            //#endregion

            //#region Find the shortest route to create the air pipe
            //double totalDistance = bestStartRoute.GetLength() + bestEndRoute.GetLength();
            //if (bestRoute.GetLength() < totalDistance)
            //{
            //    bestRoute = bestRoute.Trim(CurveEnd.End, 7);
            //    bestRoute = bestRoute.Trim(CurveEnd.Start, 7);
            //    Circle edgeAtStart = new Circle(new Plane(bestRoute.PointAtStart, bestRoute.TangentAtStart), 3.2);
            //    Circle edgeAtEnd = new Circle(new Plane(bestRoute.PointAtEnd, bestRoute.TangentAtEnd), 3.2);
            //    //Create method 1 pipe 
            //    Brep[] airPipe = Brep.CreatePipe(bestRoute, 3.2, true, PipeCapMode.Flat, false, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians);

            //    Circle pipeStartEdge = new Circle(pipeExit, 3.2);
            //    Curve pipeEndEdge = intersectionCurves[0];



            //    //airPipe = Brep.CreateBooleanSplit(airPipe[0], currModel, myDoc.ModelAbsoluteTolerance);
            //    myDoc.Objects.Add(airPipe[0], solidAttribute);
            //}
            //else
            //{
            //    Brep[] airPipe1 = Brep.CreatePipe(bestStartRoute, 2, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians);
            //    Brep[] airPipe2 = Brep.CreatePipe(bestEndRoute, 2, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians);
            //    airPipe1 = Brep.CreateBooleanSplit(airPipe1[0], currModel, myDoc.ModelAbsoluteTolerance);
            //    airPipe2 = Brep.CreateBooleanSplit(airPipe2[0], currModel, myDoc.ModelAbsoluteTolerance);
            //    myDoc.Objects.Add(airPipe1[0], solidAttribute);
            //    myDoc.Objects.Add(airPipe2[0], solidAttribute);
            //}
            #endregion

            myDoc.Views.Redraw();

            return true;
        }

        public bool GenerateLightPipe(ref Item savedItem, out List<Brep> subtrahends)
        {
            subtrahends = new List<Brep>();
            allTempBoxesGuid.Clear();
            
            
            List<Brep> customized_partBreps = savedItem.EndPointModel;
            
            int count = 0;
            
            PipeExit pipeExit = new PipeExit();
            foreach (Brep customized_part in customized_partBreps)
            {
                Point3d customized_part_center = customized_part.GetBoundingBox(true).Center;
                Intersection.BrepBrep(currModel, customized_part, myDoc.ModelAbsoluteTolerance, out Curve[] intersectionCurves, out Point3d[] intersectionPts);
                if (customized_part_center.DistanceTo(currModel.ClosestPoint(customized_part_center)) > 1 || (intersectionCurves.Length == 0 && intersectionPts.Length == 0))
                {
                    //myDoc.Objects.Add(customized_part);
                    //myDoc.Objects.AddPoint(customized_part_center);
                    RhinoApp.WriteLine("One LED Light item cannot be created. Due to impatible to other items");
                    return false;
                }

                voxelSpace = null;
                GetVoxelSpace(currModel, 1, customized_part);

                //Find the Pipe exit location
                if (pipeExit.location == null)
                {
                    List<PipeExit> candidatePipeExit = new List<PipeExit>();
                    if (ledPipeExitPts.Count == 0)
                    {
                        GetLightPipeExits(currModel);
                    }


                    bool allTaken = true;
                    foreach (var item in ledPipeExitPts)
                    {
                        if (item.isTaken == false && item.location.isTaken == false)
                        {
                            allTaken = false;
                            candidatePipeExit.Add(item);
                        }
                    }
                    if (allTaken)
                    {
                        RhinoApp.WriteLine("All pipe exits are taken, or covered. Unable to create anymore LED light parameters");
                        return false;
                    }
                    else
                    {
                        double minDistance = double.MaxValue;
                        pipeExit = candidatePipeExit.FirstOrDefault();
                        foreach(var item in candidatePipeExit)
                        {
                            if(item.actualLocation.DistanceTo(customized_part_center) < minDistance)
                            {
                                minDistance = item.actualLocation.DistanceTo(customized_part_center);
                                pipeExit = item;
                            }
                        }
                        pipeExit.isTaken = true;
                    }
                }

                List<Point3d> bestRoute1 = FindShortestPath(customized_part_center, new Point3d(pipeExit.location.X, pipeExit.location.Y, pipeExit.location.Z), customized_part, currModel, 1);

                #region Get the line that combine all gears ---> bestRoute2
                List<BoundingBox> allTempBoxes = CombineBreps(customized_part, currModel);
                List<Point3d> bestRoute2 = bestRoute1;
                if (allTempBoxes.Count > 0)
                {
                    GetVoxelSpace(currModel, 2, tempBoxes: allTempBoxes);
                    bestRoute2 = FindShortestPath(customized_part_center, new Point3d(pipeExit.location.X, pipeExit.location.Y, pipeExit.location.Z), customized_part, currModel, 1);
                }
                voxelSpace = null;
                #endregion

                #region Determine which line is better by calculating the total angle of the route
                double angle_Route1 = AngleOfCurve(bestRoute1);
                double angle_Route2 = AngleOfCurve(bestRoute2);

                //myDoc.Objects.Add(Curve.CreateInterpolatedCurve(bestRoute1, 1));
                //myDoc.Objects.Add(Curve.CreateInterpolatedCurve(bestRoute2, 1));

                Curve bestRoute;

                if (angle_Route1 <= angle_Route2)
                    bestRoute = Curve.CreateInterpolatedCurve(bestRoute1, 1);
                else
                    bestRoute = Curve.CreateInterpolatedCurve(bestRoute2, 1);

                // Delete all temp boxes
                foreach (var box in allTempBoxesGuid)
                {
                    myDoc.Objects.Delete(box, true);
                }
                #endregion

                #region Create Pipes
                //Cut the first 5mm of the bestRoute to generate the inner pipe
                Curve soluablePipeRoute = bestRoute.Trim(CurveEnd.Start, bestRoute.GetLength() / 10);

                Brep[] soluablePipe = Brep.CreatePipe(soluablePipeRoute, 3.2, false, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians);
                Brep[] conductivePipe = Brep.CreateThickPipe(soluablePipeRoute, 3, 3.2, false, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians);


                //BrepEdge of the start of the soluablePipe
                Vector3d tangent = soluablePipeRoute.TangentAtStart;
                Circle pipeStartEdge = new Circle(new Plane(soluablePipeRoute.PointAtStart, tangent), soluablePipeRoute.PointAtStart, 3.2);
                Circle actual_pipeStartCircle = new Circle(new Point3d(pipeExit.actualLocation.X, pipeExit.actualLocation.Y, pipeExit.actualLocation.Z - 1), 3.2);

                bool valid = false;
                //Generate the soluable pipe that perfectly match the light source from the foundation
                while (pipeStartEdge.IsValid && !valid)
                {
                    Curve curve1 = actual_pipeStartCircle.ToNurbsCurve();
                    Curve curve2 = pipeStartEdge.ToNurbsCurve();
                    if (curve1.IsClosed && curve2.IsClosed)
                    {
                        if (!Curve.DoDirectionsMatch(curve1, curve2))
                            curve2.Reverse();
                        Point3d start = curve1.PointAtStart;
                        curve2.ClosestPoint(start, out double t);
                        curve2.ChangeClosedCurveSeam(t);
                        Curve[] crossSectionCurves = new Curve[] { curve1, curve2 };
                        Brep[] loftBreps = Brep.CreateFromLoft(crossSectionCurves, Point3d.Unset, Point3d.Unset, LoftType.Normal, false);
                        Brep soluableExtension = loftBreps[0];
                        soluableExtension = soluableExtension.CapPlanarHoles(myDoc.ModelAbsoluteTolerance);

                        start = soluablePipeRoute.PointAtStart;
                        Vector3d temp_tangent = soluablePipeRoute.TangentAtStart;
                        temp_tangent.Unitize();
                        start = start + temp_tangent;
                        pipeStartEdge = new Circle(new Plane(start, tangent), start, 3);
                        actual_pipeStartCircle = new Circle(new Point3d(pipeExit.actualLocation.X, pipeExit.actualLocation.Y, pipeExit.actualLocation.Z - 1), 3);
                        curve1 = actual_pipeStartCircle.ToNurbsCurve();
                        curve2 = pipeStartEdge.ToNurbsCurve();

                        if (curve1.IsClosed && curve2.IsClosed)
                        {
                            if (!Curve.DoDirectionsMatch(curve1, curve2))
                                curve2.Reverse();
                            start = curve1.PointAtStart;
                            curve2.ClosestPoint(start, out t);
                            curve2.ChangeClosedCurveSeam(t);
                            crossSectionCurves = new Curve[] { curve1, curve2 };
                            loftBreps = Brep.CreateFromLoft(crossSectionCurves, Point3d.Unset, Point3d.Unset, LoftType.Normal, false);
                            Brep soluableExtension_Cutter = loftBreps[0];
                            soluableExtension_Cutter = soluableExtension_Cutter.CapPlanarHoles(myDoc.ModelAbsoluteTolerance);
                            Brep[] soluableExtensions = Brep.CreateBooleanDifference(soluableExtension_Cutter, soluableExtension, myDoc.ModelAbsoluteTolerance);
                            if(soluableExtensions != null && soluableExtensions.Length > 0)
                            {
                                soluableExtension = soluableExtensions[0];
                                valid = true;

                                curve1 = new Circle(new Point3d(pipeExit.actualLocation.X, pipeExit.actualLocation.Y, pipeExit.actualLocation.Z - 1.1), 3.2).ToNurbsCurve();
                                curve2 = new Circle(new Plane(soluablePipeRoute.PointAtStart, tangent), soluablePipeRoute.PointAtStart, 3.2).ToNurbsCurve();
                                if (!Curve.DoDirectionsMatch(curve1, curve2))
                                    curve2.Reverse();
                                start = curve1.PointAtStart;
                                curve2.ClosestPoint(start, out t);
                                curve2.ChangeClosedCurveSeam(t);
                                crossSectionCurves = new Curve[] { curve1, curve2 };
                                loftBreps = Brep.CreateFromLoft(crossSectionCurves, Point3d.Unset, Point3d.Unset, LoftType.Normal, false);
                                Brep cutter = loftBreps[0];
                                cutter = cutter.CapPlanarHoles(myDoc.ModelAbsoluteTolerance);

                                lightSourcePipePairs.Add((soluableExtension, cutter));
                            }
                            else
                            {
                                //The current generated light source pipe cannot be created, we cut the route more.
                                soluablePipeRoute = soluablePipeRoute.Trim(CurveEnd.Start, 1);

                                soluablePipe = Brep.CreatePipe(soluablePipeRoute, 3.2, false, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians);
                                conductivePipe = Brep.CreateThickPipe(soluablePipeRoute, 3, 3.2, false, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians);


                                //BrepEdge of the start of the soluablePipe
                                tangent = soluablePipeRoute.TangentAtStart;
                                pipeStartEdge = new Circle(new Plane(soluablePipeRoute.PointAtStart, tangent), soluablePipeRoute.PointAtStart, 3.2);
                                actual_pipeStartCircle = new Circle(new Point3d(pipeExit.actualLocation.X, pipeExit.actualLocation.Y, pipeExit.actualLocation.Z - 1), 3.2);
                                continue;
                            }
                        }

                        //Guid a = myDoc.Objects.Add(soluableExtension);
                        //specialPipes.Add(a);
                        //ignorePipesGuid.Add(a);
                        //led_pipes.Add(soluableExtension);
                        //led_pipes_guid.Add(a);
                    }
                }





                Guid conductivePipeGuid = myDoc.Objects.Add(conductivePipe[0], redAttribute);
                specialPipes.Add(conductivePipeGuid);
                led_pipes.Add(conductivePipe[0]);
                led_pipes_guid.Add(conductivePipeGuid);
                InViewObject conductiveObject = new InViewObject(conductivePipe[0], conductivePipeGuid, "conductive pipe");



                mainPipePairs.Add(soluablePipe[0]);

                combinableLightPipeRoute.Add(soluablePipeRoute.Trim(CurveEnd.Both, 7));
                combinableLightPipe.Add(soluablePipe[0]);
                conductiveObjects.Add(conductiveObject);

                Intersection.BrepBrep(customized_part, currModel, myDoc.ModelAbsoluteTolerance, out Curve[] intersectionCurves1, out Point3d[] intersectionPoints1);

                //BrepEdge of the end of the soluablePipe
                tangent = soluablePipeRoute.TangentAtEnd;
                Circle pipeEndEdge = new Circle(new Plane(soluablePipeRoute.PointAtEnd, tangent), soluablePipeRoute.PointAtEnd, 3.2);

                //Generate the light pipe that perfectly match the user's defined pattern
                if (pipeEndEdge.IsValid)
                {
                    Curve curve1 = intersectionCurves1[0];
                    Curve curve2 = pipeEndEdge.ToNurbsCurve();

                    if (curve1.IsClosed && curve2.IsClosed)
                    {
                        if (!Curve.DoDirectionsMatch(curve1, curve2))
                            curve2.Reverse();
                        Point3d start = curve1.PointAtStart;
                        curve2.ClosestPoint(start, out double t);
                        curve2.ChangeClosedCurveSeam(t);
                        Curve[] crossSectionCurves = new Curve[] { curve1, curve2 };
                        Brep[] loftBreps = Brep.CreateFromLoft(crossSectionCurves, Point3d.Unset, Point3d.Unset, LoftType.Normal, false);
                        Brep lightGuidPipe = loftBreps[0];
                        lightGuidPipe = lightGuidPipe.CapPlanarHoles(myDoc.ModelAbsoluteTolerance);

                        //Get the patch for the pipe
                        if (!lightGuidPipe.IsSolid)
                        {
                            lightGuidPipe = Brep.MergeBreps(new[] { lightGuidPipe, savedItem.customized_part_patch[count] }, myDoc.ModelAbsoluteTolerance);
                        }

                        Guid a = myDoc.Objects.Add(lightGuidPipe, lightGuideAttribute);
                        if (a == Guid.Empty)
                        {
                            RhinoApp.WriteLine("Fail to generate one LED light.");
                            myDoc.Objects.Delete(conductivePipeGuid, true);
                            led_pipes.Remove(conductivePipe[0]);
                            led_pipes_guid.Remove(conductivePipeGuid);
                            return false;
                        }
                        Brep[] breps = Brep.CreateBooleanDifference(currModel_Hollowed, lightGuidPipe, myDoc.ModelAbsoluteTolerance);
                        if (breps != null && breps.Length > 0)
                            GetSimilarVolumeBrep(breps, currModel_Hollowed, out currModel_Hollowed);
                        specialPipes.Add(a);
                        ignorePipesGuid.Add(a);
                        //led_pipes.Add(lightGuidPipe);
                        //led_pipes_guid.Add(a);
                        count++;
                    }
                }

                #endregion
            }


            ignorePipesGuid.Clear();
            myDoc.Objects.Hide(currModelObjId, true);
            myDoc.Views.Redraw();

            return true;
        }

        public bool GenerateSwipePipe(ref Item savedItem)
        {  
            return false;
        }

        public bool GenerateTouchPipe(ref Item savedItem, out List<Brep> subtrahends)
        {
            subtrahends = new List<Brep>();

            Point3d end_point = savedItem.EndPoint;
            Brep customized_part = savedItem.EndPointModel[0];
            Intersection.BrepBrep(customized_part, currModel, myDoc.ModelAbsoluteTolerance, out Curve[] intersectionCurves, out Point3d[] intersectionPoints);
            Curve customized_part_curve = intersectionCurves[0];


            voxelSpace = null;
            GetVoxelSpace(currModel, 1, currModel);

            //Find a available conductive source
            PipeExit pipeExit = new PipeExit();
            if (pipeExit.location == null)
            {
                if (conductivePipeExitPts.Count == 0)
                {
                    BoundingBox boundingBox = currModel.GetBoundingBox(true);
                    GetConductivePipeExits(currModel);
                }

                List<PipeExit> candidatePipeExit = new List<PipeExit>();
                bool allTaken = true;
                foreach (var item in conductivePipeExitPts)
                {
                    if (item.isTaken == false && item.location.isTaken == false)
                    {
                        candidatePipeExit.Add(item);
                        allTaken = false;
                    }
                }
                if (allTaken)
                {
                    RhinoApp.WriteLine("All pipe exits are taken, or covered. Unable to create anymore Touch parameters");
                    return false;
                }
                else
                {
                    double minDistance = double.MaxValue;
                    pipeExit = candidatePipeExit.FirstOrDefault();
                    foreach (var item in candidatePipeExit)
                    {
                        if (item.actualLocation.DistanceTo(end_point) < minDistance)
                        {
                            minDistance = item.actualLocation.DistanceTo(end_point);
                            pipeExit = item;
                        }
                    }
                    pipeExit.isTaken = true;
                }
            }

            string approach = "combine others";

            //Method 1: Use the A* result directly
            List<Point3d> bestRoute1 = FindShortestPath(end_point, new Point3d(pipeExit.location.X, pipeExit.location.Y, pipeExit.location.Z), currModel, currModel, 2);
            Curve bestRoute = Curve.CreateInterpolatedCurve(bestRoute1, 1);

            //Method 2: Use a air pipe or led pipe to be a part of the route
            int closest_index = -1;
            double closest_distance = double.MaxValue;
            if(combinableLightPipeRoute.Count != 0)
            {
                for(int i = 0; i < combinableLightPipeRoute.Count; i++)
                {
                    double current_distance = combinableLightPipeRoute[i].PointAtStart.DistanceTo(pipeExit.actualLocation);
                    if (current_distance < closest_distance)
                    {
                        closest_index = i;
                        closest_distance = current_distance;
                    }
                }
            }

            List<Point3d> bestEndRoute_pts = new List<Point3d>();
            List<Point3d> bestStartRoute_pts = new List<Point3d>();
            Curve combinablePipeRoute = null;
            if (closest_index >= 0)
            {
                ignorePipesGuid.Add(conductiveObjects[closest_index].guid);
                voxelSpace = null;
                GetVoxelSpace(currModel, 1, currModel);
                combinablePipeRoute = combinableLightPipeRoute[closest_index];
                Brep combinablePipe = combinableLightPipe[closest_index];

                bestEndRoute_pts = FindShortestPath(end_point, combinablePipeRoute.PointAtEnd, currModel, currModel, 1);

                if (bestEndRoute_pts.Count == 0)
                    approach = "A*";

                bestStartRoute_pts = FindShortestPath(combinablePipeRoute.PointAtStart, new Point3d(pipeExit.location.X, pipeExit.location.Y, pipeExit.location.Z), currModel, currModel, 1);
                if (bestStartRoute_pts.Count == 0)
                    approach = "A*";
            }
            else
            {
                approach = "A*";
            }
            

            Curve bestEndRoute = null;
            Curve bestStartRoute = null;


            //Find the best approach
            if(approach != "A*")
            {
                //myDoc.Objects.Add(Curve.CreateInterpolatedCurve(bestStartRoute_pts, 1));
                //myDoc.Objects.AddPoint(Curve.CreateInterpolatedCurve(bestStartRoute_pts, 1).PointAtEnd);
                //myDoc.Objects.AddPoint(combinablePipeRoute.PointAtStart);
                bestStartRoute_pts.Add(combinablePipeRoute.PointAtStart);
                bestEndRoute = Curve.CreateInterpolatedCurve(bestEndRoute_pts, 1);
                bestStartRoute = Curve.CreateInterpolatedCurve(bestStartRoute_pts, 1);

                //Compare between two methods, use the shorter one
                if (bestEndRoute.GetLength() + bestStartRoute.GetLength() > bestRoute.GetLength())
                    approach = "A*";
            }




            if(approach == "A*")
            {
                bestRoute = bestRoute.Trim(CurveEnd.Start, bestRoute.GetLength() / 15);
                Brep conductive_pipe = Brep.CreatePipe(bestRoute, 3, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];

                Curve conductive_source_circle = new Circle(new Point3d(pipeExit.actualLocation.X, pipeExit.actualLocation.Y, pipeExit.actualLocation.Z - 1), 3).ToNurbsCurve();
                Curve pipe_start_circle = new Circle(new Plane(bestRoute.PointAtStart, bestRoute.TangentAtStart), 3).ToNurbsCurve();
                if (!Curve.DoDirectionsMatch(conductive_source_circle, pipe_start_circle))
                    pipe_start_circle.Reverse();
                Point3d start = conductive_source_circle.PointAtStart;
                pipe_start_circle.ClosestPoint(start, out double t);
                pipe_start_circle.ChangeClosedCurveSeam(t);
                Curve[] crossSectionCurves = new Curve[] { conductive_source_circle, pipe_start_circle };
                Brep[] loftBreps = Brep.CreateFromLoft(crossSectionCurves, Point3d.Unset, Point3d.Unset, LoftType.Normal, false);
                Brep source_extension = loftBreps[0];
                source_extension = source_extension.CapPlanarHoles(myDoc.ModelAbsoluteTolerance);

                Brep customized_part_extension = Brep.CreatePipe(new Line(bestRoute.PointAtEnd, end_point).ToNurbsCurve(), 3, true, PipeCapMode.Round, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];


                Brep[] difference = Brep.CreateBooleanDifference(currModel_Hollowed, customized_part, myDoc.ModelAbsoluteTolerance);
                if (difference != null && difference.Length > 0)
                    GetSimilarVolumeBrep(difference, currModel_Hollowed, out currModel_Hollowed);

                difference = Brep.CreateBooleanDifference(conductive_pipe, currModel_Hollowed, myDoc.ModelAbsoluteTolerance);
                if (difference != null && difference.Length > 0)
                    GetSimilarVolumeBrep(difference, conductive_pipe, out conductive_pipe);

                difference = Brep.CreateBooleanDifference(new[] { customized_part_extension }, new[] { currModel_Hollowed, customized_part }, myDoc.ModelAbsoluteTolerance);
                if (difference != null && difference.Length > 0)
                    GetSimilarVolumeBrep(difference, customized_part_extension, out customized_part_extension);

                Guid source_extension_guid = myDoc.Objects.Add(source_extension);
                Guid customized_part_extension_guid = myDoc.Objects.Add(customized_part_extension);
                Guid conductive_pipe_guid = myDoc.Objects.Add(conductive_pipe);

                specialPipes.Add(source_extension_guid);
                specialPipes.Add(customized_part_extension_guid);
                specialPipes.Add(conductive_pipe_guid);

                conductive_pipes_guid.Add(source_extension_guid);
                conductive_pipes_guid.Add(customized_part_extension_guid);
                conductive_pipes_guid.Add(conductive_pipe_guid);

                conductive_pipes.Add(source_extension);
                conductive_pipes.Add(customized_part_extension);
                conductive_pipes.Add(conductive_pipe);


                myDoc.Objects.Add(customized_part);


                myDoc.Objects.Hide(currModelObjId, true);
                return true;
            }
            else
            {
                //Using the second approach, it will create a pipe that made up of 5 parts: pipe source part --> front pipe part --> mid pipe part(already have) --> end pipe part --> customized part
                bestStartRoute = bestStartRoute.Trim(CurveEnd.Start, bestStartRoute.GetLength() / 6);

                //Generate the pipe source part
                Curve conductive_source_circle = new Circle(new Point3d(pipeExit.actualLocation.X, pipeExit.actualLocation.Y, pipeExit.actualLocation.Z - 1), 3).ToNurbsCurve();
                Curve pipe_start_circle = new Circle(new Plane(bestStartRoute.PointAtStart, bestStartRoute.TangentAtStart), 3).ToNurbsCurve();

                if (!Curve.DoDirectionsMatch(conductive_source_circle, pipe_start_circle))
                    pipe_start_circle.Reverse();
                Point3d start = conductive_source_circle.PointAtStart;
                pipe_start_circle.ClosestPoint(start, out double t);
                pipe_start_circle.ChangeClosedCurveSeam(t);
                Curve[] crossSectionCurves = new Curve[] { conductive_source_circle, pipe_start_circle };
                Brep[] loftBreps = Brep.CreateFromLoft(crossSectionCurves, Point3d.Unset, Point3d.Unset, LoftType.Normal, false);
                Brep source_extension = loftBreps[0];
                source_extension = source_extension.CapPlanarHoles(myDoc.ModelAbsoluteTolerance);

                Brep[] difference = Brep.CreateBooleanDifference(source_extension, currModel_Hollowed, myDoc.ModelAbsoluteTolerance);
                if (difference != null && difference.Length > 0)
                    GetSimilarVolumeBrep(difference, source_extension, out source_extension);

                //Generate the front pipe
                Brep front_pipe = Brep.CreatePipe(bestStartRoute, 3, true, PipeCapMode.Round, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];
                difference = Brep.CreateBooleanDifference(front_pipe, currModel_Hollowed, myDoc.ModelAbsoluteTolerance);
                if (difference != null && difference.Length > 0)
                    GetSimilarVolumeBrep(difference, front_pipe, out front_pipe);
                difference = Brep.CreateBooleanDifference(front_pipe, conductiveObjects[closest_index].brep, myDoc.ModelAbsoluteTolerance);
                if (difference != null && difference.Length > 0)
                    GetSimilarVolumeBrep(difference, front_pipe, out front_pipe);

                //Generate the end pipe
                Brep end_pipe = Brep.CreatePipe(bestEndRoute, 3, true, PipeCapMode.Round, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];
                difference = Brep.CreateBooleanDifference(end_pipe, currModel_Hollowed, myDoc.ModelAbsoluteTolerance);
                if (difference != null && difference.Length > 0)
                    GetSimilarVolumeBrep(difference, end_pipe, out end_pipe);
                difference = Brep.CreateBooleanDifference(end_pipe, conductiveObjects[closest_index].brep, myDoc.ModelAbsoluteTolerance);
                if (difference != null && difference.Length > 0)
                    GetSimilarVolumeBrep(difference, end_pipe, out end_pipe);

                //Generate the customized part
                Brep customized_part_extension = Brep.CreatePipe(new Line(bestEndRoute.PointAtEnd, end_point).ToNurbsCurve(), 3, true, PipeCapMode.Round, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];
                difference = Brep.CreateBooleanDifference(customized_part_extension, currModel_Hollowed, myDoc.ModelAbsoluteTolerance);
                if (difference != null && difference.Length > 0)
                    GetSimilarVolumeBrep(difference, customized_part_extension, out customized_part_extension);

                Guid source_extension_guid = myDoc.Objects.Add(source_extension);
                Guid front_pipe_guid = myDoc.Objects.Add(front_pipe);
                Guid end_pipe_guid = myDoc.Objects.Add(end_pipe);
                Guid customized_part_extension_guid = myDoc.Objects.Add(customized_part_extension);
                Guid customized_part_guid = myDoc.Objects.Add(customized_part);

                specialPipes.Add(source_extension_guid);
                specialPipes.Add(front_pipe_guid);
                specialPipes.Add(end_pipe_guid);
                specialPipes.Add(customized_part_extension_guid);

                conductive_pipes_guid.Add(source_extension_guid);
                conductive_pipes_guid.Add(front_pipe_guid);
                conductive_pipes_guid.Add(end_pipe_guid);
                conductive_pipes_guid.Add(customized_part_extension_guid);

                conductive_pipes.Add(source_extension);
                conductive_pipes.Add(front_pipe);
                conductive_pipes.Add(end_pipe);
                conductive_pipes.Add(customized_part_extension);

                conductiveObjects.RemoveAt(closest_index);
                combinableLightPipe.RemoveAt(closest_index);
                combinableLightPipeRoute.RemoveAt(closest_index);

                myDoc.Objects.Hide(currModelObjId, true);
                return true;
            }
            
        }

        public bool GenerateButtonPipe(ref Item savedItem, out List<Brep> subtrahends)
        {
            subtrahends = new List<Brep>();
            Brep conductive_part = savedItem.ButtonSet[0];
            Brep spring_part = savedItem.ButtonSet[1];
            Curve spring_part_circle = savedItem.Button_Spring_Circle;
            Point3d end_point = savedItem.EndPoint;
            Extrusion pipe = Extrusion.Create(spring_part_circle, 5.5, true);

            
            Brep[] differences = Brep.CreateBooleanDifference(currModel_Hollowed, pipe.ToBrep(), myDoc.ModelAbsoluteTolerance);
            //Show button on the model if possible
            if (differences != null && differences.Length > 0)
            {
                voxelSpace = null;
                GetVoxelSpace(currModel, 1, currModel);

                //Find a available conductive source
                PipeExit pipeExit = new PipeExit();
                if (pipeExit.location == null)
                {
                    if (conductivePipeExitPts.Count == 0)
                    {
                        BoundingBox boundingBox = currModel.GetBoundingBox(true);
                        GetConductivePipeExits(currModel);
                    }

                    List<PipeExit> candidatePipeExit = new List<PipeExit>();
                    bool allTaken = true;
                    foreach (var item in conductivePipeExitPts)
                    {
                        if (item.isTaken == false && item.location.isTaken == false)
                        {
                            candidatePipeExit.Add(item);
                            allTaken = false;
                        }
                    }
                    if (allTaken)
                    {
                        RhinoApp.WriteLine("All pipe exits are taken, or covered. Unable to create anymore Button parameters");
                        return false;
                    }
                    else
                    {
                        double minDistance = double.MaxValue;
                        pipeExit = candidatePipeExit.FirstOrDefault();
                        foreach (var item in candidatePipeExit)
                        {
                            if (item.actualLocation.DistanceTo(end_point) < minDistance)
                            {
                                minDistance = item.actualLocation.DistanceTo(end_point);
                                pipeExit = item;
                            }
                        }
                        pipeExit.isTaken = true;
                    }
                }

                //Method 1: Use A* directly
                Point3d customized_part_center;
                spring_part_circle.TryGetCircle(out Circle circle);

                List<Point3d> bestRoute1 = FindShortestPath(circle.Center, new Point3d(pipeExit.location.X, pipeExit.location.Y, pipeExit.location.Z), currModel, currModel, 2);
                Curve bestRoute = Curve.CreateInterpolatedCurve(bestRoute1, 1);

                bestRoute = bestRoute.Trim(CurveEnd.Start, bestRoute.GetLength() / 15);
                Brep conductive_pipe = Brep.CreatePipe(bestRoute, 3, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];

                //Method 2: Use a air pipe or led pipe to be a part of the route
                string approach = "combine others";
                int closest_index = -1;
                double closest_distance = double.MaxValue;
                if (combinableLightPipeRoute.Count != 0)
                {
                    for (int i = 0; i < combinableLightPipeRoute.Count; i++)
                    {
                        double current_distance = combinableLightPipeRoute[i].PointAtStart.DistanceTo(pipeExit.actualLocation);
                        if (current_distance < closest_distance)
                        {
                            closest_index = i;
                            closest_distance = current_distance;
                        }
                    }
                }

                List<Point3d> bestEndRoute_pts = new List<Point3d>();
                List<Point3d> bestStartRoute_pts = new List<Point3d>();
                Curve combinablePipeRoute = null;
                if (closest_index >= 0)
                {
                    ignorePipesGuid.Add(conductiveObjects[closest_index].guid);
                    voxelSpace = null;
                    GetVoxelSpace(currModel, 1, currModel);
                    combinablePipeRoute = combinableLightPipeRoute[closest_index];
                    Brep combinablePipe = combinableLightPipe[closest_index];

                    bestEndRoute_pts = FindShortestPath(end_point, combinablePipeRoute.PointAtEnd, currModel, currModel, 1);

                    if (bestEndRoute_pts.Count == 0)
                        approach = "A*";

                    bestStartRoute_pts = FindShortestPath(combinablePipeRoute.PointAtStart, new Point3d(pipeExit.location.X, pipeExit.location.Y, pipeExit.location.Z), currModel, currModel, 1);
                    if (bestStartRoute_pts.Count == 0)
                        approach = "A*";
                }
                else
                {
                    approach = "A*";
                }


                Curve bestEndRoute = null;
                Curve bestStartRoute = null;


                //Find the best approach
                if (approach != "A*")
                {
                    bestStartRoute_pts.Add(combinablePipeRoute.PointAtStart);
                    bestEndRoute = Curve.CreateInterpolatedCurve(bestEndRoute_pts, 1);
                    bestStartRoute = Curve.CreateInterpolatedCurve(bestStartRoute_pts, 1);

                    //Compare between two methods, use the shorter one
                    if (bestEndRoute.GetLength() + bestStartRoute.GetLength() > bestRoute.GetLength())
                        approach = "A*";
                }

                if(approach == "A*")
                {
                    Curve conductive_source_circle = new Circle(new Point3d(pipeExit.actualLocation.X, pipeExit.actualLocation.Y, pipeExit.actualLocation.Z - 1), 3).ToNurbsCurve();
                    Curve pipe_start_circle = new Circle(new Plane(bestRoute.PointAtStart, bestRoute.TangentAtStart), 3).ToNurbsCurve();
                    if (!Curve.DoDirectionsMatch(conductive_source_circle, pipe_start_circle))
                        pipe_start_circle.Reverse();
                    Point3d start = conductive_source_circle.PointAtStart;
                    pipe_start_circle.ClosestPoint(start, out double t);
                    pipe_start_circle.ChangeClosedCurveSeam(t);
                    Curve[] crossSectionCurves = new Curve[] { conductive_source_circle, pipe_start_circle };
                    Brep[] loftBreps = Brep.CreateFromLoft(crossSectionCurves, Point3d.Unset, Point3d.Unset, LoftType.Normal, false);
                    Brep source_extension = loftBreps[0];

                    Curve pipe_end_circle = new Circle(new Plane(bestRoute.PointAtEnd, bestRoute.TangentAtEnd), 3).ToNurbsCurve();
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

                    if (differences != null && differences.Length > 0)
                    {
                        GetSimilarVolumeBrep(differences, currModel_Hollowed, out currModel_Hollowed);
                    }

                    differences = Brep.CreateBooleanDifference(conductive_pipe, currModel_Hollowed, myDoc.ModelAbsoluteTolerance);
                    if (differences != null && differences.Length > 0)
                        GetSimilarVolumeBrep(differences, conductive_pipe, out conductive_pipe);

                    differences = Brep.CreateBooleanDifference(spring_extension, currModel_Hollowed, myDoc.ModelAbsoluteTolerance);
                    if (differences != null && differences.Length > 0)
                        GetSimilarVolumeBrep(differences, spring_extension, out spring_extension);


                    Guid conductive_pipe_guid = myDoc.Objects.Add(conductive_pipe);
                    Guid source_extension_guid = myDoc.Objects.Add(source_extension);
                    Guid spring_extension_guid = myDoc.Objects.Add(spring_extension);

                    specialPipes.Add(conductive_pipe_guid);
                    specialPipes.Add(source_extension_guid);
                    specialPipes.Add(spring_extension_guid);

                    conductive_pipes_guid.Add(conductive_pipe_guid);
                    conductive_pipes_guid.Add(source_extension_guid);
                    conductive_pipes_guid.Add(spring_extension_guid);

                    myDoc.Objects.Hide(currModelObjId, true);
                    conductive_pipes.Add(conductive_part);
                    conductive_pipes_guid.Add(myDoc.Objects.Add(conductive_part));
                    conductive_pipes.Add(spring_part);
                    conductive_pipes_guid.Add(myDoc.Objects.Add(spring_part));



                    differences = Brep.CreateBooleanDifference(currModel_Hollowed, spring_extension, myDoc.ModelAbsoluteTolerance);

                    if (differences != null && differences.Length > 0)
                    {
                        GetSimilarVolumeBrep(differences, currModel_Hollowed, out currModel_Hollowed);
                    }

                    myDoc.Objects.Delete(currModel_Hollowed_ObjId, true);
                }
                else
                {
                    //Using the second approach, it will create a pipe that made up of 5 parts: pipe source part --> front pipe part --> mid pipe part(already have) --> end pipe part --> customized part
                    bestStartRoute = bestStartRoute.Trim(CurveEnd.Start, bestStartRoute.GetLength() / 6);


                    //Generate the pipe source part
                    Curve conductive_source_circle = new Circle(new Point3d(pipeExit.actualLocation.X, pipeExit.actualLocation.Y, pipeExit.actualLocation.Z - 1), 3).ToNurbsCurve();
                    Curve pipe_start_circle = new Circle(new Plane(bestStartRoute.PointAtStart, bestStartRoute.TangentAtStart), 3).ToNurbsCurve();

                    if (!Curve.DoDirectionsMatch(conductive_source_circle, pipe_start_circle))
                        pipe_start_circle.Reverse();
                    Point3d start = conductive_source_circle.PointAtStart;
                    pipe_start_circle.ClosestPoint(start, out double t);
                    pipe_start_circle.ChangeClosedCurveSeam(t);
                    Curve[] crossSectionCurves = new Curve[] { conductive_source_circle, pipe_start_circle };
                    Brep[] loftBreps = Brep.CreateFromLoft(crossSectionCurves, Point3d.Unset, Point3d.Unset, LoftType.Normal, false);
                    Brep source_extension = loftBreps[0];
                    source_extension = source_extension.CapPlanarHoles(myDoc.ModelAbsoluteTolerance);

                    Brep[] difference = Brep.CreateBooleanDifference(source_extension, currModel_Hollowed, myDoc.ModelAbsoluteTolerance);
                    if (difference != null && difference.Length > 0)
                        GetSimilarVolumeBrep(difference, source_extension, out source_extension);

                    //Generate the front pipe
                    Brep front_pipe = Brep.CreatePipe(bestStartRoute, 3, true, PipeCapMode.Round, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];
                    difference = Brep.CreateBooleanDifference(front_pipe, currModel_Hollowed, myDoc.ModelAbsoluteTolerance);
                    if (difference != null && difference.Length > 0)
                        GetSimilarVolumeBrep(difference, front_pipe, out front_pipe);
                    difference = Brep.CreateBooleanDifference(front_pipe, conductiveObjects[closest_index].brep, myDoc.ModelAbsoluteTolerance);
                    if (difference != null && difference.Length > 0)
                        GetSimilarVolumeBrep(difference, front_pipe, out front_pipe);

                    //Generate the end pipe
                    Brep end_pipe = Brep.CreatePipe(bestEndRoute, 3, true, PipeCapMode.Round, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];
                    difference = Brep.CreateBooleanDifference(end_pipe, currModel_Hollowed, myDoc.ModelAbsoluteTolerance);
                    if (difference != null && difference.Length > 0)
                        GetSimilarVolumeBrep(difference, end_pipe, out end_pipe);
                    difference = Brep.CreateBooleanDifference(end_pipe, conductiveObjects[closest_index].brep, myDoc.ModelAbsoluteTolerance);
                    if (difference != null && difference.Length > 0)
                        GetSimilarVolumeBrep(difference, end_pipe, out end_pipe);

                    //Generate the customized part
                    Curve pipe_end_circle = new Circle(new Plane(bestEndRoute.PointAtEnd, bestEndRoute.TangentAtEnd), 3).ToNurbsCurve();
                    if (!Curve.DoDirectionsMatch(spring_part_circle, pipe_end_circle))
                        pipe_start_circle.Reverse();
                    start = spring_part_circle.PointAtStart;
                    pipe_end_circle.ClosestPoint(start, out t);
                    pipe_end_circle.ChangeClosedCurveSeam(t);
                    crossSectionCurves = new Curve[] { spring_part_circle, pipe_end_circle };
                    loftBreps = Brep.CreateFromLoft(crossSectionCurves, Point3d.Unset, Point3d.Unset, LoftType.Normal, false);
                    Brep spring_extension = loftBreps[0];
                    spring_extension = spring_extension.CapPlanarHoles(myDoc.ModelAbsoluteTolerance);

                    if (differences != null && differences.Length > 0)
                    {
                        GetSimilarVolumeBrep(differences, currModel_Hollowed, out currModel_Hollowed);
                    }

                    Guid source_extension_guid = myDoc.Objects.Add(source_extension);
                    Guid front_pipe_guid = myDoc.Objects.Add(front_pipe);
                    Guid end_pipe_guid = myDoc.Objects.Add(end_pipe);
                    Guid spring_extension_guid = myDoc.Objects.Add(spring_extension);

                    specialPipes.Add(source_extension_guid);
                    specialPipes.Add(front_pipe_guid);
                    specialPipes.Add(end_pipe_guid);
                    specialPipes.Add(spring_extension_guid);

                    conductive_pipes_guid.Add(source_extension_guid);
                    conductive_pipes_guid.Add(front_pipe_guid);
                    conductive_pipes_guid.Add(end_pipe_guid);
                    conductive_pipes_guid.Add(spring_extension_guid);

                    conductive_pipes.Add(source_extension);
                    conductive_pipes.Add(front_pipe);
                    conductive_pipes.Add(end_pipe);
                    conductive_pipes.Add(spring_extension);

                    conductiveObjects.RemoveAt(closest_index);
                    combinableLightPipe.RemoveAt(closest_index);
                    combinableLightPipeRoute.RemoveAt(closest_index);

                    differences = Brep.CreateBooleanDifference(currModel_Hollowed, spring_extension, myDoc.ModelAbsoluteTolerance);

                    if (differences != null && differences.Length > 0)
                    {
                        GetSimilarVolumeBrep(differences, currModel_Hollowed, out currModel_Hollowed);
                    }

                    conductive_pipes.Add(conductive_part);
                    conductive_pipes_guid.Add(myDoc.Objects.Add(conductive_part));
                    conductive_pipes.Add(spring_part);
                    conductive_pipes_guid.Add(myDoc.Objects.Add(spring_part));
                }

                //currModel_Hollowed_ObjId = myDoc.Objects.Add(currModel_Hollowed);
            }
            else
            {
                RhinoApp.WriteLine("Fail to create a button on the selected area");
                var allObjects = new List<RhinoObject>(RhinoDoc.ActiveDoc.Objects.GetObjectList(ObjectType.AnyObject));
                return false;
            }

            myDoc.Objects.Hide(currModelObjId, true);
            return true;
        }

        private void GetConductivePipeExits(Brep currModel)
        {
            BoundingBox boundingBox = currModel.GetBoundingBox(true);

            //Left upper corner of the PCB
            Point3d leftUpperCorner = new Point3d(pcb_origin.X + 15, pcb_origin.Y + 70, pcb_origin.Z);

            //Right upper corner of the PCB
            Point3d rightUpperCorner = new Point3d(pcb_origin.X + 70, pcb_origin.Y + 70, pcb_origin.Z);

            //Left lower corner of the PCB
            Point3d leftLowerCorner = new Point3d(pcb_origin.X + 15, pcb_origin.Y + 15, pcb_origin.Z);

            //Right lower corner of the PCB
            Point3d rightLowerCorner = new Point3d(pcb_origin.X + 70, pcb_origin.Y + 15, pcb_origin.Z);

            Index lu = FindClosestPointIndex(leftUpperCorner, currModel, "accurate");
            Index ru = FindClosestPointIndex(rightUpperCorner, currModel, "accurate");
            Index ll = FindClosestPointIndex(leftLowerCorner, currModel, "accurate");
            Index rl = FindClosestPointIndex(rightLowerCorner, currModel, "accurate");

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



            conductivePipeExitPts.Add(new PipeExit(voxelSpace[lu.i, lu.j, lu.k], leftUpperCorner));
            conductivePipeExitPts.Add(new PipeExit(voxelSpace[ru.i, ru.j, ru.k], rightUpperCorner));
            conductivePipeExitPts.Add(new PipeExit(voxelSpace[ll.i, ll.j, ll.k], leftLowerCorner));
            conductivePipeExitPts.Add(new PipeExit(voxelSpace[rl.i, rl.j, rl.k], rightLowerCorner));

            myDoc.Objects.AddPoint(leftUpperCorner);
            myDoc.Objects.AddPoint(rightUpperCorner);
            myDoc.Objects.AddPoint(leftLowerCorner);
            myDoc.Objects.AddPoint(rightLowerCorner);
        }

        private bool GetLongestCurve(Curve[] curves, out Curve curve)
        {
            if(curves != null && curves.Length > 0)
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

        public bool GenerateVibration(ref Item savedItem)
        {
            return false;
        }

        public bool GenerateTouch(ref Item savedItem)
        {
            return false;
        }

        #region Rotational Motion Helper function
        public List<Brep> getAllBreps()
        {
            List<Brep> allBreps = new List<Brep>();
            foreach (var item in myDoc.Objects.GetObjectList(ObjectType.Brep))
            {
                ObjRef objRef = new ObjRef(myDoc, item.Id);
                allBreps.Add(objRef.Brep());
            }
            return allBreps;
        }

        public List<Guid> getAllBrepsGuid()
        {
            List<Guid> allBreps = new List<Guid>();
            foreach (var item in myDoc.Objects.GetObjectList(ObjectType.Brep))
            {
                allBreps.Add(item.Id);
            }
            return allBreps;
        }

        /// <summary>
        /// This function check if the input brep is intersected with any breps in the current view
        /// </summary>
        /// <param name="gear">an input brep</param>
        /// <returns>return false if no intersection found.</returns>
        public bool checkIntersection(Brep input_brep, List<Guid> allowBreps)
        {
            for (int i = 0; i < allBreps.Count; i++)
            {
                if (allowBreps.Any(guid => guid == allBreps_guid[i]))
                    continue;
                if (Intersection.BrepBrep(input_brep, allBreps[i], myDoc.ModelAbsoluteTolerance, out Curve[] curves, out Point3d[] pts))
                {
                    if (curves.Length != 0 || pts.Length != 0)
                        return true;
                }
            }
            return false;
        }

        public bool IsBrepInsideBrep(Brep brep1, Brep brep2)
        {
            // Get the bounding box of brep1
            BoundingBox bbox = brep1.GetBoundingBox(true);

            // Get the center point of the bounding box
            Point3d center = bbox.Center;

            // Check if the center point of brep1 is inside brep2
            return brep2.IsPointInside(center, Rhino.RhinoMath.ZeroTolerance, true);
        }


        private Vector3d GetOrthogonalWithMinZ(Vector3d original)
        {
            Vector3d orthogonal = new Vector3d();
            orthogonal.PerpendicularTo(original);
            double smallest_z = orthogonal.Z;
            Vector3d orthogonal_temp = orthogonal;
            int count = 0;
            while (count < 360)
            {
                orthogonal_temp.Rotate(Math.PI / 180, original);
                if (smallest_z >= orthogonal_temp.Z)
                {
                    orthogonal = orthogonal_temp;
                    smallest_z = orthogonal_temp.Z;
                }
                count++;
            }
            return orthogonal;
        }

        /// <summary>
        /// This method calculates the number of teeth given the tip radius of the gear.
        /// </summary>
        /// <param name="tipRadius">the tip radius of the target gear</param>
        /// <returns>number of teeth</returns>
        private int getNumTeeth(double tipRadius)
        {
            int numTeeth = ((int)((2 * tipRadius - 2 * module) / module));
            return numTeeth;
        }

        private double getTipRadius(int teethNum)
        {
            double pitchDiameter = module * teethNum;
            double outDiameter = pitchDiameter + 2 * module;
            return outDiameter / 2;
        }

        private double getPitchRadius(int teethNum)
        {
            double pitchDiameter = module * teethNum;
            return pitchDiameter / 2;
        }
        #endregion

        #region LED Light Helper function
        private void GetVoxelSpace(Brep currModel, int mode, Brep customized_part = null, List<BoundingBox> tempBoxes = null, Brep ignorePart = null)
        {
            if (mode == 1) // For initializing the voxelSpace
            {
                var allObjects = new List<RhinoObject>(myDoc.Objects.GetObjectList(ObjectType.Brep));
                List<BoundingBox> normalObjects_BrepBox = new List<BoundingBox>();
                List<Brep> specialObjects_Brep = new List<Brep>();
                List<BoundingBox> boundingBoxes = new List<BoundingBox>();
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
                            //allObjects.Remove(item);
                            continue;
                        }
                        else if (brep.IsDuplicate(customized_part, myDoc.ModelAbsoluteTolerance))
                        {
                            customized_part_Guid = guid;
                            //allObjects.Remove(item);
                            continue;
                        }
                        else if (ignorePipesGuid.Any(g => guid == g))
                        {
                            //allObjects.Remove(item);
                            continue;
                        }
                        if (specialPipes.Any(g => guid == g))
                        {
                            specialObjects_Brep.Add(brep);
                            continue;
                        }

                        BoundingBox boundingbox = brep.GetBoundingBox(true);
                        if (!boundingbox.Equals(BoundingBox.Empty))
                        {
                            boundingbox.Inflate(6);
                            normalObjects_BrepBox.Add(boundingbox);
                        }

                    }
                }
                BoundingBox foundation_box = foundation.GetBoundingBox(true);
                foundation_box.Inflate(0, 0, -7);
                normalObjects_BrepBox.Add(foundation_box);
                //myDoc.Objects.Add(foundation_box.ToBrep());
                //foreach (var box in normalObjects_BrepBox)
                //    myDoc.Objects.Add(box.ToBrep());

                BoundingBox boundingBox = currModel.GetBoundingBox(true);

                int w = (int)Math.Abs(boundingBox.Max.X - boundingBox.Min.X) /2; //width
                int l = (int)Math.Abs(boundingBox.Max.Y - boundingBox.Min.Y) /2; //length
                int h = (int)Math.Abs(boundingBox.Max.Z - boundingBox.Min.Z) /2; //height



                voxelSpace = new Voxel[w, l, h];

                Double pipe_radius = 1; //pipe width
                Double thickness = 3; //hollowed-out model thickness
                Double distance_from_edge = pipe_radius + thickness;

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
                            if (voxelSpace[i, j, k].isTaken == false)
                            {
                                if (currModel.ClosestPoint(currentPt).DistanceTo(currentPt) < distance_from_edge)
                                {
                                    voxelSpace[i, j, k].isTaken = true;
                                    continue;
                                }
                                foreach (var box in normalObjects_BrepBox)
                                {
                                    //See if the point is strictly inside of the brep
                                    if (currentPt.X < box.Max.X && currentPt.Y < box.Max.Y && currentPt.Z < box.Max.Z && currentPt.X > box.Min.X && currentPt.Y > box.Min.Y && currentPt.Z > box.Min.Z)
                                    {
                                        voxelSpace[i, j, k].isTaken = true;
                                        break;
                                    }
                                }
                                foreach (var brep in specialObjects_Brep)
                                {
                                    //See if the point is strictly inside of the brep
                                    if (brep != null)
                                    {
                                        if (brep.IsPointInside(currentPt, myDoc.ModelAbsoluteTolerance, true))
                                        {
                                            voxelSpace[i, j, k].isTaken = true;
                                            break;
                                        }

                                        //See if the point is too close to the brep and will cause intersection after creating the pipe
                                        if (brep.ClosestPoint(currentPt).DistanceTo(currentPt) <= pipe_radius)
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

                int w = (int)Math.Abs((boundingBox.Max.X - boundingBox.Min.X) / 2); //width
                int l = (int)Math.Abs((boundingBox.Max.Y - boundingBox.Min.Y) / 2); //length
                int h = (int)Math.Abs((boundingBox.Max.Z - boundingBox.Min.Z) / 2); //height


                Parallel.For(0, w, i =>
                {
                    for (int j = 0; j < l; j++)
                    {
                        for (int k = 0; k < h; k++)
                        {
                            foreach (var box in tempBoxes)
                            {
                                Point3d currentPt = new Point3d(voxelSpace[i, j, k].X, voxelSpace[i, j, k].Y, voxelSpace[i, j, k].Z);
                                if (currentPt.X < box.Max.X && currentPt.Y < box.Max.Y && currentPt.Z < box.Max.Z && currentPt.X > box.Min.X && currentPt.Y > box.Min.Y && currentPt.Z > box.Min.Z)
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

        private void GetLightPipeExits(Brep currModel)
        {
            BoundingBox boundingBox = currModel.GetBoundingBox(true);

            //Left upper corner of the PCB
            Point3d leftUpperCorner = new Point3d(pcb_origin.X + 6.2, pcb_origin.Y + 46.7, pcb_origin.Z);

            //Right upper corner of the PCB
            Point3d rightUpperCorner = new Point3d(pcb_origin.X + 45.8, pcb_origin.Y + 46.3, pcb_origin.Z);

            //Left lower corner of the PCB
            Point3d leftLowerCorner = new Point3d(pcb_origin.X + 2.9, pcb_origin.Y + 5.55, pcb_origin.Z);

            //Right lower corner of the PCB
            Point3d rightLowerCorner = new Point3d(pcb_origin.X + 44.5, pcb_origin.Y + 6.5, pcb_origin.Z);

            Index lu = FindClosestPointIndex(leftUpperCorner, currModel, "accurate");
            Index ru = FindClosestPointIndex(rightUpperCorner, currModel, "accurate");
            Index ll = FindClosestPointIndex(leftLowerCorner, currModel, "accurate");
            Index rl = FindClosestPointIndex(rightLowerCorner, currModel, "accurate");

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



            ledPipeExitPts.Add(new PipeExit(voxelSpace[lu.i, lu.j, lu.k], leftUpperCorner));
            ledPipeExitPts.Add(new PipeExit(voxelSpace[ru.i, ru.j, ru.k], rightUpperCorner));
            ledPipeExitPts.Add(new PipeExit(voxelSpace[ll.i, ll.j, ll.k], leftLowerCorner));
            ledPipeExitPts.Add(new PipeExit(voxelSpace[rl.i, rl.j, rl.k], rightLowerCorner));

            myDoc.Objects.AddPoint(leftUpperCorner);
            myDoc.Objects.AddPoint(rightUpperCorner);
            myDoc.Objects.AddPoint(leftLowerCorner);
            myDoc.Objects.AddPoint(rightLowerCorner);
        }

        /// <summary>
        /// This method generates the total angle of a list of control points of a curve of degree 1
        /// </summary>
        /// <param name="controlPoints">a list of Point3d object that is meant to be control points of a curve of degree 1</param>
        /// <returns>The total angle of the curve</returns>
        private double AngleOfCurve(List<Point3d> controlPoints)
        {
            //Calculate the total angles
            double angleSum = 0.0;
            for (int i = 0; i < controlPoints.Count - 2; i++)
            {
                Vector3d dir1 = controlPoints[i + 1] - controlPoints[i];
                Vector3d dir2 = controlPoints[i + 2] - controlPoints[i + 1];

                double angle = Vector3d.VectorAngle(dir1, dir2);
                angleSum += angle;
            }

            // Convert the angle to degrees
            angleSum = RhinoMath.ToDegrees(angleSum);

            return angleSum;
        }

        /// <summary>
        /// This method generate a box that connect two breps' bounding boxes on overlap areas on x-y plane
        /// </summary>
        /// <param name="brepA">a Brep Object</param>
        /// <param name="brepB">a Brep Object</param>
        /// <returns>a bounding box that connects two breps</returns>
        private BoundingBox GetOverlapBoundingBox(Brep brepA, Brep brepB)
        {
            BoundingBox bboxA = brepA.GetBoundingBox(true);
            BoundingBox bboxB = brepB.GetBoundingBox(true);


            // Calculate overlap region in X and Y dimensions
            double xMin = Math.Max(bboxA.Min.X, bboxB.Min.X);
            double xMax = Math.Min(bboxA.Max.X, bboxB.Max.X);
            double yMin = Math.Max(bboxA.Min.Y, bboxB.Min.Y);
            double yMax = Math.Min(bboxA.Max.Y, bboxB.Max.Y);

            // Calculate overlap region in Z dimension
            double zMin = Math.Max(bboxA.Min.Z, bboxB.Min.Z);
            double zMax = Math.Min(bboxA.Max.Z, bboxB.Max.Z);

            // Calculate the dimensions of the overlap box
            double width = xMax - xMin;
            double length = yMax - yMin;
            double height = zMax - zMin;

            // Create the overlapping bounding box
            Point3d min = new Point3d(xMin, yMin, zMin);
            Point3d max = new Point3d(xMin + width, yMin + length, zMax);
            BoundingBox overlapBox = new BoundingBox(min, max);

            return overlapBox;
        }

        /// <summary>
        /// This method determines if two breps' bounding boxes are overlaped on x-y plane
        /// </summary>
        /// <param name="brepA"></param>
        /// <param name="brepB"></param>
        /// <returns></returns>
        private bool OverlapsInXY(Brep brepA, Brep brepB)
        {
            // Get bounding boxes for brepA and brepB
            BoundingBox bboxA = brepA.GetBoundingBox(true);
            BoundingBox bboxB = brepB.GetBoundingBox(true);

            // Check if the bounding boxes overlap in the X-Y dimension but not in the Z dimension
            bool overlapInX = (bboxA.Max.X > bboxB.Min.X && bboxA.Min.X < bboxB.Max.X) || (bboxB.Max.X > bboxA.Min.X && bboxB.Min.X < bboxA.Max.X);
            bool overlapInY = (bboxA.Max.Y > bboxB.Min.Y && bboxA.Min.Y < bboxB.Max.Y) || (bboxB.Max.Y > bboxA.Min.Y && bboxB.Min.Y < bboxA.Max.Y);
            //bool overlapInZ = (bboxA.Max.Z > bboxB.Min.Z && bboxA.Min.Z < bboxB.Max.Z) || (bboxB.Max.Z > bboxA.Min.Z && bboxB.Min.Z < bboxA.Max.Z);

            return overlapInX && overlapInY;
        }


        /// <summary>
        /// This method generates overlap boxes of all generated Brep objects in the view, except pipes, currModel and customized_part
        /// </summary>
        /// <param name="customized_part">The customized part that user wants</param>
        /// <param name="currModel">current model that the user wants to add pipe into</param>
        private List<BoundingBox> CombineBreps(Brep customized_part, Brep currModel)
        {
            List<Brep> breps = new List<Brep>();
            var allObjects = new List<RhinoObject>(myDoc.Objects.GetObjectList(ObjectType.Brep));
            foreach (var item in allObjects)
            {
                Guid guid = item.Id;
                ObjRef currObj = new ObjRef(myDoc, guid);
                Brep brep = currObj.Brep();

                if (brep != null)
                {
                    //Ignore the current model and customized part
                    if (brep.IsDuplicate(currModel, myDoc.ModelAbsoluteTolerance) || brep.IsDuplicate(customized_part, myDoc.ModelAbsoluteTolerance) || allPipes.Any(pipeBrep => brep.IsDuplicate(pipeBrep, myDoc.ModelAbsoluteTolerance)))
                    {
                        continue;
                    }
                    breps.Add(brep);
                }
            }

            Sort.Quicksort(breps, 0, breps.Count - 1);

            List<BoundingBox> allTempBox = new List<BoundingBox>();
            for (int i = breps.Count - 1; i > 0; i--)
            {
                for (int j = i - 1; j >= 0; j--)
                {
                    BoundingBox overlapBox;
                    if (breps[j].GetBoundingBox(true).Min.Z < breps[i].GetBoundingBox(true).Min.Z && OverlapsInXY(breps[i], breps[j]))
                    {
                        overlapBox = GetOverlapBoundingBox(breps[i], breps[j]);
                        if (overlapBox.ToBrep() != null)
                        {
                            overlapBox.Inflate(7);
                            allTempBox.Add(overlapBox);
                        }
                    }
                }
            }
            return allTempBox;
        }


        /// <summary>
        /// This method finds the estimated index in the 3D grid of the current model that has the closest location to the given point
        /// </summary>
        /// <param name="point">A point that needs to be estimated</param>
        /// <param name="currModel">current model that the user wants to add pipe into</param>
        /// <returns>the estimated index in the 3D grid of the current model</returns>
        private Index FindClosestPointIndex(Point3d point, Brep currModel, string mode = null)
        {
            Index index = new Index();

            if (mode == null)
            {
                //Calculate the approximate index of Point3d. Then obtain the precise index that has the smallest distance within the 2*2*2 bounding box of the Point3d
                BoundingBox boundingBox = currModel.GetBoundingBox(true);

                #region Calculate an estimated index
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
            }

            else
            {
                //Calculate the approximate index of Point3d. Then obtain the precise index that has the smallest distance within the 2*2*2 bounding box of the Point3d
                BoundingBox boundingBox = currModel.GetBoundingBox(true);

                #region Calculate an estimated index
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

                #region Traverse the 5*5*5 bounding box of the estimated index to see if there is a better one
                for (int i = estimated_i - 8; i < estimated_i + 9; i++)
                {
                    for (int j = estimated_j - 8; j < estimated_j + 9; j++)
                    {
                        for (int k = estimated_k - 8; k < estimated_k + 9; k++)
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
            }



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
        #endregion


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

            if(start.isTaken == true)
                return new List<Point3d>();
            if (goal.isTaken == true)
                return new List<Point3d>();

            start.Cost = 0; //TODO: Start is null, please FIX
            start.Distance = 0;
            start.SetDistance(goal.X, goal.Y, goal.Z);

            Queue<Voxel> voxelRoute = new Queue<Voxel>();

            #endregion

            //int count = 0;
            //foreach (var item in voxelSpace)
            //{
            //    if (item.isTaken == false)
            //        count++;
            //}


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
                            if (next != goal && next.Z < goal.Z + 5 && next.Z > goal.Z)
                                new_cost -= 3;
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
            if (mode == 1)
            {
                List<Point3d> interpolatedRoute_Point3d = new List<Point3d>();
                interpolatedRoute_Point3d.Add(bestRoute_Point3d[0]);

                Boolean isIntersected = false;
                int index = 1;
                Guid temp_guid = myDoc.Objects.Add(currModel_Hollowed);
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
                    //betterRoute = betterRoute.Trim(CurveEnd.Both, betterRoute.GetLength()/8);
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
                            //Ignore the current model and customized part
                            if (brep.IsDuplicate(customized_part, myDoc.ModelAbsoluteTolerance) || ignorePipesGuid.Any(g => guid == g))
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
                myDoc.Objects.Delete(temp_guid, true);
                return interpolatedRoute_Point3d;
            }
            
            #endregion
            return bestRoute_Point3d;

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

        public bool GetSimilarVolumeBrep(IEnumerable<Brep> breps, Brep originalBrep, out Brep brep)
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
    }
}


