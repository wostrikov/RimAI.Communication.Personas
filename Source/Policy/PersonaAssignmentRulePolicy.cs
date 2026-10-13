namespace Ustas.RimAI.Communication.Personas.Policy
{
    /// <summary>
    /// The part of a library assignment rule that is arithmetic. A rule picks
    /// which presets a new pawn may be given; besides faction, race and
    /// xenotype it can go by age, which is what keeps a child from being
    /// handed the persona of a retired mercenary.
    /// </summary>
    public static class PersonaAssignmentRulePolicy
    {
        public const int MinAllowedAge = 0;
        // Past any lifespan a mod gives its races; the field takes three digits.
        public const int MaxAllowedAge = 999;

        public static int ClampAge(int years)
        {
            if (years < MinAllowedAge) return MinAllowedAge;
            return years > MaxAllowedAge ? MaxAllowedAge : years;
        }

        /// <summary>
        /// Both ends inclusive, in biological years. A range typed the wrong way
        /// round is read the way it was meant rather than matching nobody.
        /// </summary>
        public static bool AgeMatches(int ageYears, int minAge, int maxAge)
        {
            int low = minAge <= maxAge ? minAge : maxAge;
            int high = minAge <= maxAge ? maxAge : minAge;
            return ageYears >= low && ageYears <= high;
        }
    }
}
