# GoldenHook — MVP Implementation Report

**Project:** GoldenHook VR Grappling Physics Prototype
**Engine:** Unity 6 (6000.3.7f1)
**XR Stack:** OpenXR 1.16.1 · XRI 3.3.1 · Input System 1.18.0
**Target Platform:** Meta Quest (via Quest Link for in-editor testing)
**Date Completed:** 2026-03-12

---

## Overview

GoldenHook is a mechanic-first VR prototype where the player uses dual grappling hooks driven by real segmented physics chains. The player hooks objects thrown by an enemy, controls them through chain tension and retraction, then releases at the right moment to redirect objects back at the monster.

**Design principle:** No fake placeholders. Every system uses real physics — launched projectile, segmented `ConfigurableJoint` chain, kinematic anchor pull retraction, velocity-sampled release throw.

---

## Architecture Summary

The implementation is divided into 7 batches. Each batch was wired into the scene automatically via a Unity Editor menu item (`GoldenHook → N. Setup Batch N`), with all Inspector references assigned programmatically using `SerializedObject` — zero manual wiring required.

---

## Batch 0 — Project Configuration

**Purpose:** Folder structure, input asset, editor tooling foundation.

| File | Role |
|------|------|
| `Assets/Input/GrappleInputActions.inputactions` | Input System asset. Two action maps: `Grapple` (fire/retract per hand) and `XRTracking` (device position/rotation/isTracked/trackingState). Per-hand qualified paths avoid conflicts with default InputSystem_Actions. |
| `Assets/Scripts/Editor/GoldenHookSetup.cs` | Static editor class. 9 `[MenuItem]` methods that build and wire the entire scene programmatically from scratch. |

**Physics Layer Setup (Menu Item 1):**

| Layer | Index | Collision Ignores |
|-------|-------|-------------------|
| Player | 8 | ChainLink, GrappleHook |
| ChainLink | 9 | ChainLink, Player |
| GrappleHook | 10 | Player, ChainLink |
| Throwable | 11 | ChainLink, GrappleHook |
| Arena | 12 | — |

---

## Batch 1 — Input & Hand Ownership

**Purpose:** Map XR input to per-hand intent. No physics coupling here.

| File | Role |
|------|------|
| `Core/GrappleHand.cs` | `enum Hand { Left, Right }` and `enum GrappleState { Idle, Fired, Attached, Retracting }` |
| `Core/VRPrototypeRigRefs.cs` | Scene-wide reference hub: `XROrigin`, left/right controller `GameObject`s, left/right hand `Transform`s |
| `Core/XRHandGrappleInput.cs` | Reads `GrappleInputActions`. Fire uses `action.performed` callback + consume flag (frame-safe). Retract exposed as `IsPressed()` for direct FixedUpdate polling. |
| `Core/GrappleHandController.cs` | Per-hand state machine in FixedUpdate. Events: `OnFireRequested`, `OnRetractChanged`, `OnReleaseRequested`, `OnAttached`. `ForceRelease()` for programmatic detach. `SimulateFire/Retract/Release` methods for stress testing. |
| `Core/HandAnchorProvider.cs` | Kinematic `Rigidbody` child of each controller. Follows hand transform via `MovePosition`/`MoveRotation` in FixedUpdate. `detectCollisions = false`. Acts as the player-side chain joint anchor. |

**Key design decision:** Fire input captured via callback (never missed regardless of frame rate), state transitions dispatched in FixedUpdate so downstream physics systems see consistent state within a physics step.

---

## Batch 2 — Hook Launch & Attach Resolution

**Purpose:** Physical hook projectile that flies, hits, and attaches to grappleable objects.

| File | Role |
|------|------|
| `Hook/GrappleHookProjectile.cs` | `[RequireComponent(Rigidbody, SphereCollider)]`. `Launch(pos, velocity)` fires it. `OnCollisionEnter` → `GrappleAttachResolver.Resolve()`. On attach: goes kinematic, parents to target. Miss detection via `maxRange`. Events: `OnAttached`, `OnMissed`, `OnDetached`. |
| `Hook/GrappleAttachResolver.cs` | Static. `Resolve(Collision)` walks up hierarchy via `GetComponentInParent<GrappleTarget>()`. Returns `GrappleAttachResult`. |
| `Hook/GrappleAttachResult.cs` | Data class: `bool IsValid`, `Rigidbody TargetBody`, `Vector3 WorldAttachPoint`, `GrappleTarget Target` |
| `Hook/GrappleTarget.cs` | MonoBehaviour on grappleable objects. `bool IsGrappleable`, optional `Transform AttachSocket`. `GetAttachPoint(hitPoint)` returns socket or raw hit point. |
| `Hook/GrappleLauncher.cs` | Per-hand. Subscribes to `GrappleHandController` events. On fire: instantiates hook prefab. On attach: calls `GrappleChainFactory.SpawnChain()` + begins release velocity tracking. On release: applies velocity BEFORE chain destroy. Fires `GrappleRuntimeEvents` at each lifecycle point. |

**Prefab created by setup:** `Assets/Prefabs/GrappleHook.prefab` — sphere (0.06m), `SphereCollider`, `Rigidbody` (ContinuousDynamic, Interpolate, no gravity), `GrappleHookProjectile`.

---

## Batch 3 — Segmented Chain Physics

**Purpose:** Real multi-link physics chain connecting player hand to attached target.

| File | Role |
|------|------|
| `Chain/GrappleChainConfig.cs` | `[CreateAssetMenu]` ScriptableObject. Tunable: `maxLinkCount=10`, `linkSpacing=0.15m`, `linkMass=0.05kg`, `linkDrag=2`, `linkAngularDrag=5`, `solverIterations=16`, `angularDamper=10`, `linkRadius=0.02m` |
| `Chain/GrappleChainLink.cs` | Marker component + `Rigidbody Body` + `int Index` |
| `Chain/GrappleChainJointDriver.cs` | Owns `ConfigurableJoint`. `Initialize()` sets Locked xyzMotion + Free angular + angular damper drive + `enablePreprocessing=false`. `Reconnect(newBody)` updates joint in-place (no destroy/recreate — avoids solver-reset jolt on retraction). |
| `Chain/GrappleChainFactory.cs` | Static. `SpawnChain(playerAnchor, attachResult, config)`. Computes `linkCount = Clamp(Round(dist/spacing), 1, max)`. Places link centers at `startPos + dir * spacing * (i + 0.5f)` — joints are exactly satisfied at spawn, no spawn jolt. Last link gets a second joint to the target body. |
| `Chain/GrappleChainRuntime.cs` | Runtime container: `Links[]`, `PlayerAnchor`, `TargetBody`, `AttachResult`. `RemovePlayerSideLink()` rewires link 1's joint to player anchor in-place, destroys link 0, rebuilds array. `Release()` cleans endpoint, destroys chain GO. |
| `Chain/GrappleChainEndpoint.cs` | Added to target by factory at attach time. Tracks `AttachedChain`, `IsAttached`. Polled passively by `ThrowablePhysicsObject` — no cross-system coupling. |
| `Chain/GrappleChainCollisionManager.cs` | `IgnoreTargetColliders()` — per-instance collision filtering between chain links and target object. |

**Joint configuration:** Each link uses `ConfigurableJoint` with all three linear DOFs `Locked` (inextensible link) and all angular DOFs `Free` (ball-and-socket), plus angular damper drive. The chain behaves like a real physical chain — taut under tension, slack when compression is applied.

**Config asset:** `Assets/Configs/GrappleChainConfig.asset`

---

## Batch 4 — Retraction, Tension & Release

**Purpose:** Player-controlled chain length change, tension monitoring, and release-throw velocity.

| File | Role |
|------|------|
| `Control/GrappleLengthController.cs` | `AddRetraction(meters, chain)`: accumulates retraction, calls `chain.RemovePlayerSideLink()` when accumulator ≥ `chain.LinkSpacing`. Discrete link removal = smooth retraction without jolt. |
| `Control/GrappleRetractionMotor.cs` | FixedUpdate: when `State == Retracting`, calls `lengthController.AddRetraction(retractSpeed * fixedDeltaTime, chain)`. `retractSpeed = 2 m/s`. |
| `Control/GrappleTensionAnalyzer.cs` | Reads `driver.Joint.currentForce.magnitude` from the player-side chain link's joint. Exposes `CurrentTension` (Newtons). |
| `Control/GrappleDetachController.cs` | Auto-detaches if `tensionAnalyzer.CurrentTension > maxTension`. `maxTension = 0` (disabled by default — enable and tune to add a break threshold). |
| `Control/GrappleReleaseVelocityHelper.cs` | `BeginTracking(targetBody)` starts sampling `targetBody.linearVelocity` each FixedUpdate into a rolling queue (6 samples). `ApplyRelease()` computes linearly-weighted average, clamped to `maxReleaseSpeed = 20 m/s`, and applies as the player's throw velocity. Called by `GrappleLauncher` before chain is destroyed. |

---

## Batch 5 — Combat Loop

**Purpose:** Monster throws objects, player intercepts and redirects them back.

| File | Role |
|------|------|
| `Combat/ThrowablePhysicsObject.cs` | State machine: `Pooled → ThrownByMonster → HookedByPlayer → ReleasedByPlayer → Spent`. Polls `GrappleChainEndpoint` passively in FixedUpdate to detect hook/release. `IsValidPlayerProjectile()` returns true if released within `validHitWindow = 6s`. |
| `Combat/ThrowableObjectStateTracker.cs` | Companion: records `StateEnteredTime`, `TimeInCurrentState`, `LifecycleCount` (increments on each new throw). |
| `Combat/ThrowableObjectPool.cs` | Queue-based pool. `preloadCount = 5`. `Get()` dequeues or instantiates. `Return(obj)` reparents, deactivates, enqueues. |
| `Combat/MonsterThrower.cs` | Throws every `throwInterval ± throwIntervalVariance` seconds (default 3 ± 1s). Aims at player + random spread. `throwSpeed = 7 m/s`. |
| `Combat/MonsterHitReceiver.cs` | `OnCollisionEnter`: validates `throwable.IsValidPlayerProjectile()`, calls `Recycle()`, increments `HitCount`. `OnValidHit` + `OnDeath` events. `hitsToKill = 5`. Fires `GrappleRuntimeEvents.RaiseMonsterHit/Defeated`. |
| `Combat/ArenaRecycleZone.cs` | Trigger box at y=−10. `OnTriggerEnter` → `throwable.Recycle()`. Catches anything that escapes the arena. |

**Prefab created by setup:** `Assets/Prefabs/Throwable.prefab` — sphere (0.25m), `SphereCollider`, `Rigidbody` (ContinuousDynamic, Interpolate), `GrappleTarget`, `ThrowablePhysicsObject`, `ThrowableObjectStateTracker`, Throwable layer.

---

## Batch 6 — Debug, Telemetry & Haptics

**Purpose:** Scene-view visualization, event logging, controller haptics, automated stress testing.

| File | Role |
|------|------|
| `Debug/GrappleRuntimeEvents.cs` | Static event bus. Events: `OnHookFired(Hand)`, `OnHookAttached(Hand, Vector3)`, `OnHookMissed(Hand)`, `OnChainReleased(Hand)`, `OnMonsterHit(int)`, `OnMonsterDefeated()`. Fired from `GrappleLauncher` and `MonsterHitReceiver`. |
| `Debug/GrappleDebugGizmos.cs` | `OnDrawGizmos` during play. Finds `GrappleChainRuntime` and `GrappleHookProjectile` instances at runtime. Draws link spheres, link-to-link lines, anchor line, attach point sphere. |
| `Debug/GrappleTelemetry.cs` | Subscribes to `GrappleRuntimeEvents`. Logs timestamped events to Console. Inspector shows live counters: `ShotsFired`, `Hits`, `Misses`, `Attaches`, `Releases`, `MonsterHits`, `SessionTime`. Final defeated log includes accuracy %. |
| `Debug/GrapplePhysicsProfile.cs` | Inspector readout updated every frame: `ActiveLinkCount`, `CurrentTensionN`, `TargetSpeed (m/s)`, `TargetDistance (m)`. No custom editor needed — public fields visible in play mode. |
| `Debug/ImpactFeedback.cs` | `[RequireComponent(AudioSource)]`. Subscribes to events and plays assigned `AudioClip`s. Optional `ParticleSystem` refs for attach/hit VFX. Functional without any clips assigned. |
| `Debug/GrappleHapticsDriver.cs` | Per-hand. Uses `InputDevices.GetDevicesAtXRNode(XRNode.LeftHand/RightHand)` → `SendHapticImpulse(channel, amplitude, duration)`. Tunable impulse profiles for fire, attach, miss, release, monster hit. |
| `Debug/ChainStressTester.cs` | Right-click in Inspector → "Run Stress Test". Programmatically fires, waits for attach, retracts, releases, repeats N times. Logs successes/failures. Uses `GrappleHandController.Simulate*()` methods. |

---

## Scene Setup — Step-by-Step

After importing the project, run these menu items in order (only once per scene):

| Step | Menu Item | What It Does |
|------|-----------|--------------|
| 1 | `GoldenHook → 1. Setup Physics Layers` | Creates 5 named layers + collision matrix |
| 2 | *(skip — use XRI starter prefab)* | Drop XRI "XR Origin (XR Rig)" prefab into scene manually |
| 3 | `GoldenHook → 3. Enable Quest Link Settings` | Enables OculusTouch + MetaQuestTouchPlus + MetaQuestTouchPro OpenXR profiles for Standalone |
| 4 | `GoldenHook → 4. Wire Grapple onto XRI Prefab` | Adds grapple components to XROrigin; finds controllers by name ("left"/"right" substring search) |
| 5 | `GoldenHook → 5. Setup Batch 2 (Hook + Launcher)` | Creates `GrappleHook.prefab`, spawns Test Throwable, adds `GrappleLauncher` to controllers |
| 6 | `GoldenHook → 6. Setup Batch 3 (Chain Config)` | Creates `GrappleChainConfig.asset`, wires into launchers |
| 7 | `GoldenHook → 7. Setup Batch 4 (Retraction + Release)` | Adds retraction/tension/detach/release components to controllers, wires all refs |
| 8 | `GoldenHook → 8. Setup Batch 5 (Combat Loop)` | Creates `Throwable.prefab` + pool, wires MonsterThrower/HitReceiver, adds recycle zone |
| 9 | `GoldenHook → 9. Setup Batch 6 (Debug + Haptics)` | Adds debug host with gizmos/telemetry/haptics, stress tester on controllers |

All Inspector references are assigned programmatically by the setup scripts. No manual wiring is needed.

---

## Data Flow Diagram

```
[Quest Controller Input]
        │
        ▼
 XRHandGrappleInput
  (fire callback / retract IsPressed)
        │
        ▼
 GrappleHandController
  (state machine: Idle → Fired → Attached → Retracting)
        │ OnFireRequested
        ▼
 GrappleLauncher ──────────────► GrappleRuntimeEvents (event bus)
        │ Instantiate                    │
        ▼                                ▼
 GrappleHookProjectile    ImpactFeedback / GrappleHapticsDriver / GrappleTelemetry
  (flight physics)
        │ OnCollisionEnter
        ▼
 GrappleAttachResolver
  (find GrappleTarget in hierarchy)
        │
        ▼
 GrappleChainFactory
  (spawn ConfigurableJoint links: satisfied at spawn)
        │
        ▼
 GrappleChainRuntime ◄──── GrappleLengthController ◄──── GrappleRetractionMotor
  (Links[], PlayerAnchor,              │                    (grip held → retract 2 m/s)
   TargetBody, Release)                │ RemovePlayerSideLink()
                                       │ (Reconnect() in-place — no jolt)
 GrappleTensionAnalyzer ──► GrappleDetachController
  (joint.currentForce)        (break if tension > threshold)

 ThrowablePhysicsObject
  (polls GrappleChainEndpoint)
  ThrownByMonster → HookedByPlayer → ReleasedByPlayer
                                            │
                             GrappleReleaseVelocityHelper
                              (6-sample weighted avg velocity)
                                            │
                                      [Object flies]
                                            │
                                    MonsterHitReceiver
                                     (IsValidPlayerProjectile?)
                                            │
                                    +1 hit → 5 hits → defeated
```

---

## Key Technical Decisions

### XRI 3.3.1 Controller Discovery
XRI 3.3.1 dropped `ActionBasedController` from the starter prefab in favor of `XRControllerActionBasedMapping` + `TrackedPoseDriver`. Controller GameObjects are located by name substring search ("left"/"right") via recursive `FindChildGOByName` rather than component-type search.

### Chain Spawn With No Jolt
Links are placed at `startPos + dir * linkSpacing * (i + 0.5f)`. This means the back anchor of link `i` and the front anchor of link `i−1` are exactly coincident at spawn — `ConfigurableJoint` Locked constraints are already satisfied. No impulse at frame 0.

### Retraction Without Jolt
`GrappleChainJointDriver.Reconnect(newBody)` updates `connectedBody` on the existing joint in-place. The solver does not reset. Destroying and recreating the joint at the same position would cause a one-frame constraint violation impulse.

### Release Velocity Smoothing
`GrappleReleaseVelocityHelper` samples `targetBody.linearVelocity` 6 times over the last N fixed frames and applies a linearly-weighted average. This filters out the collision impulse from the hook attach and gives the player the "swing arc" velocity rather than the instantaneous spike.

### InputActionReference Wiring
`InputActionReference.Create(action)` + `AssetDatabase.AddObjectToAsset()` embeds references as sub-assets inside the `.inputactions` file itself. Idempotent on re-run. No extra `.asset` files created.

---

## File Count

| Folder | Scripts |
|--------|---------|
| `Scripts/Core` | 5 |
| `Scripts/Hook` | 5 |
| `Scripts/Chain` | 7 |
| `Scripts/Control` | 5 |
| `Scripts/Combat` | 6 |
| `Scripts/Debug` | 7 |
| `Scripts/Editor` | 1 |
| **Total** | **36** |

---

## Out of Scope (Not Implemented)

- Menu / UI / HUD
- Custom 3D models (placeholders used: spheres, capsule)
- Advanced VFX / shaders
- Enemy AI beyond fixed-interval throwing
- Player locomotion / movement
- Environment grappling (only the Test Throwable and pooled throwables are grappleable)
- Networking / multiplayer
- Audio clips (ImpactFeedback wired but clips must be assigned manually)

---

## GDD Alignment Analysis

Comparison against the [Golden Hook Game Design Document](https://www.notion.so/Golden-Hook-321840c525ad8050b8f0d374b386d30f).

---

### ✅ Aligned

| GDD Requirement | Implementation |
|---|---|
| VR physics-based prototype in Unity 6 | Unity 6 (6000.3.7f1), OpenXR + XRI 3.3.1 |
| Two grappling hooks (one per hand) | `GrappleLauncher` on both left and right controllers |
| Fire hook with trigger press | `LeftGrappleFire` / `RightGrappleFire` bound to trigger in `GrappleInputActions` |
| Hook automatically attaches on hit | `GrappleHookProjectile.OnCollisionEnter` → `GrappleAttachResolver` |
| Fully physics-simulated chain | `ConfigurableJoint` per link, Locked linear DOF, Free angular DOF |
| Force propagates through chain | Chain is real rigid-body joints — arm movement drives tension which pulls the object |
| Chain length constraint | `maxLinkCount` + `linkSpacing` in `GrappleChainConfig` (serialized, tunable) |
| Chain length is a serialized parameter | `GrappleChainConfig.asset` in `Assets/Configs/` |
| Hook does not wrap around objects | Joints attach point-to-point only; no wrapping behavior |
| Object launches using its velocity at release | `GrappleReleaseVelocityHelper` samples 6-frame weighted average velocity, applied before chain destroy |
| Monster throws objects at player | `MonsterThrower` — interval-based, aims at XROrigin transform |
| Monster takes damage from returned objects | `MonsterHitReceiver` validates `IsValidPlayerProjectile()`, counts hits, fires death event |
| Single arena environment | Walled arena created by `SetupScene` / setup item 2 |
| Controller haptics | `GrappleHapticsDriver` — `SendHapticImpulse` on fire, attach, miss, release, monster hit |
| Impact sounds (wired) | `ImpactFeedback` subscribes to `GrappleRuntimeEvents`, plays `AudioClip`s via `AudioSource` |
| Particle effects (wired) | `ImpactFeedback` has `ParticleSystem` fields for attach and monster hit |
| No menu system | No UI or menu implemented |
| MVP core systems: VR rig, hook, chain, pulling, capture, redirection, monster attack | All present across Batches 1–5 |

---

### ⚠️ Partially Aligned

| GDD Requirement | What Was Built | Gap |
|---|---|---|
| **Trigger Hold = Pull chain** | Grip button (`RightRetract` / `LeftRetract`) mapped to grip axis, not trigger hold | GDD specifies trigger for both fire and pull — implemented as trigger (fire) + grip (retract). More natural for VR but differs from spec. |
| **Trigger Release = Detach and launch** | Second trigger press while attached triggers release | GDD implies releasing the held trigger detaches the chain. Current design uses a second press as the release signal, which means "hold trigger to pull, press again to release." |
| **Heavy / resistance feel** | Physics chain with real mass and damping values set | Feel is tunable but the Throwable prefab uses `mass = 0.5 kg` — relatively light. GDD asks for objects to feel heavy with resistance. May need tuning. |
| **Strong movement dampening** | `linkDrag = 2`, `linkAngularDrag = 5` on chain links; throwable `drag = 0.5` | No global gravity reduction or scene-wide dampening applied. Only chain links have elevated drag. |
| **Polished visual feedback (particles, effects)** | `ImpactFeedback` component wired and ready | No actual `ParticleSystem` assets created or assigned. The system is ready but empty — requires art assets. |
| **Impact sounds** | `ImpactFeedback` wired | No `AudioClip` assets created or assigned. The system plays whatever is assigned but nothing is assigned. |
| **Hit feedback** | Haptics + Console telemetry log | No on-screen visual hit indicator or camera shake. |

---

### ❌ Not Aligned (Missing)

| GDD Requirement | Status | Notes |
|---|---|---|
| **Low gravity environment** | Not implemented | GDD explicitly states "Low gravity — keeps motion readable." Default Unity gravity (−9.81) is used. A simple `Physics.gravity = new Vector3(0, -3f, 0)` call in the scene setup would address this. |
| **Both chains attach same object → object destroyed** | Not implemented | `GrappleChainEndpoint` tracks one attached chain per object but does not check for a second attach. No destruction logic when a second hook hits an already-hooked object. |
| **Player takes damage from object collisions** | Not implemented | `MonsterHitReceiver` handles monster-side damage only. No player health, hit detection on the player body, or damage response exists. |
| **One button to start the experience** | Not implemented | GDD: "one button to start the experience." No start trigger or scene-entry mechanism is present. |
| **Objects can damage the player** | Not implemented | Throwable objects fly freely — no `PlayerHitReceiver` or player Rigidbody hit detection. |

---

### Summary

| Category | Count |
|---|---|
| Fully aligned | 14 |
| Partially aligned | 7 |
| Not implemented | 5 |

The core physics loop — hook, chain, retract, release, redirect, monster damage — is fully aligned with the GDD. The four main gaps are: **low gravity**, **dual-hook destruction rule**, **player damage**, and the **start button**. The input mapping (grip vs trigger-hold for retraction) is a deliberate design deviation that may want a revisit for feel.
