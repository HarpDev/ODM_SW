# Titan AI System - Documentation

## Overview
The Titan AI System is a modular, performance-optimized AI framework for Unity that implements patrol, chase, and attack behaviors using NavMesh navigation. The system follows SOLID principles and is designed for scalability and maintainability.

## Architecture

### Component Hierarchy
```
TitanAI (Main Coordinator)
├── TitanBrain (State Machine)
├── TitanDetector (Perception)
└── TitanPathfinder (Navigation)
```

### Design Patterns
- **State Machine Pattern**: TitanBrain manages behavioral states
- **Observer Pattern**: Event-based communication between components
- **Single Responsibility**: Each component handles one specific concern
- **Dependency Injection**: Components are initialized with their dependencies

## Components

### 1. TitanAI
**Purpose**: Main coordinator that initializes and manages all AI components.

**Key Features**:
- Component initialization and dependency injection
- Event routing between components
- Public API for external control
- Inspector validation with OnValidate()
- Visual debugging with Gizmos

**Public API**:
```csharp
void SetDetectionRadius(float newRadius)
void SetPatrolSpeed(float newSpeed)
void SetChaseSpeed(float newSpeed)
void ForceState(TitanBrain.TitanState state)
TitanBrain.TitanState GetCurrentState()
```

### 2. TitanBrain
**Purpose**: Finite State Machine that controls AI behavior and decision-making.

**States**:
- **Idle**: Stationary for a set duration before beginning patrol
- **Patrol**: Moves between waypoints
- **Alert**: Searches last known player position after losing sight
- **Chase**: Actively pursues detected player
- **Attack**: In range to attack the player

**State Transitions**:
```
Idle → Patrol (timer expires)
Patrol → Chase (player detected)
Chase → Attack (in attack range)
Chase → Alert (player lost)
Alert → Patrol (timer expires or search complete)
Alert → Chase (player reacquired)
Attack → Chase (player leaves attack range)
```

**Public API**:
```csharp
void ForceState(TitanState state)
void ResetBehavior()
TitanState CurrentState { get; }
GameObject TargetPlayer { get; }
```

### 3. TitanDetector
**Purpose**: Handles player detection within radius and field of view.

**Key Features**:
- Coroutine-based periodic detection (configurable interval)
- Field of view cone detection
- Optional line-of-sight checking
- Uses OverlapSphereNonAlloc for zero-allocation detection
- Squared distance calculations for performance

**Performance Optimizations**:
- Reusable collision buffer (reduces GC)
- Configurable detection interval
- Squared distance comparisons (avoids sqrt)
- Early exit conditions

**Public API**:
```csharp
void UpdateRadius(float newRadius)
void UpdateFieldOfView(float newFov)
void UpdateDetectionInterval(float newInterval)
GameObject GetCurrentTarget()
void ClearTarget()
bool IsTargetDetected(GameObject target)
```

**Events**:
```csharp
event Action<GameObject> OnPlayerEntered
event Action OnPlayerExited
```

### 4. TitanPathfinder
**Purpose**: Manages all navigation and pathfinding behavior.

**Key Features**:
- NavMesh-based pathfinding
- Waypoint patrol system (sequential or random)
- Dynamic obstacle avoidance
- Path recalculation throttling
- Last known position tracking

**Patrol Modes**:
- **Loop**: Cycles through waypoints continuously
- **Ping-pong**: Reverses direction at endpoints
- **Random**: Selects random waypoints

**Performance Optimizations**:
- Cached NavMeshPath for reuse
- Throttled path recalculation
- NavMesh position sampling for valid paths
- Coroutine-based path updates

**Public API**:
```csharp
void SetDestination(Vector3 target, float speed)
void Stop()
void Resume()
void Patrol()
void Chase(Vector3 targetPosition)
void SearchLastKnown()
bool IsDestinationReached()
bool HasPath()
float GetRemainingDistance()
void SetPatrolWaypoints(Transform[] newWaypoints)
```

## Setup Guide

### Basic Setup
1. Create an empty GameObject in your scene
2. Add the `TitanAI` component
3. The required components will be automatically added
4. Configure the following in the Inspector:

**TitanAI Settings**:
- Detection Radius: How far the Titan can detect players
- Player Layer: LayerMask for player detection
- Patrol Waypoints: Array of Transform points for patrol route
- Patrol Speed: Movement speed during patrol
- Chase Speed: Movement speed when chasing player

### Advanced Configuration

**TitanBrain**:
- Idle Duration: How long to stay idle before patrolling
- Alert Duration: How long to search after losing player
- Attack Range: Distance required to enter attack state
- Debug State Changes: Enable console logging for state transitions

**TitanDetector**:
- Detection Interval: How often to check for players (lower = more responsive, higher = better performance)
- FOV Angle: Field of view cone (0-360 degrees)
- Require Line of Sight: Enable raycast checks for occlusion
- Obstacle Mask: LayerMask for line-of-sight obstacles

**TitanPathfinder**:
- Path Update Interval: How often to recalculate paths
- Stopping Distance: Distance from destination to consider "arrived"
- Enable Dynamic Avoidance: Automatically avoid obstacles
- Loop Patrol: Whether to loop waypoints or ping-pong
- Randomize Patrol: Use random waypoint selection

### Layer Setup
1. Create a layer called "Player"
2. Assign player GameObjects to this layer
3. Set the Player Layer mask in TitanAI
4. Create a layer for obstacles if using line-of-sight detection

### NavMesh Setup
1. Mark all walkable surfaces as "Navigation Static"
2. Bake the NavMesh (Window > AI > Navigation)
3. Ensure patrol waypoints are on the NavMesh
4. Verify the Titan spawns on the NavMesh

## Performance Considerations

### Optimizations Implemented
1. **Zero-Allocation Detection**: Uses `OverlapSphereNonAlloc` with reusable buffer
2. **Squared Distance**: Avoids expensive square root calculations
3. **Throttled Path Updates**: Prevents excessive path recalculation
4. **Coroutine-Based Updates**: Detection and pathfinding run on intervals
5. **Cached NavMeshPath**: Reuses path objects to reduce allocations
6. **Early Exit Conditions**: Validates before expensive operations

### Performance Tuning
For better performance in scenes with many Titans:
- Increase `detectionInterval` (0.3-0.5 for 20+ Titans)
- Increase `pathUpdateInterval` (0.75-1.0 for 20+ Titans)
- Reduce `detectionRadius` where appropriate
- Disable line-of-sight checking if not needed
- Use simpler obstacle avoidance on NavMeshAgent

For better responsiveness:
- Decrease `detectionInterval` (0.1-0.15)
- Decrease `pathUpdateInterval` (0.25-0.4)
- Enable debug visualization to tune ranges

## Debugging

### Visual Debugging
Enable debug visualization in the Inspector:
- **TitanAI**: Shows detection radius and patrol waypoints
- **TitanBrain**: Shows current state as colored sphere above Titan
- **TitanDetector**: Shows FOV cone, detection radius, and line to target
- **TitanPathfinder**: Shows waypoints, current path, and last known position

### Console Logging
Enable in Inspector:
- `debugStateChanges` in TitanBrain for state transition logs
- `debugPaths` in TitanPathfinder for navigation logs

### Common Issues

**Titan not moving**:
- Verify NavMesh is baked
- Check that Titan is on NavMesh
- Ensure patrol waypoints are assigned and valid
- Check NavMeshAgent component is enabled

**Titan not detecting player**:
- Verify Player LayerMask is set correctly
- Check detection radius is large enough
- Ensure player GameObject is on correct layer
- Disable line-of-sight if obstruction is the issue

**Titan stuck or jittering**:
- Increase stopping distance
- Adjust NavMeshAgent radius
- Check for overlapping waypoints
- Increase path update interval

**Performance issues**:
- Reduce detection frequency
- Increase update intervals
- Limit number of active Titans
- Use object pooling for multiple Titans

## Extension Points

### Adding Custom States
1. Add new state to `TitanState` enum in TitanBrain
2. Implement `UpdateXXXState()` method
3. Implement `EnterXXXState()` method
4. Add state transitions in `TransitionToState()`
5. Update debug visualization color

### Adding Custom Behaviors
1. Create new component inheriting from MonoBehaviour
2. Add initialization in TitanAI.InitializeComponents()
3. Wire events in TitanAI.WireEvents()
4. Call from appropriate state in TitanBrain

### Integration Examples

**Animation Integration**:
```csharp
// In TitanBrain.EnterAttackState()
animator.SetTrigger("Attack");

// In TitanBrain.UpdatePatrolState()
animator.SetFloat("Speed", pathfinder.Agent.velocity.magnitude);
```

**Audio Integration**:
```csharp
// In TitanBrain.HandlePlayerDetected()
audioSource.PlayOneShot(detectionSound);
```

**Damage System Integration**:
```csharp
// In TitanBrain.UpdateAttackState()
if (Time.time - lastAttackTime > attackCooldown)
{
    targetPlayer.GetComponent<Health>()?.TakeDamage(attackDamage);
    lastAttackTime = Time.time;
}
```

## Best Practices

1. **Use Events**: Prefer events over direct component references for loose coupling
2. **Validate Input**: Always validate public API parameters
3. **Cache References**: Cache frequently accessed components
4. **Tune Intervals**: Balance responsiveness vs performance based on your needs
5. **Test at Scale**: Profile with the expected number of AI agents
6. **Layer Management**: Use proper layer masks to avoid unwanted detections
7. **NavMesh Quality**: Ensure clean NavMesh baking for best pathfinding

## Version History

### v2.0 (Optimized)
- Implemented zero-allocation detection
- Added squared distance optimizations
- Added path recalculation throttling
- Improved state machine with enter/exit logic
- Added comprehensive debug visualization
- Added input validation throughout
- Improved documentation and code comments
- Fixed bug: Missing Stop() method in pathfinder
- Fixed bug: State machine not handling null targets properly
- Fixed bug: Detection buffer overflow potential
- Added OnValidate() for editor-time validation
- Added extensive Gizmo visualization

### v1.0 (Original)
- Basic state machine implementation
- Simple detection and pathfinding
- Event-based architecture

## License
This AI system is provided as-is for use in Unity projects.

## Support
For issues or questions, enable debug visualization and check console logs.
Common solutions are documented in the "Common Issues" section above.
