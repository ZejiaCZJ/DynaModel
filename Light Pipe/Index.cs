using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DynaModel_v2.Light_Pipe
{
    public class Index
    {
        public int i { get; set; }
        public int j { get; set; }
        public int k { get; set; }

        public Index()
        {
            i = 0;
            j = 0;
            k = 0;
        }

        public Index(int i, int j, int k)
        {
            this.i = i;
            this.j = j;
            this.k = k;
        }

        public bool Equal(Index index)
        {
            return index.i == i && index.j == j && index.k == k;
        }
    }
}
