# Recovery Training Parameters Setup Guide

## RacingAgent Component Configuration

Configure your `RacingAgent` component with the following parameters for optimal recovery training:

### Observation Settings:
- `numRaycasts`: 36 (Increased from 25 for better boundary detection)
- `raycastDistance`: 120 (Increased for better long-range planning)
- `raycastAngle`: 240 (Increased from 190 for wider field of view)
- `upcomingCheckpointsToTrack`: 8 (Increased from 6 for better planning)
- `raycastVerticalOffset`: 0.4 (Keep as is)

### Action Settings:
- `steerMultiplier`: 1.0
- `accelMultiplier`: 1.0
- `brakeMultiplier`: 1.2 (Slightly increased to improve braking precision)

### Reward Settings:
- `checkpointReward`: 0.01 (Keep as is)
- `lapCompletionReward`: 0.5 (Keep as is)
- `speedRewardFactor`: 0.2 (Slightly reduced to not overly prioritize speed)
- `progressRewardFactor`: 0.5 (Increased to favor making progress)
- `timePenalty`: -0.006 (Keep as is)
- `centerlineRewardFactor`: 0.01 (Slightly increased for better track adherence)
- `centerlineRewardZoneWidth`: 3.0 (Keep as is)
- `beatBestLapBonus`: 50.0 (Keep as is)
- `boundaryHitPenalty`: -1.5 (Reduced from -10.0 to allow recovery)
- `continuousOffTrackPenalty`: -0.1 (Reduced to be less harsh)
- `wrongCheckpointPenalty`: -1.0 (Reduced to be less harsh)
- `resetPenalty`: -2.0 (Keep as is)
- `recoveryReward`: 2.0 (New parameter for recovery encouragement)

### Episode Control & Reset:
- `maxEpisodeSteps`: 20000 (Increased to allow longer episodes)
- `checkpointReachedDistance`: 4.5 (Keep as is)
- `progressTimeout`: 15.0 (Increased to allow more time for recovery)
- `minimumSpeedForProgress`: 0.1 (Reduced to prevent early resets)
- `resetOnBoundaryHit`: false (Critical change to allow recovery)
- `resetWhenUpsideDown`: true (Still reset when upside down)
- `upsideDownTimeThreshold`: 3.0 (Increased to give more time to recover)
- `minCheckpointFractionForLap`: 0.9 (Slightly reduced to be more forgiving)

### Debug Visualization (Optional):
- `showRaycasts`: true (Useful during initial training to verify setup)
- `showUpcomingCheckpoints`: true (Useful during initial training)

## Training Configuration

The optimal configuration for ML-Agents has been updated in the `racing_config.yaml` file:

### Key Changes:
1. Increased buffer size (32768)
2. Higher exploration parameters (beta: 0.015)
3. Longer sequence memory (128)
4. Added GAIL for imitation learning
5. Increased max steps and time horizon
6. Added self-play option for competitive learning

## Training Procedure

1. Record demonstration data by manually driving the car, showing proper recovery behaviors
2. Save these demonstrations to `demos/racing_demos.demo`
3. Start training with `mlagents-learn racing_config.yaml --run-id recovery_training`
4. Monitor progress through TensorBoard
5. Test intermediate models to verify recovery behaviors
6. Continue training until the agent consistently recovers from boundary hits

## Expected Outcomes

- The AI will initially struggle with recovery but gradually improve
- Early models will show erratic behavior when off-track
- Mid-training models will start showing basic recovery patterns
- Final models should smoothly recover from boundary hits and continue racing
- Lap times may be slightly slower than reset-based models but will provide a better racing experience 