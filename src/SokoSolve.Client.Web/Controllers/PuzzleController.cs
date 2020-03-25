using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SokoSolve.Core.Lib;
using SokoSolve.Core.Solver;

namespace SokoSolve.Client.Web.Controllers
{
    public class PuzzleController : Controller
    {
        private static readonly ConcurrentDictionary<long, SolverModel> staticState = new ConcurrentDictionary<long, SolverModel>();
        private LibraryComponent compLib;

        public PuzzleController(LibraryComponent compLib)
        {
            this.compLib = compLib;
        }

        public IActionResult Home(string id)
        {
            var ident = PuzzleIdent.Parse(id);
            var p = compLib.GetPuzzleWithCaching(ident);
            return View(p);
        }
        
        public IActionResult SolveStart(string id)
        {
            var ident = PuzzleIdent.Parse(id);
            var p     = compLib.GetPuzzleWithCaching(ident);
            
            var solver = new MultiThreadedForwardReverseSolver();
            var solverCommand = new SolverCommand()
            {
                Puzzle = p.Puzzle,
                ExitConditions = ExitConditions.Default3Min()
            };
            var model = new SolverModel()
            {
                Token = DateTime.Now.Ticks,
                Puzzle = p,
                Command = solverCommand,
                Result = solver.Init(solverCommand)
            };

            staticState[model.Token] = model;

            model.Task = Task.Run(() =>
            {
                solver.Solve(model.Result);
                model.IsFinished = true;
            });

            return RedirectToAction("SolveMem", new {id, token=model.Token});
        }

        public class SolverModel
        {
            public long                Token      { get; set; }
            public LibraryPuzzle       Puzzle     { get; set; }
            public Task                Task       { get; set; }
            public SolverCommand       Command    { get; set; }
            public SolverCommandResult Result     { get; set; }
            public bool                IsFinished { get; set; }
            public bool                IsRunning  => !IsFinished;
        }
        
        public IActionResult SolveMem(string id, long token)
        {
            if (staticState.TryGetValue(token, out var state))
            {
                return View(state);    
            }

            return RedirectToAction("Home", new {id});
        }

        public IActionResult ReportClash(string id, long token)
        {
            if (staticState.TryGetValue(token, out var state))
            {
                if (state.IsRunning) return Content("Must be complete");
        
                if (state.Result is MultiThreadedForwardReverseSolver.CommandResult multiResult)
                {
                    var all = multiResult.PoolForward.GetAll().Union(multiResult.PoolReverse.GetAll());
                    var group = all.GroupBy(x => x.GetHashCode())
                                   .OrderByDescending(x=>x.Count());
                    return View(group);
                }

                return Content("Not Supported");

            }
        
            return RedirectToAction("Home", new {id});
        }
    }
}