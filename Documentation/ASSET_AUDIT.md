# Source asset audit

Audit measured at 2026-10-01T15:06:21+07:00. Project: `D:\APP\TYCOON`. Source files were read only; no source was renamed, overwritten, repaired, or backed up.

## Verdict

23 source files were inspected by size, header, SHA-256 and the actual Blender importer selected by their declared extension. 21 passed that importer. `Item3.fbx` and `Item3.glb` are incorrectly named copies of a valid compressed native Blender file and must never be sent to Unity as FBX/GLB. No supplied character, animal or machine contains a skeleton, skin weights or animation actions.

## Environment and method

- Authoritative audit: Blender **5.2.0 LTS**, `C:\Program Files\Blender Foundation\Blender 5.2\blender.exe`.
- Initial cross-check: Blender 4.5.1 LTS also imported the standard FBX/GLB files. Its newer-file warning on Item3 was resolved by rerunning the complete audit in Blender 5.2.
- CPU import and mesh analysis only. No GPU render, simulation, image generation, or source-side script execution was requested. `--disable-autoexec` and `use_scripts=False` protected native-file loading.
- FBX reader parsed binary FBX version 7400. GLB validation checked magic, version 2, declared total length, complete JSON/BIN chunks and actual glTF import. All ten genuine GLBs are self-contained and list no external buffer/image URI. Their manifest generator is `Khronos glTF Blender I/O v4.0.43`.
- Header inspection and SHA-256 were independent of file extensions. The script hashed every file before and after import. All 23 comparisons passed.
- Mesh connectivity uses real mesh edges; material islands use shared edges with identical material index; UV islands require matching endpoint UVs. A second, non-mutating calculation merges positions rounded to 0.000001 Blender units to distinguish UV seams from independent geometry.

## File inventory and importer outcome

| File in ASSET | Bytes | Actual header/content | Declared import | Tested loader |
| --- | --- | --- | --- | --- |
| Character1.fbx | 31,511,196 | FBX_BINARY | PASS | FBX importer |
| Character1.glb | 32,680,336 | GLB | PASS | glTF importer |
| Character2.fbx | 30,094,796 | FBX_BINARY | PASS | FBX importer |
| Character2.glb | 31,197,840 | GLB | PASS | glTF importer |
| Character3.fbx | 28,069,996 | FBX_BINARY | PASS | FBX importer |
| Character3.glb | 28,775,416 | GLB | PASS | glTF importer |
| Chicken.fbx | 25,521,276 | FBX_BINARY | PASS | FBX importer |
| Chicken.glb | 27,216,168 | GLB | PASS | glTF importer |
| Cow.fbx | 26,209,436 | FBX_BINARY | PASS | FBX importer |
| Cow.glb | 27,592,520 | GLB | PASS | glTF importer |
| Item1.fbx | 25,479,804 | FBX_BINARY | PASS | FBX importer |
| Item1.glb | 26,365,528 | GLB | PASS | glTF importer |
| Item2.fbx | 31,506,780 | FBX_BINARY | PASS | FBX importer |
| Item2.glb | 33,029,536 | GLB | PASS | glTF importer |
| Item3.blend | 36,296,225 | Zstandard-compressed native Blender | PASS | Blender native loader |
| Item3.fbx | 36,296,225 | Zstandard-compressed native Blender | REJECT | FBX importer |
| Item3.glb | 36,296,225 | Zstandard-compressed native Blender | REJECT | glTF importer |
| Machine.fbx | 27,589,996 | FBX_BINARY | PASS | FBX importer |
| Machine.glb | 28,681,364 | GLB | PASS | glTF importer |
| Machine2.fbx | 29,695,276 | FBX_BINARY | PASS | FBX importer |
| Machine2.glb | 31,171,924 | GLB | PASS | glTF importer |
| Machine3.fbx | 29,884,860 | FBX_BINARY | PASS | FBX importer |
| Machine3.glb | 32,067,172 | GLB | PASS | glTF importer |

## Item3 diagnosis

All three filenames have **36,296,225 bytes** and identical SHA-256:

`D3CF602F8A1BD499FC5F551B9434FCD51CEABB9E33F09DDB4115810169BC2DD4`

Their first 32 bytes are:

`28b52ffda04106010005c500d678c445f0b23c34efe984e33bcc7487db2ebe0c`

The first four bytes `28 b5 2f fd` identify a Zstandard stream, so absence of a literal `BLENDER` prefix does not mean the native blend is corrupt. The native Blender loader successfully read `Item3.blend`; loaded file version is `[5, 2, 44]`. It contains one mesh plus an authoring camera and light. These authoring objects are not gameplay assets.

Actual negative tests:

- `Item3.fbx`: FBX parser raised `OSError: Invalid header`; importer returned `Error: Couldn't open file ... (Invalid header)`.
- `Item3.glb`: glTF importer returned `Error: Bad glTF: json error: utf-8`.

Use `ASSET/Item3.blend` with Blender 5.2 as the sole Item3 processing input. Create a genuine new export under `ArtSource/Processed/`; do not rename either mislabeled duplicate or replace any original. The three files remain intact.

## Geometry measurements

All valid files contain one visible source mesh and one assigned material slot. Counts below describe imported source geometry, before any cleanup. CC means vertex-edge connected components. Welded CC is the diagnostic coordinate-merged count; it does not alter the mesh. UV seam boundaries in a GLB are not automatically physical holes.

| File | Meshes | Verts | Triangles | CC | Welded CC | UV islands | Boundary edges | >2-face edges | Zero-area faces |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Character1.fbx | 1 | 24939 | 49886 | 1 | 1 | 530 | 0 | 0 | 0 |
| Character1.glb | 1 | 32299 | 49886 | 530 | 1 | 530 | 13780 | 0 | 0 |
| Character2.fbx | 1 | 24916 | 49884 | 1 | 1 | 840 | 0 | 0 | 0 |
| Character2.glb | 1 | 33877 | 49884 | 840 | 1 | 840 | 16540 | 0 | 0 |
| Character3.fbx | 1 | 24989 | 49998 | 1 | 1 | 715 | 0 | 0 | 0 |
| Character3.glb | 1 | 33220 | 50004 | 715 | 1 | 715 | 15306 | 0 | 0 |
| Chicken.fbx | 1 | 25002 | 50000 | 1 | 1 | 512 | 0 | 0 | 0 |
| Chicken.glb | 1 | 31172 | 50000 | 512 | 1 | 512 | 11718 | 0 | 0 |
| Cow.fbx | 1 | 25004 | 50004 | 1 | 1 | 271 | 0 | 0 | 0 |
| Cow.glb | 1 | 29272 | 50004 | 271 | 1 | 271 | 8228 | 0 | 0 |
| Item1.fbx | 1 | 25002 | 50000 | 1 | 1 | 442 | 0 | 0 | 0 |
| Item1.glb | 1 | 31200 | 50000 | 442 | 1 | 442 | 11728 | 0 | 0 |
| Item2.fbx | 1 | 25048 | 50132 | 2 | 2 | 896 | 0 | 0 | 0 |
| Item2.glb | 1 | 35550 | 50132 | 896 | 2 | 896 | 19482 | 0 | 0 |
| Item3.blend | 1 | 33199 | 49998 | 717 | 1 | 717 | 15592 | 0 | 0 |
| Machine.fbx | 1 | 25025 | 50054 | 1 | 1 | 574 | 0 | 0 | 0 |
| Machine.glb | 1 | 32488 | 50068 | 577 | 1 | 577 | 14008 | 0 | 0 |
| Machine2.fbx | 1 | 25006 | 50012 | 1 | 1 | 618 | 0 | 0 | 0 |
| Machine2.glb | 1 | 32221 | 50020 | 620 | 1 | 620 | 13498 | 0 | 0 |
| Machine3.fbx | 1 | 25002 | 50000 | 1 | 1 | 569 | 0 | 0 | 0 |
| Machine3.glb | 1 | 32072 | 50000 | 569 | 1 | 569 | 13354 | 0 | 0 |

The paired FBX and GLB files are not always identical geometry: Character3 has 49,998 vs 50,004 triangles; Machine has 50,054 vs 50,068; Machine2 has 50,012 vs 50,020. Do not silently claim interchangeability or replace a processed version without checking appearance. All FBX meshes have zero boundary, nonmanifold and zero-area-face counts in this audit.

## Hierarchy, semantic splitting and limitations

Every FBX is a single `node_0` mesh without parent hierarchy. Each GLB is also a single mesh, with hundreds of disconnected vertex groups caused by UV seam duplication: after coordinate merging they become one geometric component, except Item2 which has two. Material islands therefore follow the same geometry/UV fragments; there are no separate named material regions for body, head, limbs, doors, wheels or conveyors. `Separate by Loose Parts` would create UV fragments, not semantic machine or character pieces.

Item2 FBX has one main closed component with 24,837 vertices / 49,714 faces and a small closed component with 211 vertices / 418 faces. The small component bounds are X [-0.226705, -0.200899], Y [-0.432419, -0.417147], Z [0.036869, 0.046189]. Its role has not been visually identified; do not label or delete it merely because it is small.

This audit validates format, parseability, connectivity, material/UV presence and rig/animation absence. It does **not** prove semantic segmentation, visual normals, anatomical alignment, deformation quality, animation quality, Unity material conversion, collision, or runtime performance. Those require visual inspection, authored processing and gameplay tests. No blind automatic split or rig has been accepted by this audit.

## Material, UV and animation inventory

| File | Assigned material slots | Packed images | Image resolution | All packed | Armatures | Actions |
| --- | --- | --- | --- | --- | --- | --- |
| Character1.fbx | 1 | 4 | [4096, 4096] | True | 0 | 0 |
| Character1.glb | 1 | 3 | [4096, 4096] | True | 0 | 0 |
| Character2.fbx | 1 | 4 | [4096, 4096] | True | 0 | 0 |
| Character2.glb | 1 | 3 | [4096, 4096] | True | 0 | 0 |
| Character3.fbx | 1 | 4 | [4096, 4096] | True | 0 | 0 |
| Character3.glb | 1 | 3 | [4096, 4096] | True | 0 | 0 |
| Chicken.fbx | 1 | 4 | [4096, 4096] | True | 0 | 0 |
| Chicken.glb | 1 | 3 | [4096, 4096] | True | 0 | 0 |
| Cow.fbx | 1 | 4 | [4096, 4096] | True | 0 | 0 |
| Cow.glb | 1 | 3 | [4096, 4096] | True | 0 | 0 |
| Item1.fbx | 1 | 4 | [4096, 4096] | True | 0 | 0 |
| Item1.glb | 1 | 3 | [4096, 4096] | True | 0 | 0 |
| Item2.fbx | 1 | 4 | [4096, 4096] | True | 0 | 0 |
| Item2.glb | 1 | 3 | [4096, 4096] | True | 0 | 0 |
| Item3.blend | 1 | 3 | [4096, 4096] | True | 0 | 0 |
| Machine.fbx | 1 | 4 | [4096, 4096] | True | 0 | 0 |
| Machine.glb | 1 | 3 | [4096, 4096] | True | 0 | 0 |
| Machine2.fbx | 1 | 4 | [4096, 4096] | True | 0 | 0 |
| Machine2.glb | 1 | 3 | [4096, 4096] | True | 0 | 0 |
| Machine3.fbx | 1 | 4 | [4096, 4096] | True | 0 | 0 |
| Machine3.glb | 1 | 3 | [4096, 4096] | True | 0 | 0 |

All meshes have one `UVMap`. FBX embeds separate base-color, metallic, normal and roughness images; GLB/native Item3 embeds base color, normal and combined metallic/roughness. Embedded images load at 4096 x 4096. Different source assets reuse image filenames such as `texture_pbr_20250901.png`; processed texture exports must use asset-specific folders or unique filenames to prevent accidental cross-asset overwrites. FBX image paths may mention `ASSET/output.fbm`, but the image payload is packed; the audit did not create that directory.

No armatures, vertex groups, skinning modifiers or animation actions were found. Characters need authored humanoid rigs; Cow needs a quadruped skeleton; Chicken needs a bird skeleton. Author and verify the required poses and locomotion/actions before claiming them animated. Machines should use transform animation of visually verified moving parts, not arbitrary skeletal rigs.

## Recommended processing inputs

| Role | Preferred source | Reason / next gate |
| --- | --- | --- |
| Initial player | ASSET/Character1.fbx | Closed connected mesh; 24,939 vertices / 49,886 triangles; fewer UV islands than the other characters. Visual pose review and rig/weight/pose tests still required. |
| Worker/customer variants | ASSET/Character2.fbx, ASSET/Character3.fbx | Valid closed connected alternate sources. Do not reuse Character1 joint positions without checking each anatomy. |
| Cow | ASSET/Cow.fbx | Closed connected source; 25,004 vertices / 50,004 triangles; packed PBR maps; quadruped rig required. |
| Chicken | ASSET/Chicken.fbx | Closed connected source; 25,002 vertices / 50,000 triangles; packed PBR maps; bird rig required. |
| Processing machine candidates | ASSET/Machine.fbx, ASSET/Machine2.fbx, ASSET/Machine3.fbx | All valid connected sources. Choose by actual visible function, then visually segment mechanical moving parts. |
| Props | ASSET/Item1.fbx, ASSET/Item2.fbx | Valid geometry; verify real semantic role visually and review Item2 small component. |
| Item3 prop | ASSET/Item3.blend | Only correctly labeled Item3 source; load in Blender 5.2 and export a true derivative. |

Recommended order: visually inspect each candidate; establish semantic part selection; normalize orientation/scale/pivots in memory; author deformation rigs only where necessary; verify weights with extreme pose tests; decimate/retopologize with silhouette and UV checks; create LODs and game-sized texture derivatives; export to `ArtSource/Processed/`; import a verified copy to `Assets/_Game/Art/Imported/`. Source 4K images and roughly 50K-triangle meshes are authoring inputs, not proven final runtime budgets. Preserve originals and do not treat suggested optimization targets as measured performance.

## SHA-256 preservation evidence

| File | SHA-256 before and after | Unchanged |
| --- | --- | --- |
| Character1.fbx | AC2F17E78BEA0B67480D53E41693057D598C02AF3FE3F950EC080A399562ACF9 | True |
| Character1.glb | 1AE30F17000DA96665CE58639E7E35B51494135F46C094AB937852FDE8D1E85E | True |
| Character2.fbx | DA2C345AAF8F4A6A0CF3436F4F7C6783645EE7CFE416B198FE7163A15F71B2E8 | True |
| Character2.glb | 19BEAF240C9B3D5B6330453206071E37DB2892E869CD70360D6F9A91D3BEAF79 | True |
| Character3.fbx | A6E64CA80966A6AC56F499D2F683B9F13D789947ABE19FD599E25EC05219293A | True |
| Character3.glb | 0FD47CCCCEFA37778DEBAFEF0C2FDEA3B8E9039D4015833C18DB2A69E5E904C7 | True |
| Chicken.fbx | 8322A624393CC054EC81BF6FAB6E301845A5599D1D699422D58C3883D8FF0FB7 | True |
| Chicken.glb | 94C271CCF2467412FB5433D14F4F749F2AB0A6919C97A4C72454B6A5EFB8EEC8 | True |
| Cow.fbx | E3DF0E4CD8F2315D9364FE6140FA5B65E55570BB076AF34A467A54D35B91F829 | True |
| Cow.glb | CDBA973B66B5565C97BD08D22C86701059C0B6B87044008E2EF463EB69B1ECC0 | True |
| Item1.fbx | D166EAC98CA50D2483AC06544BF12FEC7535C249B68717B9B2903D5C796CC850 | True |
| Item1.glb | FDFE7F92F2E015BBFF12320D24B628A501FC98E51B0466A2F36EF7AFBFC2CF31 | True |
| Item2.fbx | 6ED330C0E3256EED1E7FDE3646521BBEA2EECF9B788D3626B66872E9B46E5B69 | True |
| Item2.glb | BDA4089E0037085FAC5EBD2793078386837C8AA2221D3F31326CE7AF829D21E7 | True |
| Item3.blend | D3CF602F8A1BD499FC5F551B9434FCD51CEABB9E33F09DDB4115810169BC2DD4 | True |
| Item3.fbx | D3CF602F8A1BD499FC5F551B9434FCD51CEABB9E33F09DDB4115810169BC2DD4 | True |
| Item3.glb | D3CF602F8A1BD499FC5F551B9434FCD51CEABB9E33F09DDB4115810169BC2DD4 | True |
| Machine.fbx | CFF70CB0132345614E30AE5F9B34C9EF43C004CBFD44572AD1E18D5E857EB65E | True |
| Machine.glb | 2F83874D6CC935FC73300B13F2F48FB8F1C0CBF4D206150E889AFCAF31B3E93B | True |
| Machine2.fbx | 1F9FF8AB175AD06EA5B2A413844CE99D2715BDACE3A11804FB9F6C6A533C0073 | True |
| Machine2.glb | 7F6C13BFA9DBBA945EB631E367ED62C95FE2DB974F182B7B0D514E467395EC07 | True |
| Machine3.fbx | 609E6E7A4BF593192876B3B3C3DFADB6BA67CB073E5A50464E2DA2FB531961A0 | True |
| Machine3.glb | 90B81A4A491AF70A2605F6F5192FD0C121BEBE4168EB3E5CF545E781DF18C2A3 | True |

Only exact duplicate group: `Item3.blend`, `Item3.fbx`, `Item3.glb`. All other source SHA-256 values differ. The source directory still contains exactly the original 23 files and no importer-created side files.

## Reproduce

```powershell
& "C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" --background --factory-startup --disable-autoexec --threads 4 --python "D:\APP\TYCOON\Tools\Blender\audit_assets.py" -- --source "D:\APP\TYCOON\ASSET" --output "D:\APP\TYCOON\work\asset-audit\audit.json"
& "C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" --background --factory-startup --disable-autoexec --python "D:\APP\TYCOON\Tools\Blender\write_audit_docs.py" -- --report "D:\APP\TYCOON\work\asset-audit\audit.json" --documentation "D:\APP\TYCOON\Documentation"
```

The JSON is an intermediate report; after verifying these documents and confirming the audit process has exited, remove only that task-created file and its empty task directory. No source copies, backup files or archives are required.
