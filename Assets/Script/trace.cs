using UnityEngine.UI;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine.SceneManagement;

public class trace : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text afterImagesText;

    private bool isRunning;

    [Header("Animation")]
    [SerializeField] private Animator animator;

    [Header("Movement")]
    [SerializeField] private float walkSpeed = 4f;

    [Header("Jump")]
    [SerializeField, Min(0.1f)] private float jumpSpeed = 8f;
    [SerializeField] private float gravity = -24f;
    [SerializeField, Min(0.01f)] private float groundCheckDistance = 0.1f;
    [SerializeField] private LayerMask groundLayers = ~0;
    [SerializeField] private Key jumpKey = Key.W;

    [Header("Dash")]
    [SerializeField, Min(0.1f)] private float dashSpeed = 12f;
    [SerializeField, Min(0.01f)] private float dashDuration = 0.2f;

    [Header("After-Image")]
    [SerializeField] private Key activateAfterImageKey = Key.Q;
    [SerializeField, Min(1)] private int maxAfterImagesPerLevel = 3;
    [SerializeField, Min(0.5f)] private float replaySeconds = 2f;
    [SerializeField, Min(0.01f)] private float historyRecordInterval = 0.016f;
    [SerializeField, Min(0f)] private float afterImageHoldSeconds = 2f;
    [SerializeField, Min(0.01f)] private float afterImageFadeOutSeconds = 0.2f;
    [SerializeField] private Color afterImageTint = new Color(0.45f, 0.95f, 1f, 0.7f);
    [SerializeField] private SpriteRenderer sourceRenderer;

    [Header("Health")]
    [SerializeField, Min(1f)] private float startingHealth = 100f;

    private readonly List<AfterImageReplay.ReplayFrame> _history = new();
    private readonly RaycastHit2D[] _groundHits = new RaycastHit2D[6];
    private Vector2 _velocity;
    private Vector2 _dashDirection = Vector2.right;
    private Vector2 _lastNonZeroMoveInput = Vector2.right;
    private float _dashTimer;
    private float _verticalVelocity;
    private float _lastHistoryRecordTime;
    private HealthEntity _health;
    private Collider2D _playerCollider;
    private Rigidbody2D _rigidbody2D;
    private int _afterImagesRemaining;
    private bool _isGrounded;

    public int AfterImagesRemaining => _afterImagesRemaining;
    public HealthEntity Health => _health;

    private void OnValidate()
    {
        SanitizeInputBindings();
    }

    private void Awake()
    {
        SanitizeInputBindings();

        _rigidbody2D = GetComponent<Rigidbody2D>();
        if (_rigidbody2D == null)
        {
            _rigidbody2D = gameObject.AddComponent<Rigidbody2D>();
            _rigidbody2D.bodyType = RigidbodyType2D.Dynamic;
            _rigidbody2D.gravityScale = 0f;
            _rigidbody2D.constraints = RigidbodyConstraints2D.FreezeRotation;
            _rigidbody2D.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            _rigidbody2D.interpolation = RigidbodyInterpolation2D.Interpolate;
        }

        if (sourceRenderer == null)
        {
            sourceRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        _health = GetComponent<HealthEntity>();
        if (_health == null)
        {
            _health = gameObject.AddComponent<HealthEntity>();
        }

        _playerCollider = GetComponent<Collider2D>();
        if (_playerCollider == null)
        {
            _playerCollider = GetComponentInChildren<Collider2D>();
        }

        _health.SetMaxAndCurrent(startingHealth, startingHealth);

        _afterImagesRemaining = Mathf.Max(1, maxAfterImagesPerLevel);
        RecordSnapshot(force: true);
    }

    private void Update()

            // Reset level if R key is pressed
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            {
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            }
    {
        float horizontalInput = GetHorizontalInput();
        bool jumpPressed = WasPressedThisFrame(jumpKey);

        if (WasPressedThisFrame(activateAfterImageKey))
        {
            TriggerDashAndAfterImage(horizontalInput);
        }

        HandleMovement(horizontalInput, jumpPressed); // Update movement logic

        if (Time.time - _lastHistoryRecordTime >= historyRecordInterval)
        {
            RecordSnapshot(force: false);
        }

        // Update UI text with after images remaining
        if (afterImagesText != null)
        {
            afterImagesText.text = _afterImagesRemaining.ToString();
        }
    }

    public void SetAfterImageChargesForLevel(int charges)
    {
        _afterImagesRemaining = Mathf.Max(0, charges);
    }

    private void HandleMovement(float horizontalInput, bool jumpPressed)
    {
        // Set Animator Speed parameter: 1 (right), 0 (idle), -1 (left)
        if (animator != null)
        {
            int speedValue = 0;
            if (horizontalInput > 0.01f)
                speedValue = 1;
            else if (horizontalInput < -0.01f)
                speedValue = -1;
            animator.SetFloat("Speed", speedValue);
        }
        isRunning = Mathf.Abs(horizontalInput) > 0.01f;
        animator.SetBool("isRunning", isRunning);

        
        if (Mathf.Abs(horizontalInput) > 0.0001f)
        {
            _lastNonZeroMoveInput = new Vector2(Mathf.Sign(horizontalInput), 0f);
        }

        UpdateGroundedState();

        if (jumpPressed && _isGrounded)
        {
            _verticalVelocity = jumpSpeed;
            _isGrounded = false;
        }

        // Only handle input and visuals here; movement is now in FixedUpdate
        if (sourceRenderer != null && Mathf.Abs(horizontalInput) > 0.01f)
        {
            sourceRenderer.flipX = horizontalInput < 0f;
        }

            if (_dashTimer > 0f)
            {
                _dashTimer = Mathf.Max(0f, _dashTimer - Time.fixedDeltaTime);
                _velocity = _dashDirection * dashSpeed;
            }
            else
            {
                _velocity = new Vector2(horizontalInput * walkSpeed, 0f);
            }

            if (!_isGrounded || _verticalVelocity > 0f)
            {
                _verticalVelocity += gravity * Time.fixedDeltaTime;
            }

            if (_isGrounded && _verticalVelocity < 0f)
            {
                _verticalVelocity = 0f;
            }

            Vector2 move = new Vector2(_velocity.x, _verticalVelocity);
            Vector2 nextPosition = _rigidbody2D.position + move * Time.fixedDeltaTime;
            _rigidbody2D.MovePosition(nextPosition);

            UpdateGroundedState();
    }

    private float GetHorizontalInput()
    {
        float horizontalInput = 0f;
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.aKey.isPressed)
            {
                horizontalInput -= 1f;
            }

            if (keyboard.dKey.isPressed)
            {
                horizontalInput += 1f;
            }
        }

        return Mathf.Clamp(horizontalInput, -1f, 1f);
    }

    private void UpdateGroundedState()
    {
        if (_playerCollider == null)
        {
            _isGrounded = false;
            return;
        }

        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(groundLayers);
        filter.useTriggers = false;

        int hitCount = _playerCollider.Cast(Vector2.down, filter, _groundHits, groundCheckDistance);
        _isGrounded = hitCount > 0;

        if (_isGrounded && _verticalVelocity < 0f)
        {
            _verticalVelocity = 0f;
        }
    }

    private void SanitizeInputBindings()
    {
        if (!IsValidKey(jumpKey) || jumpKey == Key.None)
        {
            jumpKey = Key.W;
        }

        if (!IsValidKey(activateAfterImageKey) || activateAfterImageKey == Key.None)
        {
            activateAfterImageKey = Key.Q;
        }
    }

    private bool IsValidKey(Key key)
    {
        return Enum.IsDefined(typeof(Key), key);
    }

    private void TriggerDashAndAfterImage(float horizontalInput)
    {
        if (!ActivateAfterImage())
        {
            return;
        }

        StartDash(horizontalInput);
    }

    private void StartDash(float horizontalInput)
    {
        _dashDirection = Mathf.Abs(horizontalInput) > 0.0001f
            ? new Vector2(Mathf.Sign(horizontalInput), 0f)
            : _lastNonZeroMoveInput;

        if (_dashDirection.sqrMagnitude <= 0.0001f)
        {
            _dashDirection = Vector2.right;
        }

        _dashTimer = dashDuration;
    }

    private bool WasPressedThisFrame(Key key)
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || key == Key.None || !IsValidKey(key))
        {
            return false;
        }

        return keyboard[key] != null && keyboard[key].wasPressedThisFrame;
    }

    private void RecordSnapshot(bool force)
    {
        if (!force && _history.Count > 0 && Time.time - _lastHistoryRecordTime < historyRecordInterval)
        {
            return;
        }

        _lastHistoryRecordTime = Time.time;
        _history.Add(new AfterImageReplay.ReplayFrame
        {
            time = Time.time,
            position = transform.position,
            rotation = transform.rotation,
            flipX = sourceRenderer != null && sourceRenderer.flipX
        });

        float cutoffTime = Time.time - replaySeconds - 0.5f;
        while (_history.Count > 2 && _history[0].time < cutoffTime)
        {
            _history.RemoveAt(0);
        }
    }

    private bool ActivateAfterImage()
    {
        if (_afterImagesRemaining <= 0)
        {
            return false;
        }

        if (sourceRenderer == null || sourceRenderer.sprite == null)
        {
            Debug.LogWarning("After-image activation needs a SpriteRenderer with a sprite.", this);
            return false;
        }

        GameObject clone = new GameObject("AfterImageReplay");
        AfterImageReplay replay = clone.AddComponent<AfterImageReplay>();
        replay.Initialize(
            sourceRenderer,
            afterImageTint,
            replaySeconds,
            this.transform,
            _history,
            afterImageHoldSeconds,
            afterImageFadeOutSeconds,
            _health.CurrentHealth,
            GetComponentsInChildren<Collider2D>());
        _afterImagesRemaining -= 1;
        return true;
    }

    private List<AfterImageReplay.ReplayFrame> BuildReplayFrames()
    {
        List<AfterImageReplay.ReplayFrame> result = new();
        if (_history.Count == 0)
        {
            return result;
        }

        float endTime = Time.time;
        float startTime = endTime - replaySeconds;

        int firstIndex = _history.FindIndex(frame => frame.time >= startTime);
        if (firstIndex < 0)
        {
            firstIndex = _history.Count - 1;
        }

        if (firstIndex > 0 && _history[firstIndex].time > startTime)
        {
            AfterImageReplay.ReplayFrame previous = _history[firstIndex - 1];
            AfterImageReplay.ReplayFrame next = _history[firstIndex];
            float t = Mathf.InverseLerp(previous.time, next.time, startTime);

            result.Add(new AfterImageReplay.ReplayFrame
            {
                time = 0f,
                position = Vector3.Lerp(previous.position, next.position, t),
                rotation = Quaternion.Slerp(previous.rotation, next.rotation, t),
                flipX = t < 0.5f ? previous.flipX : next.flipX
            });
        }

        for (int i = Mathf.Max(firstIndex, 0); i < _history.Count; i++)
        {
            AfterImageReplay.ReplayFrame snapshot = _history[i];
            if (snapshot.time > endTime)
            {
                break;
            }

            result.Add(new AfterImageReplay.ReplayFrame
            {
                time = snapshot.time - startTime,
                position = snapshot.position,
                rotation = snapshot.rotation,
                flipX = snapshot.flipX
            });
        }

        if (result.Count >= 2)
        {
            result[0] = new AfterImageReplay.ReplayFrame
            {
                time = 0f,
                position = result[0].position,
                rotation = result[0].rotation,
                flipX = result[0].flipX
            };
        }

        return result;
    }
}
