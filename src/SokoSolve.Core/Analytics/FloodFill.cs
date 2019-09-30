﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sokoban.Core.Primitives;

namespace Sokoban.Core.Analytics
{
    public static class FloodFill
    {
       


        public static Bitmap Fill(IBitmap contraints, VectorInt2 p)
        {
            var result = new Bitmap(contraints.Size);
            FillCell(contraints, result, p);

            return result;
        }

        private static void FillCell(IBitmap contraints, IBitmap result, VectorInt2 p)
        {
            if (p.X < 0 || p.Y < 0) return;
            if (p.X > contraints.Size.X || p.Y > contraints.Size.Y) return;

            if (contraints[p]) return;
            if (result[p]) return;

            result[p] = true;

            FillCell(contraints, result, p + VectorInt2.Up);
            FillCell(contraints, result, p + VectorInt2.Down);
            FillCell(contraints, result, p + VectorInt2.Left);
            FillCell(contraints, result, p + VectorInt2.Right);
        }
    }
}