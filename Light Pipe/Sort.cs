using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Rhino.Geometry;

namespace DynaModel_v2.Light_Pipe
{
    public class Sort
    {
        /// <summary>
        /// This method sorts a Point3d array based its Z value
        /// </summary>
        /// <param name="arr">A Point3d array</param>
        /// <param name="left">The start index of the sorting process</param>
        /// <param name="right">The end index of the sorting process</param>
        public static void Quicksort(Point3d[] arr, int left, int right)
        {
            if (left < right)
            {
                int pivotIndex = Partition(arr, left, right);

                Quicksort(arr, left, pivotIndex - 1);
                Quicksort(arr, pivotIndex + 1, right);
            }
        }

        private static int Partition(Point3d[] arr, int left, int right)
        {
            Point3d pivot = arr[right];
            int i = left - 1;

            for (int j = left; j < right; j++)
            {
                if (arr[j].Z < pivot.Z)
                {
                    i++;
                    Swap(arr, i, j);
                }
            }

            Swap(arr, i + 1, right);
            return i + 1;
        }

        private static void Swap(Point3d[] arr, int i, int j)
        {
            Point3d temp = arr[i];
            arr[i] = arr[j];
            arr[j] = temp;
        }

        /// <summary>
        /// This method sorts a list of Brep objects based bounding boxes' center Z value
        /// </summary>
        /// <param name="arr">A list of Brep objects</param>
        /// <param name="left">The start index of the sorting process</param>
        /// <param name="right">The end index of the sorting process</param>
        public static void Quicksort(List<Brep> arr, int left, int right)
        {
            if (left < right)
            {
                int pivotIndex = Partition(arr, left, right);

                Quicksort(arr, left, pivotIndex - 1);
                Quicksort(arr, pivotIndex + 1, right);
            }
        }

        private static int Partition(List<Brep> arr, int left, int right)
        {
            Brep pivot = arr[right];
            int i = left - 1;

            for (int j = left; j < right; j++)
            {
                if (arr[j].GetBoundingBox(true).Center.Z < pivot.GetBoundingBox(true).Center.Z)
                {
                    i++;
                    Swap(arr, i, j);
                }
            }

            Swap(arr, i + 1, right);
            return i + 1;
        }

        private static void Swap(List<Brep> arr, int i, int j)
        {
            Brep temp = arr[i];
            arr[i] = arr[j];
            arr[j] = temp;
        }

    }
}
