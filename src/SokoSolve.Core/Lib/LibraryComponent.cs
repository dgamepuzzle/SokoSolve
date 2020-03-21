﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using SokoSolve.Core.Analytics;
using SokoSolve.Core.Game;
using SokoSolve.Core.Model.DataModel;
using Path = System.IO.Path;

namespace SokoSolve.Core.Lib
{
    public class LibraryComponent
    {
        private readonly string basePath;
        Dictionary<string, Library> cacheLibs = new Dictionary<string, Library>();
        private readonly LibraryCollection collection = new LibraryCollection
        {
            IdToFileName = new Dictionary<string, string>()
            {
                {"L1", "Sasquatch.ssx"}
            }
        };

        public LibraryComponent(string basePath)
        {
            this.basePath = basePath;
        }

        public string GetPathData(string rel)
        {
            return Path.Combine(basePath, rel);
        }

        public LibraryCollection GetDefaultLibraryCollection() => collection;

        public Library GetLibraryWithCaching(string id)
        {
            if (cacheLibs.TryGetValue(id, out var l)) return l;

            l = LoadLibrary(GetPathData( GetDefaultLibraryCollection().IdToFileName[id]));
            cacheLibs.Add(id, l);
            return l;
        }
        
        public LibraryPuzzle GetPuzzleWithCaching(PuzzleIdent ident)
        {
            var l = GetLibraryWithCaching(ident.Library);
            return l?.First(x=>x.Ident.Puzzle == ident.Puzzle);
        }
        
        public Library LoadLibraryRel(string fileName)
        {
            return LoadLegacySokoSolve_SSX(Path.Combine(basePath, fileName));
        }

        public Library LoadLibrary(string fileName)
        {
            return LoadLegacySokoSolve_SSX(fileName);
        }

        public void SaveLibrary(Library lib, string fileName)
        {
            throw new NotImplementedException();
        }

        public Profile LoadProfile(string fileName)
        {
            var profile = new Profile
            {
                FileName = fileName,
                Current = null,
                Statistics = new Statistics()
            };
            var pairs = TrivialNameValueFileFormat.Load(fileName);
            var binder = new TrivialNameValueFileFormat.WithBinder<Profile>();
            foreach (var pair in pairs)
            {
                binder.SetWhen(pair, profile, x => x.Name);
                binder.SetWhen(pair, profile, x => x.Created);
                binder.SetWhen(pair, profile, x => x.TimeInGame);
                binder.With(profile, x => x.Current)
                    .SetWhen(pair, x => x.Library)
                    .SetWhen(pair, x => x.Puzzle);
                binder.With(profile, x => x.Statistics)
                    .SetWhen(pair, x => x.Pushes)
                    .SetWhen(pair, x => x.Steps)
                    .SetWhen(pair, x => x.Started)
                    .SetWhen(pair, x => x.Undos)
                    .SetWhen(pair, x => x.Restarts)
                    ;
            }

            return profile;
        }


        public TrivialNameValueFileFormat SaveProfile(Profile lib, string fileName)
        {
            var txt = TrivialNameValueFileFormat.Serialise(lib);
            txt.Save(fileName);
            return txt;
        }

        public Library LoadLegacySokoSolve_SSX(string fileName)
        {
            var ser = new XmlSerializer(typeof(SokobanLibrary));

            SokobanLibrary xmlLib = null;
            using (var reader = File.OpenText(fileName))
            {
                xmlLib = (SokobanLibrary) ser.Deserialize(reader);
            }

            var lib = Convert(xmlLib);
            var cc = 0;
            foreach (var puzzle in lib)
            {
                puzzle.Rating = StaticAnalysis.CalculateRating(puzzle.Puzzle);
            }

            return lib;
        }

        private Library Convert(SokobanLibrary xmlLib)
        {
            var lib = new Library()
            {
                Details = new AuthoredItem()
                {
                    Id = xmlLib.LibraryID
                }
            };
            foreach (var puzzle in xmlLib.Puzzles)
            {
                var map = puzzle.Maps.FirstOrDefault();
                if (map != null && map.Row != null)
                {
                    var lp = Convert(puzzle, map);
                    lp.Ident = new PuzzleIdent(lib.Details.Id, lp.Details.Id);
                    lib.Add(lp);
                }
            }

            return lib;
        }

        private LibraryPuzzle Convert(SokobanLibraryPuzzle xmlLib, SokobanLibraryPuzzleMap xp)
        {
            return new LibraryPuzzle(Puzzle.Builder.FromLines(xp.Row))
            {
                Name = xmlLib.PuzzleDescription != null ? xmlLib.PuzzleDescription.Name : null,
                Details = new AuthoredItem
                {
                    Id = xmlLib.PuzzleID,
                    Name = xmlLib.PuzzleDescription != null ? xmlLib.PuzzleDescription.Name : null
                }
            };
        }

        public List<LibraryPuzzle> LoadAllPuzzles(IEnumerable<PuzzleIdent> idents)
        {
            var libs = idents.Select(x => x.Library)
                .Distinct()
                .Select(
                    x => new Tuple<string, Library>(x, LoadLibrary(GetPathData(x))))
                .ToDictionary(x => x.Item1, x => x.Item2);

            var res = new List<LibraryPuzzle>();
            foreach (var ident in idents)
            {
                var p = libs[ident.Library][ident.Puzzle];
                if (p == null) throw new Exception("Ident not found:" + ident);
                res.Add(p);
            }

            return res;
        }
    }
}