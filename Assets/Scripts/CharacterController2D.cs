using UnityEngine;
using UnityEngine.InputSystem;

public enum GroundType { None, Soft, Hard }
public class CharacterController2D : MonoBehaviour
{
    readonly Vector3 flippedScale = new Vector3(-1, 1, 1);
    readonly Quaternion flippedRotation = new Quaternion(0, 0, 1, 0);

    [Header("Character")]
    [SerializeField] Animator animator = null;
    [SerializeField] Transform puppet = null;
    [SerializeField] CharacterAudio audioPlayer = null;
    //NewOptimizations

    [Header("Tail")]
    [SerializeField] Transform tailAnchor = null;
    [SerializeField] Rigidbody2D tailRigidbody = null;

    [Header("Equipment")]
    [SerializeField] Transform handAnchor = null;
    [SerializeField] UnityEngine.U2D.Animation.SpriteLibrary spriteLibrary = null;

    [Header("Movement")]
    [SerializeField] float acceleration = 0.0f;
    [SerializeField] float maxSpeed = 0.0f;
    [SerializeField] float jumpForce = 0.0f;
    [SerializeField] float minFlipSpeed = 0.1f;
    [SerializeField] float jumpGravityScale = 1.0f;
    [SerializeField] float fallGravityScale = 1.0f;
    [SerializeField] float groundedGravityScale = 1.0f;
    [SerializeField] bool resetSpeedOnLand = false;

    private Rigidbody2D controllerRigidbody;
    private Collider2D controllerCollider;
    private LayerMask softGroundMask;
    private LayerMask hardGroundMask;

    private Vector2 movementInput;
    private bool jumpInput;

    private Vector2 prevVelocity;
    private GroundType groundType;
    private bool isFlipped;
    private bool isJumping;
    private bool isFalling;

    private int animatorGroundedBool;
    private int animatorRunningSpeed;
    private int animatorJumpTrigger;

    // ОПТИМИЗАЦИЯ: Выносим векторы, чтобы не создавать их каждый кадр (убираем GC Alloc)
    private Vector2 jumpVector;
    private Vector2 moveVector;

    public bool CanMove { get; set; }

    void Start()
    {
#if UNITY_EDITOR
        // ... (оставляем код редактора без изменений)
        if (Keyboard.current == null)
        {
            var playerSettings = new UnityEditor.SerializedObject(Resources.FindObjectsOfTypeAll<UnityEditor.PlayerSettings>()[0]);
            var newInputSystemProperty = playerSettings.FindProperty("enableNativePlatformBackendsForNewInputSystem");
            bool newInputSystemEnabled = newInputSystemProperty != null ? newInputSystemProperty.boolValue : false;

            if (newInputSystemEnabled)
            {
                var msg = "New Input System backend is enabled but it requires you to restart Unity, otherwise the player controls won't work. Do you want to restart now?";
                if (UnityEditor.EditorUtility.DisplayDialog("Warning", msg, "Yes", "No"))
                {
                    UnityEditor.EditorApplication.ExitPlaymode();
                    var dataPath = Application.dataPath;
                    var projectPath = dataPath.Substring(0, dataPath.Length - 7);
                    UnityEditor.EditorApplication.OpenProject(projectPath);
                }
            }
        }
#endif

        controllerRigidbody = GetComponent<Rigidbody2D>();
        controllerCollider = GetComponent<Collider2D>();
        softGroundMask = LayerMask.GetMask("Ground Soft");
        hardGroundMask = LayerMask.GetMask("Ground Hard");

        animatorGroundedBool = Animator.StringToHash("Grounded");
        animatorRunningSpeed = Animator.StringToHash("RunningSpeed");
        animatorJumpTrigger = Animator.StringToHash("Jump");

        CanMove = true;
        
        // ОПТИМИЗАЦИЯ: Инициализируем векторы один раз при старте
        jumpVector = new Vector2(0, jumpForce);
    }

    void Update()
    {
        var keyboard = Keyboard.current;
        if (!CanMove || keyboard == null) return;

        float moveHorizontal = 0.0f;
        if (keyboard.leftArrowKey.isPressed || keyboard.aKey.isPressed) moveHorizontal = -1.0f;
        else if (keyboard.rightArrowKey.isPressed || keyboard.dKey.isPressed) moveHorizontal = 1.0f;

        // ОПТИМИЗАЦИЯ: Используем существующий вектор вместо создания нового
        moveVector.x = moveHorizontal;
        moveVector.y = 0;
        movementInput = moveVector;

        if (!isJumping && keyboard.spaceKey.wasPressedThisFrame)
            jumpInput = true;
    }

    // ... (FixedUpdate и UpdateGrounding без изменений)

    void FixedUpdate()
    {
        UpdateGrounding();
        UpdateVelocity();
        UpdateDirection();
        UpdateJump();
        UpdateTailPose();
        UpdateGravityScale();
        prevVelocity = controllerRigidbody.linearVelocity;
    }

    private void UpdateGrounding()
    {
        if (controllerCollider.IsTouchingLayers(softGroundMask)) groundType = GroundType.Soft;
        else if (controllerCollider.IsTouchingLayers(hardGroundMask)) groundType = GroundType.Hard;
        else groundType = GroundType.None;

        animator.SetBool(animatorGroundedBool, groundType != GroundType.None);
    }

    private void UpdateVelocity()
    {
        Vector2 velocity = controllerRigidbody.linearVelocity;
        velocity += movementInput * acceleration * Time.fixedDeltaTime;
        movementInput = Vector2.zero;
        velocity.x = Mathf.Clamp(velocity.x, -maxSpeed, maxSpeed);
        controllerRigidbody.linearVelocity = velocity;

        var horizontalSpeedNormalized = Mathf.Abs(velocity.x) / maxSpeed;
        animator.SetFloat(animatorRunningSpeed, horizontalSpeedNormalized);
        audioPlayer.PlaySteps(groundType, horizontalSpeedNormalized);
    }

    private void UpdateJump()
    {
        if (isJumping && controllerRigidbody.linearVelocity.y < 0) isFalling = true;

        if (jumpInput && groundType != GroundType.None)
        {
            // ОПТИМИЗАЦИЯ: Используем заранее созданный вектор
            controllerRigidbody.AddForce(jumpVector, ForceMode2D.Impulse);
            animator.SetTrigger(animatorJumpTrigger);
            jumpInput = false;
            isJumping = true;
            audioPlayer.PlayJump();
        }
        else if (isJumping && isFalling && groundType != GroundType.None)
        {
            if (resetSpeedOnLand)
            {
                prevVelocity.y = controllerRigidbody.linearVelocity.y;
                controllerRigidbody.linearVelocity = prevVelocity;
            }
            isJumping = false;
            isFalling = false;
            audioPlayer.PlayLanding(groundType);
        }
    }

    // ... (UpdateDirection, UpdateTailPose, UpdateGravityScale, GrabItem, SwapSprites без изменений)
    
    private void UpdateDirection()
    {
        if (controllerRigidbody.linearVelocity.x > minFlipSpeed && isFlipped) { isFlipped = false; puppet.localScale = Vector3.one; }
        else if (controllerRigidbody.linearVelocity.x < -minFlipSpeed && !isFlipped) { isFlipped = true; puppet.localScale = flippedScale; }
    }

    private void UpdateTailPose()
    {
        Vector2 targetPosition = tailAnchor.position;
        targetPosition += controllerRigidbody.linearVelocity * Time.fixedDeltaTime;
        tailRigidbody.MovePosition(targetPosition);
        if (isFlipped) tailRigidbody.SetRotation(tailAnchor.rotation * flippedRotation);
        else tailRigidbody.SetRotation(tailAnchor.rotation);
    }

    private void UpdateGravityScale()
    {
        var gravityScale = groundedGravityScale;
        if (groundType == GroundType.None) gravityScale = controllerRigidbody.linearVelocity.y > 0.0f ? jumpGravityScale : fallGravityScale;           
        controllerRigidbody.gravityScale = gravityScale;
    }

    public void GrabItem(Transform item)
    {
        item.SetParent(handAnchor, false);
        item.localPosition = Vector3.zero;
        item.localRotation = Quaternion.identity;
    }

    public void SwapSprites(UnityEngine.U2D.Animation.SpriteLibraryAsset spriteLibraryAsset)
    {
        spriteLibrary.spriteLibraryAsset = spriteLibraryAsset;
    }
}