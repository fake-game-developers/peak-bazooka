using UnityEngine;

namespace Peak.Shotgun;

/// <summary>
/// Local visual + collision for one rocket. Every client spawns one from the shooter's <see cref="BazookaShooter"/>
/// (not the gun, which PEAK destroys when it is put away); only the shooter's copy reports the impact so damage
/// stays host-authorized.
/// </summary>
internal sealed class BazookaRocket : MonoBehaviour
{
    private const float Radius = 0.18f;

    private const float GravityScale = 0.28f;

    private const float MaxLife = 6f;

    private static readonly RaycastHit[] Hits = new RaycastHit[32];

    internal BazookaShooter? Shooter;

    internal int ShotId;

    internal bool ReportsImpact;

    private Vector3 velocity;

    private float age;

    private bool exploded;

    internal void Launch(Vector3 origin, Vector3 direction, float speed)
    {
        transform.position = origin;
        transform.rotation = Quaternion.LookRotation(direction);
        velocity = direction.normalized * speed;
    }

    private void Update()
    {
        if (exploded)
        {
            return;
        }

        age += Time.deltaTime;
        velocity += Physics.gravity * GravityScale * Time.deltaTime;
        Vector3 step = velocity * Time.deltaTime;
        float distance = step.magnitude;
        if (distance < 0.0001f)
        {
            return;
        }

        Vector3 direction = step / distance;
        if (TryFindImpact(transform.position, direction, distance, out Vector3 impact))
        {
            Explode(impact);
            return;
        }

        transform.position += step;
        if (velocity.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(velocity);
        }

        if (age >= MaxLife)
        {
            Explode(transform.position);
        }
    }

    /// <summary>
    /// Nearest thing this step's sweep touches, skipping the shooter. A single SphereCast stops at the first
    /// collider: when that was the shooter's own arm the whole step was skipped, so a zombie right in front of you
    /// was flown through. It also misses colliders the rocket starts inside (a zombie on top of you); the
    /// "All" query reports those at distance 0.
    /// </summary>
    private bool TryFindImpact(Vector3 from, Vector3 direction, float distance, out Vector3 impact)
    {
        int mask = HelperFunctions.CharacterAndDefaultMask | HelperFunctions.terrainMapMask | ShotTargetRegistry.ExtraLayerMask;
        int count = Physics.SphereCastNonAlloc(from, Radius, direction, Hits, distance, mask, QueryTriggerInteraction.Collide);
        float best = float.MaxValue;
        impact = from;
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = Hits[i];
            if (hit.collider == null || hit.distance >= best || IsShooterCollider(hit.collider))
            {
                continue;
            }

            best = hit.distance;
            impact = hit.distance <= 0f ? from : hit.point;
        }

        return best < float.MaxValue;
    }

    private bool IsShooterCollider(Collider collider)
    {
        Character? shooter = Shooter != null ? Shooter.Character : null;
        if (shooter == null || collider == null)
        {
            return false;
        }

        return collider.GetComponentInParent<Character>() == shooter;
    }

    private void Explode(Vector3 point)
    {
        if (exploded)
        {
            return;
        }

        exploded = true;
        if (ReportsImpact && Shooter != null)
        {
            Shooter.DetonateRocket(ShotId, point, velocity.sqrMagnitude > 0.01f ? velocity.normalized : transform.forward);
        }

        Destroy(gameObject);
    }
}
