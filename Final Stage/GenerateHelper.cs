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

namespace DynaModel_v2.Final_Stage
{
    public class GenerateHelper
    {
        //General parameter
        RhinoDoc myDoc = RhinoDoc.ActiveDoc;
        Guid currModelObjId = Guid.Empty;
        Brep currModel = null;
        List<Brep> allBreps = new List<Brep>();
        List<Guid> allBreps_guid = new List<Guid>();
        Voxel[,,] voxelSpace = null;
        List<Brep> allPipes = new List<Brep>();
        ObjectAttributes solidAttribute, lightGuideAttribute, redAttribute, yellowAttribute, soluableAttribute;

        //Gear parameter
        private double module = 1.5;
        private double pressure_angle = 20;
        private double thickness = 5;
        private double ratio;
        private double clearance = 0.5;

        //LED Light parameter
        private double pcbWidth = 20;
        private double pcbHeight = 20;
        private double pipeRadius = 3.5;
        private int voxelSpace_offset = 1;
        private List<Guid> allTempBoxesGuid= new List<Guid>();
        private List<PipeExit> ledPipeExitPts = new List<PipeExit>();
        private List<Curve> combinableLightPipeRoute = new List<Curve>();
        private List<Brep> combinableLightPipe = new List<Brep>();
        private List<InViewObject> conductiveObjects = new List<InViewObject>();

        


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
            }

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

            if (cuttedBrep.Length != 2)
                return false;

            //Update current model
            if (cuttedBrep[0].Equals(endEffector))
                currModel = cuttedBrep[1];
            else
                currModel = cuttedBrep[0];

            myDoc.Objects.Hide(currModelObjId, true);
            myDoc.Views.Redraw();
            currModelObjId = myDoc.Objects.Add(currModel);
            myDoc.Views.Redraw();
            myDoc.Objects.Show(endEffectorObjId, true);
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
            Point3d start_gear_centerPoint = new Point3d(mainModel_bbox.Center.X, mainModel_bbox.Center.Y, mainModel_bbox.Min.Z + 3);
            Vector3d start_gear_Direction = new Vector3d(0, 0, 1);
            Vector3d start_gear_xDir = new Vector3d(0, 0, 0);
            int start_gear_teethNum = 10;
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
            Vector3d end_gear_dir = save_item.gearEssentials.CutterPlane.Normal;
            //double distance = pointConnection1.DistanceTo(mainModel.GetBoundingBox(true).Center);
            double distance = pointConnection1.DistanceTo(mainModel.ClosestPoint(pointConnection1)) + 1;
            Line endEffector_rail = new Line(pointConnection1, end_gear_dir, distance);

            if (!cuttedBrep[0].IsManifold || !cuttedBrep[0].IsSolid)
            {
                RhinoApp.WriteLine("This rotational motion item cannot be created. Due to impatible to other items");
                return false;
            }


            if (!cuttedBrep[0].IsPointInside(endEffector_rail.To, myDoc.ModelAbsoluteTolerance, true)) // TODO: We need to make sure if the main model is closed and manifold. Perhaps write a function to fix the original model in the first place.
            {
                end_gear_dir.Reverse();
                endEffector_rail = new Line(pointConnection1, end_gear_dir, distance);
            }
            #endregion

            #region Calculate the cone angle of the end bevel gear and its driven gear
            double end_gear_coneAngle = RhinoMath.ToDegrees(Vector3d.VectorAngle(new Vector3d(0, 0, 1), end_gear_dir));

            myDoc.Views.Redraw();
            #endregion

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
            int end_gear_teethNum = 10;
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
            int first_driven_gear_teethNum = 10;
            double first_driven_gear_pitchRadius = getPitchRadius(first_driven_gear_teethNum) + clearance;

            Line rail5 = new Line(rail1.To, new Vector3d(rail2.Direction.X, rail2.Direction.Y, 0), first_driven_gear_pitchRadius);


            Point3d first_driven_gear_centerPoint = rail5.To;
            Vector3d first_driven_gear_Direction = new Vector3d(0, 0, 1);
            Vector3d first_driven_gear_xDir = new Vector3d(0, 0, 0);
            double first_driven_gear_selfRotAngle = 0;
            if (first_driven_gear_centerPoint.X > end_gear_centerPoint.X && first_driven_gear_centerPoint.Y < end_gear_centerPoint.Y)
            {
                RhinoApp.WriteLine("first_driven_gear_centerPoint At fourth axis");
                first_driven_gear_selfRotAngle = -RhinoMath.ToDegrees(Vector3d.VectorAngle(new Vector3d(rail5.Direction.X, rail5.Direction.Y, 0), new Vector3d(1, 0, 0)));
                end_gear.Rotate(first_driven_gear_selfRotAngle - 360 / first_driven_gear_teethNum / 2);
            }
            else if (first_driven_gear_centerPoint.X > end_gear_centerPoint.X && first_driven_gear_centerPoint.Y > end_gear_centerPoint.Y)
            {
                RhinoApp.WriteLine("first_driven_gear_centerPoint At first axis");
                first_driven_gear_selfRotAngle = RhinoMath.ToDegrees(Vector3d.VectorAngle(new Vector3d(rail5.Direction.X, rail5.Direction.Y, 0), new Vector3d(1, 0, 0)));
                end_gear.Rotate(first_driven_gear_selfRotAngle - 360 / first_driven_gear_teethNum / 2);
            }
            else if (first_driven_gear_centerPoint.X < end_gear_centerPoint.X && first_driven_gear_centerPoint.Y < end_gear_centerPoint.Y)
            {
                RhinoApp.WriteLine("first_driven_gear_centerPoint At third axis");
                first_driven_gear_selfRotAngle = -RhinoMath.ToDegrees(Vector3d.VectorAngle(new Vector3d(rail5.Direction.X, rail5.Direction.Y, 0), new Vector3d(1, 0, 0)));
                end_gear.Rotate(first_driven_gear_selfRotAngle - 360 / first_driven_gear_teethNum / 2);
            }
            else if (first_driven_gear_centerPoint.X < end_gear_centerPoint.X && first_driven_gear_centerPoint.Y > end_gear_centerPoint.Y)
            {
                RhinoApp.WriteLine("first_driven_gear_centerPoint At second axis");
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
            if (second_driven_gear_tipRadius > getTipRadius(4))
            {
                Line start_gear_connection_rail = new Line(start_gear.CenterPoint, connector_gear.CenterPoint);
                start_gear_connection_rail = new Line(start_gear.CenterPoint, start_gear_connection_rail.Direction, start_gear.BaseRadius + second_driven_gear_tipRadius);
                second_driven_gear_centerPoint = start_gear_connection_rail.To;
                second_driven_gear = new SpurGear(second_driven_gear_centerPoint, second_driven_gear_Direction, second_driven_gear_xDir, second_driven_gear_teethNum, module, pressure_angle, thickness, second_driven_gear_selfRotAngle, true);
            }
            #endregion

            #region shafts of first driven gear and connector gear
            Line rail6 = new Line(new Point3d(first_driven_gear.CenterPoint.X, first_driven_gear.CenterPoint.Y, first_driven_gear.Boundingbox.Max.Z), new Point3d(first_driven_gear.CenterPoint.X, first_driven_gear.CenterPoint.Y, connector_gear.Boundingbox.Min.Z));
            rail6.Extend(1, 1);
            Brep shaft = Brep.CreatePipe(rail6.ToNurbsCurve(), 1.5, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];
            rail6.Extend(1, 1);
            Brep shaft_clearance = Brep.CreatePipe(rail6.ToNurbsCurve(), 1.7, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];
            #endregion

            #region gaskets for first driven gear and connector gear
            //Gaskets of first driven gear
            Point3d startPoint = new Point3d(first_driven_gear.CenterPoint.X, first_driven_gear.CenterPoint.Y, first_driven_gear.Boundingbox.Min.Z - 2);
            Point3d endPoint = new Point3d(first_driven_gear.CenterPoint.X, first_driven_gear.CenterPoint.Y, first_driven_gear.Boundingbox.Min.Z - 0.3);
            rail6 = new Line(startPoint, endPoint);
            Brep first_driven_gear_bottom_gasket = Brep.CreateThickPipe(rail6.ToNurbsCurve(), 1.7, 4, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];

            startPoint.Z = first_driven_gear.Boundingbox.Max.Z + 3;
            endPoint.Z = first_driven_gear.Boundingbox.Max.Z + 0.3;
            rail6 = new Line(startPoint, endPoint);
            Brep first_driven_gear_top_gasket = Brep.CreateThickPipe(rail6.ToNurbsCurve(), 1.7, 4, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];

            //Gaskets of connector gear
            startPoint = new Point3d(connector_gear.CenterPoint.X, connector_gear.CenterPoint.Y, connector_gear.Boundingbox.Min.Z - 2);
            endPoint = new Point3d(connector_gear.CenterPoint.X, connector_gear.CenterPoint.Y, connector_gear.Boundingbox.Min.Z - 0.3);
            rail6 = new Line(startPoint, endPoint);
            Brep connector_gear_bottom_gasket = Brep.CreateThickPipe(rail6.ToNurbsCurve(), 1.7, 4, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];

            startPoint.Z = connector_gear.Boundingbox.Max.Z + 3;
            endPoint.Z = connector_gear.Boundingbox.Max.Z + 0.3;
            rail6 = new Line(startPoint, endPoint);
            Brep connector_gear_top_gasket = Brep.CreateThickPipe(rail6.ToNurbsCurve(), 1.7, 4, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];
            #endregion

            #region gaskets and shafts for end gear
            Line extended_endEffector_rail = endEffector_rail;
            extended_endEffector_rail.Extend(100, 100);

            Brep end_gear_bottom_gasket = null;
            Brep end_gear_top_gasket = null;
            Brep end_gear_shaft = null;
            Brep end_gear_clearance_shaft = null;
            if (Intersection.CurveBrep(extended_endEffector_rail.ToNurbsCurve(), end_gear.Boundingbox_big, myDoc.ModelAbsoluteTolerance, out _, out intersectionPoints))
            {
                Vector3d dir = endEffector_rail.Direction;
                Line r1 = new Line(intersectionPoints[0], dir, 1.7);
                dir.Reverse();
                Line r2 = new Line(intersectionPoints[1], dir, 2.7);
                end_gear_bottom_gasket = Brep.CreateThickPipe(r1.ToNurbsCurve(), 1.7, 4, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];
                end_gear_top_gasket = Brep.CreateThickPipe(r2.ToNurbsCurve(), 1.7, 4, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];
                //myDoc.Objects.Add(end_gear_bottom_gasket);
                //myDoc.Objects.Add(end_gear_top_gasket);

                distance = pointConnection1.DistanceTo(intersectionPoints[1]) - 1;
                rail6 = new Line(pointConnection1, end_gear_dir, distance);
                end_gear_shaft = Brep.CreatePipe(rail6.ToNurbsCurve(), 1.5, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];

                distance += 2;
                rail6 = new Line(pointConnection1, end_gear_dir, distance);
                end_gear_clearance_shaft = Brep.CreatePipe(rail6.ToNurbsCurve(), 1.7, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];
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
                if (second_driven_gear_tipRadius > getTipRadius(4))
                {
                    second_driven_gear_teethNum = getNumTeeth(second_driven_gear_tipRadius);
                    Line start_gear_connection_rail = new Line(start_gear.CenterPoint, connector_gear.CenterPoint);
                    start_gear_connection_rail = new Line(start_gear.CenterPoint, start_gear_connection_rail.Direction, start_gear.BaseRadius + second_driven_gear_tipRadius);
                    second_driven_gear_centerPoint = start_gear_connection_rail.To;
                    second_driven_gear = new SpurGear(second_driven_gear_centerPoint, second_driven_gear_Direction, second_driven_gear_xDir, second_driven_gear_teethNum, module, pressure_angle, thickness, second_driven_gear_selfRotAngle, false);
                }
                else
                {
                    second_driven_gear = null;
                }

                //Adjust the shaft and gaskets for connector gear and first driven gear
                rail6 = new Line(new Point3d(first_driven_gear.CenterPoint.X, first_driven_gear.CenterPoint.Y, first_driven_gear.Boundingbox.Max.Z), new Point3d(first_driven_gear.CenterPoint.X, first_driven_gear.CenterPoint.Y, connector_gear.Boundingbox.Min.Z));
                rail6.Extend(1, 1);
                shaft = Brep.CreatePipe(rail6.ToNurbsCurve(), 1.5, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];
                rail6.Extend(1, 1);
                shaft_clearance = Brep.CreatePipe(rail6.ToNurbsCurve(), 1.7, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];

                startPoint = new Point3d(first_driven_gear.CenterPoint.X, first_driven_gear.CenterPoint.Y, first_driven_gear.Boundingbox.Min.Z - 2);
                endPoint = new Point3d(first_driven_gear.CenterPoint.X, first_driven_gear.CenterPoint.Y, first_driven_gear.Boundingbox.Min.Z - 0.3);
                rail6 = new Line(startPoint, endPoint);
                first_driven_gear_bottom_gasket = Brep.CreateThickPipe(rail6.ToNurbsCurve(), 1.7, 4, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];

                startPoint.Z = first_driven_gear.Boundingbox.Max.Z + 3;
                endPoint.Z = first_driven_gear.Boundingbox.Max.Z + 0.3;
                rail6 = new Line(startPoint, endPoint);
                first_driven_gear_top_gasket = Brep.CreateThickPipe(rail6.ToNurbsCurve(), 1.7, 4, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];

                //Gaskets of connector gear
                startPoint = new Point3d(connector_gear.CenterPoint.X, connector_gear.CenterPoint.Y, connector_gear.Boundingbox.Min.Z - 2);
                endPoint = new Point3d(connector_gear.CenterPoint.X, connector_gear.CenterPoint.Y, connector_gear.Boundingbox.Min.Z - 0.3);
                rail6 = new Line(startPoint, endPoint);
                connector_gear_bottom_gasket = Brep.CreateThickPipe(rail6.ToNurbsCurve(), 1.7, 4, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];

                startPoint.Z = connector_gear.Boundingbox.Max.Z + 3;
                endPoint.Z = connector_gear.Boundingbox.Max.Z + 0.3;
                rail6 = new Line(startPoint, endPoint);
                connector_gear_top_gasket = Brep.CreateThickPipe(rail6.ToNurbsCurve(), 1.7, 4, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];

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


                    Line r1 = new Line(bottomPoint, dir, 1.7);
                    dir.Reverse();
                    Line r2 = new Line(topPoint, dir, 2.7);

                    if (isReversed)
                    {
                        dir.Reverse();
                        r1 = new Line(bottomPoint, dir, 2.7);
                        dir.Reverse();
                        r2 = new Line(topPoint, dir, 1.7);
                    }


                    end_gear_bottom_gasket = Brep.CreateThickPipe(r1.ToNurbsCurve(), 1.7, 4, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];
                    end_gear_top_gasket = Brep.CreateThickPipe(r2.ToNurbsCurve(), 1.7, 4, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];

                    distance = pointConnection1.DistanceTo(topPoint) - 1;
                    rail6 = new Line(pointConnection1, end_gear_dir, distance);
                    end_gear_shaft = Brep.CreatePipe(rail6.ToNurbsCurve(), 1.5, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];

                    distance += 2;
                    rail6 = new Line(pointConnection1, end_gear_dir, distance);
                    end_gear_clearance_shaft = Brep.CreatePipe(rail6.ToNurbsCurve(), 1.7, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];
                    //mainModel = Brep.CreateBooleanDifference(mainModel, end_gear_shaft, myDoc.ModelAbsoluteTolerance, false)[0];
                    //mainModel = Brep.CreateBooleanDifference(mainModel, end_gear_clearance_shaft, myDoc.ModelAbsoluteTolerance, false)[0];
                }
            }
            #endregion


            GearSet bestGearSet = workable_gearsets[0];
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
                    bestGearSet.ConnectorGear.Rotate(180 - angle);
                    bestGearSet.SecondDrivenGear.Rotate(-angle);
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

            myDoc.Objects.Add(bestGearSet.FirstDrivenGearBottomGasket);
            myDoc.Objects.Add(bestGearSet.FirstDrivenGearTopGasket);
            myDoc.Objects.Add(bestGearSet.ConnectorGearBottomGasket);
            myDoc.Objects.Add(bestGearSet.ConnectorGearTopGasket);

            myDoc.Objects.Add(bestGearSet.EndGear.Model);
            if (bestGearSet.SecondDrivenGear != null)
                myDoc.Objects.Add(bestGearSet.SecondDrivenGear.Model);
            myDoc.Objects.Add(bestGearSet.FirstDrivenGear.Model);
            myDoc.Objects.Add(bestGearSet.ConnectorGear.Model);

            myDoc.Objects.Add(bestGearSet.EndGearBottomGasket);
            myDoc.Objects.Add(bestGearSet.EndGearTopGasket);

            myDoc.Objects.Add(bestGearSet.Shaft);
            myDoc.Objects.Add(bestGearSet.EndGearShaft);

            myDoc.Views.Redraw();

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

            Point3d customized_part_center = savedItem.EndPoint;
            Brep customized_part = savedItem.EndPointModel;

            Intersection.BrepBrep(currModel, customized_part, myDoc.ModelAbsoluteTolerance, out Curve[] intersectionCurves, out Point3d[] intersectionPts);
            if (customized_part_center.DistanceTo(currModel.ClosestPoint(customized_part_center)) > 1 || (intersectionCurves.Length == 0 && intersectionPts.Length == 0))
            {
                myDoc.Objects.Add(customized_part);
                myDoc.Objects.AddPoint(customized_part_center);
                RhinoApp.WriteLine("This LED Light item cannot be created. Due to impatible to other items");
                return false;
            }

            voxelSpace = null;
            GetVoxelSpace(currModel, 1, customized_part);

            //Find the Pipe exit location
            if (ledPipeExitPts.Count == 0)
            {
                BoundingBox boundingBox = currModel.GetBoundingBox(true);
                double base_z = boundingBox.Min.Z;
                double base_x = (boundingBox.Max.X - boundingBox.Min.X) / 2 + boundingBox.Min.X;
                double base_y = (boundingBox.Max.Y - boundingBox.Min.Y) / 2 + boundingBox.Min.Y;
                Point3d basePartCenter = new Point3d(base_x, base_y, base_z);
                GetPipeExits(basePartCenter, currModel);
            }

            Point3d pipeExit = new Point3d();
            bool allTaken = true;
            foreach (var item in ledPipeExitPts)
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
                return false;
            }

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
            #endregion

            myDoc.Views.Redraw();

            return true;
        }

        public bool GenerateLightPipe(ref Item savedItem, out List<Brep> subtrahends)
        {
            subtrahends = new List<Brep>();
            allTempBoxesGuid.Clear();

            Point3d customized_part_center = savedItem.EndPoint;
            Brep customized_part = savedItem.EndPointModel;

            Intersection.BrepBrep(currModel, customized_part, myDoc.ModelAbsoluteTolerance, out Curve[] intersectionCurves, out Point3d[] intersectionPts);
            if (customized_part_center.DistanceTo(currModel.ClosestPoint(customized_part_center)) > 1 || (intersectionCurves.Length == 0 && intersectionPts.Length == 0))
            {
                myDoc.Objects.Add(customized_part);
                myDoc.Objects.AddPoint(customized_part_center);
                RhinoApp.WriteLine("This LED Light item cannot be created. Due to impatible to other items");
                return false;
            }

            voxelSpace = null;
            GetVoxelSpace(currModel, 1, customized_part);

            //Find the Pipe exit location
            if (ledPipeExitPts.Count == 0)
            {
                BoundingBox boundingBox = currModel.GetBoundingBox(true);
                double base_z = boundingBox.Min.Z;
                double base_x = (boundingBox.Max.X - boundingBox.Min.X) / 2 + boundingBox.Min.X;
                double base_y = (boundingBox.Max.Y - boundingBox.Min.Y) / 2 + boundingBox.Min.Y;
                Point3d basePartCenter = new Point3d(base_x, base_y, base_z);
                GetPipeExits(basePartCenter, currModel);
            }

            Point3d pipeExit = new Point3d();
            bool allTaken = true;
            foreach (var item in ledPipeExitPts)
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
                return false;
            }

            List<Point3d> bestRoute1 = FindShortestPath(customized_part_center, pipeExit, customized_part, currModel, 1);

            #region Get the line that combine all gears ---> bestRoute2
            List<Brep> allTempBoxes = CombineBreps(customized_part, currModel);
            List<Point3d> bestRoute2 = bestRoute1;
            if (allTempBoxes.Count > 0)
            {
                GetVoxelSpace(currModel, 2, tempBoxes: allTempBoxes);
                bestRoute2 = FindShortestPath(customized_part_center, pipeExit, customized_part, currModel, 1);
            }
            voxelSpace = null;
            #endregion

            #region Determine which line is better by calculating the total angle of the route
            double angle_Route1 = AngleOfCurve(bestRoute1);
            double angle_Route2 = AngleOfCurve(bestRoute2);

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
            //Double[] divisionParameters = bestRoute.DivideByLength(5, true, out Point3d[] points);
            Curve lightGuidePipeRoute = bestRoute.Trim(CurveEnd.Start, bestRoute.GetLength() - 7);
            Curve soluablePipeRoute = bestRoute.Trim(CurveEnd.End, 7);

            List<GeometryBase> geometryBases = new List<GeometryBase>();
            geometryBases.Add(customized_part);

            lightGuidePipeRoute = lightGuidePipeRoute.Extend(CurveEnd.End, 100, CurveExtensionStyle.Line);
            soluablePipeRoute = soluablePipeRoute.Extend(CurveEnd.Start, 100, CurveExtensionStyle.Line);

            Brep[] soluablePipe = Brep.CreatePipe(soluablePipeRoute, 2, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians);
            Brep[] lightGuidePipe = Brep.CreatePipe(lightGuidePipeRoute, 2, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians);
            Brep[] conductivePipe = Brep.CreateThickPipe(soluablePipeRoute, 2, 2.2, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians);

            soluablePipe = Brep.CreateBooleanSplit(soluablePipe[0], currModel, myDoc.ModelAbsoluteTolerance);
            lightGuidePipe = Brep.CreateBooleanSplit(lightGuidePipe[0], currModel, myDoc.ModelAbsoluteTolerance);
            conductivePipe = Brep.CreateBooleanSplit(conductivePipe[0], currModel, myDoc.ModelAbsoluteTolerance);
            Guid conductivePipeGuid = myDoc.Objects.Add(conductivePipe[0], redAttribute);
            InViewObject conductiveObject = new InViewObject(conductivePipe[0], conductivePipeGuid, "conductive pipe");

            soluablePipeRoute = bestRoute.Trim(CurveEnd.End, 7);
            //soluablePipeRoute = soluablePipeRoute.Trim(1, 1);
            combinableLightPipeRoute.Add(soluablePipeRoute);
            combinableLightPipe.Add(soluablePipe[0]);
            conductiveObjects.Add(conductiveObject);

            myDoc.Objects.AddBrep(soluablePipe[0], soluableAttribute); //soluablePipe[1] if used CreateBooleanSplit
            myDoc.Objects.AddBrep(lightGuidePipe[0], lightGuideAttribute);
            #endregion

            myDoc.Views.Redraw();

            return true;
        }

        public bool GenerateSwipePipe(ref Item savedItem)
        {  
            return false;
        }

        public bool GenerateButtonPipe(ref Item savedItem)
        {
            return false;
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
                                Point3d currentPt = new Point3d(voxelSpace[i,j,k].X, voxelSpace[i, j, k].Y, voxelSpace[i, j, k].Z);
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



            ledPipeExitPts.Add(new PipeExit(voxelSpace[lu.i, lu.j, lu.k]));
            ledPipeExitPts.Add(new PipeExit(voxelSpace[ru.i, ru.j, ru.k]));
            ledPipeExitPts.Add(new PipeExit(voxelSpace[ll.i, ll.j, ll.k]));
            ledPipeExitPts.Add(new PipeExit(voxelSpace[rl.i, rl.j, rl.k]));

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
        private List<Brep> CombineBreps(Brep customized_part, Brep currModel)
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

            List<Brep> allTempBox = new List<Brep>();
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
                            allTempBoxesGuid.Add(myDoc.Objects.Add(overlapBox.ToBrep()));
                            allTempBox.Add(overlapBox.ToBrep());
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
        #endregion

    }
}
