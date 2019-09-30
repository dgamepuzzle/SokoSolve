﻿using NUnit.Framework;
using Sokoban.Core;
using Sokoban.Core.Analytics;
using Sokoban.Core.Primitives;

namespace SokoSolve.Tests
{
    [TestFixture]
    public class FloodFillTests
    {
        [Test]
        public void Sample()
        {
            var bountry = Bitmap.Create(new string[]
            {
                "~~~###~~~~~",
                "~~## #~####",
                "~##  ###  #",
                "## X      #",
                "#    X #  #",
                "### X###  #",
                "~~#  #    #",
                "~## ## # ##",
                "~#      ##~",
                "~#     ##~~",
                "~#######~~~",
            });

            var expected = Bitmap.Create(new string[]
            {
                "~~~###~~~~~",
                "~~## #~####",
                "~##  ###  #",
                "## X      #",
                "#    X #  #",
                "### X###  #",
                "~~#  #    #",
                "~## ## # ##",
                "~#      ##~",
                "~#     ##~~",
                "~#######~~~",
            }, x=>x == ' ');

            var start = new VectorInt2(4 ,4);

            
            var result = FloodFill.Fill(bountry, start);
            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        public void Open()
        {
            var bountry = Bitmap.Create(new string[]
            {
                "~~~# #~~~~~",
                "~~## #~####",
                "~##  ###  #",
                "## X      #",
                "#    X #  #",
                "### X###  #",
                "~~#  #    #",
                "~## ## # ##",
                "~#      ##~",
                "~#     ##~~",
                "~#######~~~",
            });

            var expected = Bitmap.Create(new string[]
            {
                "~~~# #~~~~~",
                "~~## #~####",
                "~##  ###  #",
                "## X      #",
                "#    X #  #",
                "### X###  #",
                "~~#  #    #",
                "~## ## # ##",
                "~#      ##~",
                "~#     ##~~",
                "~#######~~~",
            }, x => x == ' ');

            var start = new VectorInt2(4, 4);

            var result = FloodFill.Fill(bountry, start);
            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        public void TwoRooms()
        {
            var bountry = Bitmap.Create(new string[]
            {
               "#########",
               "#  ######",
               "#  ###  #",
               "######  #",
               "#########"
            }, x=> x == ' ' );

            var expected = Bitmap.Create(new string[]
            {
               "#########",
               "#oo######",
               "#oo###  #",
               "######  #",
               "#########"
            }, x => x == 'O');

            var start = new VectorInt2(1, 1);

            var result = FloodFill.Fill(bountry, start);
            Assert.That(result, Is.EqualTo(expected));
        }
    }
}