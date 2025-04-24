# Simple Lap Counting System

This system provides a simplified approach to lap counting that uses a single start/finish line instead of the complex checkpoint system.

## Setup Instructions

1. **Add the StartFinishLine script to your project**
   - The script is already added to your project
   - It handles detecting when vehicles cross the start/finish line

2. **Create a Start/Finish Line in your scene**
   - Option 1: Use the provided StartFinishLinePrefab
   - Option 2: Create a new GameObject with:
     - Box Collider (set to Trigger)
     - StartFinishLine component

3. **Position the Start/Finish Line**
   - Place it across the track at the start/finish position
   - Make sure it's wide enough to cover the entire track width
   - Adjust the height to ensure vehicles will pass through it

4. **Configure the RaceManager**
   - Assign the StartFinishLine component to the RaceManager's startFinishLine field
   - The RaceManager will automatically connect to the StartFinishLine events

5. **Tag your vehicles correctly**
   - Make sure the player vehicle has the "Player" tag
   - Make sure the AI vehicle has the "AI" tag
   - Or adjust the tags in the StartFinishLine component to match your vehicle tags

## How It Works

1. When a vehicle crosses the start/finish line, the StartFinishLine component detects it
2. It triggers the appropriate event (OnPlayerCrossed or OnAICrossed)
3. The RaceManager handles the lap completion logic
4. The first crossing is ignored to prevent counting the initial start as a lap
5. The race ends when a vehicle completes the specified number of laps

## Troubleshooting

- If laps aren't being counted:
  - Check that the StartFinishLine is positioned correctly
  - Verify that the vehicles have the correct tags
  - Make sure the collider is set to Trigger
  - Check the console for debug messages

- If multiple laps are counted when crossing once:
  - Increase the crossingCooldown value in the StartFinishLine component

## Benefits of This Approach

- Simpler and more reliable lap counting
- No need for complex checkpoint progression
- Easier to set up and maintain
- More similar to real-world racing
- Less prone to errors from missed checkpoints
