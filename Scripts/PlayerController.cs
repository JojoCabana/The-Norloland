using Godot;

public partial class PlayerController : CharacterBody3D
{
    private const float MoveSpeed = 7.0f;
    private const float JumpVelocity = 5.5f;
    private const float MouseSensitivity = 0.0022f;
    private const float Gravity = 18.0f;

    private Camera3D _camera = null!;
    private float _pitch;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Pausable;
        Name = "Player";

        var collider = new CollisionShape3D
        {
            Name = "CollisionShape",
            Position = new Vector3(0, 0.9f, 0),
            Shape = new CapsuleShape3D
            {
                Radius = 0.34f,
                Height = 1.8f
            }
        };
        AddChild(collider);

        _camera = new Camera3D
        {
            Name = "Camera3D",
            Position = new Vector3(0, 1.62f, 0),
            Current = true,
            Fov = 80.0f
        };
        AddChild(_camera);

        Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseMotion motion &&
            Input.MouseMode == Input.MouseModeEnum.Captured)
        {
            RotateY(-motion.Relative.X * MouseSensitivity);
            _pitch = Mathf.Clamp(
                _pitch - motion.Relative.Y * MouseSensitivity,
                Mathf.DegToRad(-85.0f),
                Mathf.DegToRad(85.0f));
            _camera.Rotation = new Vector3(_pitch, 0, 0);
        }

        if (@event is InputEventKey key && key.Pressed && !key.Echo &&
            (key.Keycode == Key.Escape || key.PhysicalKeycode == Key.Escape))
        {
            Input.MouseMode = Input.MouseModeEnum.Visible;
        }

        if (@event is InputEventMouseButton mouseButton &&
            mouseButton.Pressed &&
            mouseButton.ButtonIndex == MouseButton.Left)
        {
            Input.MouseMode = Input.MouseModeEnum.Captured;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if(GetParent() is Main world && !world.TerrainReadyAt(GlobalPosition)) { Velocity=Vector3.Zero; return; }
        Vector3 velocity = Velocity;

        if (!IsOnFloor())
            velocity.Y -= Gravity * (float)delta;
        else if (Input.IsActionJustPressed("jump"))
            velocity.Y = JumpVelocity;

        Vector2 input = Input.GetVector("move_left", "move_right", "move_forward", "move_back");
        Vector3 direction = (Transform.Basis * new Vector3(input.X, 0, input.Y)).Normalized();

        if (direction.LengthSquared() > 0.001f)
        {
            velocity.X = direction.X * MoveSpeed;
            velocity.Z = direction.Z * MoveSpeed;
        }
        else
        {
            velocity.X = Mathf.MoveToward(velocity.X, 0, MoveSpeed * 7.0f * (float)delta);
            velocity.Z = Mathf.MoveToward(velocity.Z, 0, MoveSpeed * 7.0f * (float)delta);
        }

        Velocity = velocity;
        MoveAndSlide();
    }
}
