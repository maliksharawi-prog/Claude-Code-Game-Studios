using System;
using System.Collections.Generic;
using NUnit.Framework;
using SweetCascade.Domain.Levels;

namespace SweetCascade.Domain.Tests.Levels
{
    /// <summary>
    /// Edit-Mode unit tests for the schema v1 <see cref="LevelData"/> Domain POCO —
    /// construction, optional-field defaults, the closed <see cref="ObjectiveType"/> enum, and
    /// the additive <see cref="LevelData.DisplayName"/> field (TR-ldf-004).
    /// </summary>
    /// <remarks>
    /// Story: E02-005 (leveldata-poco-schema). Governing docs:
    /// <c>design/gdd/level-data-format.md</c> §2/§3; <c>docs/architecture/architecture.md</c> §6.
    /// Validation rules (V1–V19) and the V8 connectivity flood-fill are Story 006/007 — not
    /// exercised here; this file covers only the POCO shape/defaults/closed-enum/additive-field
    /// contract Story 005 owns.
    /// </remarks>
    [TestFixture]
    public class LevelData_Tests
    {
        // ------------------------------------------------------------------
        // Shared valid fixtures.
        // ------------------------------------------------------------------

        private static readonly string[] ValidCellMask = { "11111", "11111", "11111" };
        private static readonly PrePlacedPiece[] ValidPrePlacedPieces = Array.Empty<PrePlacedPiece>();
        private static readonly string[] ValidColorPool = { "strawberry", "citrus" };
        private static readonly Objective[] ValidObjectives = { new ScoreTargetObjective(500) };

        private static LevelData NewLevelData(
            string levelId,
            string region,
            IReadOnlyList<string> cellMask,
            IReadOnlyList<PrePlacedPiece> prePlacedPieces,
            IReadOnlyList<string> colorPool,
            IReadOnlyList<Objective> objectives)
        {
            return new LevelData(
                schemaVersion: 1,
                levelId: levelId,
                region: region,
                displayNumber: 1,
                gridWidth: 5,
                gridHeight: 3,
                cellMask: cellMask,
                prePlacedPieces: prePlacedPieces,
                colorPool: colorPool,
                moveLimit: 10,
                objectives: objectives,
                star1Score: 100,
                star2Score: 200,
                star3Score: 300,
                rngSeed: -1,
                displayName: null);
        }

        /// <summary>Minimal valid as-authored fixture; individual optional fields can be overridden per test.</summary>
        private static LevelDataRaw ValidRaw(
            string? displayName = null,
            IReadOnlyList<string>? unrecognizedOptionalFieldNames = null,
            IReadOnlyList<string>? cellMask = null,
            IReadOnlyList<PrePlacedPiece>? prePlacedPieces = null,
            int? rngSeed = null,
            IReadOnlyList<ObjectiveRaw>? objectives = null,
            int gridWidth = 5,
            int gridHeight = 3)
        {
            return new LevelDataRaw
            {
                SchemaVersion = LevelDataConstants.CurrentSchemaVersion,
                LevelId = "reg-001",
                Region = "reg",
                DisplayNumber = 1,
                GridWidth = gridWidth,
                GridHeight = gridHeight,
                CellMask = cellMask,
                PrePlacedPieces = prePlacedPieces,
                ColorPool = new[] { "strawberry", "citrus" },
                MoveLimit = 10,
                Objectives = objectives ?? new[] { new ObjectiveRaw(LevelDataConstants.ObjectiveTypeScoreTarget, Target: 500) },
                Star1Score = 100,
                Star2Score = 200,
                Star3Score = 300,
                RngSeed = rngSeed,
                DisplayName = displayName,
                UnrecognizedOptionalFieldNames = unrecognizedOptionalFieldNames,
            };
        }

        // ------------------------------------------------------------------
        // AC-1: full-field typed POCO.
        // ------------------------------------------------------------------

        [Test]
        public void Test_LevelData_Constructor_AllFieldsPresent_WithSpecifiedTypesAndValues()
        {
            var cellMask = new[] { "111", "111", "111" };
            var prePlaced = new[] { new PrePlacedPiece(0, 0, "strawberry") };
            var colorPool = new[] { "strawberry", "citrus", "lemon" };
            var objectives = new Objective[] { new ScoreTargetObjective(1000), new CollectColorObjective("strawberry", 5) };

            var data = new LevelData(
                schemaVersion: 1,
                levelId: "reg-001",
                region: "reg",
                displayNumber: 1,
                gridWidth: 7,
                gridHeight: 7,
                cellMask: cellMask,
                prePlacedPieces: prePlaced,
                colorPool: colorPool,
                moveLimit: 20,
                objectives: objectives,
                star1Score: 1000,
                star2Score: 2000,
                star3Score: 3000,
                rngSeed: -1,
                displayName: "Sweet Start");

            Assert.That(data.SchemaVersion, Is.EqualTo(1));
            Assert.That(data.LevelId, Is.EqualTo("reg-001"));
            Assert.That(data.Region, Is.EqualTo("reg"));
            Assert.That(data.DisplayNumber, Is.EqualTo(1));
            Assert.That(data.GridWidth, Is.EqualTo(7));
            Assert.That(data.GridHeight, Is.EqualTo(7));
            Assert.That(data.CellMask, Is.EqualTo(cellMask));
            Assert.That(data.PrePlacedPieces, Is.EqualTo(prePlaced));
            Assert.That(data.ColorPool, Is.EqualTo(colorPool));
            Assert.That(data.MoveLimit, Is.EqualTo(20));
            Assert.That(data.Objectives, Is.EqualTo(objectives));
            Assert.That(data.Star1Score, Is.EqualTo(1000));
            Assert.That(data.Star2Score, Is.EqualTo(2000));
            Assert.That(data.Star3Score, Is.EqualTo(3000));
            Assert.That(data.RngSeed, Is.EqualTo(-1));
            Assert.That(data.DisplayName, Is.EqualTo("Sweet Start"));
        }

        [Test]
        public void Test_LevelData_Constructor_NullLevelId_Throws() =>
            Assert.Throws<ArgumentNullException>(() =>
                NewLevelData(null!, "reg", ValidCellMask, ValidPrePlacedPieces, ValidColorPool, ValidObjectives));

        [Test]
        public void Test_LevelData_Constructor_NullRegion_Throws() =>
            Assert.Throws<ArgumentNullException>(() =>
                NewLevelData("reg-001", null!, ValidCellMask, ValidPrePlacedPieces, ValidColorPool, ValidObjectives));

        [Test]
        public void Test_LevelData_Constructor_NullCellMask_Throws() =>
            Assert.Throws<ArgumentNullException>(() =>
                NewLevelData("reg-001", "reg", null!, ValidPrePlacedPieces, ValidColorPool, ValidObjectives));

        [Test]
        public void Test_LevelData_Constructor_NullPrePlacedPieces_Throws() =>
            Assert.Throws<ArgumentNullException>(() =>
                NewLevelData("reg-001", "reg", ValidCellMask, null!, ValidColorPool, ValidObjectives));

        [Test]
        public void Test_LevelData_Constructor_NullColorPool_Throws() =>
            Assert.Throws<ArgumentNullException>(() =>
                NewLevelData("reg-001", "reg", ValidCellMask, ValidPrePlacedPieces, null!, ValidObjectives));

        [Test]
        public void Test_LevelData_Constructor_NullObjectives_Throws() =>
            Assert.Throws<ArgumentNullException>(() =>
                NewLevelData("reg-001", "reg", ValidCellMask, ValidPrePlacedPieces, ValidColorPool, null!));

        [Test]
        public void Test_LevelData_Constructor_ValidArgs_DoesNotThrow() =>
            Assert.DoesNotThrow(() =>
                NewLevelData("reg-001", "reg", ValidCellMask, ValidPrePlacedPieces, ValidColorPool, ValidObjectives));

        // ------------------------------------------------------------------
        // AC-2: optional-field defaults.
        // ------------------------------------------------------------------

        [Test]
        public void Test_FromRaw_OmittingCellMask_DefaultsToFullRectangle()
        {
            LevelDataParseResult result = LevelData.FromRaw(ValidRaw(gridWidth: 5, gridHeight: 3));

            Assert.That(result.Data.CellMask, Has.Count.EqualTo(3));
            foreach (string row in result.Data.CellMask)
            {
                Assert.That(row, Is.EqualTo("11111"));
            }
        }

        [Test]
        public void Test_FromRaw_OmittingPrePlacedPieces_DefaultsToEmpty()
        {
            LevelDataParseResult result = LevelData.FromRaw(ValidRaw());
            Assert.That(result.Data.PrePlacedPieces, Is.Empty);
        }

        [Test]
        public void Test_FromRaw_OmittingRngSeed_DefaultsToMinusOne()
        {
            LevelDataParseResult result = LevelData.FromRaw(ValidRaw());
            Assert.That(result.Data.RngSeed, Is.EqualTo(LevelDataConstants.DefaultRngSeed));
            Assert.That(result.Data.RngSeed, Is.EqualTo(-1));
        }

        [Test]
        public void Test_FromRaw_OmittingAllThreeOptionalFields_NoErrorRaised()
        {
            Assert.DoesNotThrow(() => LevelData.FromRaw(ValidRaw()));
        }

        [Test]
        public void Test_FromRaw_SuppliedCellMask_IsNotOverwrittenByDefault()
        {
            var explicitMask = new[] { "01110", "01110", "01110" };
            LevelDataParseResult result = LevelData.FromRaw(ValidRaw(cellMask: explicitMask));
            Assert.That(result.Data.CellMask, Is.EqualTo(explicitMask));
        }

        [Test]
        public void Test_FromRaw_SuppliedPrePlacedPieces_IsNotOverwrittenByDefault()
        {
            var explicitPieces = new[] { new PrePlacedPiece(0, 0, "strawberry") };
            LevelDataParseResult result = LevelData.FromRaw(ValidRaw(prePlacedPieces: explicitPieces));
            Assert.That(result.Data.PrePlacedPieces, Is.EqualTo(explicitPieces));
        }

        [Test]
        public void Test_FromRaw_SuppliedRngSeed_IsNotOverwrittenByDefault()
        {
            LevelDataParseResult result = LevelData.FromRaw(ValidRaw(rngSeed: 42));
            Assert.That(result.Data.RngSeed, Is.EqualTo(42));
        }

        [TestCase(3, 3)]
        [TestCase(9, 9)]
        [TestCase(5, 3)]
        public void Test_FromRaw_FullRectangleMask_HasCorrectDimensionsAndContent(int gridWidth, int gridHeight)
        {
            LevelDataParseResult result = LevelData.FromRaw(ValidRaw(gridWidth: gridWidth, gridHeight: gridHeight));

            Assert.That(result.Data.CellMask, Has.Count.EqualTo(gridHeight));
            string expectedRow = new string(LevelDataConstants.PlayableCellChar, gridWidth);
            foreach (string row in result.Data.CellMask)
            {
                Assert.That(row, Is.EqualTo(expectedRow));
                Assert.That(row.Length, Is.EqualTo(gridWidth));
            }
        }

        [Test]
        public void Test_FromRaw_NullRaw_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => LevelData.FromRaw(null!));
        }

        // ------------------------------------------------------------------
        // AC-3: additive display_name (TR-ldf-004) — no schema_version bump.
        // ------------------------------------------------------------------

        [Test]
        public void Test_FromRaw_DisplayNamePresent_RoundTrips_NoSchemaVersionBump()
        {
            LevelDataParseResult result = LevelData.FromRaw(ValidRaw(displayName: "Sweet Start"));
            Assert.That(result.Data.DisplayName, Is.EqualTo("Sweet Start"));
            Assert.That(result.Data.SchemaVersion, Is.EqualTo(LevelDataConstants.CurrentSchemaVersion));
        }

        [Test]
        public void Test_FromRaw_DisplayNameAbsent_DefaultsToNull_NoSchemaVersionBump()
        {
            LevelDataParseResult result = LevelData.FromRaw(ValidRaw());
            Assert.That(result.Data.DisplayName, Is.Null);
            Assert.That(result.Data.SchemaVersion, Is.EqualTo(LevelDataConstants.CurrentSchemaVersion));
        }

        [Test]
        public void Test_FromRaw_UnknownOptionalField_LoadsWithWarning_NeverRejected()
        {
            LevelDataParseResult result = LevelData.FromRaw(ValidRaw(unrecognizedOptionalFieldNames: new[] { "some_future_field" }));

            Assert.That(result.Warnings, Has.Count.EqualTo(1));
            Assert.That(result.Warnings[0], Does.Contain("some_future_field"));
            Assert.That(result.Data.SchemaVersion, Is.EqualTo(LevelDataConstants.CurrentSchemaVersion));
        }

        [Test]
        public void Test_FromRaw_MultipleUnknownOptionalFields_EachProducesAWarning()
        {
            LevelDataParseResult result = LevelData.FromRaw(
                ValidRaw(unrecognizedOptionalFieldNames: new[] { "field_a", "field_b" }));

            Assert.That(result.Warnings, Has.Count.EqualTo(2));
        }

        [Test]
        public void Test_FromRaw_NoUnknownOptionalFields_ProducesNoWarnings()
        {
            LevelDataParseResult result = LevelData.FromRaw(ValidRaw());
            Assert.That(result.Warnings, Is.Empty);
        }

        // ------------------------------------------------------------------
        // AC-4: closed-enum objective shape.
        // ------------------------------------------------------------------

        [Test]
        public void Test_FromRaw_ScoreTargetObjective_MapsToTypedRecord()
        {
            LevelDataParseResult result = LevelData.FromRaw(ValidRaw());

            Assert.That(result.Data.Objectives, Has.Count.EqualTo(1));
            var objective = result.Data.Objectives[0] as ScoreTargetObjective;
            Assert.That(objective, Is.Not.Null);
            Assert.That(objective!.Target, Is.EqualTo(500));
            Assert.That(objective.Type, Is.EqualTo(ObjectiveType.ScoreTarget));
        }

        [Test]
        public void Test_FromRaw_CollectColorObjective_MapsToTypedRecord()
        {
            var raw = ValidRaw(objectives: new[]
            {
                new ObjectiveRaw(LevelDataConstants.ObjectiveTypeCollectColor, Color: "citrus", Count: 12),
            });

            LevelDataParseResult result = LevelData.FromRaw(raw);

            var objective = result.Data.Objectives[0] as CollectColorObjective;
            Assert.That(objective, Is.Not.Null);
            Assert.That(objective!.Color, Is.EqualTo("citrus"));
            Assert.That(objective.Count, Is.EqualTo(12));
            Assert.That(objective.Type, Is.EqualTo(ObjectiveType.CollectColor));
        }

        [Test]
        public void Test_FromRaw_ObjectiveListOrder_IsPreserved()
        {
            var objectivesRaw = new[]
            {
                new ObjectiveRaw(LevelDataConstants.ObjectiveTypeCollectColor, Color: "citrus", Count: 12),
                new ObjectiveRaw(LevelDataConstants.ObjectiveTypeScoreTarget, Target: 500),
            };

            LevelDataParseResult result = LevelData.FromRaw(ValidRaw(objectives: objectivesRaw));

            Assert.That(result.Data.Objectives, Has.Count.EqualTo(2));
            Assert.That(result.Data.Objectives[0], Is.TypeOf<CollectColorObjective>());
            Assert.That(result.Data.Objectives[1], Is.TypeOf<ScoreTargetObjective>());
        }

        [Test]
        public void Test_FromRaw_UnrecognizedObjectiveType_ThrowsArgumentException_NeverPartiallyLoaded()
        {
            var raw = ValidRaw(objectives: new[] { new ObjectiveRaw("collect_bomb_candy") });
            Assert.Throws<ArgumentException>(() => LevelData.FromRaw(raw));
        }

        [Test]
        public void Test_FromRaw_ScoreTargetMissingTarget_Throws()
        {
            var raw = ValidRaw(objectives: new[] { new ObjectiveRaw(LevelDataConstants.ObjectiveTypeScoreTarget) });
            Assert.Throws<ArgumentException>(() => LevelData.FromRaw(raw));
        }

        [Test]
        public void Test_FromRaw_CollectColorMissingColor_Throws()
        {
            var raw = ValidRaw(objectives: new[] { new ObjectiveRaw(LevelDataConstants.ObjectiveTypeCollectColor, Count: 5) });
            Assert.Throws<ArgumentException>(() => LevelData.FromRaw(raw));
        }

        [Test]
        public void Test_FromRaw_CollectColorMissingCount_Throws()
        {
            var raw = ValidRaw(objectives: new[] { new ObjectiveRaw(LevelDataConstants.ObjectiveTypeCollectColor, Color: "citrus") });
            Assert.Throws<ArgumentException>(() => LevelData.FromRaw(raw));
        }

        /// <summary>
        /// AC-3 in story-005: "Objective types are a closed enum with exactly two v1 members."
        /// A future third member requires a <c>schema_version</c> bump (level-data-format.md §3)
        /// — this test is the guard that would fail the day someone quietly adds a third value.
        /// </summary>
        [Test]
        public void Test_ObjectiveType_ClosedEnum_HasExactlyTwoV1Members()
        {
            var values = (ObjectiveType[])Enum.GetValues(typeof(ObjectiveType));
            Assert.That(values, Has.Length.EqualTo(2));
            Assert.That(values, Is.EquivalentTo(new[] { ObjectiveType.ScoreTarget, ObjectiveType.CollectColor }));
        }

        // ------------------------------------------------------------------
        // LevelDataConstants bounds (centralized-config rule).
        // ------------------------------------------------------------------

        [Test]
        public void Test_LevelDataConstants_CurrentSchemaVersion_IsOne()
        {
            Assert.That(LevelDataConstants.CurrentSchemaVersion, Is.EqualTo(1));
        }

        [Test]
        public void Test_LevelDataConstants_SupportedSchemaVersions_IsExactlyOne()
        {
            Assert.That(LevelDataConstants.SupportedSchemaVersions, Is.EqualTo(new[] { 1 }));
        }

        [Test]
        public void Test_LevelDataConstants_MinPlayableCells_Is16()
        {
            Assert.That(LevelDataConstants.MinPlayableCells, Is.EqualTo(16));
        }

        [Test]
        public void Test_LevelDataConstants_DefaultRngSeed_IsMinusOne()
        {
            Assert.That(LevelDataConstants.DefaultRngSeed, Is.EqualTo(-1));
        }

        [Test]
        public void Test_LevelDataConstants_PlayableAndVoidCellChars()
        {
            Assert.That(LevelDataConstants.PlayableCellChar, Is.EqualTo('1'));
            Assert.That(LevelDataConstants.VoidCellChar, Is.EqualTo('0'));
        }

        [Test]
        public void Test_LevelDataConstants_ObjectiveTypeWireValues()
        {
            Assert.That(LevelDataConstants.ObjectiveTypeScoreTarget, Is.EqualTo("score_target"));
            Assert.That(LevelDataConstants.ObjectiveTypeCollectColor, Is.EqualTo("collect_color"));
        }

        [Test]
        public void Test_LevelDataConstants_CanonicalCandyRoster_HasExactlyFiveExpectedMembers()
        {
            Assert.That(LevelDataConstants.CanonicalCandyRoster,
                Is.EquivalentTo(new[] { "strawberry", "citrus", "lemon", "apple", "grape" }));
            Assert.That(LevelDataConstants.CanonicalCandyRoster, Has.Count.EqualTo(5));
        }

        // ------------------------------------------------------------------
        // PrePlacedPiece value semantics.
        // ------------------------------------------------------------------

        [Test]
        public void Test_PrePlacedPiece_RecordEquality_SameValues_AreEqual()
        {
            var a = new PrePlacedPiece(2, 3, "lemon");
            var b = new PrePlacedPiece(2, 3, "lemon");
            Assert.That(a, Is.EqualTo(b));
            Assert.That(a == b, Is.True);
        }

        [Test]
        public void Test_PrePlacedPiece_RecordEquality_DifferentValues_AreNotEqual()
        {
            var a = new PrePlacedPiece(2, 3, "lemon");
            var b = new PrePlacedPiece(2, 4, "lemon");
            Assert.That(a, Is.Not.EqualTo(b));
        }

        [Test]
        public void Test_PrePlacedPiece_Properties_AreAccessible()
        {
            var piece = new PrePlacedPiece(1, 2, "apple");
            Assert.That(piece.Row, Is.EqualTo(1));
            Assert.That(piece.Col, Is.EqualTo(2));
            Assert.That(piece.CandyType, Is.EqualTo("apple"));
        }
    }
}
