﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using SokoSolve.Core.Analytics;
using SokoSolve.Core.Common;
using SokoSolve.Core.Lib;
using SokoSolve.Core.Lib.DB;
using SokoSolve.Core.Reporting;
using Path = SokoSolve.Core.Analytics.Path;

namespace SokoSolve.Core.Solver
{
    public interface ISolverRunTracking
    {
        void Begin(SolverResult command);
        void End(SolverResult result);
    }
    
    /// <summary>
    ///     For memory reasons, we cannot allow ANY state from the Solver.
    ///     This would cause out of memory issues.
    /// </summary>
    public class SolverResultSummary
    {
        public SolverResultSummary(LibraryPuzzle puzzle, List<Path> solutions, ExitConditions.Conditions exited, string text, TimeSpan duration, SolverStatistics statistics)
        {
            Puzzle = puzzle;
            Solutions = solutions;
            Exited = exited;
            Text = text;
            Duration = duration;
            Statistics = statistics;
        }

        public LibraryPuzzle             Puzzle     { get;  }
        public List<Path>                Solutions  { get; }
        public ExitConditions.Conditions Exited     { get;  }
        public string                    Text       { get;  }
        public TimeSpan                  Duration   { get;  }
        public SolverStatistics          Statistics { get;  }
    }

    public class BatchSolveComponent
    {
        public BatchSolveComponent(TextWriter report, TextWriter progress, ISokobanSolutionRepository? repository, ISolverRunTracking? tracking, int stopOnConsecutiveFails, bool skipPuzzlesWithSolutions)
        {
            Report = report;
            Progress = progress;
            Repository = repository;
            Tracking = tracking;
            StopOnConsecutiveFails = stopOnConsecutiveFails;
            SkipPuzzlesWithSolutions = skipPuzzlesWithSolutions;
        }

        public BatchSolveComponent(TextWriter report, TextWriter progress)
        {
            Report = report;
            Progress = progress;
            Repository = null;
            Tracking = null;
            StopOnConsecutiveFails = 5;
            SkipPuzzlesWithSolutions = false;
        }

        public TextWriter          Report                   { get; }
        public TextWriter          Progress                 { get; }
        public ISokobanSolutionRepository? Repository               { get; }
        public ISolverRunTracking? Tracking                 { get; }
        public int                 StopOnConsecutiveFails   { get; }
        public bool                SkipPuzzlesWithSolutions { get; }
        public bool WriteSummaryToConsole { get; set; } = true;

        public List<SolverResultSummary> Run(SolverRun run, SolverCommand baseCommand, ISolver solver, bool showSummary)
        {
            if (run == null) throw new ArgumentNullException(nameof(run));
            if (baseCommand == null) throw new ArgumentNullException(nameof(baseCommand));
            if (solver == null)
                throw new ArgumentNullException(nameof(solver), "See: " + nameof(SingleThreadedForwardSolver));

            Report.WriteLine("Puzzle Exit Conditions: {0}", run.PuzzleExit);
            Report.WriteLine("Batch Exit Conditions : {0}", run.BatchExit);
            Report.WriteLine("Environment           : {0}", DevHelper.RuntimeEnvReport());
            Report.WriteLine("Solver Environment    : v{0} -- {1}", SolverHelper.VersionUniversal, SolverHelper.VersionUniversalText);
            Report.WriteLine("Started               : {0}", DateTime.Now.ToString("u"));
            Report.WriteLine();

            var res = new List<SolverResultSummary>();
            var start = new SolverStatistics
            {
                Started = DateTime.Now
            };
            SolverResult? commandResult = null;
            var pp = 0;
            var consecutiveFails = 0;
            foreach (var puzzle in run)
            {
                var memStart = Environment.WorkingSet;
                if (baseCommand.CheckAbort(baseCommand))
                {
                    Progress.WriteLine("EXITING...");
                    break;
                }

                try
                {
                    pp++;
                    Progress.WriteLine($"(Puzzle   {pp}/{run.Count}) Attempting: {puzzle.Ident} \"{puzzle.Name}\", R={StaticAnalysis.CalculateRating(puzzle.Puzzle)}. Stopping on [{baseCommand.ExitConditions}] ...");
                    
                    Report.WriteLine("           Name: {0}", puzzle);
                    Report.WriteLine("          Ident: {0}", puzzle.Ident);
                    Report.WriteLine("         Rating: {0}", StaticAnalysis.CalculateRating(puzzle.Puzzle));
                    Report.WriteLine(puzzle.Puzzle.ToString());    // Adds 2x line feeds
                    
                    IReadOnlyCollection<SolutionDTO> existingSolutions = null;
                    if (SkipPuzzlesWithSolutions && Repository != null) 
                    {
                        existingSolutions =  Repository.GetPuzzleSolutions(puzzle.Ident);
                        if (existingSolutions != null && existingSolutions.Any(
                            x => x.MachineName == Environment.MachineName && x.SolverType == solver.GetType().Name))
                        {
                            Progress.WriteLine("Skipping... (SkipPuzzlesWithSolutions)");
                            continue;    
                        }
                    }
                    
                    

                    // #### Main Block Start --------------------------------------
                    var attemptTimer = new Stopwatch();
                    attemptTimer.Start();
                    commandResult = solver.Init(new SolverCommand(baseCommand)
                    {
                        Report = Report,
                        Puzzle = puzzle.Puzzle
                    });
                    var propsReport = GetPropReport(solver, commandResult);
                    Tracking?.Begin(commandResult);
                    
                    try
                    {
                        solver.Solve(commandResult);
                    }
                    catch (Exception e)
                    {
                        commandResult.Exception = e;
                        commandResult.Exit = ExitConditions.Conditions.Error;
                        commandResult.EarlyExit = true;
                    }
                    attemptTimer.Stop();
                    // #### Main Block End ------------------------------------------


                    
                    if (Repository != null)
                    {
                        var id = StoreAttempt(solver, puzzle, commandResult, propsReport.ToString());

                        var solTxt = $"Checking against known solutions. SolutionId={id}";
                        Report.WriteLine(solTxt);
                        if (id >= 0)
                        {
                            Console.WriteLine(solTxt);    
                        }
                        
                    }
                    else
                    {
                        Report.WriteLine($"Solution Repository not available: Skipping.");
                    }

                    commandResult.Summary = new SolverResultSummary(
                        puzzle,
                        commandResult.Solutions,
                        commandResult.Exit,
                        SolverHelper.GenerateSummary(commandResult),
                        attemptTimer.Elapsed,
                        commandResult.Statistics
                    );

                    res.Add(commandResult.Summary);

                    start.TotalNodes += commandResult.Statistics.TotalNodes;
                    start.TotalDead  += commandResult.Statistics.TotalDead;

                    if (commandResult?.Summary?.Solutions != null && commandResult.Summary.Solutions.Any()) // May have been removed above
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
                    {
                        Report.WriteLine("### Statistics ###");
                        
                        MapToReporting.Create<SolverStatistics>()
                          .AddColumn("Name", x=>x.Name)
                          .AddColumn("Nodes", x=>x.TotalNodes)
                          .AddColumn("Avg. Speed", x=>x.NodesPerSec)
                          .AddColumn("Duration (sec)", x=>x.DurationInSec)
                          .AddColumn("Duplicates", x=>x.Duplicates < 0 ? null : (int?)x.Duplicates)
                          .AddColumn("Dead", x=>x.TotalDead < 0 ? null : (int?)x.Duplicates)
                          .AddColumn("Depth", x=>x.DepthCurrent < 0 ? null : (int?)x.Duplicates)
                          .RenderTo(Report, finalStats);
                        
                    }

                    Tracking?.End(commandResult);

                    Report.WriteLine("[DONE] {0}", commandResult.Summary.Text);
                    Progress.WriteLine($" -> {commandResult.Summary.Text}");
                    
                    if (commandResult.Exception != null)
                    {
                        Report.WriteLine("[EXCEPTION]");
                        WriteException(Report, commandResult.Exception);
                    }
                    if (commandResult.Exit == ExitConditions.Conditions.Aborted)
                    {
                        Progress.WriteLine("ABORTING...");
                        if (showSummary) WriteSummary(res, start);
                        return res;
                    }
                    if (start.DurationInSec > run.BatchExit.Duration.TotalSeconds)
                    {
                        Progress.WriteLine("BATCH TIMEOUT...");
                        if (showSummary) WriteSummary(res, start);
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
                    var memEnd = Environment.WorkingSet;
                    Report.WriteLine("Memory Delta: {0:#,##0}", memEnd - memStart);
                    Report.WriteLine("======================================================================");
                    Report.WriteLine();
                    if (puzzle != run.Last())
                    {
                        GC.Collect();    
                    }
                }

                Report.Flush();
            }
            if (showSummary) WriteSummary(res, start);
            
            Report.WriteLine("Completed               : {0}", DateTime.Now.ToString("u"));
            return res;
        }

        private FluentStringBuilder GetPropReport(ISolver solver, SolverResult commandResult)
        {
            Report.WriteLine("Solver: {0}", SolverHelper.Describe(solver));
            
            var propsReport = new FluentStringBuilder();
            propsReport.Append(solver.TypeDescriptor);
            try
            {
                var typeDescriptorProps = solver.GetTypeDescriptorProps(commandResult);
                if (typeDescriptorProps != null)
                {
                    foreach (var (name, text) in typeDescriptorProps)
                    {
                        propsReport.AppendLine($"-> {name,20}: {text}");
                        Report.WriteLine($"-> {name,20}: {text}");
                    }
                }
            }
            catch (NotSupportedException)
            {
                var msg = $"Solver [{solver.GetType().Name}] does not support {typeof(IExtendedFunctionalityDescriptor).Name}";
                Report.WriteLine(msg);
                propsReport.AppendLine(msg);
            }
            catch (NotImplementedException)
            {
                var msg = $"Solver [{solver.GetType().Name}] does not support {typeof(IExtendedFunctionalityDescriptor).Name}";
                Report.WriteLine(msg);
                propsReport.AppendLine(msg);
            }

            return propsReport;
        }

        private int StoreAttempt(ISolver solver, LibraryPuzzle dto, SolverResult result, string desc)
        {
            var best = result.Solutions?.OrderBy(x => x.Count).FirstOrDefault();
            

            var sol = new SolutionDTO
            {
                IsAutomated        =  true,
                PuzzleIdent        = dto.Ident.ToString(),
                Path               = best?.ToString(),
                Created            = DateTime.Now,
                Modified           = DateTime.Now,
                MachineName        = Environment.MachineName,
                MachineCPU =        DevHelper.DescribeCPU(),
                SolverType         = solver.GetType().Name,
                SolverVersionMajor = solver.VersionMajor,
                SolverVersionMinor = solver.VersionMinor,
                SolverDescription  = solver.VersionDescription,
                TotalNodes         = solver.Statistics.First().TotalNodes,
                TotalSecs          = solver.Statistics.First().DurationInSec,
                Description = desc
        
            };

            var exists = Repository.GetPuzzleSolutions(dto.Ident);
            if (exists != null && exists.Any())
            {
                var onePerMachine= exists.FirstOrDefault(x => x.MachineName == sol.MachineName && x.SolverType == sol.SolverType);
                if (onePerMachine != null)
                {
                    if (sol.HasSolution )
                    {
                        if (!onePerMachine.HasSolution)
                        {
                            sol.SolutionId = onePerMachine.SolutionId; // replace
                            Repository.Store(sol);
                            return sol.SolutionId;
                        }
                        else if (sol.TotalNodes < onePerMachine.TotalSecs)
                        {
                            sol.SolutionId = onePerMachine.SolutionId; // replace
                            Repository.Store(sol);
                            return sol.SolutionId;
                        }
                        else
                        {
                            // drop
                        }
                        
                    }
                    else 
                    {
                        if (!onePerMachine.HasSolution && sol.TotalNodes > onePerMachine.TotalNodes)
                        {
                            sol.SolutionId = onePerMachine.SolutionId; // replace
                            Repository.Store(sol);
                            return sol.SolutionId;
                        }
                    }
                }
                else
                {
                    Repository.Store(sol);
                    return sol.SolutionId;
                }
            }
            else
            {
                Repository.Store(sol);
                return sol.SolutionId;
            }

            return -1;
        }


        private void WriteException(TextWriter report, Exception exception, int indent = 0)
        {
            report.WriteLine("   Type: {0}", exception.GetType().Name);
            report.WriteLine("Message: {0}", exception.Message);
            report.WriteLine(exception.StackTrace);
            if (exception.InnerException != null) WriteException(report, exception.InnerException, indent + 1);
        }

        public void WriteSummary(List<SolverResultSummary> results, SolverStatistics start)
        {
            var cc = 0;
            
            /* Example
           GUYZEN running RT:3.1.3 OS:'WIN 6.2.9200.0' Threads:32 RELEASE x64 'AMD Ryzen Threadripper 2950X 16-Core Processor '
           Git: '[DIRTY] c724b04 Progress notifications, rev:191' at 2020-04-08 09:14:51Z, v3.1.0
            */
            var line = DevHelper.FullDevelopmentContext();
            Report.WriteLine(line);
            if (WriteSummaryToConsole) System.Console.WriteLine(line);
            
            
            foreach (var result in results)
            {
                line = $"[{result.Puzzle.Ident}] {result.Text}";
                Report.WriteLine(line);
                if (WriteSummaryToConsole)System.Console.WriteLine(line);
                cc++;
            }
        }

        
    }
}