using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Rhino.Collections;
using Rhino.DocObjects;
using Rhino;
using Rhino.Geometry;
using System.Drawing;
using DynaModel_v2.Geometry;
using Rhino.Geometry.Intersect;
using Rhino.Input;
using System.Linq;

namespace DynaModel_v2.Rotational_Motion
{
    public class CreateCutter : GH_Component
    {

        Point3d customized_part_location = new Point3d(0, 0, 0);
        private Brep currModel = new Brep();
        private List<Point3d> surfacePts = new List<Point3d>();
        private Point3dList selectedPts = new Point3dList();
        private Guid currModelObjId = Guid.Empty;
        private RhinoDoc myDoc = RhinoDoc.ActiveDoc;
        ObjectAttributes solidAttribute, lightGuideAttribute, redAttribute, yellowAttribute, soluableAttribute;
        private bool end_button_clicked;
        Brep cutter = new Brep();

        /// <summary>
        /// Initializes a new instance of the CreateCutter class.
        /// </summary>
        public CreateCutter()
          : base("CreateCutter", "Cutter",
            "This component creates a planar cutter",
            "DynaModel_v2", "Rotational Motion")
        {
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
            pManager.AddGenericParameter("Start Button", "SB", "Button to create cutter", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Essentials", "E", "The essential that contains: main model, cutter, cutter plane, cutter thickness, cutted models", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            bool isPressed = false;
            if (!DA.GetData(0, ref isPressed))
                return;

            if (isPressed)
            {
                TrueOnlyButtonValueController.finished = 0;
                Brep brep = SelectGearExit(out Plane cutterPlane);

                if (brep != new Brep())
                {
                    Guid guid = myDoc.Objects.Add(cutter);
                    Essentials essentials = new Essentials();
                    essentials.Cutter = guid;
                    essentials.CutterPlane = cutterPlane;
                    essentials.CutterThickness = 3;
                    essentials.MainModel = currModelObjId;

                    DA.SetData(0, essentials);
                }
                else
                {
                    DA.SetData(0, null);
                }


            }
        }

        private Brep SelectGearExit(out Plane cutterPlane)
        {
            ObjRef objSel_ref1;
            end_button_clicked = false;


            var rc = RhinoGet.GetOneObject("Select a model (geometry): ", false, ObjectType.AnyObject, out objSel_ref1);
            if (rc == Rhino.Commands.Result.Success)
            {
                currModelObjId = objSel_ref1.ObjectId;
                ObjRef currObj = new ObjRef(myDoc, currModelObjId);
                currModel = currObj.Brep(); //The model body

                ObjectType objectType = currObj.Geometry().ObjectType;

                if (currObj.Geometry().ObjectType == ObjectType.Mesh)
                {
                    //Mesh
                    Mesh currModel_Mesh = currObj.Mesh();

                    //TODO: Convert Mesh into Brep; or just throw an error to user saying that only breps are allowed 
                    currModel = Brep.CreateFromMesh(currModel_Mesh, false);
                    if (currModel.IsValid && currModel.IsSolid && !currModel.IsManifold)
                    {
                        currModelObjId = myDoc.Objects.AddBrep(currModel);
                        myDoc.Objects.Delete(currObj.ObjectId, true);

                        myDoc.Views.Redraw();
                    }
                    else
                    {
                        RhinoApp.WriteLine("Your model cannot be fixed to become manifold and closed, please try to fix it manually");
                    }


                }

                #region Create interactive dots around the model body for users to select
                myDoc.Objects.Hide(currModelObjId, true);
                currModelObjId = myDoc.Objects.AddBrep(currModel);


                if (currModel == null)
                {
                    cutterPlane = new Plane();
                    RhinoApp.WriteLine("Fail to select a 3D model, please try again");
                    return cutter;
                }

                // Find the candidate positions to place dots
                BoundingBox boundingBox = currModel.GetBoundingBox(true);
                double w = boundingBox.Max.X - boundingBox.Min.X;
                double l = boundingBox.Max.Y - boundingBox.Min.Y;
                double h = boundingBox.Max.Z - boundingBox.Min.Z;
                double offset = 5;

                for (double i = 0; i < h + 10; i += 1)
                {
                    Point3d Origin = new Point3d(w / 2, l / 2, i);
                    Point3d xPoint = new Point3d(boundingBox.Max.X + offset, l / 2, i);
                    Point3d yPoint = new Point3d(w / 2, boundingBox.Max.Y + offset, i);

                    Plane plane = new Plane(Origin, xPoint, yPoint);
                    PlaneSurface planeSurface = PlaneSurface.CreateThroughBox(plane, boundingBox);
                    //myDoc.Objects.Add(planeSurface);

                    Intersection.BrepSurface(currModel, planeSurface, myDoc.ModelAbsoluteTolerance, out Curve[] intersectionCurves, out Point3d[] intersectionPoints);

                    //Create Points on the Curve
                    if (intersectionCurves != null)
                    {
                        if (intersectionCurves.Length != 0)
                        {
                            foreach (Curve curve in intersectionCurves)
                            {
                                double[] curveParams = curve.DivideByLength(2, true, out Point3d[] points);
                                if (points != null)
                                    surfacePts.AddRange(points);
                            }
                        }
                    }
                }

                // Put dots on the copy
                List<Guid> pts_normals = new List<Guid>();
                foreach (Point3d point in surfacePts)
                {
                    Guid pointID = myDoc.Objects.AddPoint(point);
                    pts_normals.Add(pointID);
                }
                myDoc.Views.Redraw();

                #endregion

                #region ask the user to select points for a cutter plane



                int count = 0;
                List<Curve> cutterCurves = new List<Curve>();
                RhinoApp.WriteLine("We need to cut your model for your end effector. Please select 3 points that best describe your cutter plane");


                while (count < 3)
                {
                    ObjRef pointRef;

                    var getSelectedPts = RhinoGet.GetOneObject("Please select your Point " + (count + 1) + ": ", false, ObjectType.Point, out pointRef);


                    if (pointRef != null)
                    {
                        Point3d tempPt = new Point3d(pointRef.Point().Location.X, pointRef.Point().Location.Y, pointRef.Point().Location.Z);

                        //Check if the selected point is in selectedPts
                        //1. If so, Get rid of the bounding box
                        //2. If not, store the selected point and display bounding box

                        if (selectedPts.Any(pt => pt.Equals(tempPt)))
                        {
                            RhinoApp.WriteLine("Please choose point that are NOT red");
                            continue;
                        }
                        selectedPts.Add(tempPt);

                        Guid tempPt_ID = pointRef.ObjectId;
                        myDoc.Objects.ModifyAttributes(pointRef, redAttribute, true);

                        myDoc.Views.Redraw();

                        count++;
                    }

                }

                Circle cutterCurveCircle = new Circle(selectedPts[0], selectedPts[1], selectedPts[2]);
                Curve cutterCurve = cutterCurveCircle.ToNurbsCurve();
                cutterCurve.TryGetPlane(out cutterPlane);




                Line rail = new Line(selectedPts[0], cutterPlane.Normal, 3);
                cutter = Brep.CreateFromSweep(rail.ToNurbsCurve(), cutterCurve, true, myDoc.ModelAbsoluteTolerance)[0];
                cutter = cutter.CapPlanarHoles(myDoc.ModelAbsoluteTolerance);

                Point3d center = cutter.GetBoundingBox(true).Center;
                Transform centerTrans = Transform.Scale(center, 2);


                cutter.Transform(centerTrans);

                #endregion

                //Kill all dots and duplicate brep, Show the original brep
                foreach (var ptsID in pts_normals)
                {
                    myDoc.Objects.Delete(ptsID, true);
                }
                selectedPts.Clear();
                surfacePts.Clear();
                pts_normals.Clear();
                cutterCurves.Clear();

                return cutter;

            }
            cutterPlane = new Plane();
            return cutter;
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
            get { return new Guid("284793F2-7278-4B3B-9D72-9CECD6637849"); }
        }
    }
}