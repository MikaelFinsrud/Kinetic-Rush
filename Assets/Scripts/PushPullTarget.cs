using UnityEngine;

public class PushPullTarget : MonoBehaviour
{
    public enum TargetKind
    {
        Generic,
        Coin
    }

    [Header("Push/Pull Settings")]
    [SerializeField] private TargetKind kind = TargetKind.Generic;
    [SerializeField] private float impulseCooldown = 0.25f;
    // "Interaction mass" used for how much this influences vs is influenced by the player.
    // If zero/negative, we fall back to Rigidbody.mass.
    [SerializeField] private float interactionMass = 50f;
    // When anchored, we treat this as effectively infinite mass (world-attached).
    [SerializeField] private bool anchored = false;

    private float _nextImpulseTime;

    public TargetKind Kind => kind;
    public bool IsAnchored => anchored;

    public Rigidbody Body { get; private set; }

    public float InteractionMass
    {
        get
        {
            if (anchored)
                return float.PositiveInfinity;

            if (interactionMass > 0f)
                return interactionMass;

            if (Body != null)
                return Body.mass;

            return 50f;
        }
    }

    public bool CanReceiveImpulse => Time.time >= _nextImpulseTime;

    public void RegisterImpulse()
    {
        _nextImpulseTime = Time.time + impulseCooldown;
    }

    private void Awake()
    {
        Body = GetComponent<Rigidbody>();
    }

    /// <summary>
    /// Call this from your coin logic when it hits / sticks to a surface.
    /// </summary>
    public void SetAnchored(bool value)
    {
        anchored = value;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = anchored ? Color.red : Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 0.25f);
    }
#endif
}
