# Forest battlefield art direction

- Landscape mobile 2D battlefield with a shallow elevated three-quarter ground plane, enough depth for three staggered rows. This is a multi-character autobattler arena, not a single-line platformer.
- Warm ochre stone and soil, muted sage/teal hills, dark ink outlines, broad readable shapes.
- The existing chibi Spine hero remains the character style reference. Ground and scenery use quieter contrast than actors and attack effects.
- Place props as separate transparent cutouts, bottom-center pivots; no background image containing the whole playable composition.
- Soft upper-left illumination; no baked directional shadow across other props. Wooden dummy is a bullseye training target, not a living enemy.
- Gold sword arcs and arrows; cyan arcane orb/ring. Short-lived hits; no repeated full-screen flashes or camera shake.
- AI art is an initial prototype family. Final art approval requires user inspection in engine; it is not described as production-ready.

## Forest revision, 2026-10-12

- Structure references: Unity 2D Game Kit and STR's 2D Hand Painted Platformer Environment. No third-party pack images were copied.
- `Assets/Resources/Battle/Art/Forest/Background.jpg`: distant scenery only, 1920 x 640, JPEG quality 90 with full chroma.
- `Trees.png`: two individually sliced tree sprites, 1280 x 768 sheet.
- `Vegetation.png`: six independently sliced stones, shrub, grass, fern and signpost sprites, 768 x 512 sheet.
- `Tileset.png`: six contiguous ground panels in a 3-column, 2-row arrangement, with four-pixel extruded gutters. These assemble in named row/column order; they are not arbitrary seamless repeat tiles.
- The floor is a visible top surface and extends under the lower UI; no exposed earth cross section or floating island edge.
- Each sheet has a `.slices.json` defining pixel rectangles and pivots. The importer sets Multiple mode and stable names. Ground pivots are upper-left; prop pivots are bottom-center.
- PNGs preserve RGBA after resampling, without indexed-color reduction. Desktop imports use BC7; Android/iOS use ASTC 6x6. Mipmaps and CPU-readable copies are disabled. Mobile compression still requires device validation.
- `BattleStage` provides eight ally and eight enemy placement anchors with foot-Y sorting. Anchors prepare the map for parties; current dice battle mechanics still run one hero versus a dummy.
- Previous baked scene is preserved in `Backups/DiceTraining-before-forest.unity`. The editor migrates old generated groups only once per layout version.
- Reproduce asset normalization using `Tools/pack_forest_assets.py`; provenance and exact file sizes are in `Docs/forest-assets-manifest.json`.
- In Unity use `Dice Hero/Validate Forest Preview` to render the current map and a six-character placement preview without saving preview actors into the gameplay scene.
