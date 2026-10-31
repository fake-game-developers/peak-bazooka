using Photon.Pun;
using UnityEngine;

namespace Peak.Shotgun;

/// <summary>
/// Blows a Mesa tumbleweed away from the shooter. The weed's owner simulates it (<see cref="TumbleWeed"/> only
/// rolls when its view is mine) and has no RPC of its own, so the host broadcasts the push and the owner applies it.
/// It keeps chasing scouts afterwards, so it may come rolling back.
/// </summary>
internal sealed class TumbleweedTarget : ShotTarget<TumbleWeed>
{
    private const float PushSpeed = 18f;

    private const float PushLift = 6f;

    private const float Spin = 12f;

    internal override ShotTargetKind Kind => ShotTargetKind.Tumbleweed;

    internal override ShootableRule Rule => ShootableRules.Tumbleweeds;

    protected override void Defeat(TumbleWeed weed, ShotHit hit)
    {
        Vector3 away = Vector3.ProjectOnPlane(hit.DirectionTo(weed.transform.position), Vector3.up);
        Vector3 push = away.sqrMagnitude > 0.0001f
            ? (away.normalized * PushSpeed + Vector3.up * PushLift) * Plugin.KnockbackScale
            : Vector3.up * (PushSpeed * Plugin.KnockbackScale);
        ShotgunCombat.BroadcastEffect(this, weed, hit, push);
    }

    protected override void Effect(TumbleWeed weed, Vector3 push, int variant)
    {
        PhotonView? view = weed.GetComponent<PhotonView>();
        if (PhotonNetwork.InRoom && view != null && !view.IsMine)
        {
            return;
        }

        Rigidbody? rig = weed.GetComponent<Rigidbody>();
        if (rig == null || rig.isKinematic)
        {
            return;
        }

        rig.AddForce(push, ForceMode.VelocityChange);
        rig.AddTorque(Random.insideUnitSphere * Spin, ForceMode.VelocityChange);
    }
}
