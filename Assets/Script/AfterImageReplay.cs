using System.Collections.Generic;
using UnityEngine;

public class AfterImageReplay : MonoBehaviour
{
    private List<ReplayFrame> _playerHistory;
    [SerializeField, Min(0f)] private float followDelay = 0.3f; 
    public struct ReplayFrame
    {
        public float time;
        public Vector3 position;
        public Quaternion rotation;
        public bool flipX;
    }

    private static readonly List<AfterImageReplay> ActiveList = new();
    private readonly List<ReplayFrame> _frames = new();
    private SpriteRenderer _renderer;
    private HealthEntity _health;
    private Rigidbody2D _rigidbody2D;
    private float _elapsed;
    private float _duration;
    private float _holdSeconds;
    private float _fadeOutSeconds;
    private int _segmentIndex;
    private Color _startColor;
    private bool _isInitialized;
    private Transform _followTarget;
    private float _followDuration;
    private float _followTimer;
    private bool _isFollowingLive;

    public HealthEntity Health => _health;
    public bool IsAlive => _health != null && _health.IsAlive;

    private void OnEnable()
    {
        ActiveList.Add(this);
    }

    private void OnDisable()
    {
        ActiveList.Remove(this);
    }

    public static AfterImageReplay GetNearestAlive(Vector3 origin)
    {
        AfterImageReplay nearest = null;
        float bestDistance = float.MaxValue;

        
        for (int i = ActiveList.Count - 1; i >= 0; i--)
        {
            var candidate = ActiveList[i];
            if (candidate == null || !candidate.IsAlive)
            {
                ActiveList.RemoveAt(i);
            }
        }

        for (int i = 0; i < ActiveList.Count; i++)
        {
            AfterImageReplay candidate = ActiveList[i];
            if (candidate == null || !candidate.IsAlive)
            {
                continue;
            }

            float distance = (candidate.transform.position - origin).sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                nearest = candidate;
            }
        }

        return nearest;
    }

    public void Initialize(
        SpriteRenderer sourceRenderer,
        Color tint,
        float followDuration,
        Transform followTarget,
        List<ReplayFrame> playerHistory,
        float holdSeconds,
        float fadeOutSeconds,
        float startingHealth,
        Collider2D[] playerColliders)
    {
        if (sourceRenderer == null || sourceRenderer.sprite == null || followTarget == null)
        {
            Destroy(gameObject);
            return;
        }

        _followTarget = followTarget;
        _followDuration = followDuration;
        _followTimer = 0f;
        _isFollowingLive = true;
        _playerHistory = playerHistory;
        _holdSeconds = Mathf.Max(0f, holdSeconds);
        _fadeOutSeconds = Mathf.Max(0.01f, fadeOutSeconds);

        _rigidbody2D = gameObject.AddComponent<Rigidbody2D>();
        _rigidbody2D.bodyType = RigidbodyType2D.Dynamic;
        _rigidbody2D.gravityScale = 1f;  
        _rigidbody2D.mass = 1f;  
        _rigidbody2D.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        _rigidbody2D.interpolation = RigidbodyInterpolation2D.Interpolate;
        _rigidbody2D.constraints = RigidbodyConstraints2D.FreezeRotation;

        BuildBlockingColliders(sourceRenderer, playerColliders);

        _renderer = gameObject.AddComponent<SpriteRenderer>();
        _renderer.sprite = sourceRenderer.sprite;
        _renderer.sharedMaterial = sourceRenderer.sharedMaterial;
        _renderer.sortingLayerID = sourceRenderer.sortingLayerID;
        _renderer.sortingOrder = sourceRenderer.sortingOrder - 1;
        _renderer.flipY = sourceRenderer.flipY;
        _renderer.drawMode = sourceRenderer.drawMode;
        _renderer.size = sourceRenderer.size;
        _renderer.maskInteraction = sourceRenderer.maskInteraction;

        Color sourceColor = sourceRenderer.color;
        _startColor = new Color(
            sourceColor.r * tint.r,
            sourceColor.g * tint.g,
            sourceColor.b * tint.b,
            sourceColor.a * tint.a);
        _renderer.color = _startColor;

        transform.position = followTarget.position;
        transform.rotation = followTarget.rotation;
        _renderer.flipX = sourceRenderer.flipX;

        _health = gameObject.AddComponent<HealthEntity>();
        _health.SetMaxAndCurrent(startingHealth, startingHealth);

        _isInitialized = true;
    }

    [System.Obsolete]
    private void Update()
    {
        if (!_isInitialized)
        {
            return;
        }

        if (!_health.IsAlive)
        {
            Destroy(gameObject);
            return;
        }

        if (_isFollowingLive)
        {
            _followTimer += Time.deltaTime;
            if (_followTimer <= _followDuration)
            {
                if (_playerHistory != null && _playerHistory.Count > 0)
                {
                    float delayedTime = Time.time - followDelay;
                    
                    ReplayFrame prev = _playerHistory[0];
                    ReplayFrame next = _playerHistory[_playerHistory.Count - 1];
                    for (int i = 1; i < _playerHistory.Count; i++)
                    {
                        if (_playerHistory[i].time >= delayedTime)
                        {
                            prev = _playerHistory[i - 1];
                            next = _playerHistory[i];
                            break;
                        }
                    }
                    float t = (next.time - prev.time) > 0.0001f ? Mathf.InverseLerp(prev.time, next.time, delayedTime) : 0f;
                    transform.position = Vector3.Lerp(prev.position, next.position, t);
                    transform.rotation = Quaternion.Slerp(prev.rotation, next.rotation, t);
                    _renderer.flipX = t < 0.5f ? prev.flipX : next.flipX;
                }
                return;
            }
            
        }
        
    }

    private void ApplyReplayPose(float replayTime)
    {
        while (_segmentIndex < _frames.Count - 2 && _frames[_segmentIndex + 1].time < replayTime)
        {
            _segmentIndex++;
        }

        ReplayFrame from = _frames[_segmentIndex];
        ReplayFrame to = _frames[_segmentIndex + 1];
        float t = Mathf.InverseLerp(from.time, to.time, replayTime);

        Vector3 targetPosition = Vector3.Lerp(from.position, to.position, t);
        Quaternion targetRotation = Quaternion.Slerp(from.rotation, to.rotation, t);

        if (_rigidbody2D != null)
        {
            _rigidbody2D.MovePosition(targetPosition);
            _rigidbody2D.MoveRotation(targetRotation.eulerAngles.z);
        }
        else
        {
            transform.position = targetPosition;
            transform.rotation = targetRotation;
        }

        _renderer.flipX = t < 0.5f ? from.flipX : to.flipX;
    }

    private void BuildBlockingColliders(SpriteRenderer sourceRenderer, Collider2D[] playerColliders)
    {
        Transform sourceRoot = sourceRenderer.transform.root;
        Collider2D[] sourceColliders = sourceRoot.GetComponentsInChildren<Collider2D>();

        if (sourceColliders.Length == 0)
        {
            GameObject fallbackNode = CreateColliderNode(sourceRenderer.transform, sourceRoot);
            BoxCollider2D fallback = fallbackNode.AddComponent<BoxCollider2D>();
            fallback.size = sourceRenderer.sprite.bounds.size;
            fallback.offset = sourceRenderer.sprite.bounds.center;
            IgnorePlayerCollisions(fallback, playerColliders);
            return;
        }

        for (int i = 0; i < sourceColliders.Length; i++)
        {
            Collider2D sourceCollider = sourceColliders[i];
            GameObject targetNode = CreateColliderNode(sourceCollider.transform, sourceRoot);

            Collider2D copied = CopyCollider(sourceCollider, targetNode);
            if (copied != null)
            {
                IgnorePlayerCollisions(copied, playerColliders);
            }
        }
    }

    private GameObject CreateColliderNode(Transform sourceTransform, Transform sourceRoot)
    {
        if (sourceTransform == sourceRoot)
        {
            return gameObject;
        }

        GameObject node = new GameObject($"AfterImageCollider_{sourceTransform.name}");
        node.transform.SetParent(transform, false);
        node.transform.localPosition = sourceRoot.InverseTransformPoint(sourceTransform.position);
        node.transform.localRotation = Quaternion.Inverse(sourceRoot.rotation) * sourceTransform.rotation;
        node.transform.localScale = DivideVector(sourceTransform.lossyScale, sourceRoot.lossyScale);
        return node;
    }

    private Vector3 DivideVector(Vector3 value, Vector3 divisor)
    {
        float x = Mathf.Approximately(divisor.x, 0f) ? 1f : value.x / divisor.x;
        float y = Mathf.Approximately(divisor.y, 0f) ? 1f : value.y / divisor.y;
        float z = Mathf.Approximately(divisor.z, 0f) ? 1f : value.z / divisor.z;
        return new Vector3(x, y, z);
    }

    private Collider2D CopyCollider(Collider2D source, GameObject targetNode)
    {
        if (source == null || source.isTrigger || !source.enabled)
        {
            return null;
        }

        if (source is BoxCollider2D boxSource)
        {
            BoxCollider2D box = targetNode.AddComponent<BoxCollider2D>();
            box.offset = boxSource.offset;
            box.size = boxSource.size;
            box.compositeOperation = boxSource.compositeOperation;
            box.sharedMaterial = boxSource.sharedMaterial;
            return box;
        }

        if (source is CircleCollider2D circleSource)
        {
            CircleCollider2D circle = targetNode.AddComponent<CircleCollider2D>();
            circle.offset = circleSource.offset;
            circle.radius = circleSource.radius;
            circle.compositeOperation = circleSource.compositeOperation;
            circle.sharedMaterial = circleSource.sharedMaterial;
            return circle;
        }

        if (source is CapsuleCollider2D capsuleSource)
        {
            CapsuleCollider2D capsule = targetNode.AddComponent<CapsuleCollider2D>();
            capsule.offset = capsuleSource.offset;
            capsule.size = capsuleSource.size;
            capsule.direction = capsuleSource.direction;
            capsule.compositeOperation = capsuleSource.compositeOperation;
            capsule.sharedMaterial = capsuleSource.sharedMaterial;
            return capsule;
        }

        if (source is PolygonCollider2D polySource)
        {
            PolygonCollider2D polygon = targetNode.AddComponent<PolygonCollider2D>();
            polygon.pathCount = polySource.pathCount;
            for (int pathIndex = 0; pathIndex < polySource.pathCount; pathIndex++)
            {
                polygon.SetPath(pathIndex, polySource.GetPath(pathIndex));
            }

            polygon.compositeOperation = polySource.compositeOperation;
            polygon.sharedMaterial = polySource.sharedMaterial;
            return polygon;
        }

        if (source is EdgeCollider2D edgeSource)
        {
            EdgeCollider2D edge = targetNode.AddComponent<EdgeCollider2D>();
            edge.points = edgeSource.points;
            edge.edgeRadius = edgeSource.edgeRadius;
            edge.compositeOperation = edgeSource.compositeOperation;
            edge.sharedMaterial = edgeSource.sharedMaterial;
            return edge;
        }

        return null;
    }

    private void IgnorePlayerCollisions(Collider2D cloneCollider, Collider2D[] playerColliders)
    {
        if (cloneCollider == null || playerColliders == null)
        {
            return;
        }

        for (int i = 0; i < playerColliders.Length; i++)
        {
            Collider2D playerCollider = playerColliders[i];
            if (playerCollider == null)
            {
                continue;
            }

            Physics2D.IgnoreCollision(cloneCollider, playerCollider, true);
        }
    }
}