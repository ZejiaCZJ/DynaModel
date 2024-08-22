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
using Rhino.Runtime;
using System.Windows;
using DynaModel_v2.Rotational_Motion;

namespace DynaModel_v2.Translational_Motion
{
    public class Translational_GearSet
    {
        private Rack rack;
        private Brep rack_holder;
        private Line rack_holder_rail;

        private SpurGear spur_gear;
        private Brep spur_gear_bottom_gasket;
        private Circle spur_gear_bottom_gasket_circle;

        private BevelGear first_bevel_gear;
        private Brep first_bevel_gear_top_gasket;
        private Circle first_bevel_gear_top_gasket_circle;

        private BevelGear second_bevel_gear;
        private Brep second_bevel_gear_top_gasket;
        private Circle second_bevel_gear_top_gasket_circle;

        private SpurGear connector_gear;
        private Brep connector_gear_top_gasket;
        private Circle connector_gear_bottom_gasket_circle;

        private List<SpurGear> intermediate_gears;
        private List<Brep> intermediate_gears_top_gaskets;
        private List<Brep> intermediate_gears_bottom_gaskets;
        private List<Circle> intermediate_gears_bottom_gaskets_circles;
        private List<Circle> intermediate_gears_top_gaskets_circles;

        public Rack Rack { get; set; }
        public Brep RackHolder { get; set; }
        public Line RackHolderRail { get; set; }

        public SpurGear SpurGear { get; set; }
        public Brep SpurGearBottomGasket { get; set; }
        public Circle SpurGearBottomGasketCircle { get; set; }

        public BevelGear FirstBevelGear { get; set; }
        public Brep FirstBevelGearTopGasket { get; set; }
        public Circle FirstBevelGearTopGasketCircle { get; set; }

        public BevelGear SecondBevelGear { get; set; }
        public Brep SecondBevelGearTopGasket { get; set; }
        public Circle SecondBevelGearTopGasketCircle { get; set; }

        public SpurGear ConnectorGear { get; set; }
        public Brep ConnectorGearTopGasket { get; set; }
        public Circle ConnectorGearBottomGasketCircle { get; set; }

        public List<SpurGear> IntermediateGears { get; set; }
        public List<Brep> IntermediateGearsTopGaskets { get; set; }
        public List<Brep> IntermediateGearsBottomGaskets { get; set; }
        public List<Circle> IntermediateGearsBottomGasketsCircles { get; set; }
        public List<Circle> IntermediateGearsTopGasketsCircles { get; set; }

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
        private double shaft_radius = 3;
        private static double clearance_shaft_radius = 3.4;
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
        private List<Brep> allGears;
        private List<Brep> allGaskets;
        private List<Brep> allShafts;


        /// <summary>
        /// Initializes a new instance of the ChooseEndEffector class.
        /// </summary>
        public ChooseEndEffector()
          : base("ChooseEndEffector", "EndEffector",
              "This component create a gear set based on the selected end effector",
              "DynaModel_v2", "Translational Motion")
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

            allGears = new List<Brep>();
            allGaskets = new List<Brep>();
            allShafts = new List<Brep>();


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

                #region Load the main model and cutter
                if (original_essentials.Cutter == new Guid())
                {
                    DA.SetData(0, false);
                    return;
                }

                essentials = original_essentials.Duplicate(original_essentials);
                essentials.Speed = ratio;

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
                var rc = RhinoGet.GetOneObject("Select the end effector: ", false, ObjectType.Brep, out endEffector_ref);
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

                    #region Start gear(comes with motor)
                    BoundingBox mainModel_bbox = mainModel.GetBoundingBox(true);

                    //Find central point of the base of the bounding box
                    Point3d start_gear_centerPoint = new Point3d(mainModel_bbox.Center.X, mainModel_bbox.Center.Y, mainModel_bbox.Min.Z + 10);
                    Vector3d start_gear_Direction = new Vector3d(0, 0, 1);
                    Vector3d start_gear_xDir = new Vector3d(0, 0, 0);
                    int start_gear_teethNum = 10;
                    double start_gear_selfRotAngle = 0;

                    SpurGear start_gear = new SpurGear(start_gear_centerPoint, start_gear_Direction, start_gear_xDir, start_gear_teethNum, module, pressure_angle, thickness, start_gear_selfRotAngle, true);
                    //myDoc.Objects.Add(start_gear.Model);

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

                    #region Calculate the direction of the rack needs to be moving
                    Vector3d rackLineDirection = essentials.CutterPlane.Normal;
                    double distance = pointConnection1.DistanceTo(mainModel.ClosestPoint(pointConnection1)) + 1;
                    Line endEffector_rail = new Line(pointConnection1, rackLineDirection, distance);

                    if (!mainModel.IsManifold || !mainModel.IsSolid)
                    {
                        RhinoApp.WriteLine("Your model cannot be fixed to become manifold and closed, please try to fix it manually");
                        DA.SetData(0, false);
                        return;
                    }


                    if (mainModel.IsPointInside(endEffector_rail.To, myDoc.ModelAbsoluteTolerance, true)) // TODO: We need to make sure if the main model is closed and manifold. Perhaps write a function to fix the original model in the first place.
                    {
                        rackLineDirection.Reverse();
                        endEffector_rail = new Line(pointConnection1, rackLineDirection, distance);
                    }
                    #endregion

                    #region Rack
                    //1. Get the center point of the rack, we assume the rack's length will be constant
                    double rack_length = 60;
                    endEffector_rail = new Line(pointConnection1, Vector3d.Negate(rackLineDirection), rack_length/2);
                    Point3d rack_center_point = endEffector_rail.To;

                    //2. Get the directions vectors of the rack
                    Transform rotational_matrix = Transform.Rotation(new Vector3d(0, 0, 1), rackLineDirection, rack_center_point);
                    Vector3d rackFaceDirection = new Vector3d(1, 0, 0); //Need to be right orthogonal to rackLineDirection, for example: (1,0,0) is right orthogonal to (0,0,1)
                    rackFaceDirection.Transform(rotational_matrix);


                    Rack rack = new Rack(rack_center_point, rackLineDirection, rackFaceDirection, rack_length, module, thickness, new Vector3d(0, 0, 1), thickness, pressure_angle);
                    #endregion

                    #region Rack Holder
                    //myDoc.Objects.Add(rack.BoundingBox.ToBrep());
                    //myDoc.Objects.AddPoint(rack.BoundingBoxCenter);
                    Line rack_holder_line = new Line(rack.BoundingBoxCenter, rackLineDirection, 10);
                    Curve rack_holder_curve = rack_holder_line.ToNurbsCurve();
                    Brep rack_holder = Brep.CreateThickPipe(rack_holder_curve, 6, 10, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];
                    allGaskets.Add(rack_holder);
                    #endregion


                    #region Spur gear
                    int teeth_num = 10;
                    double selfRotAngle = 0;

                    //Generate a temporary gear to get its tip radius
                    Curve backbone = rack.BackBone.ToNurbsCurve();
                    backbone = backbone.Trim(CurveEnd.Start, 15);

                    Point3d spur_gear_center_point = backbone.PointAtStart;
                    Vector3d spur_gear_direction = rack.ExtrudeDirection;
                    Vector3d spur_gear_x_dir = new Vector3d(0, 0, 0);
                    SpurGear spur_gear = new SpurGear(spur_gear_center_point, spur_gear_direction, spur_gear_x_dir, teeth_num, module, pressure_angle, thickness, selfRotAngle, false);

                    //Get right center point such that the gear and the rack are perfectly away from each other
                    rackFaceDirection = (module + spur_gear.TipRadius) * rackFaceDirection;
                    spur_gear_center_point = spur_gear_center_point + rackFaceDirection;

                    spur_gear = new SpurGear(spur_gear_center_point, spur_gear_direction, spur_gear_x_dir, teeth_num, module, pressure_angle, thickness, selfRotAngle, false);

                    //Rotate the gear until it is engaged
                    Intersection.BrepBrep(rack.Model, spur_gear.Model, myDoc.ModelAbsoluteTolerance, out intersectionCurves, out Point3d[] intersectionPts);
                    while (intersectionCurves != null && intersectionCurves.Length > 0)
                    {
                        spur_gear.Rotate(1);
                        Intersection.BrepBrep(rack.Model, spur_gear.Model, myDoc.ModelAbsoluteTolerance, out intersectionCurves, out intersectionPts);
                    }
                    spur_gear.Rotate(2);
                    allGears.Add(spur_gear.Model);
                    #endregion

                    double bevel_gear_coneAngle = RhinoMath.ToDegrees(Vector3d.VectorAngle(new Vector3d(0, 0, 1), spur_gear_direction));

                    #region first bevel gear
                    double shaft_length = 30;
                    if (bevel_gear_coneAngle <= 100)
                        shaft_length = 10;

                    //Generate a bevel gear that connects to the spur gear and the shaft between them
                    Curve shaft_line = new Line(spur_gear.CenterPoint, spur_gear_direction, shaft_length).ToNurbsCurve();

                    Vector3d first_bevel_gear_direction = spur_gear_direction;
                    int first_bevel_gear_teeth_num = 18;
                    bool isReversed = false;
                    if (bevel_gear_coneAngle > 100)
                    {
                        first_bevel_gear_direction = Vector3d.Negate(first_bevel_gear_direction);
                        bevel_gear_coneAngle = 180 - bevel_gear_coneAngle;
                        isReversed = true;
                    }
                    BevelGear first_bevel_gear = new BevelGear(shaft_line.PointAtEnd, first_bevel_gear_direction, spur_gear_x_dir, first_bevel_gear_teeth_num, module, pressure_angle, thickness, selfRotAngle, bevel_gear_coneAngle, false);

                    allGears.Add(first_bevel_gear.Model);
                    #endregion

                    #region first shaft (shaft between spur gear and first bevel gear)
                    Point3d point1 = first_bevel_gear.TopPoint;
                    Point3d point2 = spur_gear.BottomPoint;

                    if (isReversed)
                        point1 = first_bevel_gear.BottomPoint;

                    shaft_line = new Line(point1, point2).ToNurbsCurve();
                    shaft_line = shaft_line.Extend(CurveEnd.Both, shaft_extends_from_gear, CurveExtensionStyle.Arc);

                    Brep shaft = Brep.CreatePipe(shaft_line, shaft_radius, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];
                    allShafts.Add(shaft);
                    #endregion


                    #region second_bevel_gear
                    //Calculate the vector that is perpendicular to the end gear facing direction
                    Vector3d orthogonal = GetOrthogonalWithMinZ(first_bevel_gear_direction);

                    //Get the line that is along the xy axis that has the same direction of the end gear facing direction
                    Line rail1 = new Line(first_bevel_gear.CenterPoint, orthogonal, first_bevel_gear.PitchRadius); //A line that is paralle to the end gear and through the end gear's center point
                    Line rail2 = new Line(first_bevel_gear.CenterPoint, first_bevel_gear_direction, 100); // A line that has the direction of the end gear facing direction
                    Line rail3 = new Line(first_bevel_gear.CenterPoint, new Vector3d(rail2.Direction.X, rail2.Direction.Y, 0));//A line that is along the xy plane that has the same direction of the end gear facing direction

                    //Move rail3 to minimum z of the end gear
                    double length = first_bevel_gear.CenterPoint.Z - first_bevel_gear.Model.GetBoundingBox(true).Min.Z;
                    Transform transform = Transform.Translation(new Vector3d(0, 0, -length));
                    rail3.Transform(transform);

                    //Find the center point of the gear
                    int second_bevel_gear_teethNum = first_bevel_gear_teeth_num;
                    double second_bevel_gear_pitchRadius = getPitchRadius(second_bevel_gear_teethNum) + clearance;

                    Line rail5 = new Line(rail1.To, new Vector3d(rail2.Direction.X, rail2.Direction.Y, 0), second_bevel_gear_pitchRadius);


                    Point3d second_bevel_gear_centerPoint = rail5.To;
                    Vector3d second_bevel_gear_Direction = new Vector3d(0, 0, 1);
                    Vector3d second_bevel_gear_xDir = new Vector3d(0, 0, 0);
                    double second_bevel_gear_selfRotAngle = 0;
                    if (second_bevel_gear_centerPoint.X > first_bevel_gear.CenterPoint.X && second_bevel_gear_centerPoint.Y < first_bevel_gear.CenterPoint.Y)
                    {
                        //RhinoApp.WriteLine("second_bevel_gear_centerPoint At fourth axis");
                        second_bevel_gear_selfRotAngle = -RhinoMath.ToDegrees(Vector3d.VectorAngle(new Vector3d(rail5.Direction.X, rail5.Direction.Y, 0), new Vector3d(1, 0, 0)));
                        first_bevel_gear.Rotate(second_bevel_gear_selfRotAngle - 360 / second_bevel_gear_teethNum / 2);
                    }
                    else if (second_bevel_gear_centerPoint.X > first_bevel_gear.CenterPoint.X && second_bevel_gear_centerPoint.Y > first_bevel_gear.CenterPoint.Y)
                    {
                        //RhinoApp.WriteLine("second_bevel_gear_centerPoint At first axis");
                        second_bevel_gear_selfRotAngle = RhinoMath.ToDegrees(Vector3d.VectorAngle(new Vector3d(rail5.Direction.X, rail5.Direction.Y, 0), new Vector3d(1, 0, 0)));
                        first_bevel_gear.Rotate(second_bevel_gear_selfRotAngle - 360 / second_bevel_gear_teethNum / 2);
                    }
                    else if (second_bevel_gear_centerPoint.X < first_bevel_gear.CenterPoint.X && second_bevel_gear_centerPoint.Y < first_bevel_gear.CenterPoint.Y)
                    {
                        //RhinoApp.WriteLine("second_bevel_gear_centerPoint At third axis");
                        second_bevel_gear_selfRotAngle = -RhinoMath.ToDegrees(Vector3d.VectorAngle(new Vector3d(rail5.Direction.X, rail5.Direction.Y, 0), new Vector3d(1, 0, 0)));
                        first_bevel_gear.Rotate(second_bevel_gear_selfRotAngle - 360 / second_bevel_gear_teethNum / 2);
                    }
                    else if (second_bevel_gear_centerPoint.X < first_bevel_gear.CenterPoint.X && second_bevel_gear_centerPoint.Y > first_bevel_gear.CenterPoint.Y)
                    {
                        //RhinoApp.WriteLine("second_bevel_gear_centerPoint At second axis");
                        second_bevel_gear_selfRotAngle = RhinoMath.ToDegrees(Vector3d.VectorAngle(new Vector3d(rail5.Direction.X, rail5.Direction.Y, 0), new Vector3d(1, 0, 0)));
                        first_bevel_gear.Rotate(second_bevel_gear_selfRotAngle - 360 / second_bevel_gear_teethNum / 2);
                    }
                    //RhinoApp.WriteLine($"First driven gear rotated {RhinoMath.ToDegrees(Vector3d.VectorAngle(new Vector3d(rail5.Direction.X, rail5.Direction.Y, 0), new Vector3d(1, 0, 0)))} degrees");
                    double second_bevel_gear_coneAngle = bevel_gear_coneAngle;
                    BevelGear second_bevel_gear = new BevelGear(second_bevel_gear_centerPoint, second_bevel_gear_Direction, second_bevel_gear_xDir, second_bevel_gear_teethNum, module, pressure_angle, thickness, second_bevel_gear_selfRotAngle, second_bevel_gear_coneAngle, false);

                    if (second_bevel_gear_centerPoint.Z < start_gear.CenterPoint.Z + 7.5)
                    {
                        RhinoApp.WriteLine("Cannot generate translational motion parameter given this end effector.");
                        Cancel();
                        return;
                    }

                    allGears.Add(second_bevel_gear.Model);
                    #endregion

                    #region connector_gear
                    Point3d connector_gear_centerPoint = new Point3d(second_bevel_gear.CenterPoint.X, second_bevel_gear.CenterPoint.Y, start_gear.CenterPoint.Z);
                    Vector3d connector_gear_Dir = new Vector3d(0, 0, 1);

                    Line r = new Line(connector_gear_centerPoint, start_gear.CenterPoint);

                    Vector3d connector_gear_x_dir = Vector3d.Negate(r.Direction);
                    int connector_gear_teethNum = (int)(start_gear_teethNum * ratio);
                    SpurGear connector_gear = new SpurGear(connector_gear_centerPoint, connector_gear_Dir, connector_gear_x_dir, connector_gear_teethNum, module, pressure_angle, thickness, 0, false);
                    //connector_gear.Rotate(360 / connector_gear_teethNum / 2);
                    #endregion

                    #region shaft between second bevel gear and connector gear
                    Curve connector_shaft_rail = new Line(new Point3d(second_bevel_gear.CenterPoint.X, second_bevel_gear.CenterPoint.Y, second_bevel_gear.Model.GetBoundingBox(true).Max.Z), new Point3d(connector_gear.CenterPoint.X, connector_gear.CenterPoint.Y, connector_gear.Model.GetBoundingBox(true).Min.Z)).ToNurbsCurve();
                    connector_shaft_rail = connector_shaft_rail.Extend(CurveEnd.Both, shaft_extends_from_gear, CurveExtensionStyle.Arc);
                    Brep connector_shaft = Brep.CreatePipe(connector_shaft_rail, shaft_radius, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];
                    allShafts.Add(connector_shaft);
                    #endregion

                    #region create intermediate gears
                    Vector3d intermediate_gear_Direction = new Vector3d(0, 0, 1);
                    Vector3d intermediate_gear_xDir = new Vector3d(0, 0, 0);
                    double intermediate_gear_selfRotAngle = 0;

                    //r = new Line(connector_gear_centerPoint, r.Direction, 100);
                    //start_gear = new SpurGear(r.To, start_gear_Direction, start_gear_xDir, start_gear_teethNum, module, pressure_angle, thickness, 0, false);

                    double tips_distance = (connector_gear.CenterPoint.DistanceTo(start_gear.CenterPoint) - start_gear.BaseRadius - connector_gear.BaseRadius);//Distance from connector gear's tip to start gear's tip
                    (int, int) number_of_gear = bestNumberOfGear(tips_distance);

                    List<SpurGear> intermediates_gears = new List<SpurGear>();

                    if (number_of_gear.Item1 == 0)
                    {
                        RhinoApp.WriteLine("Cannot generate translational motion parameter given this end effector.");
                        Cancel();
                        return;
                    }
                    else
                    {
                        for (int i = 0; i < number_of_gear.Item1; i++)
                        {
                            Line start_gear_connection_rail = new Line(start_gear.CenterPoint, connector_gear.CenterPoint);

                            start_gear_connection_rail = new Line(start_gear.CenterPoint, start_gear_connection_rail.Direction, start_gear.BaseRadius + (i+1) * getTipRadius(number_of_gear.Item2) + i * getBaseRadius(number_of_gear.Item2));
                            Point3d intermediate_centerPoint = start_gear_connection_rail.To;
                            intermediate_gear_xDir = start_gear_connection_rail.Direction;
                            SpurGear intermediate_gear = new SpurGear(intermediate_centerPoint, intermediate_gear_Direction, intermediate_gear_xDir, number_of_gear.Item2, module, pressure_angle, thickness, intermediate_gear_selfRotAngle, false);
                            
                            intermediates_gears.Add(intermediate_gear);

                        }

                        //Make all gears match
                        if (number_of_gear.Item1 == 1)
                        {
                            intermediates_gears[0].Rotate(360 / number_of_gear.Item2 / 2);
                            int count = 0;
                            Intersection.BrepBrep(intermediates_gears[0].Model, connector_gear.Model, myDoc.ModelAbsoluteTolerance, out intersectionCurves, out intersectionPoints);
                            while(intersectionCurves != null && intersectionCurves.Length > 0)
                            {
                                connector_gear.Rotate(0.5);
                                Intersection.BrepBrep(intermediates_gears[0].Model, connector_gear.Model, myDoc.ModelAbsoluteTolerance, out intersectionCurves, out intersectionPoints);
                                count++;
                            }
                            if(count > 0)
                            {
                                connector_gear.Rotate(2);
                                Intersection.BrepBrep(intermediates_gears[0].Model, connector_gear.Model, myDoc.ModelAbsoluteTolerance, out intersectionCurves, out intersectionPoints);
                                while (intersectionCurves != null && intersectionCurves.Length > 0)
                                {
                                    connector_gear.Rotate(0.15);
                                    Intersection.BrepBrep(intermediates_gears[0].Model, connector_gear.Model, myDoc.ModelAbsoluteTolerance, out intersectionCurves, out intersectionPoints);
                                }
                            }
                        }
                        if (number_of_gear.Item1 == 2)
                        {
                            intermediates_gears[0].Rotate(360 / number_of_gear.Item2 / 2);
                            int count = 0;
                            Intersection.BrepBrep(intermediates_gears[1].Model, connector_gear.Model, myDoc.ModelAbsoluteTolerance, out intersectionCurves, out intersectionPoints);
                            while (intersectionCurves != null && intersectionCurves.Length > 0)
                            {
                                connector_gear.Rotate(0.5);
                                Intersection.BrepBrep(intermediates_gears[1].Model, connector_gear.Model, myDoc.ModelAbsoluteTolerance, out intersectionCurves, out intersectionPoints);
                                count++;
                            }

                            if(count > 0)
                            {
                                connector_gear.Rotate(2);
                                Intersection.BrepBrep(intermediates_gears[1].Model, connector_gear.Model, myDoc.ModelAbsoluteTolerance, out intersectionCurves, out intersectionPoints);
                                while (intersectionCurves != null && intersectionCurves.Length > 0)
                                {
                                    connector_gear.Rotate(0.1);
                                    Intersection.BrepBrep(intermediates_gears[1].Model, connector_gear.Model, myDoc.ModelAbsoluteTolerance, out intersectionCurves, out intersectionPoints);
                                }
                            }
                            
                        }
                        if (number_of_gear.Item1 == 3)
                        {
                            intermediates_gears[0].Rotate(360 / number_of_gear.Item2 / 2);
                            intermediates_gears[2].Rotate(360 / number_of_gear.Item2 / 2);

                            int count = 0;

                            Intersection.BrepBrep(intermediates_gears[2].Model, connector_gear.Model, myDoc.ModelAbsoluteTolerance, out intersectionCurves, out intersectionPoints);
                            while (intersectionCurves != null && intersectionCurves.Length > 0)
                            {
                                connector_gear.Rotate(0.5);
                                Intersection.BrepBrep(intermediates_gears[2].Model, connector_gear.Model, myDoc.ModelAbsoluteTolerance, out intersectionCurves, out intersectionPoints);
                                count++;
                            }

                            if(count > 0)
                            {
                                connector_gear.Rotate(2);
                                Intersection.BrepBrep(intermediates_gears[2].Model, connector_gear.Model, myDoc.ModelAbsoluteTolerance, out intersectionCurves, out intersectionPoints);
                                while (intersectionCurves != null && intersectionCurves.Length > 0)
                                {
                                    connector_gear.Rotate(0.1);
                                    Intersection.BrepBrep(intermediates_gears[2].Model, connector_gear.Model, myDoc.ModelAbsoluteTolerance, out intersectionCurves, out intersectionPoints);
                                }
                            }
                        }
                        foreach (var intermediate_gear in intermediates_gears)
                            allGears.Add(intermediate_gear.Model);
                        allGears.Add(connector_gear.Model);
                    }
                    
                    #endregion


                    #region spur gear bottom gaskets
                    Vector3d scaledVector_closerSide = Vector3d.Negate(spur_gear.Direction);
                    scaledVector_closerSide.Unitize();
                    scaledVector_closerSide *= gasket_gear_gap;

                    point1 = spur_gear.BottomPoint + scaledVector_closerSide;

                    Vector3d scaledVector_furtherSide = Vector3d.Negate(spur_gear.Direction);
                    scaledVector_furtherSide.Unitize();
                    scaledVector_furtherSide *= (gasket_gear_gap + bottom_gasket_gear_height);
                    point2 = spur_gear.BottomPoint + scaledVector_furtherSide;

                    Curve spur_gear_bottom_gasket_rail = new Line(point1, point2).ToNurbsCurve();
                    Brep spur_gear_bottom_gasket = Brep.CreateThickPipe(spur_gear_bottom_gasket_rail, gasket_inner_radius, gasket_outer_radius, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];

                    Point3d point3 = spur_gear_bottom_gasket_rail.PointAtLength(spur_gear_bottom_gasket_rail.GetLength() / 2);
                    Circle spur_gear_bottom_gasket_circle = new Circle(new Plane(point3, spur_gear_bottom_gasket_rail.TangentAtStart), (gasket_outer_radius + gasket_inner_radius) / 2);
                    allGaskets.Add(spur_gear_bottom_gasket);
                    #endregion

                    #region  first bevel gear top gaskets
                    scaledVector_closerSide = first_bevel_gear.Direction;
                    scaledVector_furtherSide = first_bevel_gear.Direction;
                    point1 = first_bevel_gear.TopPoint;
                    point2 = first_bevel_gear.TopPoint;

                    if(isReversed)
                    {
                        scaledVector_closerSide = Vector3d.Negate(first_bevel_gear.Direction);
                        scaledVector_furtherSide = Vector3d.Negate(first_bevel_gear.Direction);
                        point1 = first_bevel_gear.BottomPoint;
                        point2 = first_bevel_gear.BottomPoint;
                        myDoc.Objects.AddPoint(first_bevel_gear.BottomPoint, soluableAttribute);
                        myDoc.Objects.AddPoint(first_bevel_gear.TopPoint, lightGuideAttribute);
                    }

                    scaledVector_closerSide.Unitize();
                    scaledVector_closerSide *= gasket_gear_gap;

                    scaledVector_furtherSide.Unitize();
                    scaledVector_furtherSide *= (gasket_gear_gap + bottom_gasket_gear_height);


                    point1 += scaledVector_closerSide;
                    point2 += scaledVector_furtherSide;

                    Curve first_bevel_gear_top_gasket_rail = new Line(point1, point2).ToNurbsCurve();
                    Brep first_bevel_gear_top_gasket = Brep.CreateThickPipe(first_bevel_gear_top_gasket_rail, gasket_inner_radius, gasket_outer_radius, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];

                    point3 = first_bevel_gear_top_gasket_rail.PointAtLength(first_bevel_gear_top_gasket_rail.GetLength() / 2);
                    Circle first_bevel_gear_top_gasket_circle = new Circle(new Plane(point3, first_bevel_gear_top_gasket_rail.TangentAtStart), (gasket_outer_radius + gasket_inner_radius) / 2);

                    allGaskets.Add(first_bevel_gear_top_gasket);
                    #endregion

                    #region  second bevel gear top gaskets
                    scaledVector_closerSide = second_bevel_gear.Direction;
                    scaledVector_furtherSide = second_bevel_gear.Direction;
                    point1 = second_bevel_gear.TopPoint;
                    point2 = second_bevel_gear.TopPoint;

                    scaledVector_closerSide.Unitize();
                    scaledVector_closerSide *= gasket_gear_gap;

                    scaledVector_furtherSide.Unitize();
                    scaledVector_furtherSide *= (gasket_gear_gap + bottom_gasket_gear_height);


                    point1 += scaledVector_closerSide;
                    point2 += scaledVector_furtherSide;

                    Curve second_bevel_gear_top_gasket_rail = new Line(point1, point2).ToNurbsCurve();
                    Brep second_bevel_gear_top_gasket = Brep.CreateThickPipe(second_bevel_gear_top_gasket_rail, gasket_inner_radius, gasket_outer_radius, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];

                    point3 = second_bevel_gear_top_gasket_rail.PointAtLength(second_bevel_gear_top_gasket_rail.GetLength() / 2);
                    Circle second_bevel_gear_top_gasket_circle = new Circle(new Plane(point3, second_bevel_gear_top_gasket_rail.TangentAtStart), (gasket_outer_radius + gasket_inner_radius) / 2);

                    allGaskets.Add(second_bevel_gear_top_gasket);
                    #endregion

                    #region  connect gear bottom gaskets
                    scaledVector_closerSide = Vector3d.Negate(connector_gear.Direction);
                    scaledVector_furtherSide = Vector3d.Negate(connector_gear.Direction);

                    point1 = connector_gear.BottomPoint;
                    point2 = connector_gear.BottomPoint;

                    scaledVector_closerSide.Unitize();
                    scaledVector_closerSide *= gasket_gear_gap;

                    scaledVector_furtherSide.Unitize();
                    scaledVector_furtherSide *= (gasket_gear_gap + bottom_gasket_gear_height);

                    point1 += scaledVector_closerSide;
                    point2 += scaledVector_furtherSide;


                    Curve connector_gear_bottom_gasket_rail = new Line(point1, point2).ToNurbsCurve();
                    Brep connector_gear_bottom_gasket = Brep.CreateThickPipe(connector_gear_bottom_gasket_rail, gasket_inner_radius, gasket_outer_radius, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];

                    point3 = connector_gear_bottom_gasket_rail.PointAtLength(connector_gear_bottom_gasket_rail.GetLength() / 2);
                    Circle connector_gear_bottom_gasket_circle = new Circle(new Plane(point3, connector_gear_bottom_gasket_rail.TangentAtStart), (gasket_outer_radius + gasket_inner_radius) / 2);

                    allGaskets.Add(connector_gear_bottom_gasket);
                    #endregion

                    #region  intermediate gear top gaskets
                    List <Brep> intermediate_gear_top_gaskets = new List<Brep>();
                    List<Circle> intermediate_gear_top_gaskets_circles = new List<Circle>();
                    foreach(var gear in intermediates_gears)
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
                    List <Brep> intermediate_gear_bottom_gaskets = new List<Brep>();
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
                    List <Brep> intermediate_gear_shafts = new List<Brep>();

                    foreach(var gear in intermediates_gears)
                    {
                        shaft_line = new Line(gear.TopPoint, gear.BottomPoint).ToNurbsCurve().Extend(CurveEnd.Both, shaft_extends_from_gear, CurveExtensionStyle.Arc);
                        Brep intermediate_gear_shaft = Brep.CreatePipe(shaft_line, shaft_radius, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];
                        intermediate_gear_shafts.Add(intermediate_gear_shaft);
                        allShafts.Add(intermediate_gear_shaft);
                    }
                    #endregion 

                    #region check for intersection
                    //Check intersection for every single generated item with the currModel
                    foreach(var brep in allShafts)
                    {
                        Intersection.BrepBrep(brep, mainModel, myDoc.ModelAbsoluteTolerance, out intersectionCurves, out intersectionPoints);

                        if(intersectionCurves!=null && intersectionCurves.Length > 0)
                        {
                            RhinoApp.WriteLine("Fail to generate a translational motion parameter with this end effector");
                            return;
                        }
                    }

                    foreach (var brep in allGaskets)
                    {
                        Intersection.BrepBrep(brep, mainModel, myDoc.ModelAbsoluteTolerance, out intersectionCurves, out intersectionPoints);

                        if (intersectionCurves != null && intersectionCurves.Length > 0)
                        {
                            RhinoApp.WriteLine("Fail to generate a translational motion parameter with this end effector");
                            return;
                        }
                    }

                    foreach (var brep in allGears)
                    {
                        Intersection.BrepBrep(brep, mainModel, myDoc.ModelAbsoluteTolerance, out intersectionCurves, out intersectionPoints);

                        if (intersectionCurves != null && intersectionCurves.Length > 0)
                        {
                            RhinoApp.WriteLine("Fail to generate a translational motion parameter with this end effector");
                            return;
                        }
                    }
                    #endregion


                    myDoc.Objects.AddPoint(first_bevel_gear.TopPoint, lightGuideAttribute);
                    myDoc.Objects.AddPoint(first_bevel_gear.BottomPoint, soluableAttribute);
                    myDoc.Objects.AddPoint(spur_gear.TopPoint, lightGuideAttribute);
                    myDoc.Objects.AddPoint(spur_gear.BottomPoint, soluableAttribute);
                    myDoc.Objects.Add(rack.Model);
                    myDoc.Objects.Add(spur_gear.Model);
                    myDoc.Objects.Add(first_bevel_gear.Model);
                    myDoc.Objects.Add(shaft);
                    myDoc.Objects.Add(second_bevel_gear.Model);
                    myDoc.Objects.Add(rack_holder);
                    myDoc.Objects.Add(connector_gear.Model);
                    myDoc.Objects.Add(connector_shaft);
                    myDoc.Objects.Add(start_gear.Model);
                    myDoc.Objects.Add(spur_gear_bottom_gasket);
                    myDoc.Objects.Add(first_bevel_gear_top_gasket);
                    myDoc.Objects.Add(second_bevel_gear_top_gasket);
                    myDoc.Objects.Add(connector_gear_bottom_gasket);

                    foreach (var gasket in intermediate_gear_top_gaskets)
                        myDoc.Objects.Add(gasket);
                    foreach (var gasket in intermediate_gear_bottom_gaskets)
                        myDoc.Objects.Add(gasket);
                    foreach (var intermediate_gear_shaft in intermediate_gear_shafts)
                        myDoc.Objects.Add(intermediate_gear_shaft);


                    foreach (var intermediate_gear in intermediates_gears)
                    {
                        myDoc.Objects.Add(intermediate_gear.Model);
                    }

                    DA.SetData(0, false);
                    Item savedItem = new Item();
                    savedItem.Name = "Translational Motion";
                    savedItem.Description = "Nnsaved";
                    savedItem.gearEssentials = essentials;
                    DA.SetData(1, savedItem);

                    TrueOnlyButtonValueController_Translational.finished = 1;
                }
                #endregion
            }
        }

        private void Cancel()
        {
            var allObjects = new List<RhinoObject>(RhinoDoc.ActiveDoc.Objects.GetObjectList(ObjectType.AnyObject));
            foreach (var singleObject in allObjects)
                if (SavedItems.originalModelGuids.All(guid => guid != singleObject.Id))
                    RhinoDoc.ActiveDoc.Objects.Delete(singleObject.Id, true);
            RhinoDoc.ActiveDoc.Objects.Show(SavedItems.originalModelGuids[0], true);
        }


        private double getBaseRadius(int teethNum)
        {
            double pitchDiameter = module * teethNum;
            double outDiameter = pitchDiameter;
            return outDiameter / 2;
        }

        private double getTipRadius(int teethNum)
        {
            double pitchDiameter = module * teethNum;
            double outDiameter = pitchDiameter + 2 * module;
            return outDiameter / 2;
        }

        private int getNumTeeth(double tipRadius)
        {
            int numTeeth = ((int)((2 * tipRadius - 2 * module) / module));
            return numTeeth;
        }

        private (int, int) bestNumberOfGear(double tips_distance)
        {
            int numTeeth = getNumTeeth(tips_distance/2);



            if (numTeeth <= 10)
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

            return (0, 0);
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

        private double getPitchRadius(int teethNum)
        {
            double pitchDiameter = 1.5 * teethNum;
            return pitchDiameter / 2;
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
            get { return new Guid("38788C77-406B-494A-AF92-0E4BD55E85E4"); }
        }
    }
}