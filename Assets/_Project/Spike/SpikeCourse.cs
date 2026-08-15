using UnityEngine;

namespace Contraption.Spike
{
    /// <summary>
    /// The crude course for the fun checkpoint: flat ground, one ramp, one gap, finish marker.
    /// Deliberately blocky and unpolished — its only job is to ask whether re-running the loop
    /// is fun, not to look like a level.
    /// </summary>
    public static class SpikeCourse
    {
        public const float StartX = 0f;
        public const float FinishX = 58f;

        /// <summary>Cargo falling below this has been lost down the gap.</summary>
        public const float KillPlaneY = -12f;

        /// <summary>Height of the plateau the ramp climbs to.</summary>
        private const float PlateauTopY = 1.4f;

        private static readonly Color GroundColor = new Color(0.30f, 0.34f, 0.38f);
        private static readonly Color RampColor = new Color(0.36f, 0.41f, 0.45f);
        private static readonly Color FinishColor = new Color(0.35f, 0.80f, 0.45f);

        /// <summary>Builds the course under <paramref name="root"/> and returns the finish sensor.</summary>
        public static SpikeFinishSensor Build(Transform root)
        {
            // Opening straight: room to get up to speed before anything happens. Top at y = 0.
            Slab(root, "GroundStart", new Vector2(8f, -1f), new Vector2(36f, 2f));

            // The ramp, and the plateau it climbs to (top at y = 1.4).
            //
            // The ramp is built from its two surface endpoints rather than from a centre,
            // rotation and size. Hand-placing a rotated box left its upper end poking 0.18 above
            // the plateau surface, and that lip launched the rover into a backflip it never
            // recovered from - a course bug that reads exactly like a physics bug. Deriving the
            // box from the endpoints makes the ramp meet the plateau flush by construction.
            Ramp(root, new Vector2(26f, 0f), new Vector2(31.5f, PlateauTopY), 1.2f);
            Slab(root, "Plateau", new Vector2(35f, PlateauTopY - 1f), new Vector2(7f, 2f));

            // The gap. Landing is lower than the plateau (top y = 0.4 against the plateau's 1.4),
            // so a fast machine launches into a drop - which is where fragile cargo gets punished.
            //
            // That 1.0 drop is tuned, not arbitrary. At 1.5 the bare rover pitched over on landing
            // and ground itself to a halt on its back every single run, while the variants with
            // forward appendages sailed through because the appendage acted as an outrigger. A
            // course where the baseline cannot finish measures the course, not the machine.
            Slab(root, "GroundLanding", new Vector2(52f, -0.6f), new Vector2(24f, 2f));

            // Bumps on the run-in to the finish, to jostle the cargo without stopping the machine.
            // They protrude ~0.2 above the landing surface, a little under half a wheel radius.
            // Anything approaching a full radius is a wall, not a bump.
            Slab(root, "Bump1", new Vector2(46f, 0.35f), new Vector2(1.2f, 0.5f));
            Slab(root, "Bump2", new Vector2(50f, 0.32f), new Vector2(1.0f, 0.6f));

            // Back wall, so a machine driven backwards off the start does not vanish forever.
            Slab(root, "BackWall", new Vector2(-11f, 1f), new Vector2(1f, 8f));

            return CreateFinish(root);
        }

        private static void Slab(Transform root, string name, Vector2 centre, Vector2 size)
        {
            SpikeVisuals.CreateBox(name, root, centre, size, 0f, GroundColor, RigidbodyType2D.Static, 1f);
        }

        /// <summary>
        /// Builds a ramp whose *top surface* runs exactly from <paramref name="surfaceStart"/> to
        /// <paramref name="surfaceEnd"/>, by sinking the box half its thickness along the surface
        /// normal. Specifying the surface directly is the whole point: it is the surface that has
        /// to line up with the geometry either side of it.
        /// </summary>
        private static void Ramp(Transform root, Vector2 surfaceStart, Vector2 surfaceEnd, float thickness)
        {
            Vector2 along = surfaceEnd - surfaceStart;
            float length = along.magnitude;
            float angleDegrees = Mathf.Atan2(along.y, along.x) * Mathf.Rad2Deg;
            Vector2 intoSlope = new Vector2(along.y, -along.x).normalized;
            Vector2 centre = ((surfaceStart + surfaceEnd) * 0.5f) + (intoSlope * (thickness * 0.5f));

            SpikeVisuals.CreateBox(
                "Ramp", root, centre, new Vector2(length, thickness), angleDegrees,
                RampColor, RigidbodyType2D.Static, 1f);
        }

        private static SpikeFinishSensor CreateFinish(Transform root)
        {
            var finish = new GameObject("FinishSensor");
            finish.transform.SetParent(root, worldPositionStays: false);
            finish.transform.localPosition = new Vector2(FinishX, 1.5f);

            BoxCollider2D trigger = finish.AddComponent<BoxCollider2D>();
            trigger.size = new Vector2(1.5f, 5f);
            trigger.isTrigger = true;

            SpikeVisuals.AddSquareSprite(finish.transform, Vector2.zero, new Vector2(0.35f, 5f), FinishColor);
            SpikeVisuals.AddSquareSprite(finish.transform, new Vector2(0.9f, 2f), new Vector2(1.6f, 1f), FinishColor);

            return finish.AddComponent<SpikeFinishSensor>();
        }
    }
}
