# Personas interior inventory — Phase 7.5.10 Wave A

Measured against `RimAI.Communication.Personas` at Wave A inventory time.
Production scope: `Source/**/*.cs` excluding `obj`/`bin`.
**No Waves B–D applied yet.**

Authoritative starting product HEADs (post-7.5.9):

| Repo | HEAD |
| --- | --- |
| Core | `f67d184` |
| Communication | `dcffaac` |
| Memory | `cd5d316` |
| Personas | `3a63a31` (7.5.8 composition) |
| integration | `ecc0685` |

---

## Starting metrics

| Metric | Value |
| --- | ---: |
| Production files | 33 |
| Production LOC | 6890 |
| `.Instance` usages | 1 (`PromptManager.Instance` — discarded read) |
| `.Current` usages | 14 |
| Static `Current` decls | 1 (`PersonasComposition.Current`) |
| `Lazy<T>` service graphs | 0 |
| Direct Verse `Log.*` | **0** |
| `RimAiLog.*` | 44 |
| Catch-all (`Exception` + bare) | 47 (by_module baseline) |
| Catch TEMPORARY (exception-file) | 21 (DOMAIN 5, HARMONY-labeled 14 residual) |
| Direct `File.*` / `Directory.*` | 0 / 0 |
| `LocalStorage.Current` | 10 |
| Ambient Personas by_module | 1 (`PersonasComposition.Current`) |
| Oversized TEMPORARY (WARNING) | 3 (`Window_BatchDirector` 619, `Window_LibraryManager` 585, `PersonasSettings` 562) |
| `AiRequestArbiter` | 0 |
| Live `[HarmonyPatch]` | 0 |

### Largest files (LOC)

| LOC | File |
| ---: | --- |
| 698 | `PersonasSettings.cs` |
| 632 | `Window_BatchDirector.cs` |
| 595 | `Window_LibraryManager.cs` |
| 465 | `Director/DirectorCharacterDataBuilder.cs` |
| 423 | `DirectorDataEngine.cs` |
| 408 | `DirectorPawnInfoFormatter.cs` |
| 363 | `PersonasMod.cs` |
| 337 | `Director/DirectorPersonalityGenerator.cs` |

---

## Responsibility map

```text
PersonasMod (handshake + Settings + LongEvent DirectorStartup)
    ↓ (handshake callback)
PersonasComposition.Start
    ↓ Harmony PatchAll (no live patches) + CommunicationBridge.Register
    ↓
Communication hooks:
  PersonaService.OverrideGenerator
  Hediff_Persona.SelectPersonality
  TalkLifecycle events → Patch_PromptService / DirectorContextTracker
  PersonaEditorChrome
    ↓
Communication-owned Hediff_Persona (RimTalk_PersonaData)  ← pawn persona SoT
    ↓
Prompt paths (see projection graph)
```

### Composition / lifecycle

- `PersonasComposition` — Start: PatchAll + `CommunicationBridge.Register` + module registry. **Stop is flag-only** (no Unpatch, no bridge unwind, no generator clear).
- `PersonasMod` — handshake `TryActivate` → `Start`; separately `LongEventHandler.ExecuteWhenFinished(DirectorStartup.Initialize)`.
- `DirectorApiAdapter` — `[StaticConstructorOnStartup]` + LongEvent Scriban registration **outside** composition Start.
- `DirectorInitializer` (GameComponent), `DirectorWorldComponent` (WorldComponent) — auto-discovered Verse components.
- `MainButtonWorker_Director` — class exists; **no MainButtonDef XML** found in workspace (likely donor-layout loss).

### Resolution (pawn → persona text)

Canonical storage is **Communication** `Hediff_Persona`, not Personas.

| Path | Owner | Behavior |
| --- | --- | --- |
| `Hediff_Persona.GetOrAddNew` | Communication | Create hediff; humanlike uses `SelectPersonality` hook |
| `Patch_GetOrAddNew.AssignViaRulesOrRandom` | Personas | Rules → enabled presets → vanilla `Constant.Personalities` |
| `DirectorInitializer.ApplyRulesToExistingPawns` | Personas | Backfill empty Personality on load |
| `PresetSynchronizer.SyncToRimTalk` | Personas | Overwrites `Constant.Personalities` with enabled user presets |
| Non-human constants | Communication | Animal/Mech/NonHuman |

Competing / ambient: `recentAssignments` static de-dupe; dead commented `RuleExecutor` (wrong Def name `RimTalk_Persona`).

### Prompt projection (critical)

**No typed Persona Projection on `PromptContext`.** Persona remains a Hediff string, re-resolved at render time.

```text
PATH TALK (CreatePawnContext)
  Cache.Get(pawn)?.Personality  → embed "Personality: {raw}"
  → TalkLifecycle.TransformPawnContext
  → Patch_PromptService.Transform
       re-GetOrAddNew(pawn)
       if raw contains "{{" and result contains raw:
         Scriban-render + string.Replace

PATH SCRIBAN {{pawn.personality}}
  magic Cache personality
  → ContextHookRegistry Override (DirectorApiAdapter)
       re-read Hediff
       new PromptContext(allPawns from ThreadStatic DirectorContextTracker)
       Scriban.Render(template)

PATH EXTRA
  {{pawn.d_*}} Director variables (eager providers)
  {{director_notes}}, {{smart_history}}
```

**Wart (Memory 7.5.9 analogue):** rich/template persona is not prepared once onto prompt state. Late re-lookup + string replace / second Scriban pass can diverge from what Communication already embedded. `VariableStore` read in Override is discarded.

Personas also calls Memory via `DirectorMemoryContext` (`GetContext` / `GetKnowledge`) and does **not** use 7.5.9 `TypedMemoryProjections`.

### Persistence

| Store | Mechanism | Compatibility freeze |
| --- | --- | --- |
| Mod settings | `PersonasSettings.ExposeData` | presets, rules, prompts, filters, notes, migration flags |
| Pawn persona | Communication hediff Scribe | `Personality`, `TalkInitiationWeight`; Def `RimTalk_PersonaData` |
| Evolve snapshots | `DirectorWorldComponent` | ticks, `dataSnapshots`, daily diffs by `thingIDNumber` |
| Library export/import | `LocalStorage` | `{SaveDataFolder}/PersonaDirector/Persona_Library_*.xml`; temp import under Temp |

No `AtomicFileWriter`. No live `File.*`. Formats must not change casually in Waves B–D.

### Generation / AI

- `AiRequestArbiter` **unused**.
- All gen: `AIService.Query<PersonalityData>` (Communication).
- Override: `DirectorPersonaGenerator` always claims generation → `DirectorUtils.GeneratePersonalityTask`.
- Evolve: `Task.Run` + `.Result` (UI/thread hazard — inventory only).

### Host / external callers

| Module | Relationship |
| --- | --- |
| Communication | Platform: hediff, PersonaService hooks, Scriban personality, editor chrome |
| Relations | Reflects Communication `PersonaService` only — no Personas assembly ref |
| Memory | Called **by** Personas (`DirectorMemoryContext`); no reverse ref |
| Voices | Shares `PersonaEditorChrome.DrawFooter` event |

---

## Observed warts (freeze in characterization; do not silently "fix" in Wave A tests)

1. **Late Talk Transform re-lookup** — `Patch_PromptService.Transform` re-reads Hediff and string-replaces after Communication already embedded raw text.
2. **Dual Scriban Override path** — `{{pawn.personality}}` Override does not run on Talk `CreatePawnContext` (no `AppendWithHook` for Personality).
3. **No `PromptContext` persona projection** — unlike Memory OPTION A.
4. **ThreadStatic `DirectorContextTracker`** — ambient multi-pawn list for template render.
5. **`TempCurrentPersona` global** — evolve-only window; contamination risk if misused.
6. **`Stop()` incomplete** — generators/hooks can remain registered after Stop.
7. **Dead code** — `RuleExecutor` commented, empty `DirectorVariableProvider`, unused `GetDataByKey` / `AddMenu`.
8. **Missing MainButtonDef** — worker exists, no Def XML.
9. **Memory bypass** — Director Memory/Knowledge path ignores 7.5.9 typed projections.

---

## Target architecture (planned Waves B–D — not implemented)

```text
PersonasComposition
  → owns CommunicationBridge register/unregister
  → owns Scriban Director variable registration
  → owns DirectorStartup / library sync (handshake-gated)
  → Persona application (assign / generate / evolve use-cases)
  → PersonaResolver (one authoritative path)
  → PersonaProjection → PromptContext (Talk + Scriban present; no late divergent lookup)
  → persistence (settings + LocalStorage sidecars; Communication hediff remains pawn SoT)
```

Public boundary: prefer Communication hooks + narrow Personas capability; do not invent DI.

---

## Characterization tests (Wave A)

Core: `Stage7510PersonaProjectionCharacterizationTests`

Freeze **today's** request/projection shapes and warts (mirrors), including:

- Talk path: raw embed then Transform re-lookup + replace when `{{` present
- Transform no-op when no `{{` or raw not in result
- Scriban Override vs Talk path divergence (Override not on CreatePawnContext)
- Absence of typed PromptContext persona projection field contract
- Multi-pawn isolation expectation for ThreadStatic context (documented)

Rule: goldens snapshot today. Suspected bugs are listed above — not corrected in Wave A assertions.

---

## Wave B–D backlog (blocked pending review)

1. Real Start/Stop ownership (bridge, Scriban register, library sync; clear hooks on Stop).
2. One PersonaResolver; converge SelectPersonality / backfill / sync.
3. Precompute PersonaProjection onto prompt context; remove late Transform / Override divergence.
4. Delete dead RuleExecutor / empty provider / unused GetDataByKey.
5. Decide MainButtonDef restore vs drop ShowMainButton.
6. Touch-site logging already RimAiLog; burn DOMAIN catch when touching.
7. Align Director Memory access with 7.5.9 typed projections (or document intentional separate path).
