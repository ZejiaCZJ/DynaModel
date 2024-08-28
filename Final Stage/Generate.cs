using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using DynaModel_v2.SharedData;
using Grasshopper.Kernel;
using Rhino;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using static Rhino.DocObjects.PhysicallyBasedMaterial;

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

                //Sort the saved items
                foreach(var item in SavedItems.items)
                {
                    string pattern = @"^\d+\. ";

                    // Replace the matched pattern with an empty string
                    string result = Regex.Replace(item.Name, pattern, string.Empty);

                    item.Name = result;
                }
                Sort(ref SavedItems.items);

                for (int i = 0; i < SavedItems.items.Count; i++)
                {
                    if (SavedItems.items[i].Name == "Rotational Motion")
                    {
                        Item item = SavedItems.items[i];
                        generateHelper.GenerateRotationalMotion(ref item, out List<Brep> subtrahends);
                    }
                    if (SavedItems.items[i].Name == "Translational Motion")
                    {
                        Item item = SavedItems.items[i];
                        generateHelper.GenerateTranslationalMotion(ref item, out List<Brep> subtrahends);
                    }
                    if (SavedItems.items[i].Name == "Car")
                    {
                        Item item = SavedItems.items[i];
                        generateHelper.GenerateCarParameter(ref item, out List<Brep> subtrahends);
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
                    if (SavedItems.items[i].Name == "Touch")
                    {
                        Item item = SavedItems.items[i];
                        generateHelper.GenerateTouchPipe(ref item, out List<Brep> subtrahends);
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
                                if (generateHelper.gaskets_guid.All(g => g != allBreps_guid[j]) && allBreps_guid[j] != generateHelper.currModel_Hollowed_ObjId && allPillars.All(g => g != allBreps_guid[j]) && generateHelper.conductive_pipes_guid.All(g => g != allBreps_guid[j]) && generateHelper.led_pipes_guid.All(g => g != allBreps_guid[j]))
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

                                //boolean difference the support pillar with intersected pipes

                                if(supportPillar != null)
                                {
                                    bool found = false;
                                    breps = Brep.CreateBooleanDifference(new[] { supportPillar }, generateHelper.led_pipes, myDoc.ModelAbsoluteTolerance);
                                    List<Brep> pillars = new List<Brep>();
                                    if(breps != null && breps.Length == 2)
                                    {
                                        Brep first = breps[0];
                                        Brep second = breps[1];
                                        if (first.GetVolume() > second.GetVolume())
                                            supportPillar = first;
                                        else
                                            supportPillar = second;
                                        pillars.Add(supportPillar);
                                        
                                    }
                                    //Some pipe cuts off the support pillar into half
                                    else if (breps != null && breps.Length > 2)
                                    {
                                        List<Brep> temp_brep = breps.ToList();
                                        temp_brep.Sort((a, b) => a.GetVolume().CompareTo(b.GetVolume()));
                                        Brep first = breps[breps.Length - 1];
                                        Brep second = breps[breps.Length - 2];
                                        pillars.Add(first);
                                        pillars.Add(second);
                                    }
                                    //else if (breps != null && breps.Length == 1)
                                    //{
                                    //    supportPillar = breps[0];
                                    //    pillars.Add(supportPillar);
                                    //}
                                    else if (breps == null || breps.Length == 0)
                                    {
                                        pillars.Add(supportPillar);
                                    }
                                    
                                    foreach(var pillar in pillars)
                                    {
                                        breps = Brep.CreateBooleanDifference(new[] { pillar }, generateHelper.conductive_pipes, myDoc.ModelAbsoluteTolerance);
                                        if (breps != null && breps.Length == 1)
                                        {
                                            Brep first = breps[0];
                                            allPillars.Add(myDoc.Objects.Add(first));
                                            found = true;
                                        }
                                        else if (breps != null && breps.Length >= 2)
                                        {
                                            List<Brep> temp_brep = breps.ToList();
                                            temp_brep.Sort((a, b) => a.GetVolume().CompareTo(b.GetVolume()));
                                            Brep first = breps[breps.Length - 1];
                                            Brep second = breps[breps.Length - 2];
                                            allPillars.Add(myDoc.Objects.Add(first));
                                            allPillars.Add(myDoc.Objects.Add(second));
                                            found = true;
                                        }
                                        else if (breps == null || breps.Length == 0)
                                        {
                                            allPillars.Add(myDoc.Objects.Add(pillar));
                                            found = true;
                                        }
                                    }


                                    if (found)
                                        break;


                                }
                                
                            }
                            
                            
                        }

                    }

                }

                List<Curve> rack_holder_curves = generateHelper.rack_holder_curves;
                List<Guid> rack_holder_guid = generateHelper.rack_holder_guids;

                for (int i = 0; i < rack_holder_curves.Count; i++)
                {
                    Curve curve = rack_holder_curves[i];

                    Double[] points1 = curve.DivideByCount(3, false, out Point3d[] temp_points);

                    int count = 0;

                    foreach (var pt in points1)
                    {
                        Point3d point = curve.PointAt(pt);

                        curve.TryGetPlane(out Plane curve_plane);

                        //Vector3d direction = Vector3d.CrossProduct(curve.TangentAt(pt), Vector3d.Negate(rack.RackDirection));

                        Vector3d direction = curve.TangentAt(pt);
                        if (count == 0)
                        {
                            direction = Vector3d.Negate(curve.TangentAt(pt));
                        }


                        Curve support_pillar_rail = new Line(point, direction, 1).ToNurbsCurve();

                        support_pillar_rail = support_pillar_rail.ExtendByLine(CurveEnd.End, new[] { generateHelper.currModel_Hollowed });

                        Brep supportPillar = Brep.CreatePipe(support_pillar_rail.ToNurbsCurve(), 1, true, PipeCapMode.Round, true, myDoc.ModelAbsoluteTolerance, myDoc.ModelAngleToleranceRadians)[0];


                        Brep[] breps = Brep.CreateBooleanDifference(supportPillar, generateHelper.currModel_Hollowed, myDoc.ModelAbsoluteTolerance, false);
                        if (breps != null)
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



                        count++;
                        myDoc.Objects.Add(supportPillar);
                    }
                }


                foreach (var item in generateHelper.toDelete)
                {
                    RhinoDoc.ActiveDoc.Objects.Add(item);
                }
                myDoc.Objects.Delete(generateHelper.currModel_Hollowed_ObjId, true);
                generateHelper.currModel_Hollowed_ObjId = myDoc.Objects.Add(generateHelper.currModel_Hollowed);

                List<(Brep, Brep)> lightSourcePipePairs = generateHelper.lightSourcePipePairs;
                List<Brep> mainPipePairs = generateHelper.mainPipePairs;


                for (int i = 0; i < lightSourcePipePairs.Count; i++)
                {
                    Brep soluableExtension = lightSourcePipePairs[i].Item1 as Brep;
                    myDoc.Objects.Add(soluableExtension, generateHelper.redAttribute);
                    for (int j = 0; j < lightSourcePipePairs.Count; j++)
                    {

                        if (i != j)
                        {
                            Brep cutter = lightSourcePipePairs[j].Item2 as Brep;
                            myDoc.Objects.Add(cutter, generateHelper.soluableAttribute);
                            //Brep[] differences = Brep.CreateBooleanDifference(new[] { soluableExtension }, new[] { cutter }, myDoc.ModelAbsoluteTolerance);
                            //if (differences != null || differences.Length > 0)
                            //    generateHelper.GetSimilarVolumeBrep(differences, soluableExtension, out soluableExtension);
                        }
                    }
                    //Guid a = myDoc.Objects.Add(soluableExtension, redAttribute);
                    //specialPipes.Add(a);
                    //ignorePipesGuid.Add(a);
                    //led_pipes.Add(soluableExtension);
                    //led_pipes_guid.Add(a);
                }

                for (int i = 0; i < mainPipePairs.Count; i++)
                {
                    Guid a = myDoc.Objects.Add(mainPipePairs[i], generateHelper.soluableAttribute);
                }

                myDoc.Objects.Add(generateHelper.foundation);
            }
        }

        private void Sort(ref List<Item> items)
        {
            List<Item> new_SavedItem  = new List<Item>();
            foreach (var item in items)
            {
                if(item.Name == "Rotational Motion")
                    new_SavedItem.Add(item);
            }
            foreach (var item in items)
            {
                if (item.Name == "Translational Motion")
                    new_SavedItem.Add(item);
            }
            foreach (var item in items)
            {
                if (item.Name == "Car")
                    new_SavedItem.Add(item);
            }
            foreach (var item in items)
            {
                if (item.Name == "LED Light")
                    new_SavedItem.Add(item);
            }
            foreach (var item in items)
            {
                if (item.Name == "Air Pipe")
                    new_SavedItem.Add(item);
            }
            foreach (var item in items)
            {
                if (item.Name == "Button")
                    new_SavedItem.Add(item);
            }
            foreach (var item in items)
            {
                if (item.Name == "Touch")
                    new_SavedItem.Add(item);
            }
            items = new_SavedItem;
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