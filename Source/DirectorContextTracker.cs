using System;
using System.Collections.Generic;
using Verse;

namespace Ustas.RimAI.Communication.Personas
{
    public static class DirectorContextTracker
    {
        [ThreadStatic]
        private static List<Pawn> _currentPawns;

        public static void SetPawns(List<Pawn> pawns)
        {
            _currentPawns = pawns;
        }

        public static List<Pawn> GetPawns()
        {
            return _currentPawns;
        }

        public static void Clear()
        {
            _currentPawns = null;
        }
    }
}