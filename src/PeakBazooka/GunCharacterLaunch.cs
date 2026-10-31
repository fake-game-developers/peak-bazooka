using System;
using System.Collections;
using Peak.Afflictions;
using Photon.Pun;
using UnityEngine;

namespace Peak.Bazooka;

public class GunCharacterLaunch : MonoBehaviourPunCallbacks
{
    private static readonly Type? ZombieType = GameCompat.FindType(GameCompat.ZombieTypeName);

    /// <summary>Standard mode: how long your own blast ragdolls you.</summary>
    internal const float BlastRagdollSeconds = 2f;

    // Share of the knockback at the very edge of the blast; the middle gets all of it.
    private const float EdgeShove = 0.4f;

    private const float ShoveUp = 0.8f;

    // A knocked-out zombie is owned by the zombie's owner and mirrored to everyone (RPC_BazookaZombieShotState), so a
    // new owner after host migration keeps it down and late joiners see it silenced.
    private bool heldDown;

    internal bool Silenced { get; private set; }

    private Coroutine? flightRoutine;

    private float flightStarted;

    // Just past PEAK's 0.2s "still allowed to jump" window, so a flight cannot be extended with a mid-air jump.
    private const float FlightSinceGrounded = 0.21f;

    public static bool IsZombie(Character character) =>
        ZombieType != null && character.GetComponent(ZombieType) != null;

    /// <summary>
    /// Zombie hit sent by a remote shooter. The zombie's owner applies it only for a shot the host accepted
    /// (sender + <paramref name="shotId"/>) and only while the room allows shooting zombies.
    /// </summary>
    [PunRPC]
    public void RPC_BazookaBlast(Vector3 direction, float strength, float knockback, int shotId, PhotonMessageInfo info)
    {
        Character? character = GetComponent<Character>();
        if (character == null || !character.photonView.IsMine || !IsZombie(character) || !ShootableRules.Zombies.Enabled)
        {
            return;
        }

        int targetId = character.photonView.ViewID;
        if (!ShotLedger.TryConsumeOrDeferHit(info.Sender, shotId, targetId, () => Blast(direction, strength, knockback)))
        {
            return;
        }

        Blast(direction, strength, knockback);
    }

    /// <summary>
    /// RocketJump-mode rocket jump: add <paramref name="velocityChange"/> to the whole body (and the held item) and fly
    /// under <see cref="RocketFlight"/> physics until you land, grab something, or get ragdolled. A rocket fired
    /// mid-flight first cancels any motion heading into the blast, then adds the launch, so jumps chain even when
    /// falling fast.
    /// </summary>
    internal void BeginRocketFlight(Vector3 velocityChange)
    {
        Character? character = GetComponent<Character>();
        if (character == null || !character.IsLocal || character.data.dead || character.data.fallSeconds > 0f)
        {
            return;
        }

        RocketFlight.Begin(character);

        // Falling onto your own rocket would only cancel the fall (PEAK falls are long and fast), so chained jumps
        // died out. Drop the part of the body's motion heading into the blast first, then launch at full strength;
        // motion in other directions (your forward momentum) is kept.
        Vector3 away = velocityChange.normalized;
        float intoBlast = Mathf.Min(0f, Vector3.Dot(character.data.avarageVelocity, away));
        Vector3 change = velocityChange - away * intoBlast;
        foreach (Bodypart part in character.refs.ragdoll.partList)
        {
            Rigidbody? rig = part != null ? part.Rig : null;
            if (rig != null && !rig.isKinematic)
            {
                rig.linearVelocity += change;
            }
        }

        Rigidbody? itemRig = character.data.currentItem != null ? character.data.currentItem.rig : null;
        if (itemRig != null && !itemRig.isKinematic)
        {
            itemRig.linearVelocity += change;
        }

        // Like a vanilla jump: CharacterMovement.CanStand needs sinceJump > 0.3s, so the floor you blasted off
        // does not count as a landing. Only reset here, never every step, or you could never land again.
        character.data.sinceJump = 0f;
        character.refs.movement.CapFallDamage(0f, 2f);
        flightStarted = Time.time;
        flightRoutine ??= StartCoroutine(RocketFlightRoutine(character));
    }

    private IEnumerator RocketFlightRoutine(Character character)
    {
        var step = new WaitForFixedUpdate();
        while (true)
        {
            yield return step;
            if (!KeepFlying(character, Time.time - flightStarted))
            {
                break;
            }

            // FallTime is min(sinceJump, sinceGrounded), and PEAK only scores a landing after 0.5s off the ground.
            // Holding sinceGrounded short means landing from a flight is never a fall (no Injury, no ragdoll),
            // while landing itself still works.
            if (character.data.sinceGrounded > FlightSinceGrounded)
            {
                character.data.sinceGrounded = FlightSinceGrounded;
            }

            character.refs.movement.CapFallDamage(0f, 2f);
        }

        flightRoutine = null;
        RocketFlight.End();
    }

    private static bool KeepFlying(Character character, float seconds)
    {
        if (character == null)
        {
            return false;
        }

        CharacterData data = character.data;
        if (data.dead || data.passedOut || data.fullyPassedOut || data.fallSeconds > 0f
            || data.carrier != null || data.isClimbing || data.isRopeClimbing || data.isVineClimbing
            || seconds > 15f)
        {
            return false;
        }

        // Touching down ends it at once: walking, ground friction and the ground animation come straight back.
        // (The floor you launched from cannot count for 0.3s; see BeginRocketFlight.)
        return !data.isGrounded;
    }

    private void OnDestroy()
    {
        if (RocketFlight.IsFlying(GetComponent<Character>()))
        {
            RocketFlight.End();
        }
    }

    /// <summary>
    /// A rocket hit on a scout or zombie this client owns: ragdoll and a push away from the blast, both scaled by
    /// <paramref name="strength"/> (1 in the middle of the blast, 0 at its edge). <paramref name="knockback"/> is the
    /// shooter's <see cref="Plugin.Knockback"/>. Zombies are knocked out.
    /// </summary>
    public void Blast(Vector3 direction, float strength, float knockback)
    {
        Character? character = GetComponent<Character>();
        if (character == null || !character.photonView.IsMine || character.data.dead)
        {
            return;
        }

        strength = Mathf.Clamp01(strength);
        character.Fall(Mathf.Lerp(1.5f, 3f, strength));

        // One velocity change on every bone, like the rocket jump. Pushing over several steps dragged the body along
        // the ground, where friction ate most of it. Fall() already zeroes ragdoll control on the next step; do it
        // now so PEAK's drag (scaled by control) cannot take a bite first.
        character.data.currentRagdollControll = 0f;
        float speed = Mathf.Clamp(knockback, 0f, Plugin.MaxKnockback) * Mathf.Lerp(EdgeShove, 1f, strength);
        Vector3 launch = (direction.normalized + Vector3.up * ShoveUp).normalized * speed;
        foreach (Bodypart part in character.refs.ragdoll.partList)
        {
            Rigidbody? rig = part != null ? part.Rig : null;
            if (rig != null && !rig.isKinematic)
            {
                rig.linearVelocity += launch;
            }
        }
        if (IsZombie(character))
        {
            KnockDown(character);
        }
    }

    private void LateUpdate()
    {
        if (!heldDown)
        {
            return;
        }

        Character? character = GetComponent<Character>();
        if (character == null || !character.photonView.IsMine)
        {
            return;
        }

        if (character.data.dead)
        {
            heldDown = false;
            BroadcastShotState();
            return;
        }

        // A finished lunge recovery sends the zombie back to chasing. Hold the knockout.
        HoldDown(character);
    }

    private void KnockDown(Character character)
    {
        heldDown = true;
        character.PassOutInstantly();
        HoldDown(character);
        Silence();
        BroadcastShotState();
    }

    private void BroadcastShotState()
    {
        if (PhotonNetwork.InRoom && photonView != null)
        {
            photonView.RPC(nameof(RPC_BazookaZombieShotState), RpcTarget.Others, heldDown);
        }
    }

    [PunRPC]
    public void RPC_BazookaZombieShotState(bool down, PhotonMessageInfo info)
    {
        if (info.Sender == null || info.Sender != photonView.Owner)
        {
            return;
        }

        heldDown = down;
        if (down)
        {
            Silence();
        }
    }

    public override void OnPlayerEnteredRoom(Photon.Realtime.Player newPlayer)
    {
        if (photonView != null && photonView.IsMine && heldDown)
        {
            photonView.RPC(nameof(RPC_BazookaZombieShotState), newPlayer, heldDown);
        }
    }

    private void Silence()
    {
        if (Silenced)
        {
            return;
        }

        Silenced = true;
        foreach (AudioSource source in GetComponentsInChildren<AudioSource>(true))
        {
            source.Stop();
            source.mute = true;
        }
    }

    private static void HoldDown(Character character)
    {
        character.refs.afflictions.SetStatus(CharacterAfflictions.STATUSTYPE.Drowsy, 1f, false);
        character.data.passedOut = true;
        character.data.fullyPassedOut = true;
        if (character.data.fallSeconds < 1f)
        {
            character.data.fallSeconds = 5f;
        }
    }
}
