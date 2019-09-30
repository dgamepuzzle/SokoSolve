﻿using System;
using NUnit.Framework;
using NUnit.Framework.Constraints;
using Sokoban.Core.Analytics;
using Sokoban.Core.Library;
using Sokoban.Core.Primitives;
using Sokoban.Core.PuzzleLogic;
using Sokoban.Core.Solver;

namespace SokoSolve.Tests
{
    [TestFixture]
    public class DeadMapAnalysisTests
    {
        [Test]
        public void DeadMap()
        {
            // Init
            var report = new TestReport();

            var stat = StaticAnalysis.Generate(TestLibrary.Default);

            var dead = DeadMapAnalysis.FindDeadMap(stat);


            Assert.That(dead, Is.Not.Null);
            report.WriteLine(dead);

//            Assert.That(report, Is.EqualTo(new TestReport(
//@"...........
//...........
//...........
//.......X...
//...........
//...........
//.......X...
//...X..X....
//...........
//...........
//..........."
//            )));
            Assert.Inconclusive();
        }

        [Test]
        public void DynamicDeadMap()
        {
            // Init
            var report = new TestReport();

            var p = new Puzzle(
                new string[]
                {
                    "##########",
                    "#...XX...#",
                    "#...X#...#",
                    "#.P......#",
                    "##########",
                });
            var stat = StaticAnalysis.Generate(p);
            
            Assert.That(DeadMapAnalysis.DynamicCheck(stat, StateMaps.Create(p)), Is.True);

             p = new Puzzle(
               new string[]
                {
                    "##########",
                    "#...#X...#",
                    "#...X#...#",
                    "#.P......#",
                    "##########",
                });
            stat = StaticAnalysis.Generate(p);
            Assert.That(DeadMapAnalysis.DynamicCheck(stat, StateMaps.Create(p)), Is.True);

            p = new Puzzle(
             new string[]
                {
                    "##########",
                    "#...##...#",
                    "#...XX...#",
                    "#.P......#",
                    "##########",
                });
            stat = StaticAnalysis.Generate(p);
            Assert.That(DeadMapAnalysis.DynamicCheck(stat, StateMaps.Create(p)), Is.True);

            p = new Puzzle(
             new string[]
                {
                    "##########",
                    "#........#",
                    "#...XX...#",
                    "#...##...#",
                    "#.P......#",
                    "##########",
                });
            stat = StaticAnalysis.Generate(p);
            Assert.That(DeadMapAnalysis.DynamicCheck(stat, StateMaps.Create(p)), Is.True);

            p = new Puzzle(
           new string[]
                {
                    "##########",
                    "#........#",
                    "#...XX...#",
                    "#...X#...#",
                    "#.P......#",
                    "##########",
                });
            stat = StaticAnalysis.Generate(p);
            Assert.That(DeadMapAnalysis.DynamicCheck(stat, StateMaps.Create(p)), Is.True);

            p = new Puzzle(
         new string[]
                {
                    "##########",
                    "#........#",
                    "#...$$...#",
                    "#...$#...#",
                    "#.P......#",
                    "##########",
                });
            stat = StaticAnalysis.Generate(p);
            Assert.That(DeadMapAnalysis.DynamicCheck(stat, StateMaps.Create(p)), Is.False);

            p = new Puzzle(
       new string[]
                {
                    "##########",
                    "#........#",
                    "#...$$...#",
                    "#...X#...#",
                    "#.P......#",
                    "##########",
                });
            stat = StaticAnalysis.Generate(p);
            Assert.That(DeadMapAnalysis.DynamicCheck(stat, StateMaps.Create(p)), Is.True);


            p = new Puzzle(
   new string[]
                {
                    "##########",
                    "#........#",
                    "#...$$...#",
                    "#...#X...#",
                    "#.P......#",
                    "##########",
                });
            stat = StaticAnalysis.Generate(p);
            Assert.That(DeadMapAnalysis.DynamicCheck(stat, StateMaps.Create(p)), Is.True);

            p = new Puzzle(
  new string[]
                {
                    "##########",
                    "#........#",
                    "#...#X...#",
                    "#...$$...#",
                    "#.P......#",
                    "##########",
                });
            stat = StaticAnalysis.Generate(p);
            Assert.That(DeadMapAnalysis.DynamicCheck(stat, StateMaps.Create(p)), Is.True);


        }

        [Test]
        public void Box()
        {
            // Init
            var report = new TestReport();

            var puz = new Puzzle(new String[]
            {
                "#####",
                "#...#",
                "#...#",
                "#...#",
                "#####",
            });

            var stat = StaticAnalysis.Generate(puz);
            
            var dead = DeadMapAnalysis.FindDeadMap(stat);
            Assert.That(dead, Is.Not.Null);
            report.WriteLine(dead);

            Assert.That(report, Is.EqualTo(new TestReport(
@".....
.XXX.
.X.X.
.XXX.
....."
            )));
        }

        [Test]
        public void Regression2()
        {
            // Init
            var report = new TestReport();

            var puz = new Puzzle(new String[]
            {
                "################",
                "#..............#",
                "#..............#",
                "#.############.#",
                "#..............#",
                "################",
            });

            var stat = StaticAnalysis.Generate(puz);
            var dead = DeadMapAnalysis.FindDeadMap(stat);
            Assert.That(dead, Is.Not.Null);
            report.WriteLine(dead);

            //            Assert.That(report, Is.EqualTo(new TestReport(
            //@"...........
            //...........
            //...........
            //.......X...
            //...........
            //...........
            //.......X...
            //...X..X....
            //...........
            //...........
            //..........."
            //        
        }
    }
}