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
        private double shaft_radius = 3;
        ObjectAttributes solidAttribute, lightGuideAttribute, redAttribute, yellowAttribute, soluableAttribute;

        private List<Brep> allBreps;
        private List<Guid> allBreps_guid;


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
                    Point3d start_gear_centerPoint = new Point3d(mainModel_bbox.Center.X, mainModel_bbox.Center.Y, mainModel_bbox.Min.Z + 5);
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
                    #endregion

                    double bevel_gear_coneAngle = RhinoMath.ToDegrees(Vector3d.VectorAngle(new Vector3d(0, 0, 1), spur_gear_direction));

                    #region first shaft
                    double shaft_length = 30;
                    if (bevel_gear_coneAngle < 90)
                        shaft_length = 10;

                    //Generate a bevel gear that connects to the spur gear and the shaft between them
                    Curve shaft_line = new Line(spur_gear.CenterPoint, spur_gear_direction, shaft_length).ToNurbsCurve();
                    Brep shaft = Brep.CreatePipe(shaft_line, shaft_radius, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];
                    #endregion

                    #region first bevel gear
                    Vector3d first_bevel_gear_direction = spur_gear_direction;
                    bool isReversed = false;
                    if (bevel_gear_coneAngle > 90)
                    {
                        first_bevel_gear_direction = Vector3d.Negate(first_bevel_gear_direction);
                        bevel_gear_coneAngle = 180 - bevel_gear_coneAngle;
                        isReversed = true;
                    }
                    BevelGear first_bevel_gear = new BevelGear(shaft_line.PointAtEnd, first_bevel_gear_direction, spur_gear_x_dir, teeth_num, module, pressure_angle, thickness, selfRotAngle, bevel_gear_coneAngle, false);
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
                    int second_bevel_gear_teethNum = teeth_num;
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
                    #endregion

                    myDoc.Objects.Add(rack.Model);
                    myDoc.Objects.Add(spur_gear.Model);
                    myDoc.Objects.Add(first_bevel_gear.Model);
                    myDoc.Objects.Add(shaft);
                    myDoc.Objects.Add(second_bevel_gear.Model);
                    myDoc.Objects.Add(rack_holder);

                    TrueOnlyButtonValueController_Translational.finished = 1;
                }
                #endregion
            }
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