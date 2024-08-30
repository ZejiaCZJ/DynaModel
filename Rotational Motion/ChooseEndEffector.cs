using System;
using System.Collections.Generic;
using DynaModel_v2.Geometry;
using Grasshopper.Kernel;
using Rhino.DocObjects;
using Rhino;
using Rhino.Geometry;
using System.Drawing;
using Rhino.Geometry.Intersect;
using Rhino.Input;
using System.Linq;
using DynaModel_v2.SharedData;
using System.Windows.Documents;
using System.Net;
using System.Data;

namespace DynaModel_v2.Rotational_Motion
{
    public class GearSet
    {
        private BevelGear end_gear;
        private Brep end_gear_top_gasket;
        private Brep end_gear_bottom_gasket;
        private Brep end_gear_shaft;
        private Brep end_gear_shaft_clearance;

        private BevelGear first_driven_gear;
        private Brep first_driven_gear_top_gasket;
        private Brep first_driven_gear_bottom_gasket;

        private Brep shaft;
        private Brep shaft_clearance;

        private SpurGear connector_gear;
        private Brep connector_gear_top_gasket;
        private Brep connector_gear_bottom_gasket;

        private SpurGear second_driven_gear;
        private Brep second_driven_gear_shaft;
        private Brep second_driven_gear_top_gasket;
        private Brep second_driven_gear_bottom_gasket;

        private Curve end_gear_shaft_clearance_rail;
        private Curve shaft_clearance_rail;

        private Curve end_gear_shaft_rail;
        private Curve shaft_rail;

        private Curve end_gear_top_gasket_rail;
        private Curve end_gear_bottom_gasket_rail;

        private Curve first_driven_gear_top_gasket_rail;
        private Curve first_driven_gear_bottom_gasket_rail;

        private Curve connector_gear_top_gasket_rail;
        private Curve connector_gear_bottom_gasket_rail;

        private Curve second_driven_gear_top_gasket_rail;
        private Curve second_driven_gear_bottom_gasket_rail;


        public BevelGear EndGear { get => end_gear; set => end_gear = value; }

        public BevelGear FirstDrivenGear { get => first_driven_gear; set => first_driven_gear = value; }

        public SpurGear ConnectorGear { get => connector_gear; set => connector_gear = value; }

        public SpurGear SecondDrivenGear { get => second_driven_gear; set => second_driven_gear = value; }

        public Brep EndGearTopGasket { get => end_gear_top_gasket; set => end_gear_top_gasket = value.DuplicateBrep(); }

        public Brep EndGearBottomGasket { get => end_gear_bottom_gasket; set => end_gear_bottom_gasket = value.DuplicateBrep(); }

        public Brep EndGearShaft { get => end_gear_shaft; set => end_gear_shaft = value.DuplicateBrep(); }

        public Brep FirstDrivenGearTopGasket { get => first_driven_gear_top_gasket; set => first_driven_gear_top_gasket = value.DuplicateBrep(); }

        public Brep FirstDrivenGearBottomGasket { get => first_driven_gear_bottom_gasket; set => first_driven_gear_bottom_gasket = value.DuplicateBrep(); }

        public Brep ConnectorGearTopGasket { get => connector_gear_top_gasket; set => connector_gear_top_gasket = value.DuplicateBrep(); }

        public Brep ConnectorGearBottomGasket { get => connector_gear_bottom_gasket; set => connector_gear_bottom_gasket = value.DuplicateBrep(); }

        public Brep Shaft { get => shaft; set => shaft = value.DuplicateBrep(); }

        public Brep ShaftClearance { get => shaft_clearance; set => shaft_clearance = value.DuplicateBrep(); }

        public Brep EndGearShaftClearance { get => end_gear_shaft_clearance; set => end_gear_shaft_clearance = value.DuplicateBrep(); }

        public Curve EndGearShaftRail { get => end_gear_shaft_rail; set => end_gear_shaft_rail = value; }

        public Curve ShaftRail { get => shaft_rail; set => shaft_rail = value; }

        public Curve EndGearTopGasketRail { get => end_gear_top_gasket_rail; set => end_gear_top_gasket_rail = value; }

        public Curve EndGearBottomGasketRail { get => end_gear_bottom_gasket_rail; set => end_gear_bottom_gasket_rail = value; }

        public Curve FirstDrivenGearTopGasketRail { get => first_driven_gear_top_gasket_rail; set => first_driven_gear_top_gasket_rail = value; }

        public Curve FirstDrivenGearBottomGasketRail { get => first_driven_gear_bottom_gasket_rail; set => first_driven_gear_bottom_gasket_rail = value; }

        public Curve SecondDrivenGearTopGasketRail { get => second_driven_gear_top_gasket_rail; set => second_driven_gear_top_gasket_rail = value; }

        public Curve SecondDrivenGearBottomGasketRail { get => second_driven_gear_bottom_gasket_rail; set => second_driven_gear_bottom_gasket_rail = value; }

        public Curve ConnectorGearTopGasketRail { get => connector_gear_top_gasket_rail; set => connector_gear_top_gasket_rail = value; }

        public Curve ConnectorGearBottomGasketRail { get => connector_gear_bottom_gasket_rail; set => connector_gear_bottom_gasket_rail = value; }

        public Curve EndGearShaftClearanceRail { get => end_gear_shaft_clearance_rail; set => end_gear_shaft_clearance_rail = value; }

        public Curve ShaftClearanceRail { get => shaft_clearance_rail; set => shaft_clearance_rail = value; }
    }



    public class ChooseEndEffector : GH_Component
    {
        private Essentials original_essentials;
        private Brep currModel;
        private Guid currModelObjId;
        private Brep cutter;
        private RhinoDoc myDoc;
        private double module = 1.5;
        private double pressure_angle = 20;
        private double thickness = 5;
        private double ratio;
        private double clearance = 0.5;

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

        ObjectAttributes solidAttribute, lightGuideAttribute, redAttribute, yellowAttribute, soluableAttribute;

        private List<Brep> allBreps;
        private List<Guid> allBreps_guid;


        /// <summary>
        /// Initializes a new instance of the ChooseEndEffector class.
        /// </summary>
        public ChooseEndEffector()
          : base("ChooseEndEffector", "EndEffector",
              "This component create a gear set based on the selected end effector",
              "DynaModel_v2", "Rotational Motion")
        {
            myDoc = RhinoDoc.ActiveDoc;
            int solidIndex = myDoc.Materials.Add();
            Material solidMat = myDoc.Materials[solidIndex];
            solidMat.DiffuseColor = Color.White;
            solidMat.SpecularColor = Color.White;
            solidMat.Transparency = 0;
            solidMat.CommitChanges();
            solidAttribute = new ObjectAttributes();
            //solidAttribute.LayerIndex = 2;
            solidAttribute.MaterialIndex = solidIndex;
            solidAttribute.MaterialSource = ObjectMaterialSource.MaterialFromObject;
            solidAttribute.ObjectColor = Color.White;
            solidAttribute.ColorSource = ObjectColorSource.ColorFromObject;

            int lightGuideIndex = myDoc.Materials.Add();
            Material lightGuideMat = myDoc.Materials[lightGuideIndex];
            lightGuideMat.DiffuseColor = Color.Orange;
            lightGuideMat.Transparency = 0.3;
            lightGuideMat.SpecularColor = Color.Orange;
            lightGuideMat.CommitChanges();
            lightGuideAttribute = new ObjectAttributes();
            //orangeAttribute.LayerIndex = 3;
            lightGuideAttribute.MaterialIndex = lightGuideIndex;
            lightGuideAttribute.MaterialSource = ObjectMaterialSource.MaterialFromObject;
            lightGuideAttribute.ObjectColor = Color.Orange;
            lightGuideAttribute.ColorSource = ObjectColorSource.ColorFromObject;

            int redIndex = myDoc.Materials.Add();
            Material redMat = myDoc.Materials[redIndex];
            redMat.DiffuseColor = Color.Red;
            redMat.Transparency = 0.3;
            redMat.SpecularColor = Color.Red;
            redMat.CommitChanges();
            redAttribute = new ObjectAttributes();
            //redAttribute.LayerIndex = 4;
            redAttribute.MaterialIndex = redIndex;
            redAttribute.MaterialSource = ObjectMaterialSource.MaterialFromObject;
            redAttribute.ObjectColor = Color.Red;
            redAttribute.ColorSource = ObjectColorSource.ColorFromObject;

            int yellowIndex = myDoc.Materials.Add();
            Material yellowMat = myDoc.Materials[yellowIndex];
            yellowMat.DiffuseColor = Color.Yellow;
            yellowMat.Transparency = 0.3;
            yellowMat.SpecularColor = Color.Yellow;
            yellowMat.CommitChanges();
            yellowAttribute = new ObjectAttributes();
            //yellowAttribute.LayerIndex = 4;
            yellowAttribute.MaterialIndex = yellowIndex;
            yellowAttribute.MaterialSource = ObjectMaterialSource.MaterialFromObject;
            yellowAttribute.ObjectColor = Color.Yellow;
            yellowAttribute.ColorSource = ObjectColorSource.ColorFromObject;

            int soluableIndex = myDoc.Materials.Add();
            Material soluableMat = myDoc.Materials[soluableIndex];
            soluableMat.DiffuseColor = Color.Green;
            soluableMat.Transparency = 0.3;
            soluableMat.SpecularColor = Color.Green;
            soluableMat.CommitChanges();
            soluableAttribute = new ObjectAttributes();
            //yellowAttribute.LayerIndex = 4;
            soluableAttribute.MaterialIndex = soluableIndex;
            soluableAttribute.MaterialSource = ObjectMaterialSource.MaterialFromObject;
            soluableAttribute.ObjectColor = Color.Green;
            soluableAttribute.ColorSource = ObjectColorSource.ColorFromObject;
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Button", "B", "Button to create gear", GH_ParamAccess.item);
            pManager.AddGenericParameter("Essentials", "E", "The essential that contains: main model, cutter, cutter plane, cutter thickness, cutted models", GH_ParamAccess.item);
            pManager.AddTextParameter("Gear Speed", "S", "Speed for the end gear", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddBooleanParameter("Reset Button value", "B", "Button to reset", GH_ParamAccess.item);
            pManager.AddGenericParameter("Saved Item", "I", "The item object that is to be saved", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            bool isPressed = false;
            string ratioText = "NA";
            if (!DA.GetData(0, ref isPressed))
                return;
            if (!DA.GetData(1, ref original_essentials))
                return;
            if (!DA.GetData(2, ref ratioText))
                return;

            ratio = Double.Parse(ratioText);

            if (isPressed)
            {
                Essentials essentials = new Essentials();


                SpurGear start_gear = null;
                ObjRef start_gear_ref = null;

                #region Ask the user to select a start gear
                var rc = Rhino.Commands.Result.Failure;
                myDoc.Objects.Show(SavedItems.start_gear_horizontal_pair.Item2, true);
                myDoc.Objects.Show(SavedItems.start_gear_vertical_pair.Item2, true);
                myDoc.Objects.Show(SavedItems.foundation_guid, true);
                myDoc.Objects.Show(SavedItems.originalModelGuids[1], true);
                myDoc.Objects.Hide(original_essentials.MainModel, true);
                myDoc.Views.Redraw();

                bool success = false;

                while (!success)
                {
                    rc = RhinoGet.GetOneObject("Select the targetted inital gear: ", false, ObjectType.Brep, out start_gear_ref);
                    if(start_gear_ref.ObjectId.Equals(SavedItems.start_gear_horizontal_guid))
                        success = true;
                    else if (start_gear_ref.ObjectId.Equals(SavedItems.start_gear_vertical_guid))
                        success = true;
                }

                if(rc == Rhino.Commands.Result.Success)
                {
                    
                    if (start_gear_ref.ObjectId == SavedItems.start_gear_vertical_pair.Item2)
                        start_gear = SavedItems.start_gear_vertical_pair.Item1;
                    else
                        start_gear = SavedItems.start_gear_horizontal_pair.Item1;


                }

                myDoc.Objects.Hide(SavedItems.originalModelGuids[1], true);
                myDoc.Objects.Show(original_essentials.MainModel, true);

                myDoc.Objects.Hide(SavedItems.foundation_guid, true);
                myDoc.Objects.Hide(SavedItems.start_gear_horizontal_pair.Item2, true);
                myDoc.Objects.Hide(SavedItems.start_gear_vertical_pair.Item2, true);
                #endregion


                #region Load the main model and cutter
                if (original_essentials.Cutter == new Guid())
                {
                    DA.SetData(0, false);
                    return;
                }

                essentials = original_essentials.Duplicate(original_essentials);
                essentials.Speed = ratio;

                if (start_gear_ref.ObjectId == SavedItems.start_gear_vertical_pair.Item2)
                    essentials.ChoosedStartGear = SavedItems.start_gear_vertical_pair;
                else
                    essentials.ChoosedStartGear = SavedItems.start_gear_horizontal_pair;

                //Copy cutter
                ObjRef cutterObj = new ObjRef(myDoc, essentials.Cutter);
                cutter = cutterObj.Brep();

                //Copy main model
                currModelObjId = essentials.MainModel;
                ObjRef currObj = new ObjRef(myDoc, currModelObjId);
                currModel = currObj.Brep();
                #endregion

                #region Cut the main model
                Brep[] cuttedBrep = Brep.CreateBooleanDifference(currModel, cutter, myDoc.ModelAbsoluteTolerance, false);
                myDoc.Objects.Hide(essentials.Cutter, true);
                myDoc.Objects.Delete(essentials.MainModel, true);
                List<Guid> cuttedBrepObjId = new List<Guid>();
                foreach (var brep in cuttedBrep)
                {
                    Guid guid = myDoc.Objects.Add(brep);
                    cuttedBrepObjId.Add(guid);
                }
                myDoc.Views.Redraw();
                #endregion

                #region Ask which brep is the end effector and create gear train
                ObjRef endEffector_ref;
                rc = RhinoGet.GetOneObject("Select the end effector: ", false, ObjectType.Brep, out endEffector_ref);
                if (rc == Rhino.Commands.Result.Success)
                {
                    #region Get end effector and main model
                    Guid mainModelObjId;
                    Brep mainModel;
                    Guid endEffectorObjId;
                    Brep endEffector;

                    endEffectorObjId = endEffector_ref.ObjectId;
                    endEffector = endEffector_ref.Brep();

                    if (endEffectorObjId == cuttedBrepObjId[0])
                    {
                        mainModelObjId = cuttedBrepObjId[1];
                        mainModel = cuttedBrep[1];
                    }
                    else
                    {
                        mainModelObjId = cuttedBrepObjId[0];
                        mainModel = cuttedBrep[0];
                    }
                    allBreps = getAllBreps();
                    allBreps_guid = getAllBrepsGuid();
                    essentials.EndEffector = endEffectorObjId;
                    essentials.MainModel = mainModelObjId;
                    #endregion


                    if(start_gear_ref.ObjectId == SavedItems.start_gear_vertical_pair.Item2)
                    {
                        #region Calculate the point of the end effector that extend the pipe to the main model
                        Point3d pointConnection1 = new Point3d(0, 0, 0);
                        if (Intersection.BrepBrep(endEffector, cutter, myDoc.ModelAbsoluteTolerance, out Curve[] intersectionCurves, out Point3d[] intersectionPoints))
                        {
                            if (intersectionCurves.Length == 0)
                            {
                                RhinoApp.WriteLine("No intersection found");
                                DA.SetData(0, false);
                                return;
                            }

                            if (intersectionCurves.Length > 1)
                            {
                                RhinoApp.WriteLine("Your cutter cuts more than one portion of the model. Please try to cut only one portion");
                                DA.SetData(0, false);
                                return;
                            }

                            AreaMassProperties areaMass = AreaMassProperties.Compute(intersectionCurves[0], myDoc.ModelAbsoluteTolerance);
                            pointConnection1 = areaMass.Centroid;
                        }
                        else
                        {
                            RhinoApp.WriteLine("The point of the end effector that extend the pipe to the main model cannot be found. Restart and Try again.");
                            DA.SetData(0, false);
                            return;
                        }

                        #endregion

                        #region Calculate the direction of the end gear that needs to be facing.
                        Vector3d end_gear_dir = essentials.CutterPlane.Normal;
                        //double distance = pointConnection1.DistanceTo(mainModel.GetBoundingBox(true).Center);
                        double distance = pointConnection1.DistanceTo(mainModel.ClosestPoint(pointConnection1)) + 1;
                        Line endEffector_rail = new Line(pointConnection1, end_gear_dir, distance);

                        if (!mainModel.IsManifold || !mainModel.IsSolid)
                        {
                            RhinoApp.WriteLine("Your model cannot be fixed to become manifold and closed, please try to fix it manually");
                            DA.SetData(0, false);
                            Cancel();
                            return;
                        }


                        if (!mainModel.IsPointInside(endEffector_rail.To, myDoc.ModelAbsoluteTolerance, true)) // TODO: We need to make sure if the main model is closed and manifold. Perhaps write a function to fix the original model in the first place.
                        {
                            end_gear_dir.Reverse();
                            endEffector_rail = new Line(pointConnection1, end_gear_dir, distance);
                        }
                        #endregion

                        #region Calculate the cone angle of the end bevel gear and its driven gear
                        end_gear_dir.X = Math.Round(end_gear_dir.X, 3);
                        end_gear_dir.Y = Math.Round(end_gear_dir.Y, 3);
                        end_gear_dir.Z = Math.Round(end_gear_dir.Z, 3);
                        double end_gear_coneAngle = RhinoMath.ToDegrees(Vector3d.VectorAngle(new Vector3d(0, 0, 1), end_gear_dir));

                        myDoc.Views.Redraw();
                        #endregion

                        Vector3d unitized_end_gear_dir = end_gear_dir;
                        end_gear_dir.Unitize();

                        unitized_end_gear_dir.Y = Math.Abs(unitized_end_gear_dir.Y);
                    
                        //we can just use spur gears for this end effector
                        if(unitized_end_gear_dir.Equals(start_gear.Direction))
                        {
                            Brep split = endEffector.Split(cutter, myDoc.ModelAbsoluteTolerance)[0];
                            Point3d centroid = AreaMassProperties.Compute(split).Centroid;

                            endEffector_rail = new Line(centroid, new Point3d(centroid.X, start_gear.CenterPoint.Y, centroid.Z));

                            List<Brep> allGaskets = new List<Brep>();
                            List<Brep> allShafts = new List<Brep>();

                            #region end_effector_gear
                            endEffector_rail = new Line(centroid, new Point3d(centroid.X, start_gear.CenterPoint.Y, centroid.Z));
                            Point3d first_spur_gear_centerPoint = endEffector_rail.To;

                            //Check if the spur gear can be solo or any additional gears need to get involved
                            double tips_distance = (first_spur_gear_centerPoint.DistanceTo(start_gear.CenterPoint) - start_gear.BaseRadius);
                            int first_spur_gear_teethNum = getNumTeeth(tips_distance);

                            if (first_spur_gear_teethNum < 5)
                            {
                                RhinoApp.WriteLine("Cannot generate a gear for this point, try other points: too close to initial gear");
                                Cancel();
                                return;
                            }

                            bool intermediate_gears_required = false;

                            double first_spur_gear_tip_radius = getTipRadius(first_spur_gear_teethNum);

                            Line temp_line = new Line(first_spur_gear_centerPoint, new Vector3d(0, 0, -1), first_spur_gear_tip_radius);

                            if (temp_line.To.Z < SavedItems.foundation_origin.Z || first_spur_gear_teethNum > 30)
                            {
                                first_spur_gear_teethNum = start_gear.NumTeeth;
                                intermediate_gears_required = true;
                            }
                            Line start_gear_connection_rail = new Line(start_gear.CenterPoint, first_spur_gear_centerPoint);
                            Vector3d first_spur_gear_xDirection = start_gear_connection_rail.Direction;
                            //first_spur_gear_xDirection = new Vector3d(0,0,0);
                            SpurGear first_spur_gear = new SpurGear(first_spur_gear_centerPoint, start_gear.Direction, first_spur_gear_xDirection, first_spur_gear_teethNum, module, pressure_angle, thickness, 0, false);
                            #endregion

                            #region end_effector_shaft
                            Curve endEffector_curve = endEffector_rail.ToNurbsCurve().Extend(CurveEnd.End, first_spur_gear.FaceWidth + shaft_extends_from_gear,CurveExtensionStyle.Line);
                            Brep endEffector_shaft = Brep.CreatePipe(endEffector_curve, shaft_radius, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];
                            #endregion

                            #region first_spur_gear_top_gasket
                            Vector3d scaledVector_closerSide = first_spur_gear.Direction;
                            Vector3d scaledVector_furtherSide = first_spur_gear.Direction;
                            Point3d point1 = first_spur_gear.TopPoint;
                            Point3d point2 = first_spur_gear.TopPoint;

                            scaledVector_closerSide.Unitize();
                            scaledVector_closerSide *= gasket_gear_gap;

                            scaledVector_furtherSide.Unitize();
                            scaledVector_furtherSide *= (gasket_gear_gap + bottom_gasket_gear_height);

                            point1 += scaledVector_closerSide;
                            point2 += scaledVector_furtherSide;

                            Curve first_spur_gear_top_gasket_rail = new Line(point1, point2).ToNurbsCurve();
                            Brep first_spur_gear_top_gasket = Brep.CreateThickPipe(first_spur_gear_top_gasket_rail, gasket_inner_radius, gasket_outer_radius, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];

                            Point3d point3 = first_spur_gear_top_gasket_rail.PointAtLength(first_spur_gear_top_gasket_rail.GetLength() / 2);
                            Circle first_spur_gear_top_gasket_circle = new Circle(new Plane(point3, first_spur_gear_top_gasket_rail.TangentAtStart), (gasket_outer_radius + gasket_inner_radius) / 2);
                            #endregion

                            #region first_spur_gear_bottom_gasket
                            scaledVector_closerSide = Vector3d.Negate(first_spur_gear.Direction);
                            scaledVector_furtherSide = Vector3d.Negate(first_spur_gear.Direction);

                            point1 = first_spur_gear.BottomPoint;
                            point2 = first_spur_gear.BottomPoint;

                            scaledVector_closerSide.Unitize();
                            scaledVector_closerSide *= gasket_gear_gap;

                            scaledVector_furtherSide.Unitize();
                            scaledVector_furtherSide *= (gasket_gear_gap + bottom_gasket_gear_height);


                            point1 += scaledVector_closerSide;
                            point2 += scaledVector_furtherSide;

                            Curve first_spur_gear_bottom_gasket_rail = new Line(point1, point2).ToNurbsCurve();
                            Brep first_spur_gear_bottom_gasket = Brep.CreateThickPipe(first_spur_gear_bottom_gasket_rail, gasket_inner_radius, gasket_outer_radius, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];

                            point3 = first_spur_gear_bottom_gasket_rail.PointAtLength(first_spur_gear_bottom_gasket_rail.GetLength() / 2);

                            Circle first_spur_gear_bottom_gasket_circle = new Circle(new Plane(point3, first_spur_gear_bottom_gasket_rail.TangentAtStart), (gasket_outer_radius + gasket_inner_radius) / 2);
                            #endregion

                            #region intermediate gears
                            Vector3d intermediate_gear_Direction = start_gear.Direction;
                            Vector3d intermediate_gear_xDir = first_spur_gear_xDirection;
                            double intermediate_gear_selfRotAngle = 0;

                            List<SpurGear> intermediates_gears = new List<SpurGear>();
                            if (intermediate_gears_required)
                            {
                                tips_distance = (first_spur_gear.CenterPoint.DistanceTo(start_gear.CenterPoint) - start_gear.BaseRadius - first_spur_gear.BaseRadius);//Distance from first spur gear's pitch to start gear's pitch
                                (int, int) number_of_gear = bestNumberOfGear(tips_distance);

                                if (number_of_gear.Item1 == -1 || number_of_gear.Item1 == 0)
                                {
                                    RhinoApp.WriteLine("Cannot generate rotational motion given this end effector: fail to create intermediate gears");
                                    Cancel();
                                    return;
                                }
                                else
                                {
                                    if (number_of_gear.Item1 != 1)
                                    {
                                        for (int i = 0; i < number_of_gear.Item1 - 1; i++)
                                        {
                                            start_gear_connection_rail = new Line(start_gear.CenterPoint, first_spur_gear.CenterPoint);

                                            start_gear_connection_rail = new Line(start_gear.CenterPoint, start_gear_connection_rail.Direction, start_gear.BaseRadius + (i + 1) * getTipRadius(number_of_gear.Item2) + i * getBaseRadius(number_of_gear.Item2));
                                            Point3d intermediate_centerPoint = start_gear_connection_rail.To;
                                            SpurGear intermediate_gear = new SpurGear(intermediate_centerPoint, intermediate_gear_Direction, intermediate_gear_xDir, number_of_gear.Item2, module, pressure_angle, thickness, intermediate_gear_selfRotAngle, false);

                                            intermediates_gears.Add(intermediate_gear);
                                        }

                                        //Create the intermediate gear between the first spur gear and intermediate_gear[number_of_gear.Item-2], reason: the calculation for this last intermediate gear's teeth number is sometimes inconsistent
                                        tips_distance = (first_spur_gear.CenterPoint.DistanceTo(intermediates_gears[intermediates_gears.Count - 1].CenterPoint) - intermediates_gears[intermediates_gears.Count - 1].BaseRadius - first_spur_gear.BaseRadius) / 2;
                                        int teethNum = getNumTeeth(tips_distance);
                                        start_gear_connection_rail = new Line(intermediates_gears[intermediates_gears.Count - 1].CenterPoint, start_gear_connection_rail.Direction, intermediates_gears[intermediates_gears.Count - 1].BaseRadius + tips_distance);
                                        Point3d intermediate_centerPoint_temp = start_gear_connection_rail.To;
                                        SpurGear intermediate_gear_temp = new SpurGear(intermediate_centerPoint_temp, intermediate_gear_Direction, intermediate_gear_xDir, teethNum, module, pressure_angle, thickness, intermediate_gear_selfRotAngle, false);

                                        intermediates_gears.Add(intermediate_gear_temp);
                                    }
                                    else
                                    {
                                        for (int i = 0; i < number_of_gear.Item1; i++)
                                        {
                                            start_gear_connection_rail = new Line(start_gear.CenterPoint, first_spur_gear.CenterPoint);

                                            start_gear_connection_rail = new Line(start_gear.CenterPoint, start_gear_connection_rail.Direction, start_gear.BaseRadius + (i + 1) * getTipRadius(number_of_gear.Item2) + i * getBaseRadius(number_of_gear.Item2));
                                            Point3d intermediate_centerPoint = start_gear_connection_rail.To;
                                            SpurGear intermediate_gear = new SpurGear(intermediate_centerPoint, intermediate_gear_Direction, intermediate_gear_xDir, number_of_gear.Item2, module, pressure_angle, thickness, intermediate_gear_selfRotAngle, false);

                                            intermediates_gears.Add(intermediate_gear);

                                        }
                                    }


                                    //Make all gears match
                                    if (number_of_gear.Item1 == 1)
                                    {
                                        intermediates_gears[0].Rotate(360 / number_of_gear.Item2 / 2);
                                        int count = 0;
                                        Intersection.BrepBrep(intermediates_gears[0].Model, first_spur_gear.Model, myDoc.ModelAbsoluteTolerance, out intersectionCurves, out intersectionPoints);
                                        while (intersectionCurves != null && intersectionCurves.Length > 0 && count < 720)
                                        {
                                            first_spur_gear.Rotate(0.5);
                                            Intersection.BrepBrep(intermediates_gears[0].Model, first_spur_gear.Model, myDoc.ModelAbsoluteTolerance, out intersectionCurves, out intersectionPoints);
                                            count++;
                                        }
                                        if (count > 0)
                                        {
                                            first_spur_gear.Rotate(2);
                                            Intersection.BrepBrep(intermediates_gears[0].Model, first_spur_gear.Model, myDoc.ModelAbsoluteTolerance, out intersectionCurves, out intersectionPoints);
                                            while (intersectionCurves != null && intersectionCurves.Length > 0)
                                            {
                                                first_spur_gear.Rotate(0.3);
                                                Intersection.BrepBrep(intermediates_gears[0].Model, first_spur_gear.Model, myDoc.ModelAbsoluteTolerance, out intersectionCurves, out intersectionPoints);
                                                if (count > 1000)
                                                {
                                                    RhinoApp.WriteLine("Failed to create intermediate gear, try again");
                                                    Cancel();
                                                    return;
                                                }
                                                count++;
                                            }
                                        }
                                    }
                                    if (number_of_gear.Item1 == 2)
                                    {
                                        intermediates_gears[0].Rotate(360 / number_of_gear.Item2 / 2);
                                        int count = 0;
                                        Intersection.BrepBrep(intermediates_gears[1].Model, first_spur_gear.Model, myDoc.ModelAbsoluteTolerance, out intersectionCurves, out intersectionPoints);
                                        while (intersectionCurves != null && intersectionCurves.Length > 0 && count < 720)
                                        {
                                            first_spur_gear.Rotate(0.5);
                                            Intersection.BrepBrep(intermediates_gears[1].Model, first_spur_gear.Model, myDoc.ModelAbsoluteTolerance, out intersectionCurves, out intersectionPoints);
                                            count++;
                                        }

                                        if (count > 0)
                                        {
                                            first_spur_gear.Rotate(2);
                                            Intersection.BrepBrep(intermediates_gears[1].Model, first_spur_gear.Model, myDoc.ModelAbsoluteTolerance, out intersectionCurves, out intersectionPoints);
                                            while (intersectionCurves != null && intersectionCurves.Length > 0)
                                            {
                                                first_spur_gear.Rotate(0.3);
                                                Intersection.BrepBrep(intermediates_gears[1].Model, first_spur_gear.Model, myDoc.ModelAbsoluteTolerance, out intersectionCurves, out intersectionPoints);
                                                if (count > 1000)
                                                {
                                                    RhinoApp.WriteLine("Failed to create intermediate gear, try again");
                                                    Cancel();
                                                    return;
                                                }
                                                count++;
                                            }
                                        }

                                        //Double check intermediate gear 0 and 1
                                        count = 0;
                                        Intersection.BrepBrep(intermediates_gears[1].Model, intermediates_gears[0].Model, myDoc.ModelAbsoluteTolerance, out intersectionCurves, out intersectionPoints);
                                        while (intersectionCurves != null && intersectionCurves.Length > 0 && count < 720)
                                        {
                                            first_spur_gear.Rotate(0.5);
                                            Intersection.BrepBrep(intermediates_gears[1].Model, first_spur_gear.Model, myDoc.ModelAbsoluteTolerance, out intersectionCurves, out intersectionPoints);
                                            count++;
                                        }

                                        if (count > 0)
                                        {
                                            intermediates_gears[0].Rotate(2);
                                            Intersection.BrepBrep(intermediates_gears[1].Model, intermediates_gears[0].Model, myDoc.ModelAbsoluteTolerance, out intersectionCurves, out intersectionPoints);
                                            while (intersectionCurves != null && intersectionCurves.Length > 0)
                                            {
                                                first_spur_gear.Rotate(0.3);
                                                Intersection.BrepBrep(intermediates_gears[1].Model, intermediates_gears[0].Model, myDoc.ModelAbsoluteTolerance, out intersectionCurves, out intersectionPoints);
                                                if (count > 1000)
                                                {
                                                    RhinoApp.WriteLine("Failed to create intermediate gear, try again");
                                                    Cancel();
                                                    return;
                                                }
                                                count++;
                                            }
                                        }

                                    }
                                    if (number_of_gear.Item1 == 3)
                                    {
                                        intermediates_gears[0].Rotate(360 / number_of_gear.Item2 / 2);
                                        intermediates_gears[2].Rotate(360 / number_of_gear.Item2 / 2);

                                        int count = 0;

                                        Intersection.BrepBrep(intermediates_gears[2].Model, first_spur_gear.Model, myDoc.ModelAbsoluteTolerance, out intersectionCurves, out intersectionPoints);
                                        while (intersectionCurves != null && intersectionCurves.Length > 0 && count < 720)
                                        {
                                            first_spur_gear.Rotate(0.5);
                                            Intersection.BrepBrep(intermediates_gears[2].Model, first_spur_gear.Model, myDoc.ModelAbsoluteTolerance, out intersectionCurves, out intersectionPoints);
                                            count++;
                                        }

                                        if (count > 0)
                                        {
                                            first_spur_gear.Rotate(2);
                                            Intersection.BrepBrep(intermediates_gears[2].Model, first_spur_gear.Model, myDoc.ModelAbsoluteTolerance, out intersectionCurves, out intersectionPoints);
                                            while (intersectionCurves != null && intersectionCurves.Length > 0)
                                            {
                                                first_spur_gear.Rotate(0.03);
                                                Intersection.BrepBrep(intermediates_gears[2].Model, first_spur_gear.Model, myDoc.ModelAbsoluteTolerance, out intersectionCurves, out intersectionPoints);
                                                if (count > 1000)
                                                {
                                                    RhinoApp.WriteLine("Failed to create intermediate gear, try again");
                                                    Cancel();
                                                    return;
                                                }
                                                count++;
                                            }
                                        }

                                        //Double Check intermediates gear 1 and 2
                                        count = 0;
                                        Intersection.BrepBrep(intermediates_gears[1].Model, intermediates_gears[2].Model, myDoc.ModelAbsoluteTolerance, out intersectionCurves, out intersectionPoints);
                                        while (intersectionCurves != null && intersectionCurves.Length > 0 && count < 720)
                                        {
                                            intermediates_gears[1].Rotate(0.5);
                                            intermediates_gears[0].Rotate(0.5);
                                            Intersection.BrepBrep(intermediates_gears[1].Model, first_spur_gear.Model, myDoc.ModelAbsoluteTolerance, out intersectionCurves, out intersectionPoints);
                                            count++;
                                        }

                                        if (count > 0)
                                        {
                                            intermediates_gears[0].Rotate(2);
                                            Intersection.BrepBrep(intermediates_gears[1].Model, intermediates_gears[2].Model, myDoc.ModelAbsoluteTolerance, out intersectionCurves, out intersectionPoints);
                                            while (intersectionCurves != null && intersectionCurves.Length > 0)
                                            {
                                                intermediates_gears[1].Rotate(0.03);
                                                intermediates_gears[0].Rotate(0.03);
                                                Intersection.BrepBrep(intermediates_gears[1].Model, intermediates_gears[2].Model, myDoc.ModelAbsoluteTolerance, out intersectionCurves, out intersectionPoints);
                                                if (count > 1000)
                                                {
                                                    RhinoApp.WriteLine("Failed to create intermediate gear, try again");
                                                    Cancel();
                                                    return;
                                                }
                                                count++;
                                            }
                                        }
                                    }
                                }
                            }

                            #region  intermediate gear top gaskets
                            List<Brep> intermediate_gear_top_gaskets = new List<Brep>();
                            List<Circle> intermediate_gear_top_gaskets_circles = new List<Circle>();
                            foreach (var gear in intermediates_gears)
                            {
                                scaledVector_closerSide = gear.Direction;
                                scaledVector_furtherSide = gear.Direction;

                                point1 = gear.TopPoint;
                                point2 = gear.TopPoint;

                                scaledVector_closerSide.Unitize();
                                scaledVector_closerSide *= gasket_gear_gap;

                                scaledVector_furtherSide.Unitize();
                                scaledVector_furtherSide *= (gasket_gear_gap + bottom_gasket_gear_height);


                                point1 += scaledVector_closerSide;
                                point2 += scaledVector_furtherSide;

                                Curve intermediate_gear_top_gasket_rail = new Line(point1, point2).ToNurbsCurve();
                                Brep intermediate_gear_top_gasket = Brep.CreateThickPipe(intermediate_gear_top_gasket_rail, gasket_inner_radius, gasket_outer_radius, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];

                                point3 = intermediate_gear_top_gasket_rail.PointAtLength(intermediate_gear_top_gasket_rail.GetLength() / 2);
                                Circle intermediate_gear_top_gasket_circle = new Circle(new Plane(point3, intermediate_gear_top_gasket_rail.TangentAtStart), (gasket_outer_radius + gasket_inner_radius) / 2);

                                intermediate_gear_top_gaskets.Add(intermediate_gear_top_gasket);
                                intermediate_gear_top_gaskets_circles.Add(intermediate_gear_top_gasket_circle);
                                allGaskets.Add(intermediate_gear_top_gasket);
                            }

                            #endregion

                            #region  intermediate gear bottom gaskets
                            List<Brep> intermediate_gear_bottom_gaskets = new List<Brep>();
                            List<Circle> intermediate_gear_bottom_gaskets_circles = new List<Circle>();
                            foreach (var gear in intermediates_gears)
                            {
                                scaledVector_closerSide = Vector3d.Negate(gear.Direction);
                                scaledVector_furtherSide = Vector3d.Negate(gear.Direction);

                                point1 = gear.BottomPoint;
                                point2 = gear.BottomPoint;

                                scaledVector_closerSide.Unitize();
                                scaledVector_closerSide *= gasket_gear_gap;

                                scaledVector_furtherSide.Unitize();
                                scaledVector_furtherSide *= (gasket_gear_gap + bottom_gasket_gear_height);


                                point1 += scaledVector_closerSide;
                                point2 += scaledVector_furtherSide;

                                Curve intermediate_gear_bottom_gasket_rail = new Line(point1, point2).ToNurbsCurve();
                                Brep intermediate_gear_bottom_gasket = Brep.CreateThickPipe(intermediate_gear_bottom_gasket_rail, gasket_inner_radius, gasket_outer_radius, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];

                                point3 = intermediate_gear_bottom_gasket_rail.PointAtLength(intermediate_gear_bottom_gasket_rail.GetLength() / 2);
                                Circle intermediate_gear_bottom_gasket_circle = new Circle(new Plane(point3, intermediate_gear_bottom_gasket_rail.TangentAtStart), (gasket_outer_radius + gasket_inner_radius) / 2);

                                intermediate_gear_bottom_gaskets.Add(intermediate_gear_bottom_gasket);
                                intermediate_gear_bottom_gaskets_circles.Add(intermediate_gear_bottom_gasket_circle);
                                allGaskets.Add(intermediate_gear_bottom_gasket);
                            }
                            #endregion

                            #region intermediate gear shaft
                            List<Brep> intermediate_gear_shafts = new List<Brep>();

                            foreach (var gear in intermediates_gears)
                            {
                                Curve shaft_line = new Line(gear.TopPoint, gear.BottomPoint).ToNurbsCurve().Extend(CurveEnd.Both, shaft_extends_from_gear, CurveExtensionStyle.Arc);
                                Brep intermediate_gear_shaft = Brep.CreatePipe(shaft_line, shaft_radius, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];
                                intermediate_gear_shafts.Add(intermediate_gear_shaft);
                                allShafts.Add(intermediate_gear_shaft);
                            }
                            #endregion

                            #endregion

                            myDoc.Objects.Add(SavedItems.foundation.DuplicateBrep());
                            myDoc.Objects.Add(start_gear.Model.DuplicateBrep());
                            myDoc.Objects.Add(endEffector_shaft);
                            myDoc.Objects.Add(first_spur_gear.Model);
                            myDoc.Objects.Add(first_spur_gear_top_gasket);
                            myDoc.Objects.Add(first_spur_gear_bottom_gasket);

                            foreach (var gear in intermediates_gears)
                            {
                                myDoc.Objects.Add(gear.Model);
                            }

                            foreach (var gasekt in intermediate_gear_top_gaskets)
                            {
                                myDoc.Objects.Add(gasekt);
                            }


                            foreach (var gasekt in intermediate_gear_bottom_gaskets)
                            {
                                myDoc.Objects.Add(gasekt);
                            }


                            foreach (var shaft in intermediate_gear_shafts)
                            {
                                myDoc.Objects.Add(shaft);
                            }

                            DA.SetData(0, false);
                            Item savedItem = new Item();
                            savedItem.Name = "Rotational Motion";
                            savedItem.Description = "Nnsaved";
                            savedItem.gearEssentials = essentials;
                            DA.SetData(1, savedItem);

                            TrueOnlyButtonValueController.finished = 1;
                        }
                    }

                    if (start_gear_ref.ObjectId == SavedItems.start_gear_horizontal_pair.Item2)
                    {
                        #region Start gear(comes with motor)
                        BoundingBox mainModel_bbox = mainModel.GetBoundingBox(true);

                        //Find central point of the base of the bounding box
                        Point3d start_gear_centerPoint = start_gear.CenterPoint;
                        int start_gear_teethNum = start_gear.NumTeeth;


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
                                RhinoApp.WriteLine("No intersection found");
                                DA.SetData(0, false);
                                return;
                            }

                            if (intersectionCurves.Length > 1)
                            {
                                RhinoApp.WriteLine("Your cutter cuts more than one portion of the model. Please try to cut only one portion");
                                DA.SetData(0, false);
                                return;
                            }

                            AreaMassProperties areaMass = AreaMassProperties.Compute(intersectionCurves[0], myDoc.ModelAbsoluteTolerance);
                            pointConnection1 = areaMass.Centroid;
                        }
                        else
                        {
                            RhinoApp.WriteLine("The point of the end effector that extend the pipe to the main model cannot be found. Restart and Try again.");
                            DA.SetData(0, false);
                            return;
                        }

                        #endregion

                        #region Calculate the direction of the end gear that needs to be facing.
                        Vector3d end_gear_dir = essentials.CutterPlane.Normal;
                        //double distance = pointConnection1.DistanceTo(mainModel.GetBoundingBox(true).Center);
                        double distance = pointConnection1.DistanceTo(mainModel.ClosestPoint(pointConnection1)) + 1;
                        Line endEffector_rail = new Line(pointConnection1, end_gear_dir, distance);

                        if (!mainModel.IsManifold || !mainModel.IsSolid)
                        {
                            RhinoApp.WriteLine("Your model cannot be fixed to become manifold and closed, please try to fix it manually");
                            DA.SetData(0, false);
                            return;
                        }


                        if (!mainModel.IsPointInside(endEffector_rail.To, myDoc.ModelAbsoluteTolerance, true)) // TODO: We need to make sure if the main model is closed and manifold. Perhaps write a function to fix the original model in the first place.
                        {
                            end_gear_dir.Reverse();
                            endEffector_rail = new Line(pointConnection1, end_gear_dir, distance);
                        }
                        #endregion

                        #region Calculate the cone angle of the end bevel gear and its driven gear
                        double end_gear_coneAngle = RhinoMath.ToDegrees(Vector3d.VectorAngle(new Vector3d(0, 0, 1), end_gear_dir));

                        myDoc.Views.Redraw();
                        #endregion

                        if (end_gear_coneAngle == 180 || end_gear_coneAngle == 0)
                        {
                            Brep split = endEffector.Split(cutter, myDoc.ModelAbsoluteTolerance)[0];
                            Point3d centroid = AreaMassProperties.Compute(split).Centroid;

                            distance = centroid.DistanceTo(new Point3d(mainModel.GetBoundingBox(true).Center.X, mainModel.GetBoundingBox(true).Center.Y, mainModel.GetBoundingBox(true).Min.Z)) / 2;
                            endEffector_rail = new Line(centroid, end_gear_dir, distance);

                            //Connector Gear
                            Point3d connector_gear_centerPoint = new Point3d(centroid.X, centroid.Y, start_gear_centerPoint.Z);
                            Vector3d connector_gear_Direction = new Vector3d(0, 0, 1);
                            Vector3d connector_gear_xDir = new Vector3d(0, 0, 0);
                            int connector_gear_teethNum = (int)(start_gear_teethNum * ratio);
                            double connector_gear_selfRotAngle = 0;
                            SpurGear connector_gear = new SpurGear(connector_gear_centerPoint, connector_gear_Direction, connector_gear_xDir, connector_gear_teethNum, module, pressure_angle, thickness, connector_gear_selfRotAngle, false);

                            //Shaft between end gear and connector gear
                            Line shaft_rail = new Line(centroid, new Point3d(connector_gear.CenterPoint.X, connector_gear.CenterPoint.Y, connector_gear.Boundingbox.Min.Z));
                            shaft_rail.Extend(1, 1);
                            Brep shaft = Brep.CreatePipe(shaft_rail.ToNurbsCurve(), 1.5, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];

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
                                DA.SetData(0, false);
                                return;
                            }
                            else if (second_driven_gear_tipRadius <= getTipRadius(1))
                            {
                                myDoc.Objects.Add(start_gear.Model);
                                myDoc.Objects.Add(shaft);
                                myDoc.Objects.Add(connector_gear.Model);
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



                                myDoc.Objects.Add(start_gear.Model);
                                myDoc.Objects.Add(shaft);
                                myDoc.Objects.Add(connector_gear.Model);
                                myDoc.Objects.Add(second_driven_gear.Model);
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

                            #region if cone angle is smaller than 90, go ahead and make the gear train

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
                            Line shaft_clearance_rail = new Line();
                            Line rail6 = new Line(new Point3d(first_driven_gear.CenterPoint.X, first_driven_gear.CenterPoint.Y, first_driven_gear.Boundingbox.Max.Z), new Point3d(first_driven_gear.CenterPoint.X, first_driven_gear.CenterPoint.Y, connector_gear.Boundingbox.Min.Z));
                            rail6.Extend(1, 1);
                            Brep shaft = Brep.CreatePipe(rail6.ToNurbsCurve(), 1.5, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];
                            rail6.Extend(1, 1);
                            shaft_clearance_rail = rail6;
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
                            Line end_gear_clearance_shaft_rail = new Line();
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
                                end_gear_clearance_shaft_rail = new Line(pointConnection1, end_gear_dir, distance);
                                end_gear_clearance_shaft = Brep.CreatePipe(end_gear_clearance_shaft_rail.ToNurbsCurve(), 1.7, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];
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
                                                gearSet.EndGearShaftClearance = end_gear_clearance_shaft;
                                                gearSet.ShaftClearance = shaft_clearance;
                                                gearSet.Shaft = shaft;
                                                gearSet.SecondDrivenGear = second_driven_gear;
                                                gearSet.EndGearShaftClearanceRail = end_gear_clearance_shaft_rail.ToNurbsCurve();
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
                                            gearSet.EndGearShaftClearance = end_gear_clearance_shaft;
                                            gearSet.ShaftClearance = shaft_clearance;
                                            gearSet.Shaft = shaft;
                                            gearSet.SecondDrivenGear = second_driven_gear;
                                            gearSet.EndGearShaftClearanceRail = end_gear_clearance_shaft_rail.ToNurbsCurve();
                                            gearSet.ShaftClearanceRail = shaft_clearance_rail.ToNurbsCurve();
                                            workable_gearsets.Add(gearSet);
                                        }
                                    }
                                }
                                if (!IsBrepInsideBrep(first_driven_gear.Boundingbox_big, mainModel) && !IsBrepInsideBrep(connector_gear.Boundingbox_big, mainModel) && !IsBrepInsideBrep(end_gear.Boundingbox_big, mainModel))
                                {
                                    if (workable_gearsets.Count > 0)
                                        break;
                                    RhinoApp.WriteLine("Fail to create gear on your main model with the selected end effector.2");
                                    DA.SetData(0, false);
                                    return;
                                }
                                if (!IsBrepInsideBrep(first_driven_gear.Boundingbox_big, mainModel) && !IsBrepInsideBrep(connector_gear.Boundingbox_big, mainModel) && IsBrepInsideBrep(end_gear.Boundingbox_big, mainModel) && !connector_gear.CenterPoint.Equals(new Point3d(first_driven_gear.CenterPoint.X, first_driven_gear.CenterPoint.Y, start_gear_centerPoint.Z)))
                                {
                                    RhinoApp.WriteLine("Fail to create gear on your main model with the selected end effector.3");
                                    DA.SetData(0, false);
                                    return;
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
                                if (second_driven_gear_tipRadius > getTipRadius(5))
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
                                    end_gear_clearance_shaft_rail = new Line(pointConnection1, end_gear_dir, distance);
                                    end_gear_clearance_shaft = Brep.CreatePipe(end_gear_clearance_shaft_rail.ToNurbsCurve(), 1.7, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];
                                    //mainModel = Brep.CreateBooleanDifference(mainModel, end_gear_shaft, myDoc.ModelAbsoluteTolerance, false)[0];
                                    //mainModel = Brep.CreateBooleanDifference(mainModel, end_gear_clearance_shaft, myDoc.ModelAbsoluteTolerance, false)[0];
                                }
                            }
                            #endregion

                            //#region Create movement space, shaft, and gaskets for gears
                            ////Start gear
                            //mainModel = Brep.CreateBooleanDifference(mainModel, start_gear_bBox.ToBrep(), myDoc.ModelAbsoluteTolerance, false)[0];


                            ////Connector gear
                            //mainModel = Brep.CreateBooleanDifference(mainModel, connector_gear.Boundingbox_big, myDoc.ModelAbsoluteTolerance, false)[0];

                            ////Shaft of connector gear and first driven gear
                            //myDoc.Objects.Add(shaft);
                            //mainModel = Brep.CreateBooleanDifference(mainModel, shaft, myDoc.ModelAbsoluteTolerance, false)[0];
                            //mainModel = Brep.CreateBooleanDifference(mainModel, shaft_clearance, myDoc.ModelAbsoluteTolerance, false)[0];


                            ////First driven gear
                            //mainModel = Brep.CreateBooleanDifference(mainModel, first_driven_gear.Boundingbox_big, myDoc.ModelAbsoluteTolerance, false)[0];

                            ////End gear
                            //mainModel = Brep.CreateBooleanDifference(mainModel, end_gear.Boundingbox_big, myDoc.ModelAbsoluteTolerance, false)[0];

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
                            myDoc.Objects.Delete(mainModelObjId, true);
                            mainModelObjId = myDoc.Objects.Add(mainModel);

                            myDoc.Objects.Add(bestGearSet.EndGearBottomGasket);
                            myDoc.Objects.Add(bestGearSet.EndGearTopGasket);

                            myDoc.Objects.Add(bestGearSet.Shaft);
                            myDoc.Objects.Add(bestGearSet.EndGearShaft);
                            #endregion

                            #endregion
                        }
                        //myDoc.Objects.Add(start_gear.Model);
                        DA.SetData(0, false);
                        Item savedItem = new Item();
                        savedItem.Name = "Rotational Motion";
                        savedItem.Description = "Nnsaved";
                        savedItem.gearEssentials = essentials;
                        DA.SetData(1, savedItem);

                        TrueOnlyButtonValueController.finished = 1;
                    }
                }
            }
        }

        private double getBaseRadius(int teethNum)
        {
            double pitchDiameter = module * teethNum;
            double outDiameter = pitchDiameter;
            return outDiameter / 2;
        }

        private (int, int) bestNumberOfGear(double tips_distance)
        {
            int numTeeth = getNumTeeth(tips_distance / 2);

            if (numTeeth < 5)
                return (0, 0);

            else if (numTeeth > 5 && numTeeth <= 10)
                return (1, numTeeth);
            else if (numTeeth < 30 && numTeeth > 10)
            {
                tips_distance = tips_distance / 2 + module;

                return (2, getNumTeeth(tips_distance / 2));
            }
            else if (numTeeth >= 30)
            {
                tips_distance = (tips_distance + 2 * module) / 3;

                return (3, getNumTeeth(tips_distance / 2));
            }

            return (-1, -1);
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

        public void printPoint(string pointname, Point3d point)
        {
            RhinoApp.WriteLine($"{pointname} center point: {point.X},{point.Y}, {point.Z}");
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

        private void Cancel()
        {
            var allObjects = new List<RhinoObject>(RhinoDoc.ActiveDoc.Objects.GetObjectList(ObjectType.AnyObject));
            foreach (var singleObject in allObjects)
                if (SavedItems.originalModelGuids.All(guid => guid != singleObject.Id))
                    RhinoDoc.ActiveDoc.Objects.Delete(singleObject.Id, true);
            RhinoDoc.ActiveDoc.Objects.Show(SavedItems.originalModelGuids[0], true);
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
            get { return new Guid("423D3C74-B211-408F-93CB-6A0EDCD06A5F"); }
        }
    }
}