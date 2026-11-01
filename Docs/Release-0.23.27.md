# 0.23.27 — preserve FPS glyph geometry

The user reported broken/missing FPS strokes. This is separate from the head-pose failure in 0.23.25/26.

Confirmed cause: `SwanJourneyWorkshopMeshBudget.Apply` traversed the entire scene, including the FPS. Its default Surface simplifier reduced the fixed `APP     FPS` mesh from 106 to 70 triangles. The exported 0.23.26 APK contains the 70-triangle mesh. The 33 numeric variants were below the simplification threshold and retained their original geometry. A raster of actual mesh triangles confirms severe shape changes, despite similar overall bounds/area.

Repair: exclude every mesh under `SwanFpsMeshText` from surface decimation. Keep the original texture-free 5x7 glyph style, size, material, head-following transform, and 2 Hz number updates. No train, circular-part, canyon or assembly-guide budget changes are included. Source scene geometry increases by only 34 triangles.

Regression coverage: the scene check verifies original mesh references for fixed text and all 33 variants. APK inspection now verifies their authored triangle counts, rather than only requiring nonempty meshes. Running the new check against the previous APK fails exactly on the fixed label (70/106).

Evidence: `Builds/Evidence/session-output-20261031-v2327-fps-glyphs/old-apk-regression.json`, `glyph-geometry-comparison.json`, and `fps-before-after.png` (top: reduced, bottom: original). Final build and install results belong to this directory's release-validation.json. FPS glyph visual acceptance is recorded below; other scene geometry remains unaccepted.

Final delivery: 9 suites, 149 performance/loading checks, 297 APK checks and v2 signature passed. APK 504265215 bytes, SHA256 `da7e786b0abf5fff601de0c4c89faef55bf9b675cf8862d79087cf20d096a089`. Installed successfully; package manager confirms 0.23.27/code27. Saves retained. On 2026-10-31 the user confirmed on Swan: “FPS 字形完整，读数正常”. FPS glyph completeness and legibility passed; circular meshes/outlines, performance and sleep/resume stability are not covered by this acceptance.
