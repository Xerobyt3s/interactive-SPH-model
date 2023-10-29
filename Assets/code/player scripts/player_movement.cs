using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class player_movement : MonoBehaviour
{
    // Define variables
    [SerializeField] Transform player_transform;

    [Header("Movement")]
    float movement_speed = 8f;
    [SerializeField] float MovementMultiplier = 10f;
    [SerializeField] float air_mulitplyer = 0.4f;
    //[SerializeField] float gravity = 9.81f;

    [Header("Jumping")]
    [SerializeField] float jump_height = 3f;

    [Header("Drag")]
    [SerializeField] float ground_drag = 8f;
    [SerializeField] float air_drag = 2f;

    [Header("sprinting")]
    [SerializeField] bool autosprint = false;
    [SerializeField] float walkSpeed = 4f;
    [SerializeField] float sprintSpeed = 4f;
    [SerializeField] float acceleration = 10f;

    [Header("Keybinds")]
    [SerializeField] KeyCode jumpKey = KeyCode.Space;
    [SerializeField] KeyCode sprintkey = KeyCode.LeftShift;
    float player_height = 2f;

    [Header("Ground detection")]
    [SerializeField] LayerMask ground_layer;
    [SerializeField] Transform ground_check;
    float ground_check_radius = 0.4f;

    bool is_grounded;

    [Header("Veriables")]
    Vector3 movement;
    Vector3 slope_movement;

    float HorizontalMovement;
    float VerticalMovement;
    Rigidbody rb;
    RaycastHit slopehit;

    void Start()
    {
        // Get the rigidbody component from the player
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
    }

    // Check if the player is on a slope
    private bool OnSlope()
    {
        if (Physics.Raycast(transform.position, Vector3.down, out slopehit, player_height / 2 + 0.5f))
        {
            if (slopehit.normal != Vector3.up)
            {
                return true;
            } else{
                return false;
            }
        }
        return false;
    }

    // Get input from the player
    private void MyInput()
    {
        HorizontalMovement = Input.GetAxisRaw("Horizontal");
        VerticalMovement = Input.GetAxisRaw("Vertical");

        movement = transform.right * HorizontalMovement + transform.forward * VerticalMovement;

    }

    // Control the drag of the player
    void ControlDrag()
    {
        if (is_grounded)
        {
            rb.drag = ground_drag;
        } else {
            rb.drag = air_drag;
        }
    }
    
    // Control the speed of the player when they are sprinting
    void Controlspeed()
    {
        if (autosprint && is_grounded || Input.GetKey(sprintkey) && is_grounded)
        {
            movement_speed = Mathf.Lerp(movement_speed, sprintSpeed, acceleration * Time.deltaTime);
        } else{
            movement_speed = Mathf.Lerp(movement_speed, walkSpeed, acceleration * Time.deltaTime);
        }
    }

    // Make the player jump
    void Jump()
    {
        rb.velocity = new Vector3(rb.velocity.x, 0, rb.velocity.z);
        rb.AddForce(transform.up * jump_height, ForceMode.Impulse);
    }

    // Move the player
    void MovePlayer()
    {
        if (is_grounded && !OnSlope())
        {
            rb.AddForce(movement_speed * MovementMultiplier * movement.normalized, ForceMode.Acceleration);
        } 
        else if (is_grounded && OnSlope()){
            rb.AddForce(movement_speed * MovementMultiplier * slope_movement.normalized, ForceMode.Acceleration);
        }
        else if (!is_grounded){
            rb.AddForce(air_mulitplyer * movement_speed * MovementMultiplier * movement.normalized, ForceMode.Acceleration);
        }
        
    }

    // Update is called once per frame
    private void Update()
    {
        // Check if the player is grounded
        is_grounded = Physics.CheckSphere(ground_check.position, ground_check_radius, ground_layer);

        // Get input from the player and control their drag
        MyInput();
        ControlDrag();
        Controlspeed();
        
        // If the player is sprinting, increase their movement speed
        if (Input.GetKey(KeyCode.LeftShift))
        {
            movement_speed = 10f;
        } else
        {
            movement_speed = 5f;
        }

        // If the player is on the ground and presses the jump key, jump
        if (Input.GetKeyDown(jumpKey) && is_grounded)
        {
            Jump();
        }

        // Project the player's movement onto the slope they are on
        slope_movement = Vector3.ProjectOnPlane(movement, slopehit.normal);
    }

    // FixedUpdate is called once per physics update
    private void FixedUpdate()
    {
        // Move the player
        MovePlayer();
    }

}
