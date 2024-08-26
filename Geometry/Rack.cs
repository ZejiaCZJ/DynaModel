using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Rhino.Geometry;
using Rhino;

namespace DynaModel_v2.Geometry
{
    public class Rack : Component
    {
        private double _length = 0;
        private double _rootHeight = 0;
        private double _tipHeight = 0;
        private double _thickness = 0; // the thickness of the rack base
        private double _toothDepth = 0;//=tR-rR;
                                       //private double factorA = -1, factorB = -1;//Cancelled since adjustment of teeth should be automatically done.
        private int _z;//Total number of teeth
        private double _pitch = 0;//pitch ,distance between corresponding points on adjacent teeth. p=m*Pi;
        private double _module = 0;//module of teeth size, 
        private double _faceWidth = 0;
        private double _pressureAngle = 0;

        private double _rack_holder_gap = 0.5;
        private double _rackBase_holder_gap = 0.5;
        private double _rack_holder_thickness = 2;

        private double _rackBase_extend_from_rack = 3;
        private double _rackBase_thickness = 1;
        private double _rack_holder_length = 18;


        private Point3d _centerPoint = Point3d.Unset;
        private Point3d _startPoint = Point3d.Unset;
        private Point3d _endPoint = Point3d.Unset;
        private Plane _rackPlane = new Plane();
        private Vector3d _faceDirection = Vector3d.Unset;
        private Vector3d _rackDirection = Vector3d.Unset;
        private Vector3d _extrudeDirection = Vector3d.Unset;
        private Line _backBone = Line.Unset;
        private List<Point3d> TeethTips = new List<Point3d>();
        private List<Point3d> TeethBtms = new List<Point3d>();
        private Brep _boundingBox = new Brep();
        private Point3d _boundingBoxCenter = new Point3d();
        private Line _realBackBone = Line.Unset;

        private Curve _rack_holder_curve;
        private Curve _rack_holder_curve_outter_wall;
        private Curve _rack_holder_curve_inner_wall;
        private Curve _rack_holder_middle_curve_1;
        private Curve _rack_holder_middle_curve_2;
        private Brep _rack_holder = new Brep();

        RhinoDoc myDoc = RhinoDoc.ActiveDoc;

        public double Length { get => _length; private set => _length = value; }
        public double RootHeight { get => _rootHeight; private set => _rootHeight = value; }
        public double TipHeight { get => _tipHeight; private set => _tipHeight = value; }
        public double ToothDepth { get => _toothDepth; private set => _toothDepth = value; }
        /// <summary>
        /// Number of teeth. It's auto calculated using module in constructor.
        /// </summary>
        public int Z { get => _z; private set => _z = value; }
        /// <summary>
        /// Actual distance between teeth.
        /// </summary>
        public double Pitch { get => _pitch; private set => _pitch = value; }
        public double Module { get => _module; private set => _module = value; }
        public double FaceWidth { get => _faceWidth; private set => _faceWidth = value; }
        public Point3d CenterPoint { get => _centerPoint; private set => _centerPoint = value; }
        public Point3d StartPoint { get => _startPoint; private set => _startPoint = value; }
        public Point3d EndPoint { get => _endPoint; private set => _endPoint = value; }
        public Vector3d FaceDirection { get => _faceDirection; private set => _faceDirection = value; }
        public Line BackBone { get => _backBone; private set => _backBone = value; }
        public Vector3d RackDirection { get => _rackDirection; private set => _rackDirection = value; }
        public Vector3d ExtrudeDirection { get => _extrudeDirection; private set => _extrudeDirection = value; }
        public Brep BoundingBox { get => _boundingBox; private set => _boundingBox = value; }
        public Point3d BoundingBoxCenter { get => _boundingBoxCenter; private set => _boundingBoxCenter = value; }

        public Line RealBackBone { get => _realBackBone; set => _realBackBone = value; }

        public Curve RackHolderCurve { get => _rack_holder_curve; set => _rack_holder_curve = value; }

        public Curve RackHolderCurveOutterWall { get => _rack_holder_curve_outter_wall; set => _rack_holder_curve_outter_wall = value; }

        public Curve RackHolderCurveInnerWall { get => _rack_holder_curve_inner_wall; set => _rack_holder_curve_inner_wall = value; }

        public Curve RackHolderMiddleCurve1 { get => _rack_holder_middle_curve_1; set => _rack_holder_middle_curve_1 = value; }

        public Curve RackHolderMiddleCurve2 { get => _rack_holder_middle_curve_2; set => _rack_holder_middle_curve_2 = value; }

        public Brep RackHolder { get => _rack_holder; set => _rack_holder = value; }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="CP"></param>
        /// <param name="RackLineDirection"></param>
        /// <param name="RackFaceDirection"></param>
        /// <param name="len"></param>
        /// <param name="m"></param>
        /// <param name="Thickness">FaceWidth</param>
        /// <param name="extrusionDir"></param>
        /// <param name="rackThickness">Thickness from back face to teeth base</param>
        /// <param name="pressureAngle"></param>
        public Rack(Point3d CP, Vector3d RackLineDirection, Vector3d RackFaceDirection, double len, double m, double Thickness, Vector3d extrusionDir, double rackThickness, double pressureAngle)
        {
            _rackDirection = RackLineDirection / RackLineDirection.Length;
            _faceDirection = RackFaceDirection;
            _faceDirection.Unitize();
            _length = len;
            _faceWidth = Thickness;
            _centerPoint = CP;
            _startPoint = CP - RackLineDirection / RackLineDirection.Length * (len / 2);
            _endPoint = CP + RackLineDirection / RackLineDirection.Length * (len / 2);
            _backBone = new Line(_startPoint, _endPoint);
            _module = m;
            _pitch = m * Math.PI;
            _tipHeight = _rootHeight + _toothDepth;
            _z = (int)Math.Floor(_length / _pitch);
            _pitch = _length / _z;
            _extrudeDirection = extrusionDir;
            _thickness = rackThickness;
            _pressureAngle = pressureAngle * Math.PI / 180;
            Generate();
        }

        public Rack(Point3d start, Point3d end, Point3d directionPoint, double m, double pressure_angle = 20)
        {
            _startPoint = start;
            _endPoint = end;
            _rackDirection = new Vector3d(end) - new Vector3d(start);
            _rackPlane = new Plane(_startPoint, _endPoint, directionPoint);
            //extrudeDirection = rackPlane.Normal;
            _length = start.DistanceTo(end);
            _module = m;
            _pitch = m * Math.PI;
            _pressureAngle = pressure_angle * Math.PI / 180;
            Generate();
        }

        protected override void GenerateBaseCurve()
        {
            // More informaiton about rack generation: https://khkgears.net/new/gear_knowledge/gear_technical_reference/involute_gear_profile.html

            _pitch = Math.PI * _module;

            double Rf = 0.38 * _module;
            double c = 0.25 * _module;
            double L1 = Math.Sqrt(Math.Pow(Rf, 2) - Math.Pow(Rf - c, 2));
            double toothWidth = _pitch - 2 * L1 - 4 * _module * Math.Tan(_pressureAngle);

            Point3d vallyPoint = new Point3d(0, 0, 0);
            Point3d tipPoint;
            // generate the half valley before the tooth

            Arc firstValley = new Arc(new Point3d(0, 0, 0), new Vector3d(1, 0, 0), new Point3d(L1, c, 0));
            Curve firstValleyCrv = firstValley.ToNurbsCurve();

            // generate the tooth

            Curve toothFlank = new Line(new Point3d(L1, c, 0), new Point3d(L1 + Math.Tan(_pressureAngle) * 2 * _module, c + 2 * _module, 0)).ToNurbsCurve();
            Curve toothTipHalf = new Line(new Point3d(L1 + Math.Tan(_pressureAngle) * 2 * _module, c + 2 * _module, 0), new Point3d(L1 + Math.Tan(_pressureAngle) * 2 * _module + toothWidth / 2, c + 2 * _module, 0)).ToNurbsCurve();
            tipPoint = toothTipHalf.PointAtNormalizedLength(1);
            // generate the valley after the tooth
            Curve toothHalfCrv = Curve.JoinCurves(new List<Curve> { firstValleyCrv, toothFlank, toothTipHalf }, myDoc.ModelAbsoluteTolerance, false)[0];

            Transform mirror = Transform.Mirror(new Point3d(L1 + Math.Tan(_pressureAngle) * 2 * _module + toothWidth / 2, 0, 0), new Vector3d(1, 0, 0));
            Curve toothSecondHalfCrv = toothHalfCrv.DuplicateCurve();
            toothSecondHalfCrv.Transform(mirror);

            Curve toothProfileCrv = Curve.JoinCurves(new List<Curve> { toothHalfCrv, toothSecondHalfCrv }, myDoc.ModelAbsoluteTolerance, false)[0];

            // generate all the teeth on the rack

            _z = (int)Math.Floor(_length / _pitch);
            double leftover = _length - _z * _pitch;
            List<Curve> rackTeeth = new List<Curve>();

            for (int i = 0; i < _z; i++)
            {
                Transform move = Transform.Translation(new Vector3d(_pitch * i, 0, 0));
                Curve tempCrv = toothProfileCrv.DuplicateCurve();
                tempCrv.Transform(move);
                rackTeeth.Add(tempCrv);

                Point3d tip = new Point3d(tipPoint);
                tip.Transform(Transform.Translation(new Vector3d(_pitch * i, 0, 0)));
                TeethTips.Add(tip);
                Point3d vally = new Point3d(vallyPoint);
                vally.Transform(Transform.Translation(new Vector3d(_pitch * i, 0, 0)));
                TeethBtms.Add(vally);

            }
            rackTeeth.Add(new Line(new Point3d(_z * _pitch, 0, 0), new Point3d(_length, 0, 0)).ToNurbsCurve());
            rackTeeth.Add(new Line(new Point3d(_length, 0, 0), new Point3d(_length, -_thickness, 0)).ToNurbsCurve());
            rackTeeth.Add(new Line(new Point3d(_length, -_thickness, 0), new Point3d(0, -_thickness, 0)).ToNurbsCurve());
            rackTeeth.Add(new Line(new Point3d(0, -_thickness, 0), new Point3d(0, 0, 0)).ToNurbsCurve());

            base.BaseCurve = Curve.JoinCurves(rackTeeth, myDoc.ModelAbsoluteTolerance, false)[0];

        }

        protected void ModifyRack()
        {
            BoundingBox bBox = base.Model.GetBoundingBox(true);

            Point3d minPoint = new Point3d(bBox.Max.X, bBox.Min.Y, bBox.Min.Z - _rackBase_extend_from_rack);
            Point3d maxPoint = new Point3d(bBox.Min.X, bBox.Min.Y, bBox.Max.Z + _rackBase_extend_from_rack);
            Plane plane = new Plane(new Point3d((bBox.Max.X - bBox.Min.X) / 2, bBox.Min.Y, (bBox.Max.Z - bBox.Min.Z) / 2), new Vector3d(0, 1, 0));
            Rectangle3d rectangle = new Rectangle3d(plane, minPoint, maxPoint);
            Extrusion rackBase_extrusion = Extrusion.Create(rectangle.ToNurbsCurve(), _rackBase_thickness, true);
            Brep rackBase = rackBase_extrusion.ToBrep();
            Brep[] union = Brep.CreateBooleanUnion(new[] { base.Model, rackBase }, myDoc.ModelAbsoluteTolerance);

            base.Model = union[0];
        }

        protected void GenerateHolder()
        {
            BoundingBox bBox = _boundingBox.GetBoundingBox(true);

            /*
             *      Line 4->
             *          |-----|
             *          |     | <- Line 5
             *          |     |
             * Line 3-> |                       In this function, I first generate the inner wall of the holder curve, then the outter wall. When generating the inner wall, I create the curves in the order of Line 1 - Line 5, same goes to outter wall.
             *          |     | <- Point1
             *          |     | <- Line 1
             * Point1 ->|_____| <- Point2
             *          ^-Line 2
             */
            double x = (bBox.Max.X - bBox.Min.X) / 2 + 5;

            //Inner wall
            List<Curve> rack_holder_curves_inner = new List<Curve>();
            //Line 1
            Point3d point1 = new Point3d(x, bBox.Min.Y + _rackBase_holder_gap + _rackBase_thickness, bBox.Min.Z - _rack_holder_gap);
            Point3d point2 = new Point3d(x, bBox.Min.Y + _rackBase_holder_gap + _rackBase_thickness, bBox.Min.Z - _rack_holder_gap - _rackBase_extend_from_rack);
            rack_holder_curves_inner.Add(new Line(point1, point2).ToNurbsCurve());

            //Line 2
            point1 = new Point3d(x, bBox.Min.Y - _rackBase_holder_gap, bBox.Min.Z - _rackBase_extend_from_rack - _rackBase_holder_gap);
            rack_holder_curves_inner.Add(new Line(point2, point1).ToNurbsCurve());

            //Line 3
            point2 = new Point3d(x, bBox.Min.Y - _rackBase_holder_gap, bBox.Max.Z + _rackBase_extend_from_rack + _rackBase_holder_gap);
            rack_holder_curves_inner.Add(new Line(point1, point2).ToNurbsCurve());

            //Line 4
            point1 = new Point3d(x, bBox.Min.Y + _rackBase_holder_gap + _rackBase_thickness, bBox.Max.Z + _rack_holder_gap + _rackBase_extend_from_rack);
            rack_holder_curves_inner.Add(new Line(point2, point1).ToNurbsCurve());

            //Line 5
            point2 = new Point3d(x, bBox.Min.Y + _rackBase_holder_gap + _rackBase_thickness, bBox.Max.Z + _rack_holder_gap);
            rack_holder_curves_inner.Add(new Line(point1, point2).ToNurbsCurve());

            Curve[] inner_wall_curves = Curve.JoinCurves(rack_holder_curves_inner, myDoc.ModelAbsoluteTolerance, true);
            _rack_holder_curve_inner_wall = inner_wall_curves[0];

            //Outer wall
            List<Curve> rack_holder_curves_outter = new List<Curve>();
            //Line 1
            point1 = new Point3d(x, bBox.Min.Y + _rackBase_holder_gap + _rackBase_thickness + _rack_holder_thickness, bBox.Min.Z - _rack_holder_gap);
            point2 = new Point3d(x, bBox.Min.Y + _rackBase_holder_gap + _rackBase_thickness + _rack_holder_thickness, bBox.Min.Z - _rack_holder_gap - _rackBase_extend_from_rack - _rack_holder_thickness);
            rack_holder_curves_outter.Add(new Line(point1, point2).ToNurbsCurve());

            //Line 2
            point1 = new Point3d(x, bBox.Min.Y - _rackBase_holder_gap - _rack_holder_thickness, bBox.Min.Z - _rackBase_extend_from_rack - _rackBase_holder_gap - _rack_holder_thickness);
            rack_holder_curves_outter.Add(new Line(point2, point1).ToNurbsCurve());

            //Line 3
            point2 = new Point3d(x, bBox.Min.Y - _rackBase_holder_gap - _rack_holder_thickness, bBox.Max.Z + _rackBase_extend_from_rack + _rackBase_holder_gap + _rack_holder_thickness);
            rack_holder_curves_outter.Add(new Line(point1, point2).ToNurbsCurve());

            //Line 4
            point1 = new Point3d(x, bBox.Min.Y + _rackBase_holder_gap + _rackBase_thickness + _rack_holder_thickness, bBox.Max.Z + _rack_holder_gap + _rackBase_extend_from_rack + _rack_holder_thickness);
            rack_holder_curves_outter.Add(new Line(point2, point1).ToNurbsCurve());

            //Line 5
            point2 = new Point3d(x, bBox.Min.Y + _rackBase_holder_gap + _rackBase_thickness + _rack_holder_thickness, bBox.Max.Z + _rack_holder_gap);
            rack_holder_curves_outter.Add(new Line(point1, point2).ToNurbsCurve());

            Curve[] outter_wall_curve = Curve.JoinCurves(rack_holder_curves_outter, myDoc.ModelAbsoluteTolerance, true);
            _rack_holder_curve_outter_wall = outter_wall_curve[0];
            _rack_holder_curve_outter_wall.Reverse();

            List<Curve> rack_holder_curves1 = new List<Curve>();
            rack_holder_curves1.Add(_rack_holder_curve_inner_wall);
            rack_holder_curves1.Add(new Line(_rack_holder_curve_inner_wall.PointAtEnd, _rack_holder_curve_outter_wall.PointAtStart).ToNurbsCurve());
            rack_holder_curves1.Add(_rack_holder_curve_outter_wall);
            rack_holder_curves1.Add(new Line(_rack_holder_curve_outter_wall.PointAtEnd, _rack_holder_curve_inner_wall.PointAtStart).ToNurbsCurve());
            Curve[] rack_holder_curves2 = Curve.JoinCurves(rack_holder_curves1, myDoc.ModelAbsoluteTolerance, true);
            _rack_holder_curve = rack_holder_curves2[0];

            Extrusion rack_holder = Extrusion.Create(_rack_holder_curve, _rack_holder_length, true);
            _rack_holder = rack_holder.ToBrep();


            //Rack holder middle curves for support pillar 1
            List<Curve> rack_holder_middle_curves = new List<Curve>();
            //Line 1
            point2 = new Point3d(x, bBox.Min.Y + _rackBase_holder_gap + _rackBase_thickness + _rack_holder_thickness / 2, bBox.Min.Z - _rack_holder_gap - _rackBase_extend_from_rack - _rack_holder_thickness / 2);
            point1 = new Point3d(x, bBox.Min.Y - _rackBase_holder_gap - _rack_holder_thickness / 2, bBox.Min.Z - _rackBase_extend_from_rack - _rackBase_holder_gap - _rack_holder_thickness / 2);
            rack_holder_middle_curves.Add(new Line(point2, point1).ToNurbsCurve());

            //Line 1
            point2 = new Point3d(x, bBox.Min.Y - _rackBase_holder_gap - _rack_holder_thickness / 2, bBox.Max.Z + _rackBase_extend_from_rack + _rackBase_holder_gap + _rack_holder_thickness / 2);
            rack_holder_middle_curves.Add(new Line(point1, point2).ToNurbsCurve());

            //Line 3
            point1 = new Point3d(x, bBox.Min.Y + _rackBase_holder_gap + _rackBase_thickness + _rack_holder_thickness / 2, bBox.Max.Z + _rack_holder_gap + _rackBase_extend_from_rack + _rack_holder_thickness / 2);
            rack_holder_middle_curves.Add(new Line(point2, point1).ToNurbsCurve());

            Curve[] rack_holder_middle_curves1 = Curve.JoinCurves(rack_holder_middle_curves, myDoc.ModelAbsoluteTolerance, true);

            _rack_holder_middle_curve_1 = rack_holder_middle_curves1[0];


            //Rack holder middle curves for support pillar 2
            rack_holder_middle_curves = new List<Curve>();
            //Line 1
            point2 = new Point3d(x + _rack_holder_length, bBox.Min.Y + _rackBase_holder_gap + _rackBase_thickness + _rack_holder_thickness / 2, bBox.Min.Z - _rack_holder_gap - _rackBase_extend_from_rack - _rack_holder_thickness / 2);
            point1 = new Point3d(x + _rack_holder_length, bBox.Min.Y - _rackBase_holder_gap - _rack_holder_thickness / 2, bBox.Min.Z - _rackBase_extend_from_rack - _rackBase_holder_gap - _rack_holder_thickness / 2);
            rack_holder_middle_curves.Add(new Line(point2, point1).ToNurbsCurve());

            //Line 1
            point2 = new Point3d(x + _rack_holder_length, bBox.Min.Y - _rackBase_holder_gap - _rack_holder_thickness / 2, bBox.Max.Z + _rackBase_extend_from_rack + _rackBase_holder_gap + _rack_holder_thickness / 2);
            rack_holder_middle_curves.Add(new Line(point1, point2).ToNurbsCurve());

            //Line 3
            point1 = new Point3d(x + _rack_holder_length, bBox.Min.Y + _rackBase_holder_gap + _rackBase_thickness + _rack_holder_thickness / 2, bBox.Max.Z + _rack_holder_gap + _rackBase_extend_from_rack + _rack_holder_thickness / 2);
            rack_holder_middle_curves.Add(new Line(point2, point1).ToNurbsCurve());

            Curve[] rack_holder_middle_curves2 = Curve.JoinCurves(rack_holder_middle_curves, myDoc.ModelAbsoluteTolerance, true);

            _rack_holder_middle_curve_2 = rack_holder_middle_curves2[0];
        }

        protected void GenerateRack()
        {
            GenerateBaseCurve();

            RhinoApp.WriteLine("ready to transform rack");
            _faceWidth = 3.6;

            var sweep = new SweepOneRail();
            sweep.AngleToleranceRadians = myDoc.ModelAngleToleranceRadians;
            sweep.ClosedSweep = false;
            sweep.SweepTolerance = myDoc.ModelAbsoluteTolerance;

            Curve rackPathCrv = new Line(new Point3d(0, 0, 0), new Point3d(0, 0, _faceWidth)).ToNurbsCurve();
            Brep[] rackBreps = sweep.PerformSweep(rackPathCrv, base.BaseCurve);
            Brep rackBrep = rackBreps[0];
            Brep rackSolid = rackBrep.CapPlanarHoles(myDoc.ModelAbsoluteTolerance);

            rackSolid.Faces.SplitKinkyFaces(RhinoMath.DefaultAngleTolerance, true);
            if (BrepSolidOrientation.Inward == rackSolid.SolidOrientation)
                rackSolid.Flip();

            base.Model = rackSolid;
            _boundingBox = base.Model.GetBoundingBox(true).ToBrep();
            _boundingBoxCenter = base.Model.GetBoundingBox(true).Center;


            ModifyRack();
            GenerateHolder();

            Point3d baseMidPt = new Point3d(_length / 2, 0, 0);
            Point3d midPoint = (_startPoint + _endPoint) / 2;
            Transform trans0 = Transform.Translation(new Vector3d(midPoint - baseMidPt));
            Transform rotat0 = Transform.Rotation(new Vector3d(1, 0, 0), _rackDirection, midPoint);
            Vector3d rotated = new Vector3d(0, 1, 0);
            rotated.Transform(rotat0);
            Transform selfRotate = Transform.Rotation(rotated, _faceDirection, midPoint);
            base.BaseCurve.Transform(trans0);
            base.BaseCurve.Transform(rotat0);
            base.BaseCurve.Transform(selfRotate);

            base.Model.Transform(trans0);
            base.Model.Transform(rotat0);
            base.Model.Transform(selfRotate);

            _extrudeDirection.Transform(trans0);
            _extrudeDirection.Transform(rotat0);
            _extrudeDirection.Transform(selfRotate);

            _boundingBox.Transform(trans0);
            _boundingBox.Transform(rotat0);
            _boundingBox.Transform(selfRotate);

            _boundingBoxCenter.Transform(trans0);
            _boundingBoxCenter.Transform(rotat0);
            _boundingBoxCenter.Transform(selfRotate);

            _rack_holder.Transform(trans0);
            _rack_holder.Transform(rotat0);
            _rack_holder.Transform(selfRotate);

            _rack_holder_curve.Transform(trans0);
            _rack_holder_curve.Transform(rotat0);
            _rack_holder_curve.Transform(selfRotate);

            _rack_holder_curve_inner_wall.Transform(trans0);
            _rack_holder_curve_inner_wall.Transform(rotat0);
            _rack_holder_curve_inner_wall.Transform(selfRotate);

            _rack_holder_curve_outter_wall.Transform(trans0);
            _rack_holder_curve_outter_wall.Transform(rotat0);
            _rack_holder_curve_outter_wall.Transform(selfRotate);

            _rack_holder_middle_curve_1.Transform(trans0);
            _rack_holder_middle_curve_1.Transform(rotat0);
            _rack_holder_middle_curve_1.Transform(selfRotate);

            _rack_holder_middle_curve_2.Transform(trans0);
            _rack_holder_middle_curve_2.Transform(rotat0);
            _rack_holder_middle_curve_2.Transform(selfRotate);

            for (int i = 0; i < TeethTips.Count(); i++)
            {
                Point3d p = TeethTips[i];
                p.Transform(trans0);
                p.Transform(rotat0);
                p.Transform(selfRotate);
                TeethTips[i] = p;
            }
            for (int i = 0; i < TeethBtms.Count(); i++)
            {
                Point3d p = TeethBtms[i];
                p.Transform(trans0);
                p.Transform(rotat0);
                p.Transform(selfRotate);
                TeethBtms[i] = p;
            }

            //Rotate the rack so that it points upward in the best effort
            rotat0 = Transform.Rotation(RhinoMath.ToRadians(1), _rackDirection, _backBone.PointAtLength(_backBone.Length / 2));

            Curve line = new Line(_boundingBoxCenter, _faceDirection, 10).ToNurbsCurve();
            double maxZ = line.PointAtEnd.Z;
            int count = 0;
            while (maxZ <= line.PointAtEnd.Z && count < 361)
            {
                maxZ = line.PointAtEnd.Z;
                base.Model.Transform(rotat0);
                base.BaseCurve.Transform(rotat0);
                _extrudeDirection.Transform(rotat0);
                _faceDirection.Transform(rotat0);
                _boundingBoxCenter.Transform(rotat0);
                _boundingBox.Transform(rotat0);

                _rack_holder.Transform(rotat0);

                _rack_holder_curve.Transform(rotat0);

                _rack_holder_curve_inner_wall.Transform(rotat0);

                _rack_holder_curve_outter_wall.Transform(rotat0);

                _rack_holder_middle_curve_1.Transform(rotat0);
                _rack_holder_middle_curve_2.Transform(rotat0);

                line = new Line(_boundingBoxCenter, _faceDirection, 10).ToNurbsCurve();
                count++;
            }

            _realBackBone = _backBone;
            trans0 = Transform.Translation((_boundingBoxCenter - _backBone.PointAtLength(_backBone.Length / 2)));
            _realBackBone.Transform(trans0);

        }


        public override void Generate()
        {
            GenerateRack();
        }
        public override bool IsEngaged(Component obj)
        {

            if (obj.GetType() == typeof(SpurGear))
            {
                SpurGear g = (SpurGear)obj;
                //If two gears are engaged, they have to be in same direction, their interval of z direction have to overlap
                if (Math.Abs(g.Direction * _extrudeDirection) < 0.99)
                { return false; }
                if (Math.Abs(g.Direction * new Vector3d(g.CenterPoint)) - Math.Abs(_extrudeDirection * new Vector3d(CenterPoint)) > (_faceWidth + g.FaceWidth) / 2)
                { return false; }
                //and the distance of centers have to be between R1+R2 and r1+r2+h(smaller).
                double distance1 = _backBone.DistanceTo(g.CenterPoint, true);
                double distance2 = new Plane(CenterPoint, _faceDirection, _rackDirection).DistanceTo(g.CenterPoint);
                double distance3 = Math.Sqrt(distance1 * distance1 - distance2 * distance2);
                if (distance3 > _tipHeight + g.TipRadius || distance3 < _rootHeight + g.RootRadius + (_toothDepth + g.ToothDepth) / 2)
                { return false; }
                //Their teeth relationship
                if (Math.Abs(_module - g.Module) > 0.01)
                { return false; }
                //Actually the engagement of gears could be very complicated. Herer I only cover the basic parts due to my very limited knowledge in this field.

            }
            else { return false; }
            return true;
        }
        /// <summary>
        /// Use this function to move rack model to fit a gear. 
        /// Input a contact point of gear and this function would automatically move rack model to fit it. 
        /// Note that the movement is only along rack direction. Return true for success, false for fail. 
        /// Note that moving will fail if moving distance is greater than one teeth width.
        /// </summary>
        /// <param name="contactPoint">Position of closest point</param>
        /// <param name="isTip">True for a tip, false for a vally</param>
        public bool MoveAndEngage(SpurGear gear, Vector3d dir)
        {
            //First tell the closest teeth to the dir
            double maxProduct = 0;
            int maxIndex = -1;
            foreach (Vector3d v in gear.TeethDirections)
            {
                if (dir * v > maxProduct)
                {
                    maxIndex = gear.TeethDirections.IndexOf(v);
                    maxProduct = dir * v;
                }
            }
            //Then tell the offset value
            double gearOffsetRad = Math.Acos(maxProduct);
            double gearOffsetDistance = gearOffsetRad * (gear.RootRadius + gear.TipRadius) / 2;
            //tell offset distance is positive or negative
            Point3d contactPoint = gear.CenterPoint + dir * (gear.RootRadius + gear.TipRadius) / 2;
            Point3d tipPoint = gear.TeethTips[maxIndex];
            if ((tipPoint - contactPoint) * _rackDirection < 0)
                gearOffsetDistance = -gearOffsetDistance;

            double targetPosition = new Vector3d(contactPoint) * _rackDirection;
            double minProduct = _pitch;
            Vector3d offset = Vector3d.Unset;
            foreach (Point3d p in TeethBtms)
            {
                double pos = new Vector3d(p) * _rackDirection;
                double difference = targetPosition - pos;
                if (Math.Abs(difference + gearOffsetDistance) < minProduct)
                {
                    minProduct = Math.Abs(difference + gearOffsetDistance);
                    offset = _rackDirection * (difference + gearOffsetDistance);
                }
            }

            if (offset != Vector3d.Unset)
            {
                model.Transform(Transform.Translation(offset));
                return true;
            }
            return false;
        }
    }
}
