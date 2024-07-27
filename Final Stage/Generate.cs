using System;
using System.Collections.Generic;
using System.Linq;
using DynaModel_v2.SharedData;
using Grasshopper.Kernel;
using Rhino;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;

namespace DynaModel_v2.Final_Stage
{
    public class Generate : GH_Component
    {
        private RhinoDoc myDoc;

        /// <summary>
        /// Initializes a new instance of the Generate class.
        /// </summary>
        public Generate()
          : base("Generate", "Nickname",
              "This component generate all the user-defined items on the model in a 3D printer friendly fashion",
              "DynaModel_v2", "Final Stage")
        {
            myDoc = RhinoDoc.ActiveDoc;
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddBooleanParameter("Generate Button", "G", "The generate button", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddBooleanParameter("Save Button", "S", "Show the save button", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            bool generate = false;

            if(!DA.GetData(0, ref generate))
                return;

            if(generate)
            {
                GenerateHelper generateHelper = new GenerateHelper(out bool success);
                for (int i = 0; i < SavedItems.items.Count; i++)
                {
                    if (SavedItems.items[i].Name == "Rotational Motion")
                    {
                        Item item = SavedItems.items[i];
                        generateHelper.GenerateRotationalMotion(ref item, out List<Brep> subtrahends);
                    }
                    if (SavedItems.items[i].Name == "LED Light")
                    {
                        Item item = SavedItems.items[i];
                        generateHelper.GenerateLightPipe(ref item, out List<Brep> subtrahends);
                    }
                    if (SavedItems.items[i].Name == "Air Pipe")
                    {
                        Item item = SavedItems.items[i];
                        generateHelper.GenerateAirPipe(ref item, out List<Brep> subtrahends);
                    }
                    if (SavedItems.items[i].Name == "Button")
                    {
                        Item item = SavedItems.items[i];
                        generateHelper.GenerateButtonPipe(ref item, out List<Brep> subtrahends);
                    }
                }
                //RhinoDoc.ActiveDoc.Objects.Add(generateHelper.foundation);

                // Create support pillars for gaskets
                List<Guid> allPillars = new List<Guid>();
                List<Brep> allBreps = generateHelper.getAllBreps();
                List<Guid> allBreps_guid = generateHelper.getAllBrepsGuid();
                for (int i = 0; i < generateHelper.gaskets_guid.Count; i++)
                {
                    Curve circle = generateHelper.gasketCircles[i];
                    Double[] points1 = circle.ToNurbsCurve().DivideByLength(4, false, out Point3d[] temp_points);

                    foreach(var pt in points1)
                    {
                        Point3d point = circle.PointAt(pt);
                        Plane plane = new Plane();
                        if (circle.TryGetCircle(out Circle c))
                            plane = c.Plane;
                        else
                            circle.TryGetPlane(out plane);

                        Vector3d start_direction = new Vector3d(circle.TangentAt(pt));
                        Vector3d direction = new Vector3d(start_direction);

                        List<Line> lines = new List<Line>();
                        Line line = new Line();
                        
                        //Try different angles if the line does not intersect with the brep
                        int retry_count = 0;
                        while(retry_count < 8)
                        {
                            //Change direction and do again
                            line = new Line(point, direction, 500);
                            Intersection.CurveBrep(line.ToNurbsCurve(), generateHelper.currModel_Hollowed, myDoc.ModelAbsoluteTolerance, out Curve[] overlapCurves, out Point3d[] intersectionPts);
                            if(intersectionPts.Length >= 2)
                            {
                                line = new Line(point, intersectionPts[0]);
                                line.Extend(0, 2);
                                lines.Add(line);
                            }
                            retry_count++;
                            direction.Rotate(RhinoMath.QuarterPI, plane.Normal);
                        }

                       
                        foreach(var candidate_line in lines)
                        {
                            bool validBrep = true;
                            Curve crv = candidate_line.ToNurbsCurve();
                            Brep supportPillar = Brep.CreatePipe(crv, 1.2, true, PipeCapMode.Flat, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];
                            for (int j = 0; j < allBreps_guid.Count; j++)
                            {
                                if (generateHelper.gaskets_guid.All(g => g != allBreps_guid[j]) && allBreps_guid[j] != generateHelper.currModel_Hollowed_ObjId && allPillars.All(g => g != allBreps_guid[j]))
                                {
                                    Intersection.BrepBrep(supportPillar, allBreps[j], myDoc.ModelAbsoluteTolerance, out Curve[] overlapCurves, out Point3d[] intersectionPts);
                                    if (overlapCurves.Length != 0 || intersectionPts.Length != 0)
                                    {
                                        validBrep = false; break;
                                    }
                                    else
                                        validBrep = true;
                                }
                            }
                            if(validBrep)
                            {
                                Brep[] breps = Brep.CreateBooleanDifference(supportPillar, generateHelper.currModel_Hollowed, myDoc.ModelAbsoluteTolerance, false);
                                if(breps != null)
                                {
                                    double max_volume = breps[0].GetVolume();
                                    supportPillar = breps[0];
                                    foreach (var candidate_pillar in breps)
                                    {
                                        if (max_volume < candidate_pillar.GetVolume())
                                        {
                                            max_volume = candidate_pillar.GetVolume();
                                            supportPillar = candidate_pillar;
                                        }
                                    }
                                }
                                
                                allPillars.Add(myDoc.Objects.Add(supportPillar));
                                break;
                            }
                        }

                    }

                }

                foreach (var item in generateHelper.toDelete)
                {
                    RhinoDoc.ActiveDoc.Objects.Add(item);
                }
                myDoc.Objects.Delete(generateHelper.currModel_Hollowed_ObjId, true);
                generateHelper.currModel_Hollowed_ObjId = myDoc.Objects.Add(generateHelper.currModel_Hollowed);
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
            get { return new Guid("643BDDFC-AF86-49E9-AF34-717D8539FC35"); }
        }
    }
}