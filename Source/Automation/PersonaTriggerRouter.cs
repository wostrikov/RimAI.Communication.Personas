using System.Collections.Generic;
using RimWorld;
using Ustas.RimAI.Communication.Personas.Config;
using Ustas.RimAI.Communication.Personas.Policy;
using Verse;

namespace Ustas.RimAI.Communication.Personas.Automation
{
    /// <summary>
    /// Judges the game events the hooks recorded and turns the ones that matter
    /// into queued persona work. Runs on the world tick, after the events have
    /// settled: recruiting or capturing a pawn changes its faction and its guest
    /// status in separate steps, and only the state after both is its new role.
    ///
    /// The context strings are written for the model, in the same plain English
    /// as the rest of the Director's prompt data; their wording is
    /// <see cref="PersonaTriggerContextPolicy"/>'s, and this class supplies the
    /// names.
    /// </summary>
    public sealed class PersonaTriggerRouter
    {
        // One event can reach two hooks (a spouse's breakup is also a divorce;
        // twins are two births in one tick). Two in-game seconds covers both.
        const int DuplicateWindowTicks = 120;
        const int RetentionTicks = PersonaAutomationPolicy.TicksPerDay;

        readonly PersonaAutomationCoordinator _owner;
        readonly TriggerDeduplicator _recent = new TriggerDeduplicator(DuplicateWindowTicks, RetentionTicks);

        internal PersonaTriggerRouter(PersonaAutomationCoordinator owner)
        {
            _owner = owner;
        }

        static PersonaAutomationSettings Settings => PersonasMod.Settings?.Automation;

        public void Clear() => _recent.Clear();

        internal void Flush(PersonaGameEventInbox inbox, DirectorWorldComponent world, int now)
        {
            List<PersonaGameEvent> events = inbox.Drain();
            if (events == null) return;

            // A pawn can pass through several roles in one tick; what counts is
            // where it started and where it ended up.
            var roleStart = new Dictionary<int, PersonaGameEvent>();
            foreach (PersonaGameEvent gameEvent in events)
            {
                if (gameEvent.Kind == PersonaGameEventKind.RoleChange)
                {
                    if (!roleStart.ContainsKey(gameEvent.Subject.thingIDNumber))
                        roleStart[gameEvent.Subject.thingIDNumber] = gameEvent;
                    continue;
                }
                Dispatch(gameEvent, now);
            }

            foreach (PersonaGameEvent change in roleStart.Values)
            {
                PawnRoleSnapshot to = PawnRoleSnapshot.Of(change.Subject);
                if (to.Role != PawnRoleCategory.None && to.Role != change.From.Role)
                    DispatchRoleChange(change.Subject, change.From, to, world, now);
            }
        }

        void Dispatch(PersonaGameEvent gameEvent, int now)
        {
            switch (gameEvent.Kind)
            {
                case PersonaGameEventKind.NewPawn:
                    NewPawn(gameEvent.Subject, now);
                    break;
                case PersonaGameEventKind.Marriage:
                    OfferPair(gameEvent.Subject, gameEvent.Other, "marriage", other => "Married " + other.LabelShortCap + ".", now);
                    break;
                case PersonaGameEventKind.Breakup:
                    OfferPair(gameEvent.Subject, gameEvent.Other, "breakup", other => "Broke up with or divorced " + other.LabelShortCap + ".", now);
                    break;
                case PersonaGameEventKind.Birth:
                    Birth(gameEvent.Third, gameEvent.Subject == gameEvent.Other ? null : gameEvent.Subject, gameEvent.Other, now);
                    break;
                case PersonaGameEventKind.Death:
                    DirectFamilyDeath(gameEvent.Subject, now);
                    break;
                case PersonaGameEventKind.TraitGained:
                    TraitGained(gameEvent.Subject, gameEvent.Trait, now);
                    break;
            }
        }

        void DispatchRoleChange(Pawn pawn, PawnRoleSnapshot from, PawnRoleSnapshot to, DirectorWorldComponent world, int now)
        {
            PersonaAutomationSettings s = Settings;
            if (s == null || !AutoEvolveEligibility.IsLivePersonaPawn(pawn)) return;

            string key = "role:" + pawn.thingIDNumber + ":" + from.Role + ":" + to.Role;
            string context = PersonaTriggerContextPolicy.RoleChange(
                from.Role.ToString(), to.Role.ToString(), DescribeRoleState(from), DescribeRoleState(to));

            // A pawn still wearing a placeholder gets a real persona written for
            // its new role; one with a persona of its own grows into the role.
            AutoGenCategorySettings category = s.GetCategory(to.Role);
            if (s.autoGenEnabled && category != null && category.onRoleChange
                && PersonaOwnership.HasNoOwnPersona(pawn, world.History))
            {
                Offer(pawn, PersonaAutomationKind.Generate, true, key, context, now);
                return;
            }

            if (s.autoEvolveEnabled && s.evolveOnRoleChange && AutoEvolveEligibility.IsEligible(pawn, s))
                Offer(pawn, PersonaAutomationKind.Evolve, false, key, context, now);
        }

        void NewPawn(Pawn pawn, int now)
        {
            PersonaAutomationSettings s = Settings;
            if (s == null || !s.autoGenEnabled || !AutoEvolveEligibility.IsLivePersonaPawn(pawn)) return;
            AutoGenCategorySettings category = s.GetCategory(PawnRoleClassifier.Classify(pawn));
            if (category == null || !category.enabled) return;
            Offer(pawn, PersonaAutomationKind.Generate, false, "new:" + pawn.thingIDNumber, null, now);
        }

        /// <summary>
        /// The mother always; the father only as her lover, fiancé or spouse - a
        /// man who is merely the genetic father of a stranger's child has not, in
        /// his own story, become a parent. Twins and the two birth paths share one
        /// key per parent, so one birth is one request.
        /// </summary>
        void Birth(Pawn child, Pawn mother, Pawn father, int now)
        {
            string childName = child?.LabelShortCap;
            Pawn otherOfMother = father != mother ? father : null;
            if (mother != null)
                OfferEvolve(mother, "birth:" + mother.thingIDNumber,
                    PersonaTriggerContextPolicy.Birth(childName, otherOfMother?.LabelShortCap), now);
            if (father != null && father != mother && mother != null
                && LovePartnerRelationUtility.LovePartnerRelationExists(mother, father))
                OfferEvolve(father, "birth:" + father.thingIDNumber,
                    PersonaTriggerContextPolicy.Birth(childName, mother.LabelShortCap), now);
        }

        void DirectFamilyDeath(Pawn deceased, int now)
        {
            foreach (Pawn survivor in CollectDirectFamily(deceased))
            {
                if (survivor.Dead) continue;
                PawnRelationDef relation = survivor.GetMostImportantRelation(deceased);
                string who = relation != null
                    ? deceased.LabelShortCap + " (" + relation.GetGenderSpecificLabel(deceased) + ")"
                    : deceased.LabelShortCap.ToString();
                OfferEvolve(survivor, "death:" + survivor.thingIDNumber + ":" + deceased.thingIDNumber,
                    "A direct family member died: " + who + ".", now);
            }
        }

        void TraitGained(Pawn pawn, Trait trait, int now)
        {
            if (trait?.def == null || trait.Suppressed) return;
            OfferEvolve(pawn, "trait:" + pawn.thingIDNumber + ":" + trait.def.defName,
                PersonaTriggerContextPolicy.TraitGained(trait.LabelCap, DescribeTrait(trait, pawn)), now);
        }

        /// <summary>The trait's own description, as the character card shows it for this pawn.</summary>
        static string DescribeTrait(Trait trait, Pawn pawn)
        {
            string description = trait.CurrentData?.description;
            if (string.IsNullOrWhiteSpace(description)) return null;
            return description.Formatted(pawn.Named("PAWN")).AdjustedFor(pawn).Resolve().StripTags();
        }

        static string DescribeRoleState(PawnRoleSnapshot state) =>
            PersonaTriggerContextPolicy.RoleState(
                DescribeFaction(state.Faction), DescribeFaction(state.HeldBy), DescribeFaction(state.TakenFrom));

        /// <summary>The faction's name and how it stands towards the colony; null for no faction.</summary>
        static string DescribeFaction(Faction faction)
        {
            if (faction == null) return null;
            string standing = null;
            if (faction.IsPlayer)
            {
                standing = "the player's colony";
            }
            else
            {
                Faction player = Faction.OfPlayerSilentFail;
                // allowNull: a temporary faction can have no relation to anyone,
                // and asking for one the plain way logs an error about it.
                FactionRelation relation = player != null ? faction.RelationWith(player, allowNull: true) : null;
                if (relation != null)
                {
                    standing = relation.kind == FactionRelationKind.Hostile ? "hostile to the colony"
                        : relation.kind == FactionRelationKind.Ally ? "allied with the colony"
                        : "neutral to the colony";
                }
            }
            return PersonaTriggerContextPolicy.Faction(faction.Name, standing);
        }

        /// <summary>Parents, children and spouses: the relations whose death reshapes a person.</summary>
        static List<Pawn> CollectDirectFamily(Pawn pawn)
        {
            var family = new List<Pawn>();
            if (pawn?.relations == null) return family;
            void Add(Pawn member)
            {
                if (member != null && member != pawn && !family.Contains(member)) family.Add(member);
            }
            Add(pawn.GetFather());
            Add(pawn.GetMother());
            foreach (Pawn child in pawn.relations.Children) Add(child);
            foreach (Pawn spouse in pawn.GetSpouses(false)) Add(spouse);
            return family;
        }

        void OfferPair(Pawn first, Pawn second, string eventId, System.Func<Pawn, string> contextFor, int now)
        {
            if (first == null || second == null || first == second) return;
            OfferEvolve(first, eventId + ":" + first.thingIDNumber + ":" + second.thingIDNumber, contextFor(second), now);
            OfferEvolve(second, eventId + ":" + second.thingIDNumber + ":" + first.thingIDNumber, contextFor(first), now);
        }

        void OfferEvolve(Pawn pawn, string key, string context, int now)
        {
            if (!AutoEvolveEligibility.IsEligible(pawn, Settings)) return;
            Offer(pawn, PersonaAutomationKind.Evolve, false, key, context, now);
        }

        void Offer(Pawn pawn, PersonaAutomationKind kind, bool roleChange, string key, string context, int now)
        {
            if (_recent.IsDuplicate(key, now)) return;
            if (_owner.Enqueue(pawn, kind, roleChange, key, context))
                _recent.MarkAccepted(key, now);
        }
    }
}
