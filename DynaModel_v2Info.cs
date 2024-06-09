using System;
using System.Drawing;
using Grasshopper;
using Grasshopper.Kernel;

namespace DynaModel_v2
{
    public class DynaModel_v2Info : GH_AssemblyInfo
    {
        public override string Name => "DynaModel_v2";

        //Return a 24x24 pixel bitmap to represent this GHA library.
        public override Bitmap Icon => null;

        //Return a short string describing the purpose of this GHA library.
        public override string Description => "";

        public override Guid Id => new Guid("27c1d6f6-7e3b-4212-8661-9fb434116a1b");

        //Return a string identifying you or your company.
        public override string AuthorName => "";

        //Return a string representing your preferred contact details.
        public override string AuthorContact => "";
    }
}