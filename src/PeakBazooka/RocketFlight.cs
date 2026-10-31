using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Peak.Bazooka;

/// <summary>
/// RocketJump-mode rocket jump physics for the local scout. PEAK multiplies every body part's velocity by
/// <see cref="CharacterMovement.drag"/> (0.85) each physics step while you have control, which eats a launch in
/// about 0.1s. While flying, the body's shared (average) velocity skips that drag and only the limbs' motion
/// relative to it is damped, so the animation stays stable. Gravity and WASD are replaced with fixed flight values
/// (TF2-style capped air acceleration), independent of PEAK's walking tuning.
/// </summary>
internal static class RocketFlight
{
    /// <summary>Speed cap for the shared body velocity. Stops chained rockets from tunnelling through terrain.</summary>
    internal const float MaxSpeed = 40f;

    /// <summary>WASD only accelerates you up to this speed along the input direction (TF2 air strafing).</summary>
    private const float AirWishSpeed = 6f;

    private static Character? flyer;

    private static readonly List<(Rigidbody rig, CollisionDetectionMode mode)> savedModes = new();

    internal static bool IsFlying(Character? character) =>
        flyer is not null && ReferenceEquals(character, flyer);

    internal static void Begin(Character character)
    {
        if (IsFlying(character))
        {
            return;
        }

        End();
        flyer = character;
        foreach (Bodypart part in character.refs.ragdoll.partList)
        {
            Rigidbody? rig = part != null ? part.Rig : null;
            if (rig == null)
            {
                continue;
            }

            savedModes.Add((rig, rig.collisionDetectionMode));
            rig.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }
    }

    internal static void End()
    {
        foreach ((Rigidbody rig, CollisionDetectionMode mode) in savedModes)
        {
            if (rig != null)
            {
                rig.collisionDetectionMode = mode;
            }
        }

        savedModes.Clear();
        flyer = null;
    }

    /// <summary>Shared body velocity this step (PEAK's average of every part), capped at <see cref="MaxSpeed"/>.</summary>
    private static Vector3 Bulk(Character character, out Vector3 raw)
    {
        raw = character.data.avarageVelocity;
        return Vector3.ClampMagnitude(raw, MaxSpeed);
    }

    /// <summary>Keeps the shared velocity, damps each rig's motion relative to it by <paramref name="drag"/>.</summary>
    internal static void BulkPreservingDrag(Character character, Rigidbody rig, float drag)
    {
        if (rig.isKinematic)
        {
            return;
        }

        Vector3 bulk = Bulk(character, out Vector3 raw);
        rig.linearVelocity = bulk + (rig.linearVelocity - raw) * drag;
        rig.angularVelocity *= drag;
    }

    /// <summary>
    /// Quake/TF2 air acceleration: push toward the WASD direction, but only until your speed along it reaches
    /// <see cref="AirWishSpeed"/>. Steers and brakes without letting you out-run a rocket by holding W.
    /// </summary>
    internal static Vector3 AirAcceleration(Character character)
    {
        Vector3 wish = character.data.worldMovementInput_Lerp;
        wish.y = 0f;
        float amount = Mathf.Clamp01(wish.magnitude);
        if (amount < 0.05f)
        {
            return Vector3.zero;
        }

        Vector3 direction = wish.normalized;
        float add = AirWishSpeed * amount - Vector3.Dot(character.data.avarageVelocity, direction);
        if (add <= 0f)
        {
            return Vector3.zero;
        }

        float step = Time.fixedDeltaTime;
        return direction * (Mathf.Min(Plugin.JumpAirControl * step, add) / step);
    }
}

[HarmonyPatch(typeof(Bodypart), nameof(Bodypart.Drag))]
internal static class RocketFlightBodyDragPatch
{
    private static bool Prefix(Bodypart __instance, float drag, bool ignoreRagdoll)
    {
        Character character = __instance.character;
        if (ignoreRagdoll || !RocketFlight.IsFlying(character))
        {
            return true;
        }

        RocketFlight.BulkPreservingDrag(
            character,
            __instance.rig,
            Mathf.Lerp(1f, drag, character.data.currentRagdollControll));
        return false;
    }
}

[HarmonyPatch(typeof(Bodypart), nameof(Bodypart.AddMovementForce))]
internal static class RocketFlightBodyMovePatch
{
    private static bool Prefix(Bodypart __instance, float movementForce)
    {
        Character character = __instance.character;
        if (!RocketFlight.IsFlying(character))
        {
            return true;
        }

        // movementForce is 0 when PEAK says you cannot move (stunned, typing...); respect that in the air too.
        if (movementForce > 0f)
        {
            __instance.AddForce(RocketFlight.AirAcceleration(character), ForceMode.Acceleration);
        }

        return false;
    }
}

[HarmonyPatch(typeof(CharacterMovement), nameof(CharacterMovement.GetGravityForce))]
internal static class RocketFlightGravityPatch
{
    private static void Postfix(CharacterMovement __instance, ref Vector3 __result)
    {
        Character character = __instance.character;
        if (RocketFlight.IsFlying(character) && !character.data.isGrounded)
        {
            __result = Vector3.down * Plugin.JumpGravity;
        }
    }
}

/// <summary>The held bazooka gets the same drag / steering, or it lags behind and tugs your hands back.</summary>
[HarmonyPatch(typeof(CharacterItems), nameof(CharacterItems.AddDrag))]
internal static class RocketFlightItemDragPatch
{
    private static bool Prefix(CharacterItems __instance, float drag, float factor)
    {
        Character character = __instance.character;
        Item? item = RocketFlight.IsFlying(character) ? character.data.currentItem : null;
        Rigidbody? rig = item != null ? item.rig : null;
        if (rig == null)
        {
            return true;
        }

        RocketFlight.BulkPreservingDrag(character, rig, factor != 1f ? Mathf.Lerp(1f, drag, factor) : drag);
        return false;
    }
}

[HarmonyPatch(typeof(CharacterItems), nameof(CharacterItems.AddMovementForce))]
internal static class RocketFlightItemMovePatch
{
    private static bool Prefix(CharacterItems __instance, float movementForce)
    {
        Character character = __instance.character;
        Item? item = RocketFlight.IsFlying(character) ? character.data.currentItem : null;
        Rigidbody? rig = item != null ? item.rig : null;
        if (rig == null)
        {
            return true;
        }

        if (movementForce > 0f)
        {
            rig.AddForce(RocketFlight.AirAcceleration(character), ForceMode.Acceleration);
        }

        return false;
    }
}
