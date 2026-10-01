# Asset sources and usage record

Last source audit: 2026-10-01T15:06:21+07:00. See `ASSET_AUDIT.md` for file hashes, format disposition and import evidence.

## User-provided input

The user explicitly supplied the `ASSET/` directory and authorized internal use in TYCOON. Provenance is `USER_PROVIDED`. No creator identification, redistribution license or separate license metadata was supplied with the 23 files, so every group is marked `LICENSE_METADATA_UNKNOWN`. This records authorization for internal project use; it does not invent a license or grant public redistribution/relicensing rights. Preserve source files.

| Asset group | Source/local files | Provenance | Creator | License metadata | Usage basis |
| --- | --- | --- | --- | --- | --- |
| Character1 | `ASSET/Character1.fbx`, `ASSET/Character1.glb` | USER_PROVIDED | Not supplied | LICENSE_METADATA_UNKNOWN | Internal project use authorized by master task |
| Character2 | `ASSET/Character2.fbx`, `ASSET/Character2.glb` | USER_PROVIDED | Not supplied | LICENSE_METADATA_UNKNOWN | Internal project use authorized by master task |
| Character3 | `ASSET/Character3.fbx`, `ASSET/Character3.glb` | USER_PROVIDED | Not supplied | LICENSE_METADATA_UNKNOWN | Internal project use authorized by master task |
| Chicken | `ASSET/Chicken.fbx`, `ASSET/Chicken.glb` | USER_PROVIDED | Not supplied | LICENSE_METADATA_UNKNOWN | Internal project use authorized by master task |
| Cow | `ASSET/Cow.fbx`, `ASSET/Cow.glb` | USER_PROVIDED | Not supplied | LICENSE_METADATA_UNKNOWN | Internal project use authorized by master task |
| Item1 | `ASSET/Item1.fbx`, `ASSET/Item1.glb` | USER_PROVIDED | Not supplied | LICENSE_METADATA_UNKNOWN | Internal project use authorized by master task |
| Item2 | `ASSET/Item2.fbx`, `ASSET/Item2.glb` | USER_PROVIDED | Not supplied | LICENSE_METADATA_UNKNOWN | Internal project use authorized by master task |
| Item3 | `ASSET/Item3.blend`, `ASSET/Item3.fbx`, `ASSET/Item3.glb` | USER_PROVIDED | Not supplied | LICENSE_METADATA_UNKNOWN | Internal project use authorized by master task |
| Machine | `ASSET/Machine.fbx`, `ASSET/Machine.glb` | USER_PROVIDED | Not supplied | LICENSE_METADATA_UNKNOWN | Internal project use authorized by master task |
| Machine2 | `ASSET/Machine2.fbx`, `ASSET/Machine2.glb` | USER_PROVIDED | Not supplied | LICENSE_METADATA_UNKNOWN | Internal project use authorized by master task |
| Machine3 | `ASSET/Machine3.fbx`, `ASSET/Machine3.glb` | USER_PROVIDED | Not supplied | LICENSE_METADATA_UNKNOWN | Internal project use authorized by master task |

## Format exclusion

`ASSET/Item3.fbx` and `ASSET/Item3.glb` are mislabeled byte-identical copies of `ASSET/Item3.blend`, not true FBX/GLB exports. Preserve them as supplied but exclude them from Unity import. Use the valid native `.blend` through the processing pipeline.

## Derivative destinations

Approved pipeline: `ASSET/` -> Blender processing scripts in `Tools/Blender/` -> `ArtSource/Processed/` -> `Assets/_Game/Art/Imported/`. Processed models/textures inherit their source provenance and unknown license metadata; creating a derivative does not relicense the source. Names and material references must identify the originating asset to prevent the identical embedded texture filenames from colliding.

The source audit itself creates no processed models, animations, texture exports, backups or archives. Record each delivered derivative and the script/source used when processing is actually completed; do not list planned exports as existing artifacts.

## External and original assets

No external online assets were downloaded or added by this audit. No online license was inferred from the generic embedded texture names or the glTF exporter string. Any future external asset entry must include asset name, exact source URL, creator, explicit usage terms/license and local destination. Original project-created support geometry/audio/UI should be labeled as original with its generator/source file and output location when authored.
