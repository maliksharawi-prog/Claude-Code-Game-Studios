using System;
using UnityEditor;
using UnityEngine;

namespace SweetCascade.Editor.ManifestGen
{
    /// <summary>
    /// Inert scaffold for the Level Manifest generator. See
    /// <c>docs/architecture/adr-006-level-manifest.md</c> for the full design:
    /// this class will become the SOLE writer of
    /// <c>assets/data/level_manifest.asset</c>, invoked only from the
    /// <c>Sweet Cascade ▸ Level Manifest ▸ Regenerate</c> menu item.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Story: E01-006 (project-scaffold-ci). This is scaffold only — the
    /// generation/validation logic (scan, append, tombstone, prefix-invariant
    /// guard, cross-validation) is delivered in E02/E03 per ADR-006 §Decision.
    /// </para>
    /// <para>
    /// Ownership boundaries this stub respects (do not violate when
    /// implementing the real generator):
    /// </para>
    /// <list type="bullet">
    /// <item><description>
    /// The two manifests (<c>level_manifest.asset</c>,
    /// <c>world_map_manifest.asset</c>) are loaded via a DIRECT SERIALIZED
    /// REFERENCE at boot, never Addressables (ADR-006 + ADR-002 as amended
    /// 2026-07-18).
    /// </description></item>
    /// <item><description>
    /// The pure <c>ManifestCrossValidator</c> lives in
    /// <c>SweetCascade.Domain</c> — a Domain type authored in E02/E03, not
    /// referenced here.
    /// </description></item>
    /// <item><description>
    /// No <c>IPreprocessBuildWithReport</c> hook, no <c>AssetDatabase</c>
    /// write, and no <c>Validate</c> call are registered by this stub.
    /// </description></item>
    /// </list>
    /// </remarks>
    public static class LevelManifestGenerator
    {
        /// <summary>
        /// Reserved API surface for the real generator (ADR-006 Key
        /// Interfaces). <paramref name="apply"/> = <see langword="true"/>
        /// writes <c>level_manifest.asset</c> (the Regenerate menu path);
        /// <see langword="false"/> is the dry-run diff used by the pre-build
        /// hook and the CI verify step. NOT YET IMPLEMENTED.
        /// </summary>
        /// <param name="apply">True to write the asset; false for a dry-run diff.</param>
        /// <exception cref="NotImplementedException">
        /// Always thrown — this is E01 scaffold only. Generation logic lands
        /// in E02/E03 per ADR-006.
        /// </exception>
        public static void Reconcile(bool apply)
        {
            throw new NotImplementedException(
                "LevelManifestGenerator is scaffolded in E01; generation logic is " +
                "delivered in E02/E03 per ADR-006 (docs/architecture/adr-006-level-manifest.md).");
        }

        /// <summary>
        /// Inert menu-item wrapper. Surfaces the same "not yet implemented"
        /// notice as <see cref="Reconcile"/> without letting the exception
        /// escape into an editor error dialog. Writes no asset, registers no
        /// build hook, and runs no validation.
        /// </summary>
        [MenuItem("Sweet Cascade/Level Manifest/Regenerate")]
        private static void RegenerateMenuItem()
        {
            try
            {
                Reconcile(apply: true);
            }
            catch (NotImplementedException ex)
            {
                Debug.LogWarning($"Sweet Cascade > Level Manifest > Regenerate: {ex.Message}");
            }
        }
    }
}
