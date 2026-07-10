namespace Game.Gameplay.Match
{
    /// <summary>March rules shared by lane graph and future unit movement.</summary>
    public static class MatchMarchRules
    {
        /// <summary>
        /// Units already marching on a lane spline keep following that route after owner elimination.
        /// New spawns from eliminated player are off.
        /// </summary>
        public const bool UnitsOnSplineContinueAfterOwnerEliminated = true;
    }
}
