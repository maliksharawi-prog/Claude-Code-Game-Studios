namespace SweetCascade.Domain.Tests.Rng
{
    /// <summary>
    /// Embedded golden-vector constants for the RNG determinism regression suite (ADR-004 §5,
    /// Story E02-004 "RNG golden-vector fixture &amp; byte-for-byte regression suite"). Consumed by
    /// <see cref="Mix32_Tests"/>, <see cref="RngStream_Tests"/>, and <see cref="RngService_Tests"/>
    /// so every assertion in those files traces back to a frozen, independently-generated value
    /// rather than a hand-typed literal.
    /// </summary>
    /// <remarks>
    /// <para>
    /// GENERATED -- do not hand-edit a value here to "fix" a failing test; per
    /// <c>rng_golden_v1.json</c>'s own <c>note</c> field, a deliberate constant change is a v2
    /// <c>algorithm_version</c> bump (<c>rng_golden_v2.json</c>), never a silent edit.
    /// </para>
    /// <para>
    /// Sources (JSON is not read at test-run time -- ADR-004 §5/coding-standards.md forbid file
    /// I/O in unit tests -- so both fixtures are embedded here as literal C# constants):
    /// </para>
    /// <list type="bullet">
    /// <item><description><c>src/SweetCascade/Assets/Tests/EditMode/Rng/golden/rng_golden_v1.json</c>
    /// -- the frozen v1 fixture (ADR-004 §5), generated once and reviewed, never edited.</description></item>
    /// <item><description><c>src/SweetCascade/Assets/Tests/EditMode/Rng/golden/rng_golden_v1_draws.json</c>
    /// -- an ADDITIVE-ONLY extension (extra <c>NextInt</c> ranges, <c>special-drop</c> raw draws, two
    /// extra <c>ForkStream</c> vectors) generated for this test-authoring pass; never mutates the v1
    /// fixture.</description></item>
    /// </list>
    /// <para>
    /// Regeneration commands (both fixtures were produced by importing/running
    /// <c>tools/ci/rng_reference.py</c> -- an independent Python reference implementation of the
    /// same ADR-004 §1 primitives -- never by hand-computing a value):
    /// </para>
    /// <code>
    /// python3 tools/ci/rng_reference.py --check-anchors
    /// PYTHONPATH=tools/ci python3 tools/ci/generate_extra_rng_vectors.py \
    ///     --write src/SweetCascade/Assets/Tests/EditMode/Rng/golden/rng_golden_v1_draws.json
    /// </code>
    /// <para>
    /// (<c>generate_extra_rng_vectors.py</c> imports <c>tools/ci/rng_reference.py</c>'s
    /// <c>combine()</c>/<c>mix32()</c>/<c>stream_seed()</c>/<c>RngStream</c>/<c>fnv1a32()</c>
    /// verbatim -- it does not reimplement any primitive -- so <c>rng_reference.py</c>'s own
    /// anchor check covers every value below transitively. This C# file itself was then emitted
    /// from both JSON files programmatically, not hand-transcribed, to eliminate copy/paste risk
    /// across ~900 embedded values.)
    /// </para>
    /// </remarks>
    internal static class GoldenVectors
    {
        // ------------------------------------------------------------------
        // Anchors (rng_golden_v1.json "anchors" -- re-verified directly against Mix32 in
        // Mix32_Tests.cs; duplicated here only so every consumer can reference one source).
        // ------------------------------------------------------------------
        internal const uint AnchorMix32Zero = 0u;
        internal const uint AnchorCombine1007_3 = 1547274724u;
        internal const uint AnchorCombine42_20650 = 653539216u;
        internal const uint AnchorCombine500_1 = 73026539u;

        // ------------------------------------------------------------------
        // F1/F2 master-seed anchors ("seed_derivation.f1_master_seed" / "f2_master_seed").
        // ------------------------------------------------------------------
        internal const int F1LevelId = 1007;
        internal const int F1AttemptNumber = 3;
        internal const uint F1MasterSeedLevel1007Attempt3 = 2824445292u;

        internal const int F2DailyChallengeId = 42;
        internal const int F2CalendarDateUtc = 20650;
        internal const uint F2MasterSeedDaily42Day20650 = 3700795300u;

        // ------------------------------------------------------------------
        // Pinned master seeds ("pinned_master_seeds") -- the 5 seeds every next_raw_64 /
        // next_int_0_4_64 / next_color_5pool_64 / next_float_8 table below is keyed by.
        // ------------------------------------------------------------------
        internal const uint MasterSeedZero = 0u;
        internal const uint MasterSeedOne = 1u;
        internal const uint MasterSeedMaxUint32 = 4294967295u;
        internal const uint MasterSeedF1Anchor = 2824445292u;
        internal const uint MasterSeedF2Anchor = 3700795300u;

        // ------------------------------------------------------------------
        // F3 stream-seed table ("seed_derivation.f3_stream_seed") -- every registered stream's
        // sub-seed for master_seed=500 and master_seed=F1 anchor (StreamRegistry.All order).
        // ------------------------------------------------------------------
        internal static readonly (int StreamId, string StreamName, uint StreamSeed)[] F3StreamSeedsForMasterSeed500 =
        {
            (1, "board-refill", 1673120973u),
            (2, "special-drop", 58363368u),
            (3, "harvest", 2186048106u),
            (4, "events", 2858449688u),
        };

        internal static readonly (int StreamId, string StreamName, uint StreamSeed)[] F3StreamSeedsForMasterSeedF1Anchor =
        {
            (1, "board-refill", 2622563974u),
            (2, "special-drop", 233673590u),
            (3, "harvest", 212562630u),
            (4, "events", 2901609538u),
        };


        // ------------------------------------------------------------------
        // next_raw_64 -- first 64 RngStream.NextRaw() outputs on board-refill, per pinned seed.
        // ------------------------------------------------------------------
        internal static readonly uint[] NextRaw64_Zero =
        {
            15174878u, 2663886830u, 2294397125u, 2162977761u, 2869432047u, 1300273084u, 4197631289u, 1857680028u,
            3309773653u, 2318127978u, 3117998579u, 2682261030u, 2670539168u, 3141324870u, 3757272416u, 4139174952u,
            4008647933u, 3755959933u, 2765687964u, 3848409825u, 3438393316u, 4252108712u, 4269209597u, 3412953491u,
            856921535u, 2045552458u, 1433518841u, 3837246800u, 2904634813u, 2239007271u, 1258932969u, 3098827825u,
            2650600518u, 2082351015u, 1998696211u, 2433011846u, 2911835969u, 3055638044u, 1191043288u, 722726874u,
            4232561188u, 1459343413u, 3907898977u, 2795175590u, 2478767674u, 3312858380u, 2930637822u, 4275262155u,
            3894181503u, 936702609u, 836890723u, 3354356709u, 1369395482u, 3357764899u, 3034020398u, 1555946244u,
            517495890u, 1830007025u, 1288948659u, 890806045u, 3402139134u, 3747355450u, 1640754461u, 850817935u
        };

        internal static readonly uint[] NextRaw64_One =
        {
            2952259119u, 795497042u, 2993528199u, 3126406995u, 1933819390u, 2577012951u, 3528790447u, 3408877747u,
            2262441509u, 2694169260u, 2049065527u, 2734801267u, 179637386u, 2549154316u, 1901049439u, 2139669103u,
            1721918086u, 1789623292u, 147056591u, 2056677404u, 4005165343u, 3777925354u, 351395681u, 235769981u,
            4083142509u, 1254019142u, 3425244816u, 1098299094u, 3990379381u, 4011509377u, 3818215653u, 4255520845u,
            1743535004u, 1864315878u, 1412991195u, 4060958217u, 2972652232u, 3641368023u, 591759021u, 1965999368u,
            3731628679u, 3298818518u, 1349664386u, 1387515007u, 2506659657u, 1618174953u, 2830103151u, 2259785732u,
            2751391845u, 2419182678u, 2817722418u, 1302741549u, 1354118417u, 3784702669u, 641660323u, 1631910509u,
            1160696880u, 2104923470u, 3937195807u, 1453951456u, 967048865u, 684284917u, 1382039522u, 2332422774u
        };

        internal static readonly uint[] NextRaw64_MaxUint32 =
        {
            1317986390u, 139160221u, 2083916571u, 3212050389u, 2871621448u, 3925115372u, 1727305187u, 1697920906u,
            591887076u, 3817427628u, 1542262719u, 3195967214u, 1774806369u, 3633248820u, 313312867u, 857431273u,
            2031778404u, 1724064632u, 2798571598u, 706074507u, 1967596526u, 3456596376u, 3253937811u, 2165448591u,
            158224201u, 18561305u, 1559174889u, 3249567604u, 971212337u, 3200965627u, 917881947u, 3496852018u,
            214172731u, 4095011834u, 2536524172u, 659910804u, 1213473794u, 3122062126u, 2548331107u, 2988634239u,
            2618051629u, 3793953476u, 743745833u, 2402699413u, 2038184516u, 1686768109u, 2124830460u, 3631272632u,
            4269162067u, 1284501747u, 493075811u, 2151576770u, 967652460u, 121305570u, 3706045194u, 1807943213u,
            3179645928u, 429687208u, 412022819u, 2065578232u, 3122226270u, 400344216u, 1252750127u, 3292857193u
        };

        internal static readonly uint[] NextRaw64_F1Anchor =
        {
            3372637527u, 2053492494u, 1710864275u, 4039656135u, 1669242463u, 2714695104u, 2720848055u, 929883111u,
            2304339048u, 1222684364u, 1530098690u, 2143189787u, 1293993998u, 2806963174u, 694519226u, 3413558144u,
            3776442099u, 4094108920u, 3797030906u, 3647528529u, 648746471u, 2834981007u, 777367537u, 282899752u,
            1854327903u, 2018539253u, 1424367490u, 993620232u, 67789938u, 2682382740u, 2676953575u, 4019147699u,
            2832817361u, 2688249708u, 3419994520u, 2465351642u, 1618716203u, 290494300u, 2010886334u, 3924354754u,
            171659193u, 3799093205u, 1811101755u, 257419446u, 4126174467u, 1617665630u, 294963644u, 3610057492u,
            1201166585u, 538897205u, 2275490639u, 3836604226u, 28752603u, 2449950850u, 2136562378u, 4193560030u,
            2541363928u, 3153312353u, 2631858610u, 702010447u, 2010657591u, 2724580743u, 1452630933u, 4121335024u
        };

        internal static readonly uint[] NextRaw64_F2Anchor =
        {
            2993173380u, 4198362411u, 3749064330u, 2309401976u, 3978951551u, 625109003u, 2989665314u, 3960050526u,
            3218204006u, 3843532167u, 1504113541u, 1583706334u, 4245560915u, 2849961326u, 326270393u, 200923298u,
            4240952738u, 1645915865u, 3696019138u, 3130199404u, 769625982u, 3895682574u, 3984540747u, 1422501805u,
            2945617723u, 3769938497u, 3030745542u, 3248454815u, 78173973u, 2171914929u, 1488424824u, 1059353822u,
            4133978709u, 1060727423u, 2266750395u, 2927776451u, 4271421891u, 314614362u, 485310982u, 810641670u,
            1457100065u, 1532039256u, 3473803770u, 3227654597u, 3982990303u, 4020973941u, 1837714826u, 3728437233u,
            466263641u, 54640033u, 3972881303u, 1972948297u, 2926348902u, 2247202058u, 4210105722u, 167917901u,
            4084751575u, 177451229u, 1205524910u, 1231956767u, 2175537018u, 784738991u, 450254294u, 2612815467u
        };

        // ------------------------------------------------------------------
        // next_int_0_4_64 -- first 64 RngStream.NextInt(0,4) outputs on board-refill, per pinned seed.
        // ------------------------------------------------------------------
        internal static readonly int[] NextInt0To4_64_Zero =
        {
            0, 3, 2, 2, 3, 1, 4, 2, 3, 2,
            3, 3, 3, 3, 4, 4, 4, 4, 3, 4,
            4, 4, 4, 3, 0, 2, 1, 4, 3, 2,
            1, 3, 3, 2, 2, 2, 3, 3, 1, 0,
            4, 1, 4, 3, 2, 3, 3, 4, 4, 1,
            0, 3, 1, 3, 3, 1, 0, 2, 1, 1,
            3, 4, 1, 0
        };

        internal static readonly int[] NextInt0To4_64_One =
        {
            3, 0, 3, 3, 2, 3, 4, 3, 2, 3,
            2, 3, 0, 2, 2, 2, 2, 2, 0, 2,
            4, 4, 0, 0, 4, 1, 3, 1, 4, 4,
            4, 4, 2, 2, 1, 4, 3, 4, 0, 2,
            4, 3, 1, 1, 2, 1, 3, 2, 3, 2,
            3, 1, 1, 4, 0, 1, 1, 2, 4, 1,
            1, 0, 1, 2
        };

        internal static readonly int[] NextInt0To4_64_MaxUint32 =
        {
            1, 0, 2, 3, 3, 4, 2, 1, 0, 4,
            1, 3, 2, 4, 0, 0, 2, 2, 3, 0,
            2, 4, 3, 2, 0, 0, 1, 3, 1, 3,
            1, 4, 0, 4, 2, 0, 1, 3, 2, 3,
            3, 4, 0, 2, 2, 1, 2, 4, 4, 1,
            0, 2, 1, 0, 4, 2, 3, 0, 0, 2,
            3, 0, 1, 3
        };

        internal static readonly int[] NextInt0To4_64_F1Anchor =
        {
            3, 2, 1, 4, 1, 3, 3, 1, 2, 1,
            1, 2, 1, 3, 0, 3, 4, 4, 4, 4,
            0, 3, 0, 0, 2, 2, 1, 1, 0, 3,
            3, 4, 3, 3, 3, 2, 1, 0, 2, 4,
            0, 4, 2, 0, 4, 1, 0, 4, 1, 0,
            2, 4, 0, 2, 2, 4, 2, 3, 3, 0,
            2, 3, 1, 4
        };

        internal static readonly int[] NextInt0To4_64_F2Anchor =
        {
            3, 4, 4, 2, 4, 0, 3, 4, 3, 4,
            1, 1, 4, 3, 0, 0, 4, 1, 4, 3,
            0, 4, 4, 1, 3, 4, 3, 3, 0, 2,
            1, 1, 4, 1, 2, 3, 4, 0, 0, 0,
            1, 1, 4, 3, 4, 4, 2, 4, 0, 0,
            4, 2, 3, 2, 4, 0, 4, 0, 1, 1,
            2, 0, 0, 3
        };

        // ------------------------------------------------------------------
        // next_color_5pool_64 -- first 64 RngStream.NextColor(pool) outputs on board-refill, per pinned seed.
        // ------------------------------------------------------------------
        internal static readonly string[] ColorPool5 =
        {
            "red", "blue", "green", "yellow", "purple"
        };

        internal static readonly string[] NextColor5Pool64_Zero =
        {
            "red", "yellow", "green", "green", "yellow", "blue",
            "purple", "green", "yellow", "green", "yellow", "yellow",
            "yellow", "yellow", "purple", "purple", "purple", "purple",
            "yellow", "purple", "purple", "purple", "purple", "yellow",
            "red", "green", "blue", "purple", "yellow", "green",
            "blue", "yellow", "yellow", "green", "green", "green",
            "yellow", "yellow", "blue", "red", "purple", "blue",
            "purple", "yellow", "green", "yellow", "yellow", "purple",
            "purple", "blue", "red", "yellow", "blue", "yellow",
            "yellow", "blue", "red", "green", "blue", "blue",
            "yellow", "purple", "blue", "red"
        };

        internal static readonly string[] NextColor5Pool64_One =
        {
            "yellow", "red", "yellow", "yellow", "green", "yellow",
            "purple", "yellow", "green", "yellow", "green", "yellow",
            "red", "green", "green", "green", "green", "green",
            "red", "green", "purple", "purple", "red", "red",
            "purple", "blue", "yellow", "blue", "purple", "purple",
            "purple", "purple", "green", "green", "blue", "purple",
            "yellow", "purple", "red", "green", "purple", "yellow",
            "blue", "blue", "green", "blue", "yellow", "green",
            "yellow", "green", "yellow", "blue", "blue", "purple",
            "red", "blue", "blue", "green", "purple", "blue",
            "blue", "red", "blue", "green"
        };

        internal static readonly string[] NextColor5Pool64_MaxUint32 =
        {
            "blue", "red", "green", "yellow", "yellow", "purple",
            "green", "blue", "red", "purple", "blue", "yellow",
            "green", "purple", "red", "red", "green", "green",
            "yellow", "red", "green", "purple", "yellow", "green",
            "red", "red", "blue", "yellow", "blue", "yellow",
            "blue", "purple", "red", "purple", "green", "red",
            "blue", "yellow", "green", "yellow", "yellow", "purple",
            "red", "green", "green", "blue", "green", "purple",
            "purple", "blue", "red", "green", "blue", "red",
            "purple", "green", "yellow", "red", "red", "green",
            "yellow", "red", "blue", "yellow"
        };

        internal static readonly string[] NextColor5Pool64_F1Anchor =
        {
            "yellow", "green", "blue", "purple", "blue", "yellow",
            "yellow", "blue", "green", "blue", "blue", "green",
            "blue", "yellow", "red", "yellow", "purple", "purple",
            "purple", "purple", "red", "yellow", "red", "red",
            "green", "green", "blue", "blue", "red", "yellow",
            "yellow", "purple", "yellow", "yellow", "yellow", "green",
            "blue", "red", "green", "purple", "red", "purple",
            "green", "red", "purple", "blue", "red", "purple",
            "blue", "red", "green", "purple", "red", "green",
            "green", "purple", "green", "yellow", "yellow", "red",
            "green", "yellow", "blue", "purple"
        };

        internal static readonly string[] NextColor5Pool64_F2Anchor =
        {
            "yellow", "purple", "purple", "green", "purple", "red",
            "yellow", "purple", "yellow", "purple", "blue", "blue",
            "purple", "yellow", "red", "red", "purple", "blue",
            "purple", "yellow", "red", "purple", "purple", "blue",
            "yellow", "purple", "yellow", "yellow", "red", "green",
            "blue", "blue", "purple", "blue", "green", "yellow",
            "purple", "red", "red", "red", "blue", "blue",
            "purple", "yellow", "purple", "purple", "green", "purple",
            "red", "red", "purple", "green", "yellow", "green",
            "purple", "red", "purple", "red", "blue", "blue",
            "green", "red", "red", "yellow"
        };

        // ------------------------------------------------------------------
        // next_float_8 -- first 8 RngStream.NextFloat() outputs on board-refill, per pinned seed.
        // ------------------------------------------------------------------
        internal static readonly double[] NextFloat8_Zero =
        {
            0.003533176612108946d, 0.6202344852499664d, 0.534205959411338d, 0.5036075043026358d,
            0.6680917104240507d, 0.3027434190735221d, 0.9773371948394924d, 0.43252483662217855d
        };

        internal static readonly double[] NextFloat8_One =
        {
            0.6873763909097761d, 0.18521608831360936d, 0.6969850973691791d, 0.7279233529698104d,
            0.45025241328403354d, 0.600007584085688d, 0.8216105510946363d, 0.793691199971363d
        };

        internal static readonly double[] NextFloat8_MaxUint32 =
        {
            0.30686761951074004d, 0.032400763826444745d, 0.4851996365468949d, 0.7478637595195323d,
            0.6686014700680971d, 0.9138871384784579d, 0.40216957847587764d, 0.39532801741734147d
        };

        internal static readonly double[] NextFloat8_F1Anchor =
        {
            0.7852533662226051d, 0.47811597911641d, 0.398341630352661d, 0.9405557380523533d,
            0.38865079707466066d, 0.6320642083883286d, 0.6334968039300293d, 0.2165052832569927d
        };

        internal static readonly double[] NextFloat8_F2Anchor =
        {
            0.6969024846330285d, 0.977507422445342d, 0.8728970610536635d, 0.5376995485275984d,
            0.9264218506868929d, 0.14554453152231872d, 0.6960856993682683d, 0.922021112870425d
        };

        // ------------------------------------------------------------------
        // shuffle -- RngStream.Shuffle(...) on the F1-anchor board-refill stream.
        // ------------------------------------------------------------------
        internal const uint ShuffleMasterSeed = 2824445292u;

        internal static readonly int[] ShuffleLen20Input = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19 };
        internal static readonly int[] ShuffleLen20Result = { 5, 1, 17, 11, 14, 0, 13, 12, 4, 10, 3, 16, 2, 8, 18, 6, 19, 7, 9, 15 };
        internal const int ShuffleLen20DrawsConsumed = 19;

        internal static readonly int[] ShuffleLen0Input = System.Array.Empty<int>();
        internal static readonly int[] ShuffleLen0Result = System.Array.Empty<int>();
        internal const int ShuffleLen0DrawsConsumed = 0;

        internal static readonly int[] ShuffleLen1Input = { 0 };
        internal static readonly int[] ShuffleLen1Result = { 0 };
        internal const int ShuffleLen1DrawsConsumed = 0;

        internal static readonly int[] ShuffleLen2Input = { 0, 1 };
        internal static readonly int[] ShuffleLen2Result = { 0, 1 };
        internal const int ShuffleLen2DrawsConsumed = 1;

        // ------------------------------------------------------------------
        // fork -- ForkStream(board-refill, "probe") under the F1-anchor master seed.
        // ------------------------------------------------------------------
        internal const uint ForkMasterSeed = 2824445292u;
        internal const string ForkParentStreamName = "board-refill";
        internal const uint ForkParentInitialSeed = 2622563974u;
        internal const string ForkLabel = "probe";
        internal const uint ForkChildSeed = 454598268u;
        internal static readonly uint[] ForkFirst16Draws =
        {
            1595470486u, 3624744604u, 1729723604u, 1317481275u, 741083366u, 2521744252u, 2241620362u, 57881112u,
            285693805u, 791188088u, 2829483313u, 2563112182u, 1399010006u, 3930634957u, 2749426817u, 2316041629u
        };

        // ==================================================================
        // Additions from rng_golden_v1_draws.json (additive-only extension).
        // ==================================================================

        // next_int_extra_ranges -- NextInt(min,max) sequences beyond the (0,4) table above.
        internal const uint ExtraRangeMasterSeed = 2824445292u; // board-refill, F1 anchor

        internal const int Range10To20Min = 10;
        internal const int Range10To20Max = 20;
        internal static readonly int[] NextIntRange10To20 =
        {
            18, 15, 14, 20, 14, 16, 16, 12, 15, 13,
            13, 15, 13, 17, 11, 18, 19, 20, 19, 19
        };

        internal const int RangeMinus50To50Min = -50;
        internal const int RangeMinus50To50Max = 50;
        internal static readonly int[] NextIntRangeMinus50To50 =
        {
            29, -2, -10, 44, -11, 13, 13, -29, 4, -22,
            -15, 0, -20, 16, -34, 30, 38, 46, 39, 35
        };

        internal const int Range7To7Min = 7;
        internal const int Range7To7Max = 7;
        internal static readonly int[] NextIntRange7To7 =
        {
            7, 7, 7, 7, 7
        };

        // special_drop_raw -- 30 raw draws on the special-drop stream (proves the value drawn
        // WHILE interleaving matches an independent oracle too, not just the board-refill side).
        internal static readonly uint[] SpecialDropRaw30_Seed500 =
        {
            2287444869u, 1857088158u, 4053527144u, 1860904893u, 751560310u, 2386141000u, 402023381u, 387658840u,
            4048390286u, 1446294970u, 910211918u, 2214234486u, 2356622111u, 2559558086u, 2006798138u, 3539757475u,
            247731898u, 1387875601u, 546715419u, 773711872u, 3835288350u, 747206806u, 153726211u, 2038404028u,
            2611657399u, 1063744272u, 2003344936u, 2675339250u, 1197490627u, 309169919u
        };

        internal static readonly uint[] SpecialDropRaw30_F1Anchor =
        {
            1960005144u, 3027267216u, 2613725858u, 1684484243u, 1013894663u, 4101388753u, 1436517920u, 1197672013u,
            3506715203u, 3969346197u, 1256268028u, 2020857164u, 2568162249u, 2197321924u, 2231530618u, 3865962304u,
            1618657908u, 128199770u, 2580222391u, 696932276u, 900605065u, 799260547u, 1923256793u, 2075816117u,
            2565559166u, 3207655805u, 3180640687u, 2022602433u, 2901518048u, 127328664u
        };

        // fork_extra -- a second label off the same parent, and the same label off a different
        // parent, both under the F1-anchor master seed.
        internal const string ForkExtraLabelProbe2 = "probe2";
        internal const uint ForkExtraBoardRefillProbe2ChildSeed = 437144445u;
        internal static readonly uint[] ForkExtraBoardRefillProbe2Draws =
        {
            1998759578u, 1930182817u, 3282601087u, 332847391u, 3644062173u, 4164419429u, 2801058824u, 3665500342u
        };

        internal const string ForkExtraSpecialDropStreamName = "special-drop";
        internal const uint ForkExtraSpecialDropProbeParentInitialSeed = 233673590u;
        internal const uint ForkExtraSpecialDropProbeChildSeed = 3853381867u;
        internal static readonly uint[] ForkExtraSpecialDropProbeDraws =
        {
            2344256619u, 3324292910u, 556734796u, 2131550144u, 1806781848u, 288973813u, 1991957501u, 1265972513u
        };
    }
}
