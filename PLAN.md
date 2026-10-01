# TYCOON — execution plan

## CURRENT MILESTONE
M2 — integrate persistence and a playable continuous scene.

## CURRENT GOAL
Continue all master-task work in D:\APP\TYCOON through code, background commands,
Blender scripts and automated tests. Desktop control is reserved for the user or Luna.
Do not create subagents unless explicitly requested. The full game scope and acceptance
gates remain in Documentation/QA.md; component tests do not prove final game quality.

## DONE
- Verified root, main branch and origin https://github.com/letam10/TYCOON.git; fetched safely.
- Preserved the preexisting README edit and all original ASSET files.
- Unity 6000.6.3f1 URP 17.6.0 project initialized at repository root.
- All 23 source assets audited: 21 valid declared-format imports; Item3.fbx/glb are mislabeled compressed Blender copies.
- RTX 4060/D3D11 verified in the earlier graphics session; current CPU tests use Null Device.
- Fixed missing Particle System dependency, StableId name shadowing, obsolete GetInstanceID and editor reflection API.
- Clean compilation and 53/53 EditMode tests PASS: foundation, commerce and production.
- 33/33 PlayMode tests PASS: player/input/camera, NavMesh customers/pooling, interaction/progression/HUD data.
- Source audit, asset provenance, architecture and acceptance matrix recorded.

## IN PROGRESS
- Commit/push tested bootstrap and gameplay component milestones separately.
- Save coordinator integrating world inventories, worker carry, machine progress, progression and checkout receipts.
- Scene/data/prefab/NavMesh integration for an actual continuous play loop.

## NEXT
1. Complete save integration and transaction/restart tests.
2. Integrate Player, three crops, Cow/Milk, Chicken/Egg, carry/storage, two shelves and checkout.
3. Verify 10–15 customers, real sales, pads/upgrades, Farmer/Restocker/Cashier and processing in one playable scene.
4. Integrate Supermarket and Bakery/Restaurant progression using upstream products and the same save.
5. Process/split/rig user models via Blender scripts; validate pose/animation, materials and important visible models.
6. Complete original/licensed audio and environment/UI/VFX; perform three measured polish passes.
7. Build Windows x64 at Build/Windows/TYCOON.exe; clean-profile end-to-end, quit/restart/load, runtime errors and performance validation.
8. Deliver FINAL_REPORT and user/Luna manual QA instructions, preserving any unverified visual gate explicitly.

## BLOCKERS
No current compile blocker. Scene integration, rig/art/animation, full-game validation
and Windows build are incomplete. Character1 diagnostic rig has fused hand/hip stretch;
no invalid rig has been exported into Unity.

## LAST TEST RESULT
2026-10-01: compile PASS; TestResults/core-edit-r3.xml 53/53 PASS;
TestResults/core-play-r1.xml 33/33 PASS. Zero reported compiler issues, Console errors,
exceptions or assertions in both runs. These are component tests under Null Device.

## LAST BUILD RESULT
NOT BUILT. No TYCOON.exe yet.

## Completion rules
After each stable milestone update PLAN/DEVLOG, inspect staged changes, commit only
task-owned work and push without force. Keep user README changes, caches, temporary files,
secrets and unapproved user-model derivatives out of commits. Never infer whole-game
completion from compilation or test counts. Preserve every master-task acceptance gate.
