using Sokoban.Core.Primitives;
using Sokoban.Core.PuzzleLogic;

namespace Sokoban.Core.Analytics
{
    public interface IStateMaps
    {
        Bitmap CrateMap { get;  }
        Bitmap MoveMap { get;  }
    }

    public class StateMaps : IStateMaps
    {
        public Bitmap CrateMap { get; set; }
        public Bitmap MoveMap { get; set; }

        public static StateMaps Create(Puzzle puzzle)
        {
            return new StateMaps()
            {
                CrateMap = puzzle.ToMap(puzzle.Definition.AllCrates),
                MoveMap = FloodFill.Fill(puzzle.ToMap(puzzle.Definition.Wall), puzzle.Player.Position)
            };
        }

        public override int GetHashCode()
        {
            return CrateMap.GetHashCode() ^  MoveMap.GetHashCode(); 
        }

        public override bool Equals(object obj)
        {
            var rhs = obj as StateMaps;
            if (rhs == null) return false;
            return rhs.CrateMap.Equals(CrateMap) && rhs.MoveMap.Equals(MoveMap);
        }

        public override string ToString()
        {
            return string.Format("CrateMap:\n{0}\nMoveMap:\n{1}", CrateMap, MoveMap);
        }

    }
}