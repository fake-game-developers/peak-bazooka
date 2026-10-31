using Photon.Pun;
using UnityEngine;

namespace Peak.Bazooka;

/// <summary>
/// Kills a spider. PEAK has no spider death (only <see cref="Spider.Bonk"/>, a stun), so every client lets go of
/// whoever it holds, takes it off <see cref="SpiderManager"/> (which would otherwise switch it back on when a scout
/// comes near) and hides it. Late joiners get it from the host's world-defeat list.
/// </summary>
internal sealed class SpiderTarget : ShotTarget<Spider>
{
    internal override ShotTargetKind Kind => ShotTargetKind.Spider;

    internal override ShootableRule Rule => ShootableRules.Spiders;

    internal override bool BroadcastsDefeat => true;

    internal override Component? Classify(Collider collider)
    {
        Spider? spider = collider.GetComponentInParent<Spider>();
        if (spider == null)
        {
            SpiderTrigger? trigger = collider.GetComponentInParent<SpiderTrigger>();
            spider = trigger != null ? trigger.spider : null;
        }

        // Its colliders include the tall trigger column it drops down: only count a blast near the spider's body.
        if (spider == null || spider.spider == null)
        {
            return null;
        }

        float reach = Plugin.BlastReach + 1f;
        return (spider.spider.transform.position - BazookaCombat.BlastPoint).sqrMagnitude <= reach * reach ? spider : null;
    }

    protected override PhotonView? ViewOf(Spider spider) =>
        spider.photonView != null ? spider.photonView : base.ViewOf(spider);

    protected override void Defeat(Spider spider, ShotHit hit) => Kill(spider);

    internal override void ClientDefeat(PhotonView? view, int extra) =>
        Kill(view != null ? Near<Spider>(view.gameObject) : null);

    private static void Kill(Spider? spider)
    {
        if (spider == null)
        {
            return;
        }

        if ((object?)spider.targetPlayer != null)
        {
            // Local on every client (each one runs Kill), like the vanilla RpcTarget.All let-go.
            spider.RPCA_LetGo();
        }

        if (SpiderManager.instance != null)
        {
            SpiderManager.instance.Unregister(spider);
        }

        spider.gameObject.SetActive(false);
    }
}
