﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using SokoSolve.Core.Analytics;
using SokoSolve.Core.Common;
using SokoSolve.Core.Debugger;
using SokoSolve.Core.Lib;
using SokoSolve.Core.Lib.DB;
using Path = SokoSolve.Core.Analytics.Path;

namespace SokoSolve.Core.Solver
{
    public interface ISolverRunTracking
    {
        void Begin(SolverCommandResult command);
        void End(SolverCommandResult result);
    }

    public class SolverRunComponent
    {
        public SolverRunComponent()
        {
            Report = Console.Out;
            Progress = Console.Out;
        }

        public TextWriter Report { get; set; }
        public TextWriter Progress { get; set; }

        // Optional
        public ISokobanRepository? Repository { get; set; }
        public ISolverRunTracking? Tracking { get; set; }
        public int StopOnConsecutiveFails { get; set; }

        public bool SkipPuzzlesWithSolutions { get; set; }

        public List<SolverResultSummary> Run(SolverRun run, SolverCommand baseCommand, ISolver solver)
        {
            if (run == null) throw new ArgumentNullException(nameof(run));
            if (baseCommand == null) throw new ArgumentNullException(nameof(baseCommand));
            if (solver == null)
                throw new ArgumentNullException(nameof(solver), "See: " + nameof(SingleThreadedForwardSolver));

            Report.WriteLine("Puzzle Exit Conditions: {0}", run.PuzzleExit);
            Report.WriteLine("Batch Exit Conditions : {0}", run.BatchExit);
            Report.WriteLine("Solver                : {0}", SolverHelper.Describe(solver));
            Report.WriteLine("CPU                   : {0}", DebugHelper.GetCPUDescription());
            Report.WriteLine("Machine               : {0} {1}", Environment.MachineName, Environment.Is64BitProcess ? "x64" : "x32");
            Report.WriteLine();

            var res = new List<SolverResultSummary>();
            var start = new SolverStatistics
            {
                Started = DateTime.Now
            };
            SolverCommandResult? commandResult = null;
            var pp = 0;
            var consecutiveFails = 0;
            foreach (var puzzle in run)
            {
                if (baseCommand.CheckAbort(baseCommand))
                {
                    Progress.WriteLine("EXITING...");
                    break;
                }

                try
                {
                    pp++;
                    Progress.WriteLine($"({pp}/{run.Count}) Attempting: {puzzle.Ident} \"{puzzle.Name}\", R={StaticAnalysis.CalculateRating(puzzle.Puzzle)}. Stopping:[{baseCommand.ExitConditions}] ...");

                    
                    Report.WriteLine("           Name: {0}", puzzle);
                    Report.WriteLine("          Ident: {0}", puzzle.Ident);
                    Report.WriteLine("         Rating: {0}", StaticAnalysis.CalculateRating(puzzle.Puzzle));
                    Report.WriteLine(puzzle.Puzzle.ToString());
                    Report.WriteLine();


                    IReadOnlyCollection<SolutionDTO> existingSolutions = null;
                    if (SkipPuzzlesWithSolutions && Repository != null) 
                    {
                        existingSolutions =  Repository.GetSolutions(puzzle.Ident);
                        if (existingSolutions != null && existingSolutions.Any(
                            x => x.HostMachine == Environment.MachineName && x.SolverType == solver.GetType().Name))
                        {
                            Progress.WriteLine("Skipping... (SkipPuzzlesWithSolutions)");
                            continue;    
                        }
                    }

                    Report.WriteLine();
                    
                    // #### Main Block Start
                    var attemptTimer = new Stopwatch();
                    attemptTimer.Start();
                    commandResult = solver.Init(new SolverCommand(baseCommand)
                    {
                        Report = Report,
                        Puzzle = puzzle.Puzzle
                    });
                    Tracking?.Begin(commandResult);
                    solver.Solve(commandResult);
                    attemptTimer.Stop();
                    // #### Main Block End
                    
                    
                    if (Repository != null && commandResult.Solutions?.Any() == true)
                    {
                        StoreSolution(solver, puzzle, commandResult.Solutions);
                    }
                    
                    commandResult.Summary = new SolverResultSummary
                    {
                        Puzzle = puzzle,
                        Exited = commandResult.Exit,
                        Solutions = commandResult.Solutions,
                        Duration = attemptTimer.Elapsed,
                        Statistics = commandResult.Statistics
                    };
                    
                    commandResult.Summary.Text = SolverHelper.GenerateSummary(commandResult);
                    res.Add(commandResult.Summary);

                    start.TotalNodes += commandResult.Statistics.TotalNodes;
                    start.TotalDead  += commandResult.Statistics.TotalDead;

                    if (commandResult.Summary.Solutions.Any()) // May have been removed above
                    {
                        consecutiveFails = 0;
                    }
                    else
                    {
                        consecutiveFails++;
                        if (StopOnConsecutiveFails != 0 && consecutiveFails > StopOnConsecutiveFails)
                        {
                            Progress.WriteLine("ABORTING... StopOnConsecutiveFails");
                            break;
                        }
                    }

                    var finalStats = solver.Statistics;
                    if (finalStats != null)
                        foreach (var fs in finalStats)
                            Report.WriteLine("Statistics | {0}", fs);

                   
                    if (Tracking != null) Tracking.End(commandResult);

                    Report.WriteLine("[DONE] {0}", commandResult.Summary.Text);
                    if (commandResult.Exception != null)
                    {
                        Report.WriteLine("[EXCEPTION]");
                        WriteException(Report, commandResult.Exception);
                    }

                    Progress.WriteLine($" -> {commandResult.Summary.Text}");

                    if (commandResult.Exit == ExitConditions.Conditions.Aborted)
                    {
                        Progress.WriteLine("ABORTING...");
                        WriteSummary(res, start);
                        return res;
                    }

                    if (start.DurationInSec > run.BatchExit.Duration.TotalSeconds)
                    {
                        Progress.WriteLine("BATCH TIMEOUT...");
                        WriteSummary(res, start);
                        return res;
                    }

                    Progress.WriteLine();
                }
                catch (Exception ex)
                {
                    if (commandResult != null) commandResult.Exception = ex;
                    Progress.WriteLine("ERROR: " + ex.Message);
                    WriteException(Report, ex);
                }
                finally
                {
                    commandResult = null;
                    // Report.WriteLine("Ending Memory: {0}", Environment.WorkingSet);
                    // GC.Collect();
                    // Report.WriteLine("Post-GC Memory: {0}", Environment.WorkingSet);
                    // Report.WriteLine("===================================");
                    // Report.WriteLine();
                }

                Report.Flush();
            }

            WriteSummary(res, start);
            return res;
        }

        private void StoreSolution(ISolver solver, LibraryPuzzle dto, List<Path> solutions)
        {
            var best = solutions.OrderBy(x => x.Count).First();

            var sol = new SolutionDTO
            {
                PuzzleIdent        = dto.Ident.ToString(),
                Path               = best.ToString(),
                Created            = DateTime.Now,
                Modified           = DateTime.Now,
                HostMachine        = Environment.MachineName,
                SolverType         = solver.GetType().Name,
                SolverVersionMajor = solver.VersionMajor,
                SolverVersionMinor = solver.VersionMinor,
                SolverDescription  = solver.VersionDescription,
                TotalNodes         = solver.Statistics.First().TotalNodes,
                TotalSecs          = solver.Statistics.First().DurationInSec
            };

            var exists = Repository.GetSolutions(dto.Ident);
            if (exists.Any())
            {
                var thisMachine= exists.Where(x => x.HostMachine == sol.HostMachine && x.SolverType == sol.SolverType);
                if (thisMachine.Any())
                {
                    var exact = thisMachine.OrderByDescending(x => x.Path.Length).First();
                    // Is Better
                    if (exact.TotalSecs > sol.TotalSecs)
                    {
                        // Replace
                        sol.SolutionId = exact.SolutionId;
                        sol.Created = exact.Created;
                        Repository.Update(sol);
                    }
                }
                else
                {
                    Repository.Store(sol);
                }
            }
            else
            {
                Repository.Store(sol);
            }
        }


        private void WriteException(TextWriter report, Exception exception, int indent = 0)
        {
            report.WriteLine("   Type: {0}", exception.GetType().Name);
            report.WriteLine("Message: {0}", exception.Message);
            report.WriteLine(exception.StackTrace);
            if (exception.InnerException != null) WriteException(report, exception.InnerException, indent + 1);
        }

        private void WriteSummary(List<SolverResultSummary> results, SolverStatistics start)
        {
            var cc = 0;
            var line = $"Run Stats: {start}";
            Report.WriteLine(line);
            Console.WriteLine(line);
            foreach (var result in results)
            {
                line = $"[{result.Puzzle.Ident}] {result.Text}";
                Report.WriteLine(line);
                Console.WriteLine(line);
                cc++;
            }
        }

        /// <summary>
        ///     For memory reasons, we cannot allow ANY state from the Solver.
        ///     This would cause out of memory issues.
        /// </summary>
        public class SolverResultSummary
        {
            public string Text { get; set; }
            public LibraryPuzzle Puzzle { get; set; }
            public ExitConditions.Conditions Exited { get; set; }
            public List<Path> Solutions { get; set; }
            public TimeSpan Duration { get; set; }
            public SolverStatistics Statistics { get; set; }
        }
    }
}