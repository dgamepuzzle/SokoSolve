﻿using System;
using SokoSolve.Core.Library;
using SokoSolve.Core.Solver;
using Xunit;

namespace SokoSolve.Tests.NUnitTests
{
    public class SolverRunTests
    {
        private readonly TestHelper helper = new TestHelper();

        [Xunit.Fact]
        public void CanLoadAll()
        {
            Console.WriteLine(Environment
                .CurrentDirectory); // C:\Projects\SokoSolve\src\SokoSolve.Tests\bin\Debug\netcoreapp3.0

            var res = new SolverRun();
            res.Load(new LibraryComponent(helper.GetDataPath()), "SolverRun-Default.tff");
        }
    }
}