HousingNpcPose v0.2.2 named pose patch

Adds confirmed named poses based on discovery:
- sit / groundsit: InPositionLoop param 1
- bench / chair / sitbench: InPositionLoop param 2
- doze / bed / lie: InPositionLoop param 3
- stepdance: InPositionLoop param 4
- harvestdance: InPositionLoop param 5

Adds /hnpcpose pose <idx> <name> and direct aliases like /hnpcpose doze <idx>.
Keeps the Namazu/non-humanoid block and restores.
