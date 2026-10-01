# TYCOON blockers

Work resumed by explicit user request; command/background testing only.

## SOLVED — Particle System module missing
- Symptom: Unity batch startup compile abort, return code 1.
- Exact error: `Assets/_Game/Scripts/Upgrades/PurchasePad.cs(14,34): CS1069`,
  `UnityEngine.ParticleSystem` forwarded to `UnityEngine.ParticleSystemModule`.
- Attempts: one compile; no fix/retest before the user-requested pause.
- Root cause evidence: compiler explicitly requests enabling the built-in Particle System package.
- Sources: Unity 6000.6.3f1 compiler output, recorded in STOP_HANDOFF.md.
- Chosen next fix: verify and enable the compatible built-in module, then compile and test.
- Fix: enabled installed module com.unity.modules.particlesystem 1.0.0.
- Verification: compilation clean; 53 EditMode and 33 PlayMode component tests pass.

## Under verification
- Unity 6 editor, Windows build module, license and compatible packages.
- Actual formats of Item3.blend / Item3.fbx / Item3.glb (identical hashes; Zstandard header).
- User source model topology, material/UV availability and rigging requirements.

For each confirmed blocker record symptom, exact error, attempts, root cause,
researched sources, chosen fix and status. After two failed fixes use a minimal
reproduction and version-specific primary documentation before further changes.

## SOLVED — Unity 6.6 integration APIs
- Qualified TYCOON.StableId in UpgradeDefinition to avoid its string property shadowing the helper.
- Updated pooling identity test to documented GetEntityId.
- Updated editor reflection to UnityEngine.Assemblies.CurrentAssemblies.GetLoadedAssemblies per installed 6000.6 API docs.
- Verified by clean compile and the 86 passing component tests.
