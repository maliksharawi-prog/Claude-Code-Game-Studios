#!/usr/bin/env bash
#
# domain-purity-scan.sh — Sweet Cascade Domain-purity CI guard (Layer 2 / L2).
#
# Implements ADR-004 §6 "L2 — CI denylist scan" over
# src/SweetCascade/Assets/Domain/: a pre-build, license-free ripgrep pass that
# blocks any of the forbidden non-deterministic / engine-coupled tokens from
# ever landing in the pure-C# Domain assembly. See:
#   docs/architecture/adr-004-deterministic-rng.md §6 (L2, verbatim denylist)
#   docs/architecture/control-manifest.md ("Editor & CI Layer Rules")
#   production/epics/project-scaffold-ci/story-003-domain-purity-ci-denylist-scan.md
#
# Usage:
#   tools/ci/domain-purity-scan.sh [DOMAIN_ROOT]
#       Scan DOMAIN_ROOT (default: src/SweetCascade/Assets/Domain) for the
#       denylist. Exits 0 if clean, 1 if any forbidden token is found (and not
#       suppressed by a "// rng-purity-allow: <reason>" pragma on the same
#       line), 2 on a usage/setup error (e.g. missing domain root, missing rg).
#
#   tools/ci/domain-purity-scan.sh --self-test
#       Runs the injected-violation self-test in a disposable temp copy
#       (never touches the real Assets/Domain/ tree). Proves the guard is
#       armed, not vacuously green. Exits 0 if every assertion passes, 1
#       otherwise.
#
# Requires: bash, ripgrep (rg). No Unity license or Unity install required —
# this is the one E01 CI gate that runs before any Unity invocation.

set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" >/dev/null 2>&1 && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." >/dev/null 2>&1 && pwd)"
DEFAULT_DOMAIN_ROOT="${REPO_ROOT}/src/SweetCascade/Assets/Domain"

# ADR-004 §6 L2 denylist (verbatim, minus the path-scoped float rule below).
GENERAL_PATTERNS=(
    '\bSystem\.Random\b'
    '\bnew\s+Random\s*\('
    '\bUnityEngine\b'
    '\bUnityEditor\b'
    '\bDateTime\s*\.\s*(Now|UtcNow|Today)\b'
    '\bEnvironment\s*\.\s*TickCount\b'
    '\bStopwatch\b'
    '\bGuid\s*\.\s*NewGuid\b'
)
FLOAT_PATTERN='\bfloat\b'
PRAGMA='// rng-purity-allow:'

require_rg() {
    if ! command -v rg >/dev/null 2>&1; then
        echo "domain-purity-scan: ERROR — ripgrep (rg) is not installed/on PATH." >&2
        exit 2
    fi
}

# scan_pattern PATTERN ROOT — prints non-pragma-suppressed "file:line:content"
# hits for PATTERN under ROOT (*.cs files only), one per line.
scan_pattern() {
    local pattern="$1" root="$2" raw=""
    raw="$(rg -n --no-heading --glob '*.cs' -e "${pattern}" "${root}" 2>/dev/null)"
    [ -z "${raw}" ] && return 0
    while IFS= read -r line; do
        [ -z "${line}" ] && continue
        case "${line}" in
            *"${PRAGMA}"*) continue ;;  # reviewed per-line escape hatch (ADR-004 §6)
            *) printf '%s\n' "${line}" ;;
        esac
    done <<< "${raw}"
}

# run_scan DOMAIN_ROOT — the actual guard. Prints a report and returns
# 0 (clean) / 1 (violations found) / 2 (setup error).
run_scan() {
    local domain_root="$1"
    local hits="" pattern="" rng_root=""

    if [ ! -d "${domain_root}" ]; then
        echo "domain-purity-scan: ERROR — domain root not found: ${domain_root}" >&2
        return 2
    fi

    for pattern in "${GENERAL_PATTERNS[@]}"; do
        hits+="$(scan_pattern "${pattern}" "${domain_root}")"$'\n'
    done

    rng_root="${domain_root%/}/Rng"
    if [ -d "${rng_root}" ]; then
        hits+="$(scan_pattern "${FLOAT_PATTERN}" "${rng_root}")"$'\n'
    fi

    # collapse to only non-blank lines
    hits="$(printf '%s' "${hits}" | sed '/^[[:space:]]*$/d')"

    if [ -n "${hits}" ]; then
        echo "domain-purity-scan: FAIL — forbidden-token hit(s) under ${domain_root}:" >&2
        printf '%s\n' "${hits}" >&2
        return 1
    fi

    echo "domain-purity-scan: PASS — no forbidden tokens under ${domain_root}"
    return 0
}

# ---------------------------------------------------------------------------
# Self-test: proves the guard is armed (ADR-004 §6; story-003 QA Test Cases).
# Operates ONLY inside a disposable mktemp -d copy — never touches the real
# Assets/Domain/ tree.
# ---------------------------------------------------------------------------
self_test() {
    local tmp domain failures=0 out
    tmp="$(mktemp -d)" || { echo "self-test: mktemp failed" >&2; return 2; }
    trap 'rm -rf "${tmp}"' RETURN
    domain="${tmp}/Domain"
    mkdir -p "${domain}/Rng" "${domain}/Scoring"
    out="$(mktemp)"

    check_fail() {  # label
        if run_scan "${domain}" >"${out}" 2>&1; then
            echo "  [FAIL] $1: expected the scan to FAIL (non-zero) but it PASSED"
            failures=$((failures + 1))
        else
            echo "  [PASS] $1: scan correctly failed"
        fi
    }
    check_pass() {  # label
        if run_scan "${domain}" >"${out}" 2>&1; then
            echo "  [PASS] $1: scan correctly passed"
        else
            echo "  [FAIL] $1: expected the scan to PASS (zero) but it FAILED:"
            sed 's/^/      /' "${out}"
            failures=$((failures + 1))
        fi
    }

    echo "domain-purity-scan --self-test: starting (temp domain: ${domain})"

    # 1) A clean fixture file passes.
    cat > "${domain}/Scoring/CleanFixture.cs" <<'EOF'
namespace SweetCascade.Domain.Scoring
{
    internal static class CleanFixture
    {
        internal const int SampleConstant = 42;
    }
}
EOF
    check_pass "clean Domain fixture"

    # 2) Each denylist token, injected individually, trips the scan; removed
    #    again afterwards, the tree returns to clean.
    local labels=(
        "System.Random"
        "new Random("
        "UnityEngine"
        "UnityEditor"
        "DateTime.Now"
        "Environment.TickCount"
        "Stopwatch"
        "Guid.NewGuid"
    )
    local snippets=(
        'internal static readonly System.Random Bad = null;'
        'internal static readonly object Bad = new Random(1);'
        'internal static readonly object Bad = typeof(UnityEngine.Object);'
        'internal static readonly object Bad = typeof(UnityEditor.Editor);'
        'internal static readonly System.DateTime Bad = System.DateTime.Now;'
        'internal static readonly int Bad = System.Environment.TickCount;'
        'internal static readonly object Bad = new System.Diagnostics.Stopwatch();'
        'internal static readonly System.Guid Bad = System.Guid.NewGuid();'
    )
    local i
    for i in "${!labels[@]}"; do
        cat > "${domain}/Scoring/_Probe.cs" <<EOF
namespace SweetCascade.Domain.Scoring
{
    internal static class ProbeFixture
    {
        ${snippets[$i]}
    }
}
EOF
        check_fail "injected violation: ${labels[$i]}"
        rm -f "${domain}/Scoring/_Probe.cs"
    done
    check_pass "tree is clean again after removing the last injected violation"

    # 3) The pragma escape suppresses exactly the line it is on...
    cat > "${domain}/Scoring/_PragmaEscape.cs" <<'EOF'
namespace SweetCascade.Domain.Scoring
{
    internal static class PragmaEscapeFixture
    {
        // Reviewed interop shim — see ADR-004 Risks table.
        internal static readonly System.Random Legacy = null; // rng-purity-allow: interop shim, reviewed
    }
}
EOF
    check_pass "pragma-escaped line is not flagged"

    # ...but a token on a DIFFERENT, un-escaped line still trips the scan
    # (the escape is per-line, not per-file).
    cat > "${domain}/Scoring/_PragmaEscapeNegative.cs" <<'EOF'
namespace SweetCascade.Domain.Scoring
{
    internal static class PragmaEscapeNegativeFixture
    {
        internal static readonly System.Random Unescaped = null;
    }
}
EOF
    check_fail "unescaped violation on a separate line (pragma is per-line, not per-file)"
    rm -f "${domain}/Scoring/_PragmaEscape.cs" "${domain}/Scoring/_PragmaEscapeNegative.cs"
    check_pass "tree is clean again after removing the pragma fixtures"

    # 4) float is NOT flagged outside Assets/Domain/Rng/ (path-scoped rule).
    cat > "${domain}/Scoring/ScoreProgress.cs" <<'EOF'
namespace SweetCascade.Domain.Scoring
{
    internal readonly struct ScoreProgressFixture
    {
        internal readonly float ScoreProgressRatio;
    }
}
EOF
    check_pass "bare float OUTSIDE Assets/Domain/Rng/ is not flagged"
    rm -f "${domain}/Scoring/ScoreProgress.cs"

    # 5) float IS flagged inside Assets/Domain/Rng/.
    cat > "${domain}/Rng/BadFloat.cs" <<'EOF'
namespace SweetCascade.Domain.Rng
{
    internal static class BadFloatFixture
    {
        internal static float NextValue()
        {
            return 0.5f;
        }
    }
}
EOF
    check_fail "bare float INSIDE Assets/Domain/Rng/ is flagged"
    rm -f "${domain}/Rng/BadFloat.cs"
    check_pass "tree is clean again after removing the Rng float fixture"

    rm -f "${out}"

    echo ""
    if [ "${failures}" -eq 0 ]; then
        echo "domain-purity-scan --self-test: PASS — all assertions correct (guard is armed)."
        return 0
    else
        echo "domain-purity-scan --self-test: FAIL — ${failures} assertion(s) incorrect. The guard may be neutered." >&2
        return 1
    fi
}

# ---------------------------------------------------------------------------
# Entry point
# ---------------------------------------------------------------------------
main() {
    require_rg

    local mode="scan" domain_root="${DEFAULT_DOMAIN_ROOT}"
    while [ $# -gt 0 ]; do
        case "$1" in
            --self-test)
                mode="self-test"
                shift
                ;;
            -h|--help)
                sed -n '2,40p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'
                exit 0
                ;;
            *)
                domain_root="$1"
                shift
                ;;
        esac
    done

    if [ "${mode}" = "self-test" ]; then
        self_test
        exit $?
    fi

    run_scan "${domain_root}"
    exit $?
}

main "$@"
