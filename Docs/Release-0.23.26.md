# 0.23.26 tracking and HUD repair candidate

User report: the opening title was very low, FPS was missing, gaze highlights worked but all pinch interactions failed.

Device evidence in `Builds/Evidence/session-output-20261031-v2326-input-hud/before.log` confirms native IPC registration failures for input and tracking, head/controller tracking-provider startup failures, headPose=False and FPS visualActive=False. Input actions and the SpatialInputDevice were present and enabled, but callbacks stayed at zero. This does not implicate the partner's mesh simplification or collision shapes.

Changes:
- Delay XR initialization until the Spatial camera's Stage is open and focused for a short settling interval.
- Require two valid head-pose samples for Ready; use public XRInputSubsystem Stop/Start for at most two recovery attempts per foreground interval.
- Reapply spatial mode after pause/focus transitions, including brief transitions between monitor ticks.
- Share the validated Unity XR head-pose reader between FPS and loading UI; reacquire the head device on each read so a replacement device is not held stale.
- Place loading at head-local (0, 0, 1.1 m). Hide the authored fallback at runtime while tracking is unavailable. Keep normal TMP export initialization to avoid missing label meshes.

Preserved: package ID and saves; gaze highlight; pinch bindings, single-hand move, double-pinch return and two-hand gesture arbitration; FPS mesh glyphs; partner 0.23.25 geometry/performance changes; deferred canyon prefab.

Validation: the second full build attempt passed 9 regression suites, including 114 performance/loading checks. The first attempt exposed a TMP initialization problem with an initially inactive loading parent; that approach was removed. Final APK validation/install status belongs to release-validation.json in the evidence directory.

Limits: subsystem retries do not prove recovery of a disconnected native IPC session. No unsupported reconnect API or SDK modifications were introduced. At the time of writing, Swan was asleep; the same-APK restart comparison has not run in the foreground. Native gesture delivery, HUD placement/following, sleep/resume behavior and performance remain pending a worn-device retest.

Final delivery: APK build and all packaged checks passed; signed v2; 504260775 bytes. SHA256 `75fbd7975b5b8f15365f2b78f0d239c95c2746a57be209a119678f34e14a7878`. Installed successfully, device package manager confirms 0.23.26/code26. Data retained. Not launched while Swan sleeps; native validation remains pending.
