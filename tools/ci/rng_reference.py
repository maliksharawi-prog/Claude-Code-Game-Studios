#!/usr/bin/env python3
"""rng_reference.py -- Independent reference implementation of Sweet Cascade's
deterministic RNG (ADR-004: Deterministic RNG Implementation & Domain-Purity
CI Guard; design/gdd/rng-service.md F1-F6).

This script exists to generate `rng_golden_v1.json` (the checked-in,
byte-for-byte cross-implementation ground truth consumed by
`RngGoldenVectorTest.cs`) from an implementation that is INDEPENDENT of the
C# implementation under test in `src/SweetCascade/Assets/Domain/Rng/`. Per
ADR-004 story 004: "Generate the fixture once from the reference
implementation, review it against `Combine(...)` GDD anchors and
`Mix32(0)==0`, then freeze. Do not hand-compute values."

Every constant and operation order below is copied verbatim from ADR-004 SS1
("The normative primitives") -- not re-derived, not "improved". If this
script and the C# implementation ever disagree, the C# implementation has a
bug (or vice versa) -- that disagreement is exactly what the golden-vector
suite exists to catch.

Usage:
    python3 tools/ci/rng_reference.py --check-anchors   # print anchor checks only
    python3 tools/ci/rng_reference.py --write-fixture PATH  # also write the fixture
"""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

MASK32 = 0xFFFFFFFF

# GDD-fixed constants (rng-service.md F1/F2/F3). DO NOT change without an
# algorithm_version bump (ADR-004 SS1).
K1 = 0x9E3779B1  # 2,654,435,761  (Knuth ~= 2^32/phi)   -- combine() term for a
K2 = 0x9E37      #        40,503  (odd)                  -- combine() term for b

# SplitMix32 / finalizer constants (ADR-004's generator choice).
GAMMA = 0x9E3779B9  # 2,654,435,769  (odd Weyl increment). NOTE: GAMMA != K1.
M1 = 0x85EBCA6B     # 2,246,822,507  (MurmurHash3 fmix32)
M2 = 0xC2B2AE35     # 3,266,489,909  (MurmurHash3 fmix32)

# FNV-1a-32 (ForkStream label hashing -- same primitive as the Save checksum, ADR-003).
FNV_OFFSET = 0x811C9DC5  # 2,166,136,261
FNV_PRIME = 0x01000193   #    16,777,619

ALGORITHM_VERSION = "v1"


def combine(a: int, b: int) -> int:
    """combine(a,b) = (a*K1 + b*K2) mod 2^32 -- F1, F2, F3 all use this exact form."""
    return (a * K1 + b * K2) & MASK32


def mix32(x: int) -> int:
    """MurmurHash3 fmix32 avalanche finalizer -- this IS rng-service.md's mix32()."""
    x &= MASK32
    x ^= x >> 16
    x = (x * M1) & MASK32
    x ^= x >> 13
    x = (x * M2) & MASK32
    x ^= x >> 16
    return x & MASK32


def fnv1a32(label: str) -> int:
    """FNV-1a-32 over the label's UTF-8 bytes (ADR-004 SS3 ForkStream)."""
    h = FNV_OFFSET
    for byte in label.encode("utf-8"):
        h ^= byte
        h = (h * FNV_PRIME) & MASK32
    return h


class RngStream:
    """One independently-seeded SplitMix32 stream (mirrors C# RngStream)."""

    def __init__(self, initial_seed: int):
        self.initial_seed = initial_seed & MASK32
        self.state = self.initial_seed

    def next_raw(self) -> int:
        self.state = (self.state + GAMMA) & MASK32
        return mix32(self.state)

    def next_int(self, min_value: int, max_value: int) -> int:
        if min_value > max_value:
            raise ValueError("min_value > max_value")
        range_size = (max_value - min_value + 1) & MASK32
        raw = self.next_raw()
        idx = (raw * range_size) >> 32
        return min_value + idx

    def next_color(self, active_colors):
        if len(active_colors) == 0:
            raise ValueError("empty color pool")
        raw = self.next_raw()
        idx = (raw * len(active_colors)) >> 32
        return active_colors[idx]

    def next_float(self) -> float:
        return self.next_raw() * (1.0 / 4294967296.0)

    def shuffle(self, source):
        result = list(source)
        for i in range(len(result) - 1, 0, -1):
            j = self.next_int(0, i)
            result[i], result[j] = result[j], result[i]
        return result


def master_seed_level(level_id: int, attempt_number: int) -> int:
    """F1 -- standard level attempt."""
    return mix32(combine(level_id & MASK32, attempt_number & MASK32))


def master_seed_daily(daily_challenge_id: int, calendar_date_utc: int) -> int:
    """F2 -- daily challenge (no player-identifying/-performance input)."""
    return mix32(combine(daily_challenge_id & MASK32, calendar_date_utc & MASK32))


def stream_seed(master_seed: int, stream_id: int) -> int:
    """F3 -- per-stream sub-seed."""
    return mix32(combine(master_seed & MASK32, stream_id & MASK32))


# Stream registry (rng-service.md SS2), append-only.
STREAM_REGISTRY = [
    ("board-refill", 1),
    ("special-drop", 2),
    ("harvest", 3),
    ("events", 4),
]
BOARD_REFILL_ID = 1
BOARD_REFILL_NAME = "board-refill"

# F5 worked-example pool (rng-service.md Formula F5), reused here for consistency
# with the GDD's own worked example.
LAUNCH_COLOR_POOL = ["red", "blue", "green", "yellow", "purple"]


def check_anchors() -> bool:
    """Verifies the GDD's fully-computed combine() anchors and Mix32(0)==0.

    Returns True iff every anchor holds.
    """
    checks = [
        ("Mix32(0)", mix32(0), 0),
        ("Combine(1007,3)", combine(1007, 3), 1547274724),
        ("Combine(42,20650)", combine(42, 20650), 653539216),
        ("Combine(500,1)", combine(500, 1), 73026539),
    ]
    all_ok = True
    print("=== GDD / ADR-004 anchor check (independent Python reference) ===")
    for label, actual, expected in checks:
        ok = actual == expected
        all_ok = all_ok and ok
        status = "PASS" if ok else "FAIL"
        print(f"  [{status}] {label} == {expected}  (got {actual})")
    print(f"=== anchor check: {'ALL PASS' if all_ok else 'FAILURE'} ===")
    return all_ok


def u(n: int) -> str:
    """Decimal uint string -- ADR-004 SS5 fixture Format ('never raw bytes -- sidesteps endianness')."""
    return str(n & MASK32)


def d(value: float) -> str:
    """Round-trip double as a decimal string (>=17 significant digits, exact
    IEEE-754 round-trip -- ADR-004 SS5 'printed to 17 significant digits /
    round-trip "R" format'). Stored as a JSON string, not a JSON number, for
    the same endianness/precision-safety reason as the uint fields."""
    return repr(value)


def build_fixture() -> dict:
    # Anchors, re-affirmed inside the fixture itself.
    anchors = {
        "mix32_zero": u(mix32(0)),
        "combine_1007_3": u(combine(1007, 3)),
        "combine_42_20650": u(combine(42, 20650)),
        "combine_500_1": u(combine(500, 1)),
    }

    # Seed-derivation tables (F1/F2/F3), including the GDD anchors.
    f1_master_seed = [
        {
            "level_id": 1007,
            "attempt_number": 3,
            "combine": u(combine(1007, 3)),
            "master_seed": u(master_seed_level(1007, 3)),
        },
    ]
    f1_anchor_master_seed = master_seed_level(1007, 3)

    f2_master_seed = [
        {
            "daily_challenge_id": 42,
            "calendar_date_utc": 20650,
            "combine": u(combine(42, 20650)),
            "master_seed": u(master_seed_daily(42, 20650)),
        },
    ]
    f2_anchor_master_seed = master_seed_daily(42, 20650)

    f3_stream_seed = []
    for ms in (500, f1_anchor_master_seed):
        for name, sid in STREAM_REGISTRY:
            f3_stream_seed.append(
                {
                    "master_seed": u(ms),
                    "stream_id": sid,
                    "stream_name": name,
                    "combine": u(combine(ms, sid)),
                    "stream_seed": u(stream_seed(ms, sid)),
                }
            )

    # Five pinned master seeds (ADR-004 SS5: ">=5 pinned master seeds (incl. 0,
    # 1, 0xFFFFFFFF, and the F1 anchor)"). The fifth slot is the F2 anchor --
    # a second GDD-traceable value rather than an arbitrary filler constant.
    pinned_seeds = [0, 1, 0xFFFFFFFF, f1_anchor_master_seed, f2_anchor_master_seed]
    pinned_seed_labels = {
        0: "zero",
        1: "one",
        0xFFFFFFFF: "max_uint32",
        f1_anchor_master_seed: "f1_anchor_level1007_attempt3",
        f2_anchor_master_seed: "f2_anchor_daily42_day20650",
    }

    def fresh_board_refill(master_seed: int) -> RngStream:
        return RngStream(stream_seed(master_seed, BOARD_REFILL_ID))

    next_raw_64 = {}
    next_int_0_4_64 = {}
    next_color_5pool_64 = {}
    next_float_8 = {}

    for ms in pinned_seeds:
        label = pinned_seed_labels[ms]

        s = fresh_board_refill(ms)
        next_raw_64[label] = {
            "master_seed": u(ms),
            "values": [u(s.next_raw()) for _ in range(64)],
        }

        s = fresh_board_refill(ms)
        next_int_0_4_64[label] = {
            "master_seed": u(ms),
            "values": [u(s.next_int(0, 4)) for _ in range(64)],
        }

        s = fresh_board_refill(ms)
        next_color_5pool_64[label] = {
            "master_seed": u(ms),
            "pool": LAUNCH_COLOR_POOL,
            "values": [s.next_color(LAUNCH_COLOR_POOL) for _ in range(64)],
        }

        s = fresh_board_refill(ms)
        next_float_8[label] = {
            "master_seed": u(ms),
            "values": [d(s.next_float()) for _ in range(8)],
        }

    # Shuffle: (seed, [0..19]) -> permutation, plus length-0/1/2 boundaries.
    # Uses the F1 anchor's board-refill stream (fresh instance per case).
    shuffle_seed = f1_anchor_master_seed
    shuffle = {
        "master_seed": u(shuffle_seed),
        "len_20": {
            "input": [u(i) for i in range(20)],
            "result": [u(v) for v in fresh_board_refill(shuffle_seed).shuffle(list(range(20)))],
            "draws_consumed": 19,
        },
        "len_0": {
            "input": [],
            "result": [],
            "draws_consumed": 0,
        },
        "len_1": {
            "input": [u(0)],
            "result": [u(v) for v in fresh_board_refill(shuffle_seed).shuffle([0])],
            "draws_consumed": 0,
        },
        "len_2": {
            "input": [u(0), u(1)],
            "result": [u(v) for v in fresh_board_refill(shuffle_seed).shuffle([0, 1])],
            "draws_consumed": 1,
        },
    }

    # Fork: (seed, parent="board-refill", label="probe") -> childSeed + first 16 draws.
    fork_seed = f1_anchor_master_seed
    parent_initial = stream_seed(fork_seed, BOARD_REFILL_ID)
    child_seed_value = mix32(combine(parent_initial, fnv1a32("probe")))
    child_stream = RngStream(child_seed_value)
    fork = {
        "master_seed": u(fork_seed),
        "parent_stream_name": BOARD_REFILL_NAME,
        "parent_initial_seed": u(parent_initial),
        "label": "probe",
        "child_seed": u(child_seed_value),
        "first_16_draws": [u(child_stream.next_raw()) for _ in range(16)],
    }

    return {
        "algorithm_version": ALGORITHM_VERSION,
        "generated_by": "tools/ci/rng_reference.py",
        "note": (
            "Frozen ground truth for algorithm_version=v1 (ADR-004 SS5). Generated "
            "once by an independent Python reference implementation, reviewed "
            "against the GDD combine() anchors and Mix32(0)==0, then frozen. Do "
            "not hand-edit a value to 'fix' a failing test -- a deliberate change "
            "is a v2 algorithm_version bump (rng_golden_v2.json), never a silent edit."
        ),
        "anchors": anchors,
        "seed_derivation": {
            "f1_master_seed": f1_master_seed,
            "f2_master_seed": f2_master_seed,
            "f3_stream_seed": f3_stream_seed,
        },
        "pinned_master_seeds": {label: u(ms) for ms, label in pinned_seed_labels.items()},
        "board_refill_stream_name": BOARD_REFILL_NAME,
        "board_refill_stream_id": BOARD_REFILL_ID,
        "next_raw_64": next_raw_64,
        "next_int_0_4_64": next_int_0_4_64,
        "next_color_5pool_64": next_color_5pool_64,
        "next_float_8": next_float_8,
        "shuffle": shuffle,
        "fork": fork,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--write-fixture",
        type=str,
        default=None,
        help="Path to write rng_golden_v1.json. If omitted, only the anchor check runs.",
    )
    parser.add_argument(
        "--check-anchors",
        action="store_true",
        help="Only run and print the anchor check (default behavior if --write-fixture is omitted).",
    )
    args = parser.parse_args()

    anchors_ok = check_anchors()
    if not anchors_ok:
        print("ABORTING: anchor check failed -- refusing to write/trust a fixture.", file=sys.stderr)
        return 1

    if args.write_fixture:
        fixture = build_fixture()
        out_path = Path(args.write_fixture)
        out_path.parent.mkdir(parents=True, exist_ok=True)
        with out_path.open("w", encoding="utf-8", newline="\n") as f:
            json.dump(fixture, f, indent=2, sort_keys=False)
            f.write("\n")
        print(f"\nWrote golden fixture: {out_path} ({out_path.stat().st_size} bytes)")

        # Re-load and spot-check a handful of values as a sanity pass.
        with out_path.open("r", encoding="utf-8") as f:
            reloaded = json.load(f)
        assert reloaded["anchors"]["mix32_zero"] == "0"
        assert reloaded["anchors"]["combine_1007_3"] == "1547274724"
        print("Fixture reload spot-check: PASS")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
