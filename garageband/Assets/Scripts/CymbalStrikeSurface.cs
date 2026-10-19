using UnityEngine;

// The transform's local XY plane follows the visible cymbal; local +Z faces the player above it.
// Dimensions are in world metres, independent of the imported drum kit's scale.
public sealed class CymbalStrikeSurface : MonoBehaviour
{
    [SerializeField] private Vector2 radii = new Vector2(.24f, .24f);
    [SerializeField] private float minimumStrikeSpeed = .08f;
    [SerializeField] private float bellHeight = .043f;
    public DrumSound Sound => GetComponent<DrumSound>();
    public Vector3 Normal => transform.forward;
    public bool ContactEnabled => isActiveAndEnabled && GetComponent<Collider>().enabled;
    private float RadialDistance(Vector3 point)
    {
        Vector3 delta = point - transform.position;
        float x = Vector3.Dot(delta, transform.right) / radii.x;
        float y = Vector3.Dot(delta, transform.up) / radii.y;
        return Mathf.Sqrt(x * x + y * y);
    }
    // The imported cymbal has a raised centre: follow its shallow dome as well as its tilt.
    public float Height(Vector3 point) => Vector3.Dot(point - transform.position, Normal) -
        bellHeight * Mathf.Clamp01(1f - RadialDistance(point));

    public bool Crosses(Vector3 from, Vector3 to, float tipRadius, float seconds, out float fraction)
    {
        fraction = 0f;
        float start = Height(from), end = Height(to);
        if (seconds <= 0f || Vector3.Dot(from - to, Normal) / seconds < minimumStrikeSpeed || start < tipRadius || end > tipRadius)
            return false;
        float low = 0f, high = 1f;
        for (int i = 0; i < 10; i++)
        {
            float mid = (low + high) * .5f;
            if (Height(Vector3.Lerp(from, to, mid)) > tipRadius) low = mid; else high = mid;
        }
        fraction = (low + high) * .5f;
        Vector3 contact = Vector3.Lerp(from, to, fraction) - transform.position;
        float x = Vector3.Dot(contact, transform.right) / (radii.x + tipRadius);
        float y = Vector3.Dot(contact, transform.up) / (radii.y + tipRadius);
        return x * x + y * y <= 1f;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Vector3 previous = transform.position + transform.right * radii.x;
        for (int i = 1; i <= 48; i++)
        {
            float angle = i * Mathf.PI * 2f / 48;
            Vector3 next = transform.position + transform.right * (Mathf.Cos(angle) * radii.x) +
                transform.up * (Mathf.Sin(angle) * radii.y);
            Gizmos.DrawLine(previous, next); previous = next;
        }
        Gizmos.DrawRay(transform.position, Normal * .12f);
    }
}
