﻿using System;
using System.Collections.Generic;
using System.Linq;
using SokoSolve.Core.Analytics;
using SokoSolve.Core.Common;
using SokoSolve.Core.Game;
using SokoSolve.Core.Primitives;
using VectorInt;

namespace SokoSolve.Core.Solver
{
    public interface INodeEvaluator
    {
        SolverNode Init(Puzzle puzzle, ISolverQueue queue);

        bool Evaluate(
            SolverCommandResult state, 
            ISolverQueue queue,
            ISolverNodeLookup pool,
            ISolverNodeLookup solutionPool, 
            SolverNode node);
    }

    public class ForwardEvaluator : INodeEvaluator
    {
        public bool IsDebugMode { get; set; }

        public SolverNode Init(Puzzle puzzle, ISolverQueue queue)
        {
            var root = SolverHelper.CreateRoot(puzzle);
            queue.Enqueue(root);
            return root;
        }

        public bool Evaluate(SolverCommandResult state, ISolverQueue queue, ISolverNodeLookup pool,
            ISolverNodeLookup solutionPool, SolverNode node)
        {
            if (node.HasChildren) throw new InvalidOperationException();

            node.Status = SolverNodeStatus.Evaluting;
            var toEnqueue = new List<SolverNode>();

            var solution = false;
            foreach (var move in node.MoveMap.TruePositions())
            foreach (var dir in VectorInt2.Directions)
            {
                var p = move;
                var pp = p + dir;
                var ppp = pp + dir;
                if (node.CrateMap[pp]) // crate to push
                    // into free space?
                    if (state.StaticMaps.FloorMap[ppp] && !node.CrateMap[ppp])
                        // Valid Push
                        if (!state.StaticMaps.DeadMap[ppp])
                        {
                            var newCrate = new Bitmap(node.CrateMap);
                            newCrate[pp]  = false;
                            newCrate[ppp] = true;
                            
                            var newMove = FloodFill.Fill(state.StaticMaps.WallMap.BitwiseOR(newCrate), pp);
                            
                            var newKid = new SolverNode(
                                p, pp, 
                                pp, ppp,
                                newCrate, newMove,
                                newCrate.BitwiseAND(state.StaticMaps.GoalMap).Count(),
                                this
                                );
                            

                            // Cycle Check: Does this node exist already?
                            var dup = pool.FindMatch(newKid);
                            if (dup != null)
                            {
                                // NOTE: newKid is NOT added as a ChildNode (which means less memory usage)

                                // Duplicate
                                newKid.Status = SolverNodeStatus.Duplicate;
                                state.Statistics.Duplicates++;

                                if (IsDebugMode) node.AddDuplicate(dup);
                            }
                            else
                            {
                                SolverNode match = null;
                                if (solutionPool != null) match = solutionPool.FindMatch(newKid);
                                if (match != null)
                                {
                                    // Add to tree / iterator
                                    node.Add(newKid);

                                    // Solution
                                    if (state.SolutionsNodesReverse == null)
                                        state.SolutionsNodesReverse = new List<SolutionChain>();
                                    var pair = new SolutionChain
                                    {
                                        ForwardNode = newKid,
                                        ReverseNode = match,
                                        FoundUsing = this
                                    };
                                    state.SolutionsNodesReverse.Add(pair);
                                    solution = true;
                                    state.Command.Debug.Raise(this, SolverDebug.Solution, pair);

                                    foreach (var n in newKid.PathToRoot().Union(match.PathToRoot()))
                                        n.Status = SolverNodeStatus.SolutionPath;
                                    newKid.Status = SolverNodeStatus.Solution;
                                    match.Status = SolverNodeStatus.Solution;

                                    if (state.Command.ExitConditions.StopOnSolution) return true;
                                }
                                else
                                {
                                    // Add to tree / iterator
                                    node.Add(newKid);

                                    if (DeadMapAnalysis.DynamicCheck(state.StaticMaps, node))
                                    {
                                        newKid.Status = SolverNodeStatus.Dead;
                                    }
                                    else
                                    {
                                        toEnqueue.Add(newKid);

                                        if (newKid.CrateMap.BitwiseAND(state.StaticMaps.GoalMap)
                                            .Equals(newKid.CrateMap))
                                        {
                                            // Solution
                                            state.SolutionsNodes.Add(newKid);
                                            state.Command.Debug.Raise(this, SolverDebug.Solution, newKid);
                                            solution = true;

                                            foreach (var n in newKid.PathToRoot())
                                                n.Status = SolverNodeStatus.SolutionPath;
                                            newKid.Status = SolverNodeStatus.Solution;
                                        }
                                    }
                                }
                            }
                        }
            }

            node.Status = node.HasChildren ? SolverNodeStatus.Evaluted : SolverNodeStatus.Dead;

            if (node.Status == SolverNodeStatus.Dead)
            {
                var p = node.Parent;
                if (p != null) p.CheckDead();
            }

            queue.Enqueue(toEnqueue);
            pool.Add(toEnqueue);


            return solution;
        }
    }
}