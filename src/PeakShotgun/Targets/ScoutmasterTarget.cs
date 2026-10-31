using UnityEngine;

namespace Peak.Shotgun;

/// <summary>
/// Scoutmaster Myres is a <see cref="Character"/>, so he is only classified for a character with
/// <c>isScoutmaster</c> (never as a scout). Each rocket knocks him down; the last one
/// (<see cref="ShootableRules.Scoutmaster"/> hits, default 3) warps him to his hidey spot for the rest of the map.
/// Runs on the host, which owns him.
/// </summary>
internal sealed class ScoutmasterTarget : ShotTarget<Scoutmaster>
{
    private const float KnockDownSeconds = 2.5f;

    // Scoutmaster.Update does nothing while this counts down: he stays put at the hidey spot.
    private const float GoneForSeconds = 1_000_000f;

    internal override ShotTargetKind Kind => ShotTargetKind.Scoutmaster;

    internal override ShootableRule Rule => ShootableRules.Scoutmaster;

    internal override ShotStage Stage => ShotStage.Scoutmaster;

    internal override Component? Classify(Collider collider)
    {
        Character? character = collider.GetComponentInParent<Character>();
        return character != null ? character.GetComponent<Scoutmaster>() : null;
    }

    protected override void PartialHit(Scoutmaster scoutmaster, ShotHit hit)
    {
        if (scoutmaster != null && scoutmaster.character != null)
        {
            scoutmaster.character.Fall(KnockDownSeconds);
        }
    }

    protected override void Defeat(Scoutmaster scoutmaster, ShotHit hit)
    {
        if (scoutmaster == null)
        {
            return;
        }

        scoutmaster.SetCurrentTarget(null, 0f);
        scoutmaster.discovered = null;
        scoutmaster.chillForSeconds = GoneForSeconds;
        scoutmaster.tpCounter = 5f;
        scoutmaster.TeleportFarAway();
        Plugin.Log.LogInfo("Scoutmaster defeated by rockets (warped to his hidey spot for the rest of the map).");
    }
}
