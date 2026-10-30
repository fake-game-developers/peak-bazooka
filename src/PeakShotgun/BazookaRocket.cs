using UnityEngine;

namespace Peak.Shotgun;

/// <summary>
/// Local visual + collision for one rocket. Every client spawns one from <see cref="Action_Gun"/>;
/// only the shooter's copy reports the impact so damage stays host-authorized.
/// </summary>
internal sealed class BazookaRocket : MonoBehaviour
{
    private const float Radius = 0.18f;

    private const float GravityScale = 0.28f;

    private const float MaxLife = 6f;

    internal Action_Gun? Gun;

    internal Character? Shooter;

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
        int mask = HelperFunctions.CharacterAndDefaultMask | HelperFunctions.terrainMapMask | ShotTargetRegistry.ExtraLayerMask;
        if (Physics.SphereCast(transform.position, Radius, direction, out RaycastHit hit, distance, mask, QueryTriggerInteraction.Collide)
            && !IsShooterCollider(hit.collider))
        {
            Explode(hit.point);
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

    private bool IsShooterCollider(Collider collider)
    {
        if (Shooter == null || collider == null)
        {
            return false;
        }

        return collider.GetComponentInParent<Character>() == Shooter;
    }

    private void Explode(Vector3 point)
    {
        if (exploded)
        {
            return;
        }

        exploded = true;
        if (ReportsImpact && Gun != null)
        {
            Gun.DetonateRocket(ShotId, point, velocity.sqrMagnitude > 0.01f ? velocity.normalized : transform.forward);
        }

        Destroy(gameObject);
    }
}
