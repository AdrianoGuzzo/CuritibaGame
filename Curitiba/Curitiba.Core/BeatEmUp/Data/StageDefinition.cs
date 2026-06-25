using System.Collections.Generic;

namespace Curitiba.Core.BeatEmUp
{
    /// <summary>
    /// The data-driven description of a beat 'em up stage: corridor geometry, parallax
    /// backdrop, per-archetype combat tuning, enemy personality profiles and the ordered
    /// list of sections (each with its waves and set pieces). This is the canonical format
    /// the game loads at runtime and the in-game editor reads/writes; a Tiled <c>.tmj</c>
    /// can be imported into it. <see cref="CapaoRasoDefault"/> reproduces the original
    /// hardcoded stage 1:1 and is used as a fallback when the JSON is missing or invalid.
    /// </summary>
    public sealed class StageDefinition
    {
        public int SchemaVersion { get; set; } = 1;
        public string Id { get; set; } = "capao-raso";

        /// <summary>Localization key (member of <c>Resources</c>) for the stage name shown in the HUD.</summary>
        public string DisplayNameKey { get; set; } = "StageCapaoRaso";

        public CorridorDef Corridor { get; set; } = new CorridorDef();
        public BackdropDef Backdrop { get; set; } = new BackdropDef();
        public TuningSet Tuning { get; set; } = new TuningSet();
        public Dictionary<string, PersonalityDef> Personalities { get; set; } = new Dictionary<string, PersonalityDef>();
        public List<SectionDef> Sections { get; set; } = new List<SectionDef>();

        /// <summary>The original Capão Raso stage, byte-for-byte equivalent to the former hardcoded setup.</summary>
        public static StageDefinition CapaoRasoDefault()
        {
            return new StageDefinition
            {
                SchemaVersion = 1,
                Id = "capao-raso",
                DisplayNameKey = "StageCapaoRaso",
                Corridor = new CorridorDef { Top = 300f, Bottom = 448f, CurbHeight = 14f },
                Backdrop = new BackdropDef
                {
                    SkyAsset = "Backgrounds/Stage1/Sky",
                    BuildingsAsset = "Backgrounds/Stage1/Buildings",
                    HorizonY = 300f,
                    SkyScroll = 0.2f,
                    BuildingsScroll = 0.5f,
                    BuildingsHeight = 360,
                },
                Tuning = new TuningSet
                {
                    Sofia = FighterTuning.SofiaDefaults(),
                    PiaLoco = FighterTuning.PiaLocoDefaults(),
                },
                Personalities = new Dictionary<string, PersonalityDef>
                {
                    ["Aggressive"] = new PersonalityDef { AttackChance = 0.92f, AttackCooldown = 0.8f, PreferredDistance = 40f, RunDistance = 320f, RunSpeedMultiplier = 1.6f },
                    ["Defensive"] = new PersonalityDef { AttackChance = 0.4f, AttackCooldown = 1.8f, PreferredDistance = 120f, RunDistance = 320f, RunSpeedMultiplier = 1.4f },
                    ["Runner"] = new PersonalityDef { AttackChance = 0.72f, AttackCooldown = 1.2f, PreferredDistance = 60f, RunDistance = 220f, RunSpeedMultiplier = 1.9f },
                    ["Balanced"] = new PersonalityDef { AttackChance = 0.7f, AttackCooldown = 1.3f, PreferredDistance = 60f, RunDistance = 300f, RunSpeedMultiplier = 1.5f },
                },
                Sections = new List<SectionDef>
                {
                    new SectionDef
                    {
                        BackgroundAsset = "Backgrounds/Stage1/Gate",
                        FallbackWidth = 800f,
                        ParallaxBackdrop = true,
                        RepeatX = 1,
                        CurbY = 325f,
                        DrivewayLeft = 320f,
                        DrivewayRight = 600f,
                        SpawnPoints = new List<SpawnPointDef>
                        {
                            new SpawnPointDef { Id = "left", Name = "LeftEntrance", Type = "Left", Y = 400f },
                            new SpawnPointDef { Id = "right", Name = "RightEntrance", Type = "Right", Y = 360f },
                        },
                        Waves = new List<WaveDef>
                        {
                            new WaveDef { LockCameraX = 0f, EnemyCount = 2, HitsToKnockdown = 3 },
                            new WaveDef { LockCameraX = 0f, EnemyCount = 3, HitsToKnockdown = 4 },
                        },
                    },
                    new SectionDef
                    {
                        BackgroundAsset = "Backgrounds/Stage1/WallInfinite",
                        FallbackWidth = 1600f,
                        ParallaxBackdrop = true,
                        RepeatX = 3,
                        CurbY = 335f,
                        SpawnPoints = new List<SpawnPointDef>
                        {
                            new SpawnPointDef { Id = "left", Name = "LeftEntrance", Type = "Left", Y = 400f },
                            new SpawnPointDef { Id = "right", Name = "RightEntrance", Type = "Right", Y = 360f },
                        },
                        Waves = new List<WaveDef>
                        {
                            new WaveDef { LockCameraX = 400f, EnemyCount = 3, HitsToKnockdown = 4 },
                            new WaveDef { LockCameraX = 1000f, EnemyCount = 4, HitsToKnockdown = 5 },
                        },
                    },
                },
            };
        }
    }

    /// <summary>Vertical walkable corridor and curb height (shared by every section).</summary>
    public sealed class CorridorDef
    {
        public float Top { get; set; } = 300f;
        public float Bottom { get; set; } = 448f;
        public float CurbHeight { get; set; } = 14f;
    }

    /// <summary>Parallax sky/buildings backdrop drawn behind sections that opt in.</summary>
    public sealed class BackdropDef
    {
        public string SkyAsset { get; set; } = "Backgrounds/Stage1/Sky";
        public string BuildingsAsset { get; set; } = "Backgrounds/Stage1/Buildings";
        public float HorizonY { get; set; } = 300f;
        public float SkyScroll { get; set; } = 0.2f;
        public float BuildingsScroll { get; set; } = 0.5f;
        public int BuildingsHeight { get; set; } = 360;
    }

    /// <summary>Per-archetype combat tuning for the stage.</summary>
    public sealed class TuningSet
    {
        public FighterTuning Sofia { get; set; } = FighterTuning.SofiaDefaults();
        public FighterTuning PiaLoco { get; set; } = FighterTuning.PiaLocoDefaults();
    }

    /// <summary>Knobs for one enemy personality (mirrors <see cref="EnemyProfile"/>).</summary>
    public sealed class PersonalityDef
    {
        public float AttackChance { get; set; }
        public float AttackCooldown { get; set; }
        public float PreferredDistance { get; set; }
        public float RunDistance { get; set; }
        public float RunSpeedMultiplier { get; set; }
    }

    /// <summary>One section of the stage (a scrolling corridor or a single fixed screen).</summary>
    public sealed class SectionDef
    {
        public string BackgroundAsset { get; set; }
        public float FallbackWidth { get; set; } = 800f;
        public bool ParallaxBackdrop { get; set; } = true;
        public int RepeatX { get; set; } = 1;
        public float CurbY { get; set; }
        public float DrivewayLeft { get; set; }
        public float DrivewayRight { get; set; }

        /// <summary>How Sofia enters this section (start of stage or transition from the previous one).
        /// Defaults reproduce the original behaviour (a fixed point at the left, mid-corridor).</summary>
        public EntryDef Entry { get; set; } = new EntryDef();

        /// <summary>Named entry points enemies can spawn from (left/right edge or a custom world point).</summary>
        public List<SpawnPointDef> SpawnPoints { get; set; } = new List<SpawnPointDef>();

        public List<WaveDef> Waves { get; set; } = new List<WaveDef>();
        public List<SetPieceDef> SetPieces { get; set; } = new List<SetPieceDef>();
    }

    /// <summary>
    /// How Sofia is placed when a section loads. <see cref="Mode"/> is one of:
    /// <list type="bullet">
    /// <item><c>Fixed</c> — stands at <see cref="X"/>/<see cref="Y"/>.</item>
    /// <item><c>Carry</c> — keeps the lane (Y) she left the previous section on, entering at <see cref="X"/>
    /// (falls back to Fixed on the first section). <see cref="CarryProportional"/> carries Y as a fraction
    /// of the corridor instead of an absolute world Y.</item>
    /// <item><c>Fall</c> — drops in from <see cref="FallHeight"/> px above the target and lands.</item>
    /// <item><c>Door</c> — appears at the target facing <see cref="Facing"/> and walks
    /// <see cref="WalkInDistance"/> px inward before control is handed over.</item>
    /// </list>
    /// <see cref="Y"/> 0 means "auto" (mid-corridor). Defaults reproduce the old fixed 90/mid placement.
    /// </summary>
    public sealed class EntryDef
    {
        public string Mode { get; set; } = "Fixed";        // Fixed | Carry | Fall | Door
        public float X { get; set; } = 90f;                 // Sofia's feet (the spot she ends up standing on)
        public float Y { get; set; }                        // 0 = auto (mid-corridor)
        public float FallHeight { get; set; } = 260f;       // Fall: px above the target where the drop starts
        public float WalkInDistance { get; set; } = 70f;    // Door: px Sofia walks inward before control
        public string Facing { get; set; } = "Right";       // initial facing (Right | Left)
        public bool CarryProportional { get; set; }         // Carry: keep Y as a corridor fraction (vs absolute Y)
    }

    /// <summary>A named entry point for a section. <see cref="Type"/> is "Left", "Right" or "Custom".
    /// For Left/Right only <see cref="Y"/> (the lane) matters; Custom uses the full <see cref="X"/>/<see cref="Y"/>.</summary>
    public sealed class SpawnPointDef
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Type { get; set; } = "Custom";
        public float X { get; set; }
        public float Y { get; set; }
    }

    /// <summary>
    /// One combat wave. When <see cref="Spawns"/> is non-empty the enemies are placed by those
    /// authored entries (a <c>SpawnPoint</c> reference or explicit X/Y target); otherwise
    /// <see cref="EnemyCount"/> enemies are placed by the legacy procedural spread. Either way the
    /// enemies are born off-screen and walk in. <see cref="Delay"/> postpones the wave's spawn after
    /// it is triggered (camera reaches the lock / previous wave cleared).
    /// </summary>
    public sealed class WaveDef
    {
        public float LockCameraX { get; set; }

        /// <summary>Seconds to wait before this wave's enemies appear, once the wave is triggered.</summary>
        public float Delay { get; set; }

        public int EnemyCount { get; set; }
        public int HitsToKnockdown { get; set; } = 3;
        public List<SpawnDef> Spawns { get; set; } = new List<SpawnDef>();
    }

    /// <summary>One authored enemy within a wave.</summary>
    public sealed class SpawnDef
    {
        /// <summary>Enemy type resolved by the EnemyFactory (e.g. "piaLoco"). Falls back to <see cref="Template"/>.</summary>
        public string Type { get; set; }

        /// <summary>Legacy alias for <see cref="Type"/> (kept for older JSON / the tuning lookup).</summary>
        public string Template { get; set; } = "piaLoco";

        public string Personality { get; set; } = "Balanced";

        /// <summary>Spawn-point reference: a point id/name, or "random" / "random:Left" / "random:Right".
        /// Empty means the enemy enters from the screen edge nearest its X/Y.</summary>
        public string SpawnPoint { get; set; }

        /// <summary>Walk-in target inside the play area (the destination, not the birth point). 0,0 = auto.</summary>
        public float X { get; set; }
        public float Y { get; set; }
    }

    /// <summary>A decorative or solid scene object placed in world space (e.g. a parked car).</summary>
    public sealed class SetPieceDef
    {
        public string Asset { get; set; }
        public float X { get; set; }
        public float Y { get; set; }

        /// <summary>Depth-sort the piece with the fighters by its Y (so combatants can pass in front/behind).</summary>
        public bool DepthSortByY { get; set; }

        /// <summary>Reserved for future collision; ignored for now (the piece is drawn only).</summary>
        public bool Solid { get; set; }
    }
}
