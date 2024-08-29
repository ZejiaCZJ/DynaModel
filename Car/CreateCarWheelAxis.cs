using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows;
using DynaModel_v2.Geometry;
using DynaModel_v2.SharedData;
using Grasshopper.Kernel;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using Rhino.Input;

namespace DynaModel_v2.Car
{
    public class CreateCarWheelAxis : GH_Component
    {
        RhinoDoc myDoc = RhinoDoc.ActiveDoc;
        private Guid currModelObjId = Guid.Empty;
        private Guid currModel_Hollowed_ObjId = Guid.Empty;
        private Brep currModel = null;
        private Brep currModel_Hollowed = null;
        private BoundingBox currModel_box;

        public double foundation_length = 64;
        public double foundation_width = 52;
        public double foundation_height = 18;
        public Point3d foundation_origin;
        public Point3d foundation_center;
        public Brep foundation { get; set; }
        public double pcb_width = 50;
        public double pcb_length = 50;
        public double pcb_origin_x = 1; //Relative coordinate to foundation
        public double pcb_origin_y = 1+13;//Relative coordinate to foundation
        public Point3d pcb_origin;
        public Point3d pcb_center;

        private List<Point3d> surfacePts = new List<Point3d>();

        private static double module = 1.5;
        private static double pressure_angle = 20;
        private static double thickness = 5;
        private static double wheel_shaft_extends_from_model = 7;
        private static double shaft_radius = 3;
        private static double shaft_extends_from_gear = 3;
        private static double gasket_inner_radius = 3.4; //shaft_radius + 0.4
        private static double gasket_outer_radius = 7;
        private static double gasket_gear_gap = 0.6;
        private static double bottom_gasket_gear_height = 4;
        public List<Curve> gasketCircles;
        public List<Guid> gaskets_guid;
        private List<SpurGear> allGears;
        private List<Brep> allGaskets;
        private List<Brep> allShafts;

        public ObjectAttributes solidAttribute, lightGuideAttribute, redAttribute, yellowAttribute, soluableAttribute;

        /// <summary>
        /// Initializes a new instance of the CreateCarWheelAxis class.
        /// </summary>
        public CreateCarWheelAxis()
          : base("CreateCarWheelAxis", "CreateCarWheelAxis",
              "This component create a car wheel axis",
              "DynaModel_v2", "Car Wheel")
        {
            allGears = new List<SpurGear>();
            allGaskets = new List<Brep>();
            allShafts = new List<Brep>();

            gaskets_guid = new List<Guid>();
            gasketCircles = new List<Curve>();

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
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Item", "I", "The Item to be saved", GH_ParamAccess.item);
        }

        protected void Initialize()
        {
            currModelObjId = SavedItems.originalModelGuids[0];
            RhinoObject currModelObj = myDoc.Objects.FindId(currModelObjId);
            if (currModelObj != null && currModelObj.Geometry.ObjectType == ObjectType.Brep)
            {
                currModel = currModelObj.Geometry as Brep;
            }
            else
            {
                RhinoApp.WriteLine("There are none or more than one models in Rhino document");
                Cancel();
                return;
            }

            currModel_Hollowed_ObjId = SavedItems.originalModelGuids[1];
            RhinoObject currModel_Hollowed_Obj = myDoc.Objects.FindId(currModel_Hollowed_ObjId);
            if (currModel_Hollowed_Obj != null && currModel_Hollowed_Obj.Geometry.ObjectType == ObjectType.Brep)
            {
                currModel_Hollowed = currModel_Hollowed_Obj.Geometry as Brep;
            }
            else
            {
                RhinoApp.WriteLine("There are none or more than one models in Rhino document");
                Cancel();
                return;
            }

            //Generate Foundation and PCB if possible
            currModel_box = currModel.GetBoundingBox(true);
            Circle circle = new Circle(new Point3d(0, 0, currModel_box.Min.Z + 0.1), 1000);
            Brep planarSurface = Brep.CreatePlanarBreps(new[] { circle.ToNurbsCurve() }, myDoc.ModelAbsoluteTolerance)[0];
            Brep[] a = planarSurface.Trim(currModel, myDoc.ModelAbsoluteTolerance);
            Brep currModel_bottom = new Brep();
            if (a.Length > 0)
                currModel_bottom = a[0];
            Point3d centroid = AreaMassProperties.Compute(currModel_bottom.Faces[0]).Centroid;

            foundation_center = new Point3d(centroid.X, centroid.Y, currModel_box.Min.Z + foundation_height / 2);
            foundation_origin = new Point3d(centroid.X - foundation_width / 2, centroid.Y - foundation_length / 2, currModel_box.Min.Z);
            foundation = new BoundingBox(foundation_origin, new Point3d(foundation_center.X + foundation_width / 2, foundation_center.Y + foundation_length / 2, currModel_box.Min.Z + foundation_height)).ToBrep();
            pcb_origin = new Point3d(foundation_origin.X + pcb_origin_x, foundation_origin.Y + pcb_origin_y, currModel_box.Min.Z + foundation_height);
            pcb_center = new Point3d(pcb_origin.X + pcb_width, pcb_origin.Y + pcb_length, pcb_origin.Z);
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
                Initialize();

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
                            Cancel();
                            return;
                        }
                    }

                    if (currModel == null)
                    {
                        RhinoApp.WriteLine("Your model cannot be fixed to become manifold and closed, please try to fix it manually");
                        Cancel();
                        return;
                    }
                    #endregion

                    #region Show initial gear
                    Point3d start_gear_centerPoint = new Point3d(foundation_origin.X + 6, foundation_origin.Y + 67.5, foundation_origin.Z + 7);
                    Vector3d start_gear_Direction = new Vector3d(0, 1, 0);
                    Vector3d start_gear_xDir = new Vector3d(0, 0, 0);
                    int start_gear_teethNum = 20;
                    double start_gear_selfRotAngle = 0;

                    SpurGear start_gear = new SpurGear(start_gear_centerPoint, start_gear_Direction, start_gear_xDir, start_gear_teethNum, module, pressure_angle, thickness, start_gear_selfRotAngle, true);

                    myDoc.Objects.Add(foundation);
                    myDoc.Objects.Add(start_gear.Model);
                    #endregion

                    #region Display points for user to choose
                    BoundingBox boundingBox = currModel_hollowed.GetBoundingBox(true);

                    double w = boundingBox.Max.X - boundingBox.Min.X;
                    double l = boundingBox.Max.Y - boundingBox.Min.Y;
                    double h = boundingBox.Max.Z - boundingBox.Min.Z;
                    double offset = 5;
                    double clearance = 7;

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
                                if (start_gear.Direction.Equals(new Vector3d(0, 1, 0)) || start_gear.Direction.Equals(new Vector3d(0, -1, 0)))
                                {
                                    foreach (Curve curve in intersectionCurves)
                                    {
                                        Double[] curveParams = curve.DivideByLength(4, true, out Point3d[] points);
                                        if (curveParams != null && curveParams.Length > 0)
                                        {
                                            foreach (var point in points)
                                            {
                                                Line line = new Line(point, start_gear.Direction, 10);
                                                line.Extend(500, 500);
                                                Intersection.CurveBrep(line.ToNurbsCurve(), currModel, myDoc.ModelAbsoluteTolerance, out intersectionCurves, out intersectionPoints);
                                                if (intersectionPoints.Length == 2)
                                                {
                                                    //Predict if the first spur gear will intersect the model
                                                    if (inRange_Y(intersectionPoints[0], intersectionPoints[1], start_gear.CenterPoint, clearance))
                                                    {
                                                        Point3d temp_point = new Point3d(start_gear.CenterPoint);
                                                        temp_point.X = intersectionPoints[0].X;
                                                        temp_point.Z = intersectionPoints[0].Z;
                                                        double tips_distance = (temp_point.DistanceTo(start_gear.CenterPoint) - start_gear.BaseRadius);

                                                        int first_spur_gear_teethNum = getNumTeeth(tips_distance);

                                                        double first_spur_gear_tip_radius = getTipRadius(first_spur_gear_teethNum);

                                                        Line temp_line = new Line(temp_point, new Vector3d(0, 0, -1), first_spur_gear_tip_radius);

                                                        if (temp_line.To.Z < foundation_origin.Z || first_spur_gear_teethNum > 30)
                                                        {
                                                            first_spur_gear_teethNum = start_gear.NumTeeth;
                                                        }

                                                        if (first_spur_gear_teethNum < 5)
                                                        {
                                                            continue;
                                                        }

                                                        SpurGear first_spur_gear = new SpurGear(temp_point, start_gear.Direction, new Vector3d(0, 0, 0), first_spur_gear_teethNum, module, pressure_angle, thickness, 0, false);
                                                        Intersection.BrepBrep(first_spur_gear.Model, currModel_Hollowed, myDoc.ModelAbsoluteTolerance, out intersectionCurves, out _);

                                                        temp_line = new Line(intersectionPoints[0], intersectionPoints[1]);

                                                        Brep shaft = Brep.CreatePipe(temp_line.ToNurbsCurve(), shaft_radius, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];
                                                        Intersection.BrepBrep(shaft, foundation, myDoc.ModelAbsoluteTolerance, out Curve[] intersectionCurves1, out Point3d[] intersectionPoints1);

                                                        if ((intersectionCurves == null || intersectionCurves.Length == 0) && (intersectionCurves1 == null || intersectionCurves1.Length == 0))
                                                            surfacePts.Add(point); //Display the point if all conditions are valid
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                                else if (start_gear.Direction.Equals(new Vector3d(1, 0, 0)) || start_gear.Direction.Equals(new Vector3d(-1, 0, 0)))
                                {
                                    foreach (Curve curve in intersectionCurves)
                                    {
                                        Double[] curveParams = curve.DivideByLength(4, true, out Point3d[] points);
                                        if (curveParams != null && curveParams.Length > 0)
                                        {
                                            foreach (var point in points)
                                            {
                                                Line line = new Line(point, start_gear.Direction, 10);
                                                line.Extend(500, 500);
                                                Intersection.CurveBrep(line.ToNurbsCurve(), currModel, myDoc.ModelAbsoluteTolerance, out intersectionCurves, out intersectionPoints);
                                                if (intersectionPoints.Length == 2)
                                                {
                                                    if (inRange_X(intersectionPoints[0], intersectionPoints[1], start_gear.CenterPoint, clearance))
                                                    {
                                                        //Predict if the first spur gear will intersect the model
                                                        Point3d temp_point = new Point3d(start_gear.CenterPoint);
                                                        temp_point.Y = intersectionPoints[0].Y;
                                                        temp_point.Z = intersectionPoints[0].Z;
                                                        double tips_distance = (temp_point.DistanceTo(start_gear.CenterPoint) - start_gear.BaseRadius);

                                                        int first_spur_gear_teethNum = getNumTeeth(tips_distance);

                                                        double first_spur_gear_tip_radius = getTipRadius(first_spur_gear_teethNum);

                                                        Line temp_line = new Line(temp_point, new Vector3d(0, 0, -1), first_spur_gear_tip_radius);

                                                        if (temp_line.To.Z < foundation_origin.Z || first_spur_gear_teethNum > 30)
                                                        {
                                                            first_spur_gear_teethNum = start_gear.NumTeeth;
                                                        }

                                                        if (first_spur_gear_teethNum < 5)
                                                        {
                                                            continue;
                                                        }

                                                        SpurGear first_spur_gear = new SpurGear(temp_point, start_gear.Direction, new Vector3d(0, 0, 0), first_spur_gear_teethNum, module, pressure_angle, thickness, 0, false);
                                                        Intersection.BrepBrep(first_spur_gear.Model, currModel_Hollowed, myDoc.ModelAbsoluteTolerance, out intersectionCurves, out _);
                                                        temp_line = new Line(intersectionPoints[0], intersectionPoints[1]);

                                                        Brep shaft = Brep.CreatePipe(temp_line.ToNurbsCurve(), shaft_radius, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];
                                                        Intersection.BrepBrep(shaft, foundation, myDoc.ModelAbsoluteTolerance, out Curve[] intersectionCurves1, out Point3d[] intersectionPoints1);

                                                        if ((intersectionCurves == null || intersectionCurves.Length == 0) && (intersectionCurves1 == null || intersectionCurves1.Length == 0))
                                                            surfacePts.Add(point); //Display the point if all conditions are valid
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }

                    List<Guid> pts_Guid = new List<Guid>();
                    foreach (Point3d point in surfacePts)
                    {
                        Guid pointID = myDoc.Objects.AddPoint(point);
                        pts_Guid.Add(pointID);
                    }
                    myDoc.Views.Redraw();
                    #endregion

                    #region Ask the user to select points to generate the area of the parameter
                    var getSelectedPts = RhinoGet.GetOneObject("Please select points for a touch parameter, press ENTER when finished", false, ObjectType.Point, out ObjRef pointRef);
                    #endregion

                    if (getSelectedPts == Rhino.Commands.Result.Success)
                    {
                        allGears = new List<SpurGear>();
                        allGaskets = new List<Brep>();
                        allShafts = new List<Brep>();

                        Item savedItem = new Item();

                        Point3d end_point = new Point3d(pointRef.Point().Location);
                        double x = end_point.X;
                        double y = end_point.Y;
                        double z = end_point.Z;

                        //Delete all points on the view
                        foreach (var ptsID in pts_Guid)
                        {
                            myDoc.Objects.Delete(ptsID, true);
                        }

                        #region first spur gear shaft
                        Curve first_spur_gear_shaft_rail = new Line(end_point, start_gear.Direction, 10).ToNurbsCurve();
                        first_spur_gear_shaft_rail = first_spur_gear_shaft_rail.Extend(CurveEnd.Both, 500, CurveExtensionStyle.Line);

                        Intersection.CurveBrep(first_spur_gear_shaft_rail, currModel, myDoc.ModelAbsoluteTolerance, out Curve[] intersectionCurves, out Point3d[] intersectionPoints);

                        if(intersectionPoints.Length != 2)
                        {
                            RhinoApp.WriteLine("Cannot generate a car gear for this point, try other points");
                            Cancel(); 
                            return;
                        }

                        first_spur_gear_shaft_rail = new Line(intersectionPoints[0], intersectionPoints[1]).ToNurbsCurve() as Curve;

                        first_spur_gear_shaft_rail = first_spur_gear_shaft_rail.Extend(CurveEnd.Both, wheel_shaft_extends_from_model, CurveExtensionStyle.Line);

                        Brep first_spur_gear_shaft = Brep.CreatePipe(first_spur_gear_shaft_rail, shaft_radius, true, PipeCapMode.Round, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];
                        allShafts.Add(first_spur_gear_shaft);
                        #endregion


                        #region first spur gear
                        Point3d first_spur_gear_CenterPoint = new Point3d(start_gear.CenterPoint.X, end_point.Y, end_point.Z);

                        //The shaft is on the y plane
                        if (Math.Abs(start_gear.Direction.Y) == 1)
                        {
                            first_spur_gear_CenterPoint = new Point3d(end_point.X, start_gear.CenterPoint.Y, end_point.Z);
                        }

                        //Check if the spur gear can be solo or any additional gears need to get involved
                        double tips_distance = (first_spur_gear_CenterPoint.DistanceTo(start_gear.CenterPoint) - start_gear.BaseRadius);

                        int first_spur_gear_teethNum = getNumTeeth(tips_distance);

                        if(first_spur_gear_teethNum < 5)
                        {
                            RhinoApp.WriteLine("Cannot generate a car gear for this point, try other points: too close to initial gear");
                            Cancel();
                            return;
                        }

                        bool intermediate_gears_required = false;

                        double first_spur_gear_tip_radius = getTipRadius(first_spur_gear_teethNum);

                        Line temp_line = new Line(first_spur_gear_CenterPoint, new Vector3d(0, 0, -1), first_spur_gear_tip_radius);

                        if(temp_line.To.Z < foundation_origin.Z || first_spur_gear_teethNum > 30)
                        {
                            first_spur_gear_teethNum = start_gear.NumTeeth;
                            intermediate_gears_required = true;
                        }
                        Line start_gear_connection_rail = new Line(start_gear.CenterPoint, first_spur_gear_CenterPoint);
                        Vector3d first_spur_gear_xDirection = start_gear_connection_rail.Direction;
                        //first_spur_gear_xDirection = new Vector3d(0,0,0);
                        SpurGear first_spur_gear = new SpurGear(first_spur_gear_CenterPoint, start_gear.Direction, first_spur_gear_xDirection, first_spur_gear_teethNum, module, pressure_angle, thickness, 0, false);
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

                        allGaskets.Add(first_spur_gear_top_gasket);
                        gasketCircles.Add(first_spur_gear_top_gasket_circle.ToNurbsCurve());
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

                        allGaskets.Add(first_spur_gear_bottom_gasket);
                        gasketCircles.Add(first_spur_gear_bottom_gasket_circle.ToNurbsCurve());
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
                                RhinoApp.WriteLine("Cannot generate car parameter given this point: fail to create a suitable end gear");
                                Cancel();
                                return;
                            }
                            else
                            {
                                if(number_of_gear.Item1 != 1)
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
                                            if(count > 1000)
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

                                allGears.AddRange(intermediates_gears);
                            }

                        }

                        #endregion

                        #region  intermediate gear top gaskets
                        List<Brep> intermediate_gear_top_gaskets = new List<Brep>();
                        List<Circle> intermediate_gear_top_gaskets_circles = new List<Circle>();
                        foreach (var gear in allGears)
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
                            gasketCircles.Add(intermediate_gear_top_gasket_circle.ToNurbsCurve());
                        }

                        #endregion

                        #region  intermediate gear bottom gaskets
                        List<Brep> intermediate_gear_bottom_gaskets = new List<Brep>();
                        List<Circle> intermediate_gear_bottom_gaskets_circles = new List<Circle>();
                        foreach (var gear in allGears)
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
                            gasketCircles.Add(intermediate_gear_bottom_gasket_circle.ToNurbsCurve());
                        }
                        #endregion

                        #region intermediate gear shaft
                        List<Brep> intermediate_gear_shafts = new List<Brep>();

                        foreach (var gear in allGears)
                        {
                            Curve shaft_line = new Line(gear.TopPoint, gear.BottomPoint).ToNurbsCurve().Extend(CurveEnd.Both, shaft_extends_from_gear, CurveExtensionStyle.Arc);
                            Brep intermediate_gear_shaft = Brep.CreatePipe(shaft_line, shaft_radius, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];
                            intermediate_gear_shafts.Add(intermediate_gear_shaft);
                            allShafts.Add(intermediate_gear_shaft);
                        }
                        #endregion



                        List<Brep> allBreps = new List<Brep>();
                        allBreps.Add(first_spur_gear_shaft);
                        allBreps.Add(first_spur_gear.Model);
                        allBreps.Add(first_spur_gear_bottom_gasket);
                        allBreps.Add(first_spur_gear_top_gasket);
                        allBreps.Add(foundation);



                        myDoc.Objects.Add(first_spur_gear_shaft);
                        myDoc.Objects.Add(first_spur_gear.Model);
                        gaskets_guid.Add(myDoc.Objects.Add(first_spur_gear_bottom_gasket));
                        gaskets_guid.Add(myDoc.Objects.Add(first_spur_gear_top_gasket));

                        foreach (var gear in intermediates_gears)
                        {
                            myDoc.Objects.Add(gear.Model);
                            allBreps.Add(gear.Model);
                        }

                        foreach (var gasekt in intermediate_gear_top_gaskets)
                        {
                            myDoc.Objects.Add(gasekt);
                            allBreps.Add(gasekt);
                        }
                            

                        foreach (var gasekt in intermediate_gear_bottom_gaskets)
                        {
                            myDoc.Objects.Add(gasekt);
                            allBreps.Add(gasekt);
                        }
                            

                        foreach (var shaft in intermediate_gear_shafts)
                        {
                            myDoc.Objects.Add(shaft);
                            allBreps.Add(shaft);
                        }
                            

                        #region Ask user to select point to create a back wheel shaft
                        bool success = false;
                        Curve second_shaft_rail = null;
                        Brep second_shaft = new Brep();
                        while (!success)
                        {
                            pts_Guid = new List<Guid>();
                            foreach (Point3d point in surfacePts)
                            {
                                Guid pointID = myDoc.Objects.AddPoint(point);
                                pts_Guid.Add(pointID);
                            }
                            myDoc.Views.Redraw();

                            getSelectedPts = RhinoGet.GetOneObject("Please select points for a touch parameter, press ENTER when finished", false, ObjectType.Point, out pointRef);

                            if (getSelectedPts == Rhino.Commands.Result.Success)
                            {
                                end_point = new Point3d(pointRef.Point().Location);
                                x = end_point.X;
                                y = end_point.Y;
                                z = end_point.Z;

                                //Delete all points on the view
                                foreach (var ptsID in pts_Guid)
                                {
                                    myDoc.Objects.Delete(ptsID, true);
                                }

                                second_shaft_rail = new Line(end_point, start_gear.Direction, 10).ToNurbsCurve();
                                second_shaft_rail = second_shaft_rail.Extend(CurveEnd.Both, 500, CurveExtensionStyle.Line);
                                Intersection.CurveBrep(second_shaft_rail, currModel, myDoc.ModelAbsoluteTolerance, out _, out intersectionPoints);
                                second_shaft_rail = new Line(intersectionPoints[0], intersectionPoints[1]).ToNurbsCurve();
                                second_shaft_rail = second_shaft_rail.Extend(CurveEnd.Both, wheel_shaft_extends_from_model, CurveExtensionStyle.Line);

                                second_shaft = Brep.CreatePipe(second_shaft_rail, shaft_radius, true, PipeCapMode.Round, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];

                                foreach(var brep in allBreps)
                                {
                                    Intersection.BrepBrep(second_shaft, brep, myDoc.ModelAbsoluteTolerance, out intersectionCurves, out _);

                                    if (intersectionCurves != null && intersectionCurves.Length > 0)
                                    {
                                        success = false;
                                        break;
                                    }
                                    else
                                    {
                                        success = true;
                                    }
                                }
                            }
                        }
                        myDoc.Objects.Add(second_shaft);
                        #endregion





                        List<Brep> gears_and_shafts = new List<Brep>();
                        gears_and_shafts.Add(first_spur_gear.Model);
                        gears_and_shafts.Add(first_spur_gear_shaft);
                        foreach (var gear in allGears)
                            gears_and_shafts.Add(gear.Model);
                        gears_and_shafts.AddRange(allShafts);
                        gears_and_shafts.Add(second_shaft);

                        savedItem.AllBreps = gears_and_shafts; //Gears and shafts
                        savedItem.AllGaskets = allGaskets; //Gaskets
                        savedItem.GasketCircles = gasketCircles; //Gasket circles
                        
                        savedItem.ShaftCurve = new List<Curve>();
                        savedItem.ShaftCurve.Add(first_spur_gear_shaft_rail); //Shaft rail to generate shaft clearance
                        savedItem.ShaftCurve.Add(second_shaft_rail);
                        savedItem.Name = "Car";

                        DA.SetData(0, savedItem);
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

        private void Cancel()
        {
            var allObjects = new List<RhinoObject>(RhinoDoc.ActiveDoc.Objects.GetObjectList(ObjectType.AnyObject));
            foreach (var singleObject in allObjects)
                if (SavedItems.originalModelGuids.All(guid => guid != singleObject.Id))
                    RhinoDoc.ActiveDoc.Objects.Delete(singleObject.Id, true);
            RhinoDoc.ActiveDoc.Objects.Show(SavedItems.originalModelGuids[0], true);
        }


        private bool inRange_Y(Point3d pt1, Point3d pt2, Point3d targetPoint, double clearance)
        {
            if(pt1.Y >= pt2.Y)
            {
                if (pt1.Y - clearance > targetPoint.Y && pt2.Y + clearance < targetPoint.Y && targetPoint.Z < pt1.Z-10)
                {
                    return true;
                }
            }
            else
            {
                if (pt2.Y - clearance > targetPoint.Y && pt1.Y + clearance < targetPoint.Y && targetPoint.Z < pt1.Z - 10)
                {
                    return true;
                }
            }

            return false;
        }

        private bool inRange_X(Point3d pt1, Point3d pt2, Point3d targetPoint, double clearance)
        {
            if (pt1.X >= pt2.X)
            {
                if (pt1.X - clearance > targetPoint.X && pt2.X + clearance < targetPoint.X && targetPoint.Z < pt1.Z - 10)
                {
                    return true;
                }
            }
            else
            {
                if (pt2.X - clearance > targetPoint.X && pt1.X + clearance < targetPoint.X && targetPoint.Z < pt1.Z - 10)
                {
                    return true;
                }
            }

            return false;
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
            get { return new Guid("60173531-8C0D-4C4A-B3FC-C75E25755FDC"); }
        }
    }
}