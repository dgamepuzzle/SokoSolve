using System;
using System.Threading.Tasks;
using ConsoleZ.Drawing;
using ConsoleZ.Drawing.Game;
using ConsoleZ.Win32;
using SokoSolve.Core.Game;
using SokoSolve.Core.Solver;
using VectorInt;

namespace SokoSolve.Core.Game.Scenes
{
    public class SolverScene : GameScene<SokoSolveMasterGameLoop, CHAR_INFO>
    {
        private readonly IRenderer<CHAR_INFO>     renderer;
        private          Puzzle                   Puzzle         { get; }
        private          ISolver?                 Solver         { get; set; }
        private          SolverCommand?           SolverCommand  { get; set; }
        private          Task?                    SolverTask     { get; set; }
        private          SolverCommandResult?     SolverState    { get; set; }
        public           Exception?               SolverException { get; set; }

        private CHAR_INFO DefaultStyle { get; } =
            new CHAR_INFO('.', CHAR_INFO_Attr.FOREGROUND_GRAY);
        private CHAR_INFO ErrorStyle { get; } =
            new CHAR_INFO('.', CHAR_INFO_Attr.FOREGROUND_RED | CHAR_INFO_Attr.FOREGROUND_INTENSITY);

        public SolverScene(SokoSolveMasterGameLoop parent, Puzzle puzzle) : base(parent)
        {
            Puzzle = puzzle;
            this.renderer = Parent.Renderer;
        }

        public override void Init()
        {
            
        }

        public override void Step(float elapsedSec)
        {
            if (Input.IsKeyPressed(ConsoleKey.Escape))
            {
                if (Solver != null)
                {
                    if (SolverCommand != null) SolverCommand.ExitConditions.ExitRequested = true;

                    if (SolverTask != null)
                    {
                        SolverTask.Wait();
                        SolverTask.Dispose();
                        SolverTask = null;
                        SolverCommand = null;
                        SolverState = null;
                        Solver = null;
                    }
                }
                
                Parent.ShowLibrary();
            }
            if (Input.IsKeyPressed(ConsoleKey.Backspace))
            {
                if (Solver != null)
                {
                    if (SolverCommand != null)
                    {
                        SolverCommand.ExitConditions.ExitRequested = true;

                        if (SolverTask != null)
                        {
                            SolverTask.Wait();
                            SolverTask.Dispose();
                            SolverTask = null;
                            SolverCommand = null;
                            SolverState = null;
                            Solver = null;
                        }
                    }
                }
            }            
            if (Solver is null)
            {
                if (Input.IsKeyPressed(ConsoleKey.F) || Input.IsKeyPressed(ConsoleKey.Enter))
                {
                    RunSolverWith(new SingleThreadedForwardSolver());
                }
                if (Input.IsKeyPressed(ConsoleKey.R))
                {
                    RunSolverWith(new SingleThreadedReverseSolver());
                }
                else if (Input.IsKeyPressed(ConsoleKey.M) )
                {
                    RunSolverWith(new MultiThreadedForwardReverseSolver());
                }
            }
            else
            {
                

            }
        }

        private void RunSolverWith(ISolver solver)
        {
            SolverException = null;
            SolverState = null;

            Solver = solver;
            SolverCommand = new SolverCommand()
            {
                Puzzle         = Puzzle,
                ExitConditions = ExitConditions.Default3Min(),
            };

            SolverTask = Task.Run(() =>
            {
                try
                {
                    SolverState = Solver.Init(SolverCommand);
                    Solver.Solve(SolverState);
                }
                catch (Exception e)
                {
                    SolverException = e;
                }
                
            });
        }

        

        public override void Draw()
        {
            var rectPuzzle = Puzzle.Area.Move((2,4));
            renderer.Box(rectPuzzle.Outset(2), DrawingHelper.AsciiBox);
            renderer.DrawMap(Puzzle, rectPuzzle.TL, x=>new CHAR_INFO(x.Underlying));
            
            if (Solver is null)
            { 
                var stats = RectInt.FromTwoPoints(renderer.Geometry.TM, renderer.Geometry.BR);
                renderer.TitleBox(stats, "Select Solver", DrawingHelper.AsciiBox);
                
                var start = stats.TL + (2, 2);
                start = renderer.DrawText(start, $"[F] {nameof(SingleThreadedForwardSolver)}", DefaultStyle);
                start = renderer.DrawText(start, $"[R] {nameof(SingleThreadedReverseSolver)}", DefaultStyle);
                start = renderer.DrawText(start, $"[M] {nameof(MultiThreadedForwardReverseSolver)}", DefaultStyle);
            }
            else
            {
                // Solver Running, show progress
                if (SolverTask.IsFaulted || SolverException != null)
                {
                    // Error
                    renderer.DrawText((0, 0), (SolverException ?? SolverTask.Exception).Message, ErrorStyle);
                }
                else if (SolverTask.IsCompleted)
                {
                    // Done
                    renderer.DrawText((0,0), $"[{SolverState.Exit}:{(SolverState.EarlyExit ? "EARLY-EXIT": "")}] Solutions:{SolverState.Solutions?.Count ?? 0}", DefaultStyle);
                }
                
                else
                {
                    // Running
                    renderer.DrawText((0,0), $"[RUNNING] {SolverState?.Statistics?.Elapased}", DefaultStyle);
                }
                
                // For all
                if (SolverState?.Statistics != null)
                {
                    var s = SolverState.Statistics;
                    var stats = RectInt.FromTwoPoints(renderer.Geometry.TM, renderer.Geometry.BR);
                    renderer.TitleBox(stats, "Statistics", DrawingHelper.AsciiBox);

                    var start = stats.TL + (2, 2);
                    start = renderer.DrawText(start, $"Solutions: {SolverState.Solutions?.Count}", DefaultStyle);
                    start = renderer.DrawText(start, $"Nodes: {s.TotalNodes} @  {s.TotalNodes / s.DurationInSec:0.0}/sec", DefaultStyle);
                    
                    if (Solver.Statistics != null)
                    {
                        foreach (var stLine in Solver.Statistics)
                        {
                            start = renderer.DrawText(start, stLine.ToStringShort(), DefaultStyle);        
                        }   
                    }
                    
                    if (SolverState is ISolverVisualisation vs && vs.TrySample(out var node))
                    {
                        renderer.DrawMapWithPosition(node.CrateMap, rectPuzzle.TL, 
                            (p, c) => new CHAR_INFO(Puzzle[p].Underlying, 
                                c ? CHAR_INFO_Attr.BACKGROUND_GREEN | CHAR_INFO_Attr.FOREGROUND_GRAY: CHAR_INFO_Attr.FOREGROUND_GRAY));
                        
                    }
                }
            }
        }

        

        public override void Dispose()
        {
            
        }
    }
}